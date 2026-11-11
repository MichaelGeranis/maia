# Contributing to Maia

Thanks for your interest in Maia. This guide covers setup, the rules the
project follows, and how to get a change merged.

By participating you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).
To report a security or privacy problem, follow [SECURITY.md](SECURITY.md)
instead of opening a public issue. **Never put real health data in an issue,
test, screenshot or log.**

## Ways to help

- **Clinical review.** The Greek plan template (`src/Maia.Infrastructure/Templates/Content/gr.json`)
  is a draft. If you are an obstetrician or midwife, a review is the most
  valuable contribution there is. Open a *Plan template* issue.
- **New countries.** A template for another country, in English and the
  country's language, with its source.
- **Bugs and small fixes:** open a pull request directly.
- **Features:** open an issue first. Maia is deliberately small, and
  [docs/product.md](docs/product.md) lists what is out of scope (for example
  symptom tracking, kick counting and any interpretation of results).

## Development setup

You need the .NET 10 SDK (`global.json`). The core libraries and their tests
run on any OS without the MAUI workload:

```bash
for p in tests/*.Tests; do dotnet test "$p"; done
./scripts/coverage.sh      # the same tests, failing below 100% line/branch coverage
```

The app itself needs the pinned MAUI workload; see
[Getting started in the README](README.md#getting-started).

## Rules the project follows

Read [CLAUDE.md](CLAUDE.md) (the working agreement) and, before UI work,
[docs/design/README.md](docs/design/README.md). The ones reviewers check most:

- **No medical advice and no interpretation of results**, anywhere. Never write
  that Maia replaces a midwife, doctor or care professional.
- **Dependencies point inwards:** Infrastructure → Application → Domain. The
  domain has no I/O and no clock.
- **Every plan change goes through `PregnancyPlan` methods**, called from
  `PlanService`. Display status is derived, never stored.
- **Stored format changes** bump `schemaVersion` and add a
  `PlanDocumentMigrator` step with a test.
- **Every UI string** is in English and Greek (`Strings.resx`, `Strings.el.resx`).
- **One design system:** the colour keys, text styles and Lucide icons in the
  design rules, never raw values. Screenshots are retaken for screens a change
  touches (`./scripts/capture-screens.ps1`).
- **100% line and branch coverage** on Domain, Application, Infrastructure and
  Presentation; warnings are errors.

## Making a change

1. Fork the repository and create a branch from `main`.
2. Make the change with behaviour tests (named as sentences, e.g.
   `A_booking_made_less_than_a_day_ahead_rings_two_hours_before`).
3. Run `./scripts/coverage.sh`.
4. Commit using [Conventional Commits](https://www.conventionalcommits.org/)
   and open a pull request against `main`, filling in the template.

## Licence

By contributing, you agree that your contributions are licensed under the
[Apache License 2.0](LICENSE).
