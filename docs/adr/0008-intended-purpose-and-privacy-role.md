# 0008 · Intended purpose and privacy role

- Status: Proposed (draft for the owner and a legal review; nothing here is legal advice)
- Date: 2026-10-01

## Context

Maia works out how far along a pregnancy is and proposes checks by week, which sits close to the line between an
organiser and medical device software under the EU MDR (review #39). It also keeps health data, which the GDPR treats
as a special category (Art. 9), and both stores ask for a privacy policy and data declarations (review #5, #40). Neither
the intended purpose nor the privacy role was written down. The full working is in [`compliance.md`](../compliance.md).

## Decision

- **Intended purpose.** Maia organises a pregnancy's appointments and paperwork: it keeps a list of checks, their
  bookings and results, reminders and questions for the doctor. It does not diagnose, monitor, predict or recommend
  treatment, and does not interpret results. The plan is a typical starting point that her doctor changes. Maia is
  meant as an organiser, not a medical device; this is to be confirmed by a short regulatory opinion before the paid
  launch.
- **Wording follows the purpose.** Store listings, in-app copy and docs keep to it: no "tracks your health",
  "monitors", "detects", "risk" or "recommends"; "Not medical advice. Your doctor decides." and Google Play's
  "not a medical device" line go in the listing. The positioning rules in product.md already require this.
- **Availability follows reviewed content.** The store listings are limited to Greece until another country has a
  clinically reviewed template (ADR 0003); `AppConfig.DefaultCountry` is "GR".
- **Privacy role.** In the free app everything stays on her phone, encrypted, and the developer receives nothing. Who
  the controller is for anything the developer does receive (a support email, a shared problem log) is the owner's
  decision, with legal advice. A DPIA is planned before the paid cloud features, which make the developer a
  controller or processor of encrypted health data.

## Consequences

- Every new feature is checked against the intended purpose; anything that interprets results, scores risk or adapts
  the plan to health data needs a new decision and a regulatory review first.
- The in-app privacy notice needs the controller's contact and a hosted policy URL before any store release.
- This record becomes Accepted once the owner has decided the open points in compliance.md and legal review is done.
