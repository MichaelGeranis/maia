#!/usr/bin/env bash
# Runs every core test project with coverage, merges the reports and fails unless line and branch coverage of the
# core libraries (Domain, Application, Infrastructure, Presentation) are both 100%. Maia.App (XAML, adapters and
# platform code) is built and smoke-tested in its own CI job and is not measured here.
#
#   ./scripts/coverage.sh                 # tests, report in TestResults/coverage, gate
#   ./scripts/coverage.sh --no-test       # gate on the reports already in TestResults
set -euo pipefail

cd "$(dirname "$0")/.."
results=TestResults
report="$results/coverage"

if [[ "${1:-}" != "--no-test" ]]; then
  rm -rf "$results"
  for project in tests/*.Tests/*.csproj; do
    dotnet test "$project" --configuration "${CONFIGURATION:-Release}" \
      --logger "trx;LogFilePrefix=$(basename "$project" .csproj)" \
      --collect "XPlat Code Coverage" --settings tests/coverage.runsettings \
      --results-directory "$results"
  done
fi

dotnet tool restore >/dev/null
dotnet tool run reportgenerator \
  "-reports:$results/**/coverage.cobertura.xml" \
  "-targetdir:$report" \
  "-reporttypes:TextSummary;Cobertura;Html" \
  "-assemblyfilters:+Maia.Domain;+Maia.Application;+Maia.Infrastructure;+Maia.Presentation" >/dev/null

summary="$report/Summary.txt"
sed -n '1,20p' "$summary"

line="$(sed -n 's/^ *Line coverage: \([0-9.]*\)%.*/\1/p' "$summary")"
branch="$(sed -n 's/^ *Branch coverage: \([0-9.]*\)%.*/\1/p' "$summary")"
if [[ "$line" != "100" || "$branch" != "100" ]]; then
  echo "::error::Coverage must be 100% (line $line%, branch $branch%). Open $report/index.html for what is missing." >&2
  grep -E '^  Maia\.' "$summary" | grep -v ' 100%$' >&2 || true
  exit 1
fi

echo "Coverage: 100% of lines and branches."
