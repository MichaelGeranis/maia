## What and why

<!-- One or two sentences. Link the issue if there is one. -->

## Checklist

- [ ] Tests added or updated for the change
- [ ] No stored-format change, or `PlanDocument.CurrentSchemaVersion` bumped with a migration and a test
- [ ] Plan template changes keep English and Greek text, and bump the template `version`
- [ ] No health data in logs, errors or analytics
- [ ] Every new UI string is in `Strings.resx` and `Strings.el.resx`; screenshots retaken for screens it touches
- [ ] `./scripts/coverage.sh` passes (100% line and branch on the core libraries)
