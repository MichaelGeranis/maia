#!/usr/bin/env bash
# The pinned MAUI workload (CI and release.yml; also handy on your own machine).
#
# Directory.Packages.props names the workload set to build with (MaiaMauiWorkloadSet) next to Microsoft.Maui.Controls.
# A plain `dotnet workload install maui-android` takes whatever set is newest that day, so two signed builds of the
# same commit could differ; installing the named set keeps them the same.
#
# Usage:
#   scripts/maui-workload.sh install   installs maui-android from the pinned workload set
#   scripts/maui-workload.sh check     fails unless the installed set is the pinned one and the MAUI it brings is the
#                                      version of Microsoft.Maui.Controls (as the workload list and the app build see it)
#
# The set belongs to the SDK band in global.json (10.0.4xx): a set from another band can't be installed on it. It is not
# pinned in global.json itself (sdk.workloadVersion): the SDK then refuses every build, even of the core libraries, on a
# machine that doesn't have exactly that set.
set -euo pipefail

cd "$(dirname "$0")/.."
props=Directory.Packages.props

fail() {
  if [ -n "${GITHUB_ACTIONS:-}" ]; then
    echo "::error::$1"
  else
    echo "error: $1" >&2
  fi
  exit 1
}

pinned="$(sed -n 's:.*<MaiaMauiWorkloadSet>\([^<]*\)</MaiaMauiWorkloadSet>.*:\1:p' "$props")"
controls="$(sed -n 's:.*Include="Microsoft\.Maui\.Controls" Version="\([^"]*\)".*:\1:p' "$props")"
[ -n "$pinned" ] || fail "No MaiaMauiWorkloadSet in $props."
[ -n "$controls" ] || fail "No Microsoft.Maui.Controls version in $props."

case "${1:-}" in
  install)
    dotnet workload install maui-android --version "$pinned"
    ;;

  check)
    installed="$(dotnet workload --version | tr -d '\r' | awk 'NF { last = $1 } END { print last }')"
    if [ "$installed" != "$pinned" ]; then
      fail "The installed workload set is '$installed', but $props pins $pinned (MaiaMauiWorkloadSet). Run: bash scripts/maui-workload.sh install"
    fi

    # "maui-android   10.0.20/10.0.100   SDK 10.0.400": the MAUI version comes before the slash.
    manifest="$(dotnet workload list | tr -d '\r' | awk '$1 == "maui-android" { print $2; exit }')"
    [ -n "$manifest" ] || fail "The maui-android workload is not installed. Run: bash scripts/maui-workload.sh install"
    maui="${manifest%%/*}"
    if [ "$maui" != "$controls" ]; then
      fail "Workload set $pinned brings MAUI $maui, but Microsoft.Maui.Controls is $controls. Move MaiaMauiWorkloadSet and Microsoft.Maui.Controls in $props together."
    fi

    # What the app build itself picks up from the workload.
    built="$(dotnet msbuild src/Maia.App/Maia.App.csproj -p:TargetFramework=net10.0-android -getProperty:MauiVersion | tr -d '\r[:space:]')"
    if [ "$built" != "$controls" ]; then
      fail "The app builds with MAUI '$built' from the workload, but Microsoft.Maui.Controls is $controls."
    fi

    echo "MAUI workload set $pinned brings MAUI $maui, the version of Microsoft.Maui.Controls."
    ;;

  *)
    echo "Usage: scripts/maui-workload.sh install|check" >&2
    exit 2
    ;;
esac
