#!/usr/bin/env bash
# Smoke test for a Release APK on a running emulator (CI: the "smoke" job in .github/workflows/ci.yml).
#
# Installs the APK, starts Maia, waits, and fails if the app died, restarted, or logged a Java FATAL EXCEPTION
# or an unhandled .NET exception. A CI build refuses Draft plan templates (ADR 0003), so Maia stops at setup
# step 1; that is enough to prove the Release-only paths (trimming, AOT, the assembly store) start up.
#
# Usage: scripts/smoke-android.sh <path to .apk> [output directory] [seconds to wait]
# The output directory gets logcat.txt (this app's process only), crash.txt and screen.png.
set -euo pipefail

apk="${1:?Usage: smoke-android.sh <apk> [out dir] [seconds]}"
out="${2:-smoke}"
wait_seconds="${3:-30}"
package="com.michailgeranis.maia"

mkdir -p "$out"
adb wait-for-device
adb install -r "$apk"
adb logcat -c || true
adb logcat -b crash -c || true

activity="$(adb shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER "$package" | tr -d '\r' | tail -n 1)"
case "$activity" in
  "$package"/*) ;;
  *) echo "::error::No launcher activity found for $package (got '$activity')."; exit 1 ;;
esac

adb shell am start -W -n "$activity"
sleep 3
pid="$(adb shell pidof "$package" | tr -d '\r' || true)"
if [ -z "$pid" ]; then
  adb logcat -d > "$out/logcat.txt" || true
  echo "::error::Maia was not running 3 seconds after launch."
  exit 1
fi

sleep "$wait_seconds"
adb exec-out screencap -p > "$out/screen.png" || true
now="$(adb shell pidof "$package" | tr -d '\r' || true)"
adb logcat -d -v threadtime --pid="$pid" > "$out/logcat.txt" || true
adb logcat -d -b crash > "$out/crash.txt" || true

failed=0
if [ "$now" != "$pid" ]; then
  echo "::error::Maia died or restarted during the smoke test (pid '$pid' became '$now')."
  failed=1
fi
if grep -q "Process: $package" "$out/crash.txt"; then
  echo "::error::A FATAL EXCEPTION in Maia is in the crash log."
  failed=1
fi
if grep -E -q "FATAL EXCEPTION|JavaProxyThrowable|UNHANDLED EXCEPTION|Unhandled [Ee]xception|Unhandled managed exception" "$out/logcat.txt"; then
  echo "::error::Maia's log has an unhandled exception."
  grep -E -n "FATAL EXCEPTION|JavaProxyThrowable|UNHANDLED EXCEPTION|Unhandled [Ee]xception|Unhandled managed exception" "$out/logcat.txt" | head -n 20
  failed=1
fi

if [ "$failed" -ne 0 ]; then
  tail -n 200 "$out/logcat.txt" || true
  exit 1
fi
echo "Maia started and kept running for $wait_seconds seconds (pid $pid) with no crash."
