# 0007 · Diagnostics without health data

- Status: Accepted (amended 2026-10-02: no preview before sharing, see below)
- Date: 2026-10-01

## Context

Failures used to go to the debug log only, so a problem on her phone left no trace (review #16). The usual fix, a
crash-reporting SDK, would send data off the phone, needs the network permission the free app doesn't have, and
exception messages and stack traces can name a check or a result's file. CLAUDE.md rules out health data in any log
or report.

## Decision

- **A problem log on the phone, codes only.** Every failure is reported with an operation code (`plan.book`,
  `reminders.set`, `startup.load`, …). `ProblemReport` writes one line: UTC time, Maia's version, the operation code,
  and the exception types with their stable codes, up to three deep (for example
  `PlanUnreadableException[key.unavailable]<DataKeyUnavailableException`). Never an exception's message or stack
  trace. Codes are cleaned to letters, digits and `. _ - + ( )` and cut to 64 characters, so no free text rides along.
  *Amended 2026-10-02:* a line `results.load ObjectDisposedException` from her phone couldn't say what was disposed
  or where, so a line now also keeps three things that are code, never her data: a disposed object's type name
  (`ObjectDisposedException(SemaphoreSlim)`, left out unless it is a plain type name), an I/O error's precise result
  code (`IOException(0x0000001C)`), and after `@` where it was thrown as `Type.Method` names: the frame that threw
  and the first frame in Maia's code (`@SemaphoreSlim.WaitCore/DataKeyProvider.GetKeyAsync`), read from the
  innermost exception. A frame's arguments, file and line are dropped, and each name is cleaned like a code.
- **Kept small and local.** `DiagnosticsFile` keeps the newest 200 lines in Maia's private data folder, in every build.
  It is excluded from cloud backup and device transfer like everything else (ADR 0002). Global handlers (Android's
  unhandled-exception raiser, the AppDomain and unobserved tasks) report through the same path.
- **Shared only when she chooses.** Settings has a Diagnostics card with one "Share diagnostics" button. Each tap
  hands the log, after a first line with Maia's and Android's versions, straight to the phone's share sheet, once,
  and she chooses where it goes; with nothing noted a toast says there is nothing to share. Nothing is ever sent by
  Maia itself. *Amended 2026-10-02:* the first version showed every line in a panel first and shared only from its
  own "Share" button; the owner asked for less on the screen, and since the log holds only codes, times and versions
  (above), the card, the privacy notice and [`compliance.md`](../compliance.md) now say exactly that instead.
- **Deleted with her data.** Delete everything and Start again clear the log (`IErrorReporter.Forget`).
- **No automatic crash reporting and no analytics** in the free app.

## Consequences

- Support can ask her to share the log and get error codes, versions and times, with nothing from her plan.
- Without full stack traces, a fault is found from its operation code, exception types and the two frame names, plus
  a reproduction. The R8 mapping file is kept with each release build for Play Console's own crash reports, if those are used.
- Whether a log she sends to the developer counts as data collected, for the store forms and the GDPR, is recorded in
  [`compliance.md`](../compliance.md).
- Adding an SDK that sends crash data would need a new decision, network permissions, store-form changes and an
  update to the privacy notice.
