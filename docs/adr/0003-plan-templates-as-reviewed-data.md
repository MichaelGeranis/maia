# 0003 · Plan templates are data, gated by clinical review

- Status: Accepted
- Date: 2026-09-30
- Updated: 2026-10-01 (see Updates)

## Context

Which checks a pregnancy needs, and when, differs by country and changes over time. The content must be written or
reviewed by clinicians, in English and Greek from v1, and must be changeable without touching code.

## Decision

- Ship one JSON template per country (`Templates/Content/<country>.json`) with checks, week windows, category,
  default inclusion and text in each language (English required as fallback).
- Validate templates through the domain constructors at load time, and in tests for every shipped file.
- Mark each template `Draft` or `ClinicallyReviewed`. Release builds set `AllowDraftTemplates = false`, so
  unreviewed clinical content cannot reach users.
- Plans store the template id and version they were created from.

## Consequences

- Content updates are data changes with a test run, not code changes.
- The first market (Greece) is blocked on clinical review of `gr.json` by design.
- Offering template updates to existing plans (new checks, changed windows) is a future feature; the stored
  template version makes it possible.

## Updates

### 2026-10-01 · Personal builds, the store gate, sign-off and updates

- **Personal builds only** (review #1). A Release build can use Draft templates only when it is a personal Android APK
  built outside CI with `-p:MaiaAllowDraftTemplates=true` (`scripts/build-release.ps1 -AllowDraftTemplates`). It is
  named "Maia (internal)" on the home screen and must never be shared. The `MaiaStoreGate` target refuses the switch
  with error `MAIA0001` for an AAB, on CI (`ContinuousIntegrationBuild`), and for iOS and Windows, in every
  configuration but Debug (which shows drafts anyway), so a custom one such as Staging can't slip past it. CI checks
  that the switch defaults to false and probes each clause of the gate on its own (CI, an AAB, iOS, a Staging AAB),
  then checks that a personal APK outside CI is let through. A store build with no reviewed template can't start a new
  plan: setup step 2 reads "The typical plan isn't ready yet", says the plan is waiting for clinical review and offers
  Restore a backup.
- **Sign-off is recorded** (review #11). Templates carry `reviewedBy` (the clinician's name and role), `reviewedOn`
  (`yyyy-MM-dd`) and `reviewNotes`. The catalog rejects a template marked `ClinicallyReviewed` unless both
  `reviewedBy` and `reviewedOn` are set; the rule is checked when a template is parsed, and a test pins it. `gr.json`
  is still Draft with no sign-off; a licensed midwife or OB-GYN must review its content and Greek wording first.
- **Existing plans take a newer version** (review #81). At start-up, and when a backup is restored,
  `PregnancyPlan.UpdateFromTemplate` (through `PlanService.UpdateFromTemplateAsync`) takes everything a newer version
  of the plan's template holds in that one update, so the version the plan remembers never hides a change it skipped.
  Every check from the template takes the new title, description and category; a check she hasn't booked, done or
  filed results under also takes the new weeks, and becomes or stops being a milestone as the new version says. A
  check the new version adds, or a milestone it turns into a check, joins her plan switched off whatever the template's
  default: nothing new to book appears without her, and it is offered when she adds what her doctor asked for after a
  visit. A milestone it adds (a date, not an exam) shows on her timeline. A check she has acted on stays exactly as
  she knows it, a check the new version drops stays as it is, and her own checks, switches, bookings and results are
  untouched. Category was already stored for every check, so the stored format did not change. Reminders are set
  again (`PlanTemplateUpdated`) and a calm toast tells her when her plan reads differently. A build that refuses drafts
  never brings in draft content, and never refuses a plan she already has.
- **Authoring rule.** Bump `version` with every content change, so existing plans pick it up. Set
  `ClinicallyReviewed` only together with `reviewedBy` and `reviewedOn`.
