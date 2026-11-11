# 0006 · Reminders on this phone

- Status: Accepted
- Date: 2026-10-01
- Updated: 2026-10-04 (calendar sync removed, ADR 0009: these are now Maia's only reminders; see Update below)

## Context

Calendar sync (ADR 0004) gives reminders only to someone who turns it on, and many people keep their calendar shared
or don't use it. "Time to book" has no calendar event at all. A notification on a pregnancy organiser can also give
away more than she wants on a lock screen others can see. Android (review #22) is in scope; iOS is not yet.

## Decision

- **Local notifications, opt-in.** "Reminders on this phone" is a switch in Settings, off by default. It covers each
  upcoming booking, her next visit and each timed task (the evening before and/or shortly before, as she chose on
  Calendar sync), plus "Time to book" at 09:00 on the day a check starts asking to be booked. Anything already in her
  calendar is left to the calendar's own alarm, so nothing rings twice. `POST_NOTIFICATIONS` is asked only when she
  turns the switch on.
- **Rules in the core.** `ReminderPlanner` (pure) decides what to set; `PhoneReminders` sets all of them again from the
  latest saved plan after every change, calendar setting change, restore and delete, and on every start. At most 64
  are set, soonest first.
- **Inexact alarms, honest words.** `setAndAllowWhileIdle`, no `SCHEDULE_EXACT_ALARM` or `USE_EXACT_ALARM`. Exact
  alarms are a Play policy question and a battery cost for an organiser. Inexact alarms can arrive up to about an hour
  late (`ReminderPlanner.MayArriveLate`), so the words name the day and time ("Today at 08:30", "Tomorrow at 00:45",
  "At 01:30"), never "in 2 hours".
- **The lock screen shows only "Maia reminder".** Each reminder is posted with its full words as a secret
  notification (never shown on a secure lock screen, and silent while locked) and, only while the phone is locked, as a
  public plain "Maia reminder" that rings. The plain one goes when she swipes the full one away, when Maia comes to the
  front, and on every replace. On a phone without a secure lock (no PIN, pattern or password), a reminder that fires
  while locked posts only the plain one. With discreet titles on, the full words are discreet too.
- **Nothing that hints at her checks is kept in the clear.** To set reminders again after a restart, the phone keeps,
  in plain preferences, one line per reminder: a running number, a time and plain words that name no check or task.
  Numbers come from a rising counter, never from a hash of what the reminder is for, which could be matched back.
  After a restart, reminders come back with those plain words until Maia next starts and sets them again in full.
- **iOS deferred.** iOS gets a scheduler that does nothing until local notifications are designed for it.

## Consequences

- She can have reminders without calendar sync, and a lock screen that gives nothing away on a phone with a secure
  lock.
- A reminder can be up to an hour late, and OEM battery savers can delay it further; this needs checking on real
  phones. Moving to exact alarms would need a policy review and a new decision.
- On a phone with only a swipe lock, a reminder posted while she was using the phone may still show its words on the
  lock screen on some models. A swipe lock offers no privacy anyway, and discreet titles cover it; the stricter option
  (only "Maia reminder" on such phones) remains open for the owner.
- Two permissions are added: `POST_NOTIFICATIONS` and `RECEIVE_BOOT_COMPLETED`.

## Update (2026-10-04): the only reminders

Calendar sync is gone (ADR 0009), so these notifications are Maia's only reminders and nothing is left to a calendar's
alarm. She turns them on in setup step 3 ("Remind me") or on the Reminders card in Settings, which also holds "Remind
me" (24 hours before, 2 hours before, or both) and "Discreet lock screen" (the discreet titles above). When they ring
depends only on the booking's time (`ReminderTimes`): a booking made less than a day ahead rings 2 hours before in place
of the 24-hour reminder, and a booking with only a day rings the evening before at 20:00 whichever is chosen.
`ReminderPlanner.For` takes her `ReminderSettings` instead of the calendar's status.
