# 0009 · No calendar sync; reminders on the phone only

- Status: Accepted
- Date: 2026-10-04
- Supersedes: [ADR 0004](0004-calendar-sync-through-the-device-calendar.md)

## Context

Calendar sync (ADR 0004) grew into the largest and most fragile part of Maia: its own calendar on the phone, the
choice of one of her Google calendars, hidden marks to find its events again after a reinstall or on a new phone,
repairs when access came and went, and a delete that had to wait for calendar access. It needed `READ_CALENDAR` and
`WRITE_CALENDAR`, a third-party package, a Data safety answer about calendar data, and, with a Google calendar, sent her
bookings to Google. Meanwhile the reminders on the phone (ADR 0006) already covered what she needs from it: being told
in time. Keeping both also meant a rule so nothing rang twice, and a Journey line that had to know what her calendar
held. Maia is only on test phones so far.

## Decision

- **Maia writes nothing to the phone's calendar.** The calendar sync code, its stores, the Android calendar gateway,
  the calendar permissions, the iOS calendar usage texts and Plugin.Maui.CalendarStore are removed in one go. No
  migration or clean-up of calendars already on the test phones: what is there stays as it is, and the old encrypted
  calendar-links file is simply left behind, unreadable once her data is deleted.
- **Reminders are Maia's own notifications** (ADR 0006), turned on in setup step 3 ("Remind me", which asks for
  notifications) or on the Reminders card in Settings. Her choices (`ReminderSettings`: when, and a discreet lock
  screen) are kept with the switch, in device preferences; a phone keeps the choices it had under the old calendar keys
  until she chooses again.
- **Reminders depend only on the booking's time** (`ReminderTimes`), for bookings and timed tasks alike:
  - "24 hours before": 24 hours before, or at 20:00 the evening before for a booking with only a day, while that is
    still ahead. Nothing rings in its place once it has passed: a booking made less than a day ahead gets no reminder.
  - "2 hours before": 2 hours before; a booking with only a day has no time to count from, so it rings the evening
    before at 20:00 instead.
  - "Both": each of them, while it is still ahead.
  "Time to book" reminders are unchanged.
- **Journey names exactly the reminders that will ring**, by the same rule, and only while reminders are on.

## Consequences

- Fewer permissions, no third-party calendar package, no Google data flow and a simpler privacy notice and Data
  safety form. Delete everything no longer depends on calendar access.
- Bookings no longer show in her calendar app, and nothing reaches her other devices. If the owner wants that back
  later, the likely route is an export or share of a booking she starts herself, as a new decision.
- Reminders are set again from the plan after every change and on every start, and the rule looks only at the booking
  and the time now, so it can't tell a booking made today from one made weeks ago. A 2-hour stand-in for a passed
  24-hour reminder would therefore also ring for bookings made long before, after the 24-hour one; the owner chose no
  stand-in. A booking made less than a day ahead, with only "24 hours before" chosen, gets no reminder.
- Reminders exist only on Android for now (ADR 0006); on iPhone there is nothing until iOS notifications are designed.
