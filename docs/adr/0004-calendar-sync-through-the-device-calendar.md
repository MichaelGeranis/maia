# 0004 · Calendar sync through the device calendar

- Status: Superseded by [ADR 0009](0009-no-calendar-sync-reminders-on-the-phone-only.md) (2026-10-04): Maia no longer
  writes to the phone's calendar; reminders are Maia's own notifications. Kept as the record of what was built.
- Date: 2026-09-30
- Updated: 2026-10-01 (see Updates: a calendar that repairs itself, her Google calendar, Maia's mark)

## Context

Parents want bookings and timed tasks in the calendar they already use, with reminders, on Android (usually Google
Calendar) and iOS (Apple Calendar).

## Decision

- Write to the phone's own calendar store through one port, `ICalendarGateway`, implemented in the app with
  Plugin.Maui.CalendarStore (EventKit / CalendarContract). Google Calendar on Android reads that store, so no
  Google sign-in is needed. (Corrected later: Google Calendar shows only the store's Google calendars, not Maia's
  local one; see "Her Google calendar" below.)
- Use a dedicated "Maia · Pregnancy" calendar so the parent's own events are never touched.
- Drive sync from domain events after each save; keep device event ids in a separate link store; offer a full
  resync when settings change.
- Sync failures are reported, never thrown back to the user: the booking in Maia is the source of truth.

## Consequences

- No OAuth or server work for calendar sync in v1.
- A parent whose Google Calendar is not synced to the device store will not see events there; direct Google
  Calendar API support can be added behind the same port if users need it. Since the owner's change request she can
  instead choose one of the Google calendars her phone already syncs (below), which needs no sign-in either.

## Updates

### 2026-10-01 · A calendar that repairs itself and tells the truth

- **On Android the calendar stays on this phone** (review #80). The plugin makes a local calendar (account type
  LOCAL). It is not added to her Google account, so it isn't on the web or her other devices. The app said so on
  Calendar sync, at setup step 3 and on Team, and no text promised Google account sync. *Correction:* this update
  also said Google Calendar shows the local calendar on the phone. It doesn't: the owner added bookings and tasks on
  her phone and found none of them in Google Calendar, which lists only Google calendars. "Her Google calendar" below
  is the answer.
- **One lock, the latest plan** (review #48, #52). Every piece of calendar work (reactions, resync, repair, delete)
  runs one at a time behind one lock and reloads the latest saved plan. Commands return after the save; the calendar
  follows in the background, off the UI thread. Unchanged events are skipped.
- **Repair and honesty** (review #12, #32). A failed call, or a calendar that had to be made again, sets a repair flag.
  The repair runs on the next change, on a resync, when Calendar sync opens, and whenever Maia comes to the front with
  access granted. Screens say an event is in her calendar only when it is; otherwise one of two calm lines says why.
  With access gone she can still turn sync off.
- **The link store is encrypted** (review #53). `calendar-links.maia` (format 2), AES-GCM with the data key and its own
  purpose, replaces the plain `calendar-links.json`, which is migrated and deleted. A damaged file reads as empty and
  marked for repair; a cleanup then avoids doubles without touching events she added herself.
- **Her events, her past** (review #29, #53). Maia's calendar is hers to add to: only unlinked upcoming events that carry
  Maia's note, or match an event Maia is about to write, are removed. Past appointments and past visits stay as written.
- **Discreet titles are discreet throughout** (review #59). With discreet titles on, events are titled "Appointment" (or
  "Reminder" for a task) with no notes or place, and the calendar itself is called just "Maia". A name change is a
  rename, so her events stay. The one exception is an Android calendar whose account name still says "Maia ·
  Pregnancy" when discreet titles go on: a rename can't change the account name, so the calendar is replaced once (her
  events move with it) by one whose account is "Maia".
- **Delete everything removes the calendar** (review #18, #42). Maia's whole calendar goes, including events she added
  to it (the confirmation says "Maia's calendar"), and its id is forgotten. Without calendar access her data is still
  deleted and she is told the calendar is still on the phone. Maia sets it aside first: its id moves to a "calendar to
  delete" note in plain preferences (an opaque id, nothing about her), which outlives the data key that goes with her
  data. That calendar is never written to or reused; the next time Maia has access (on start, coming back, or when
  she turns sync on) it is deleted before anything else, and the note goes with it. Only an uninstall before then
  leaves it, and the toast and Calendar sync (while it waits) tell her to remove it in her calendar app in that case.
  Before Maia renames, replaces or deletes a calendar by a saved id, it checks the calendar is still Maia's
  (`CalendarNames.IsStillMaias`): Android gives a deleted calendar's id to the next one made, so on Android the account
  (which never changes) must still carry one of Maia's names; iOS never reuses an id. A calendar of hers that took the
  id is never touched; the note is just forgotten. Leftovers from an earlier install are recognised by
  `CalendarNames.IsLeftover`: on Android by name and account, in any language; on iOS only by the full "Maia ·
  Pregnancy" names, so her own calendar called "Maia" is never taken for Maia's.
- **Home time zone** (review #58). Events are converted from the plan's home time zone, kept on the plan (schema 4).
  An event keeps the zone it was written in while its booking is unchanged, so neither a booking made abroad nor a
  resync there moves it.
- **Open for iOS.** When iOS is in scope: which calendar source to use (the plugin uses the default, usually iCloud, so
  the calendar syncs to her other Apple devices), how the app tells her, how a second iPhone or iPad running Maia
  avoids taking that calendar for a leftover, and a leftover "Maia" calendar after a reinstall with discreet titles on.

### 2026-10-01 · Her Google calendar (the owner's change request #2, #3)

The owner's bookings did not show in Google Calendar. Android lets an app write events into a Google calendar the
phone already syncs, but not make a new calendar inside a Google account, so she now chooses.

- **Her choice, Maia's calendar by default.** Settings' calendar card (which replaced the Calendar sync screen) lists
  Maia's own calendar on this phone first, then her Google calendars that take new events (`account_type`
  com.google, contributor access or more, synced, not deleted). Maia never picks one for her; it writes to one only
  after she taps it. Google then keeps the events and shows them on all her devices; the card and the privacy notice
  say so. Her Google calendar is never made, renamed or deleted.
- **Only Maia's events, by id.** In her Google calendar Maia adds only its own events, reads back only those (by id,
  and by its mark, below) and removes only those. There is no tidy-up by note or title there: her events are never
  read. The tidy-up, `GetEventsAsync` and replacing the calendar apply only to Maia's own calendar.
- **Which calendar is hers.** Android gives a deleted calendar's id to the next calendar made, even in the same
  account, so the choice is kept as three plain preferences: the phone's id, her account and Google's own id for it
  (`_sync_id`, else the owner account). `CalendarSettings.IsChosen` is the one rule for the sync, the status and the
  rows. Maia writes only while that calendar is still listed.
- **Moving.** On a switch, the new calendar is checked (or made) first, so a Google calendar that is gone never costs
  her the events she has; then Maia's linked events leave the old calendar by id, past ones too, and everything is
  written into the new one. Maia's own calendar stays, with any events she added, and is reused if she switches back.
  While a Google calendar is chosen, Maia's own calendar, if still on the phone, keeps being renamed (or replaced) for
  discreet titles, so it never gives the pregnancy away.
- **When her Google calendar is gone.** The status checks the phone's list every time it is read
  (`CalendarStatus.GoogleCalendarMissing`). Maia then writes nowhere, the phone's reminders take over (they are set
  again on resume when this changes), the card selects no row and asks her to choose again, and toasts say "Choose
  another in Settings" instead of "Maia will try again".
- **CalendarContract directly.** The plugin says nothing of a calendar's account type and reads every event in a
  calendar, so listing her Google calendars and finding Maia's events by id use `CalendarContract` directly
  (`Platforms/Android/GoogleCalendars`).
- **Delete everything.** Maia's Google event ids are set aside first, as opaque calendar and event ids in the plain
  preference `calendar.events_to_remove`, which outlives the data key like the calendar still to delete. They are
  removed at once, or, without access, the next time Maia has it, each only if it is still in its calendar; the
  toast and the card's Sand notice say they are still in her Google calendar until then.
- **The link store** is format 4 (format 3 added the calendar the events are in, `GoogleCalendarId`; format 2 reads
  as Maia's own calendar).
- **Accepted limits.** Two phones that both keep sync on with the same Google calendar can still undo each other's
  newer events in a full pass (settings, language or template changes); that would need a product decision. The
  Play Data safety position for events that leave the phone through her Google account is for legal to confirm
  (compliance.md).

### 2026-10-01 · Maia's mark (change request #3, follow-up)

Links live on one phone. Without more, a new phone, a reinstall, or her Google account removed and added again (which
brings Maia's events back under new ids) would leave Maia's events in her Google calendar unknown, and doubled.

- **A random mark per event.** Each event Maia writes carries a new random 128-bit code (hex), which says nothing about
  her or the booking, kept in its encrypted link too (`CalendarLink.Marker`). On Android it is a
  `CalendarContract.ExtendedProperties` row named `com.michailgeranis.maia.mark`, written right after the event with
  a sync-adapter URI for the event's own account (only sync adapters may write extended properties); the event
  itself stays an ordinary app write, so Google uploads it with the mark as a private extended property.
  `Platforms/Android/MaiaMarks` reads only rows under that name, so her own events are never read. iOS keeps no marks.
- **Re-linking and adoption** (in the calendar Maia writes to, during a full repair or resync). A marked event no link
  points to takes back its link when its mark is one Maia remembers; otherwise it becomes the event of a booking with
  none on the phone when the time and a title Maia gives it match (either language, discreet or not), and is kept as
  it is only when its title, notes, place and time match what Maia writes now. Marked events left over go while
  upcoming; past ones stay, as every past appointment does. Events without the mark are never touched; marked events
  in her other Google calendars are left alone, since she may have moved one there.
- **Events from before marks.** An upcoming event written before marks existed is written again with one by the
  repair, without telling her it is missing.
- **Delete everything** also notes, as a plain flag (`calendar.marked_events_to_remove`), that every marked event in
  all her writable Google calendars is to go, past ones too, so events an earlier install wrote go as well. The flag
  is kept only when Maia may have used a Google calendar on this install, or can remove them at once.
- **The new phone puts back what the old one takes out.** Google keeps one copy for all her phones, so turning sync
  off or deleting her data on the old phone would remove the events the new phone took over. With a Google calendar
  chosen, the repair looks, by id, at the upcoming events this phone linked and writes back any that are gone. When
  an upcoming marked event exists that none of this phone's links point to, another phone wrote it, so this phone
  leaves her calendar alone rather than undo the newer one's bookings. Settings tells her: "Changing phones or
  reinstalling Maia? Choose the same Google calendar again and Maia finds its events there, so nothing shows twice.
  On your old phone, just uninstall Maia: your bookings stay in Google Calendar."
- **Failures** to read, write, find or remove marks are reported by code only and never stop sync, which then goes by
  its links alone, as before marks.
- **To check on a phone.** Whether Google's sync keeps the mark and brings it back has only been modelled in tests.
  If it doesn't, `MaiaMarks.Name` is the one place to change, and the changing-phones line goes back to "turn sync
  off before you move".
