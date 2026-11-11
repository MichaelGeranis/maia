# Working on Maia

Maia is a .NET 10 / .NET MAUI app that organises a pregnancy: a plan of checks, bookings, results, reminders.

Read before working:

- `docs/product.md`: what Maia is, positioning rules, scope, decisions, glossary, **build order** (what to do next).
- `docs/design/README.md` (the rules: words, colours, type, icons, logo), `docs/design/screens.md` and
  `docs/design/screens/*.jpg`: look, feel and every screen. **Before any UI work, read the rules, open the screenshot
  of the screen you are building and match it**; where they disagree, `screens.md` wins. Screenshots are captured
  from the app: retake the ones a change touches, in the same change.
- `docs/architecture.md` and `docs/adr/`: before larger changes.

## Product and design rules

- Never write, anywhere, that Maia replaces a midwife, doctor or care professional. Maia organises; her doctor decides.
- Maia is its own product: no father/"dad" framing, no links to other apps. The user is the expecting mother.
- No medical advice and no interpretation of results. Blood pressure is strictly a logger: what she typed and when,
  never ranges, colours, flags, averages, charts or trends. Out of scope: symptom check-in, kick counter, baby-size content.
- **One of everything** (`docs/design/README.md`): the 14 colour keys (Green · Sea · Sun families and five neutrals),
  **no red anywhere** (Sand for attention), never raw hex or another colour; the 6 text styles, never a `FontSize` on
  a screen; Lucide icons only, named for their meaning; the filled sprout as the only logo; every button a pill.
  Font: M PLUS Rounded 1c everywhere, English and Greek (titles Bold). Something new is added to the rules first.
- **Words**: her journey is "journey" in English and «πλάνο» in Greek, everywhere (never "plan" / «διαδρομή» in the
  UI). Tabs, titles, headings, tiles and links are nouns, and a screen's title is the name she tapped; buttons are
  verbs. Each Greek string says what its English says.
- Navigation: setup (3 steps) → five-slot bottom bar Today · Journey · New · Results · Settings (Σήμερα · Πλάνο · Νέο ·
  Αποτελέσματα · Ρυθμίσεις; flat, equal slots; New and Settings are tabs, no gear; her care team is a card in Settings).
- Copy is short, warm, second person, in English **and** Greek. Respect reduced-motion settings.
- If the design and a request conflict, ask; don't invent a new visual style. Update `docs/product.md` build order when a slice lands.

## Rules

- Dependencies point inwards: Infrastructure → Application → Domain. The domain has no I/O and no clock.
- Every plan change goes through `PregnancyPlan` methods, called from `PlanService`. Never mutate from the UI.
- Display status is derived (`PlannedCheck.StatusAt`), never stored.
- Stored format changes need a `schemaVersion` bump and a `PlanDocumentMigrator` step with a test.
- Template JSON needs `en` and `el` text; keep `reviewStatus` Draft until clinically reviewed.
- Code names stay as they are (`PregnancyPlan`, `PlanPage`, `WeekPage`, `AddEntryPage`, `BreathePage`); the glossary
  in `docs/product.md` maps them to what she reads (Journey, Today, New, Breathing).
- Never log or report health data (check titles, results, notes). Error codes only.
- Warnings are errors; analyzers run in Recommended mode. Fix the cause, suppress only with a comment.

## Commands

```bash
for p in tests/*.Tests; do dotnet test "$p"; done    # core + presentation tests
./scripts/coverage.sh                                 # the same tests, failing below 100% line/branch coverage (as CI does)
dotnet build src/Maia.App -f net10.0-android          # the app
./scripts/capture-screens.ps1                         # every screen into docs/design/screens (PowerShell)
```

- Screen logic goes in `Maia.Presentation` (view models, unit-tested); `Maia.App` holds only XAML, adapters and DI.
- Core libraries (Domain, Application, Infrastructure, Presentation) stay at 100% line and branch coverage; CI fails
  otherwise. Cover new code with behaviour tests; `[ExcludeFromCodeCoverage(Justification = ...)]` only for code that
  cannot run in a unit test.
- Every UI string lives in `src/Maia.Presentation/Resources/Strings.resx` **and** `Strings.el.resx` (a test enforces it).
- Inside `Maia.App`, write `Microsoft.Maui.Controls.Application`: plain `Application` resolves to the `Maia.Application` namespace.

## Style

File-scoped namespaces, `sealed` by default, `_camelCase` private fields, `ConfigureAwait(false)` in libraries (not in view models, which resume on the UI thread),
tests named as behaviour sentences (`A_booking_made_less_than_a_day_ahead_rings_two_hours_before`).
