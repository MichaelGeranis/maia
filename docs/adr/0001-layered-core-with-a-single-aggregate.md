# 0001 · Layered core with a single plan aggregate

- Status: Accepted
- Date: 2026-09-30

## Context

Maia is a mobile app, but most of its value is rules: dating a pregnancy, deciding when a check needs booking,
keeping bookings, results and reminders consistent. Those rules must be testable without a phone and must
survive UI rewrites.

## Decision

- Split the core into `Maia.Domain` (rules, no I/O), `Maia.Application` (use cases and ports) and
  `Maia.Infrastructure` (adapters). The MAUI app sits on top and supplies platform adapters.
- Model the parent's data as one aggregate, `PregnancyPlan`, with checks, tasks and care team inside it.
  One device holds one plan, and every change is small, so a single consistency boundary is enough.
- Derive display status (book now, probably done, …) from state plus today's date instead of storing it.

## Consequences

- Rules are covered by fast unit tests; the app project stays thin.
- Loading and saving the whole plan on every command is fine at v1 sizes (tens of checks); revisit if plans grow
  large or sync needs finer-grained changes.
- Twins, or several pregnancies on one device, would need a plan list; the repository interface is the seam.
