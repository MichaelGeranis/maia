# Screens

One section per screen: what it shows, top to bottom, what she can do and its states. The look follows the rules in
[`README.md`](README.md) (words, the 14 colours, the 6 text sizes, Lucide icons, the logo); this file only names what
is on each screen. Copy is English with the Greek where it matters; every string exists in both, saying the same thing.

The images in [`screens/`](screens/) are captured from the app by `scripts/capture-screens.ps1` (README,
Screenshots), with the sample journey: today is week 24 + 3, so the due date is 109 days after the day of capture (the
images were taken on 8 October 2026: due Mon 25 Jan); her OB-GYN is "Dr. Eleni Papadaki". Dates in this file are
examples and may differ from the images. Screens that are the phone's own, or not reached on purpose, have no image.
Every screen awaits the owner's sign-off on its image (build-order items 42 and 43).

**App structure.** First-run **setup** (3 steps, no bottom bar), then the main app with a bottom bar of five flat slots
of the same size: **Today · Journey · New · Results · Settings** (Σήμερα · Πλάνο · Νέο · Αποτελέσματα · Ρυθμίσεις).
Questions, the Timer, All tasks and Calendar are pushed from Today, with a Back pill and no bottom bar; About Maia (with the
privacy notice, the terms of use and licences) from Settings; the result viewer from Results. Her care team and her
reminders are cards
in Settings. There is no settings gear and no calendar sync: Maia's reminders are its own notifications (ADR 0009).

**Shared behaviour.**

- A screen's title is the name she tapped; there is no eyebrow above it (setup step 1's "Welcome to Maia" is the one).
- Every confirmation asks once more in place on a Sand strip (never a dialog, never red): the question names what
  goes ("Delete this task?"), with one more sentence only when it matters (what stays, or "This can't be undone." where
  files or all her data go); then two buttons in words, the action first (`ConfirmButton`, repeating the question's
  verb: "Delete task") and "Keep it" / "Keep them" (`KeepButton`; «Να μείνει» / «Να μείνουν»).
- A toast confirms an action (Pine, with a tick). When something did not happen it is a calm notice instead (Sand,
  SandDeep words, no tick): "Something went wrong. Please try again." Toasts stay at least 4 seconds, longer for a
  longer message, and a tap closes them.
- Dates read "Tue 6 Oct" / «Τρί 6 Οκτ», with the year when the day is more than half a year from today ("Fri 3 Oct
  2031"); times "10:30".
- Screen content rises in the first time a screen shows; nothing moves with reduced motion or with Maia's Animations
  switch off.

---

## 1. Setup: due date — [`01-setup-due-date.jpg`](screens/01-setup-due-date.jpg), [`01-setup-due-date-lower.jpg`](screens/01-setup-due-date-lower.jpg)

- Step dots (1 of 3), then the language pills "English" / "Ελληνικά" (each in its own language; the phone's language
  chosen when it is English or Greek). Tapping the other redraws Maia in it and returns to this step with her date.
- Illustrated header card: hills, the logo, the sun rising (once).
- Eyebrow "Welcome to Maia" («Καλώς ήρθες στη Maia»), title "Let's organise your pregnancy", body "Tell Maia your due
  date…".
- Card "When is your baby due?" with a date field, starting 40 weeks from today; the link "Not sure? Use your last
  period instead" switches it to "First day of your last period" (and back). The due date offers 4 weeks ago to 40
  weeks ahead; the last period 44 weeks ago to today; switching carries the date across, kept in range.
- Sky summary, live as the date changes: "You're 24 weeks and 3 days today. Due Sun 17 Jan.", or a calm line when the
  date is out of range.
- Primary "Suggest my journey" («Πρότεινέ μου πλάνο»), off while the date is out of range.
- A quiet link "Moving from another phone? Restore a backup" (screen 15), "Your journey stays on this phone,
  encrypted." with a "Privacy notice" link (screen 19), and under it, in the same quiet caption, "By using Maia, you
  accept the" with a "Terms of use" link («Χρησιμοποιώντας τη Maia, αποδέχεσαι τους Όρους χρήσης»; screen 25).
- If Android closes Maia during setup, it reopens at the step she reached with her choices kept; a due date that no
  longer fits goes back to step 1.

## 2. Setup: a typical journey — [`02-setup-a-typical-journey.jpg`](screens/02-setup-a-typical-journey.jpg)

- Back pill, step dots (2 of 3). Title "A typical journey" («Ένα συνηθισμένο πλάνο»); body "Common checks for a
  pregnancy due Sun 17 Jan. Your doctor may suggest a different set. Switch off what doesn't apply; you can change it
  any time."
- Grouped by trimester (leaf icon, eyebrow). Each row: the check's name, its tag chip (*Routine* neutral; *Offered*,
  *Often offered*, *If advised* Sky), "Weeks 8–10 · 7 Jun – 27 Jun" and a switch. Only checks still to come are listed:
  a new journey starts with its past empty, and she adds what she has done herself on New.
- "Looks good" («Μια χαρά») at the end. Only the checks she keeps are stored.
- **Not ready** (a store build while her country's typical journey is not yet clinically reviewed): title "The typical
  journey isn't ready yet", a Sand notice "This journey is waiting for clinical review and can't be used yet.", "Used
  Maia on another phone? You can bring your journey here with a backup." and an outline "Restore a backup" (screen 15).

## 3. Setup: doctor and reminders — [`03-setup-doctor-and-reminders.jpg`](screens/03-setup-doctor-and-reminders.jpg)

- Back pill, step dots (3 of 3). Title "Doctor and reminders" («Γιατρός και υπενθυμίσεις»); body "Link your OB-GYN from
  your phone contacts, and Maia reminds you of every booking."
- Card "OB-GYN" («Γυναικολόγος»): the contacts she linked (initials avatar, name, label · masked number) and a dashed
  "Choose from contacts", then "Link another contact" once one is linked. The phone's own contact picker opens, one
  contact per pick.
- Card "Reminders" (Android only): one choice tile with a clock badge, "Reminders on this phone" («Υπενθυμίσεις στο
  κινητό»), read out as a switch. Turning it on asks for notifications; if she says no it stays off with a calm Sand
  note. Under it: "Maia's own notifications on this phone, for your bookings and timed tasks. The lock screen only
  shows “Maia reminder”."
- "Start my journey" («Ξεκίνα το πλάνο μου», primary) and "Skip for now" («Όχι τώρα»). The journey is made first;
  linking her doctor and turning reminders on are best effort. Today opens with the toast "Your journey is ready.", or
  "Your journey is ready. Your doctor couldn't be added this time; you can add them in Settings."

## 4. Today — [`04-today.jpg`](screens/04-today.jpg), [`04-today-lower.jpg`](screens/04-today-lower.jpg)

The first slot (Σήμερα), and where Maia opens. Once "Your week"; it shows more than the week.

- Header: the logo and the "maia" wordmark, and a glass pill with the week and a calendar icon, "Week 24 + 3"
  («Εβδομάδα 24 + 3»). The pill opens **Calendar** (screen 24); screen readers hear "Calendar, Week 24 + 3, button".
- Title "Today" («Σήμερα»).
- **Journey card**: the curved path through the trimester nodes, "you are here" glowing in Sea, a dotted path to the
  Sun dot of the due date; captions "2nd trimester" and "Due Sun 17 Jan". Today is the one screen that shows the week
  and the due date.
- **Next up** (the Pine hero): "NEXT UP", the title (`Hero`), "Tue 6 Oct at 10:30", and "with Dr. Eleni Papadaki"
  when Maia knows who it is with. Next up is her next booked entry of any kind, never a task; a booking whose time has
  passed today drops off (one with only a day stays all day). "With …" shows for a check from her journey once she has
  linked an OB-GYN, and for a doctor visit booked with her OB-GYN whose name doesn't already say so; never for an
  exam, a hospital visit or something else. With nothing booked: "Nothing booked yet". The whole card is one tap to
  Journey, scrolled to that booking (at today with nothing booked).
- **Backup** (a Sand card, with the upload icon): "It lives only on this phone. A backup file keeps your journey and
  results safe for a new phone." (or "Your last backup is from 1 Oct. A new one keeps everything added since."), "Make
  a backup" (opens Settings) and "Not now" (hides it for 30 days). Shown when her journey is at least 7 days old and
  holds something of hers (a booked or done check, an entry, a result, a task or a question), with no backup and no
  "Not now" in the last 30 days.
- **This week** («Αυτή την εβδομάδα»): a card with the round 44 dp Pine + ("Add a task" for screen readers).
  - The + opens the add form under the heading: the task's words ("e.g. Call clinic about results"), a "Date and
    time" switch (off at first; on shows Date and Time, starting now) and, while it is off, "When?" with "Trimester"
    (the three trimesters and "After birth") or "Week" (− Week 24 +), nothing chosen until she chooses; then "Add task"
    and "Cancel". With neither a date nor a trimester or week, "Add task" says "Choose when it is." in Sand. Toast "Task
    added for Thu 1 Oct at 09:00." or "Task added."
  - Rows: tasks due today or in the six days after, with or without a time, by day and time; at most 5, then "Show
    all" (a chevron turned down) opens the card to at most 20. It folds back to 5 when she leaves Today. With nothing
    due: "Nothing due this week."
  - A task she has just added for this week appears in the card (which opens if it falls past the first five) and
    glows SageTint for a moment.
  - Always last: one line "All tasks" («Όλες οι εργασίες») with a chevron, which opens All tasks (screen 23).
  - Each row is the task row (see screen 23).
- **Tiles**: **Questions** (message icon; "Ready when you need them", or "Jot one down when it comes up" with none
  open) and **Timer** («Χρονόμετρο»; the dandelion seed drifting with the breath; "Breathing and exercises" / «Αναπνοή
  και ασκήσεις»).
- **While Today first loads**: calm blank cards where the journey card, Next up and This week go, no words ("Getting
  things ready" for screen readers) and no week pill; never "Nothing booked yet" for a journey still being read.

## 5. Journey — [`05-journey.jpg`](screens/05-journey.jpg)

Her journey as a timeline, laid out like a calendar. English "Journey", Greek «Πλάνο».

- Title "Journey" («Πλάνο»), body "Tap any entry to book, edit or delete it."
- "Tasks" («Εργασίες») and a switch, at the start under the body: on until she turns it off, remembered on this phone;
  the timeline changes at once. Screen readers hear "Tasks, switch, on".
- Grouped by trimester: a leaf icon and the eyebrow ("2ND TRIMESTER"), then "After birth". No counts.
- A vertical line with a node per entry and one card per entry: an eyebrow with what she added it as ("DOCTOR VISIT",
  "EXAM OR TEST", "HOSPITAL VISIT", "SOMETHING ELSE"; none on checks from her typical journey), the name, the status
  chip, and its line: "2nd trimester · Week 24 · Tue 6 Oct, 10:30". A check's usual weeks read "Weeks 24–28"; with
  only a trimester there is no week. No reminder words: reminders ring as Settings says.
- **Weeks**: each pregnancy week has a heading across the timeline, above its first row: "Week 25" («Εβδομάδα 25»,
  Label size in Pine), a 1 dp Line hairline, then its days, "11–17 Oct" or "27 Sep – 3 Oct" (Caption, Muted); the line
  pauses for it and comes down from it to the next node. Every week from the first on the timeline to the due date
  shows; one with nothing in it says "Nothing this week" («Τίποτα αυτή την εβδομάδα», Caption, Muted) beside the line.
  After birth, only weeks with something in them show. Screen readers hear a heading, "Week 25, 11–17 Oct". A row with
  only a trimester has no week: it stands before the heading of the week it falls in.
- Statuses (chip and node colours in the README): **Not booked** («Δεν έχει κλειστεί»), **Booked** («Κλεισμένο»),
  **Done** («Έγινε»), **Probably done** («Μάλλον έγινε»: a check kept at setup, never booked, whose weeks have passed;
  it behaves as Done; never an entry she added), **Due date** («Τοκετός», at the end, no actions). A booking turns Done
  by itself at its time, at home.
- Order: a booked entry stands at its day; an unbooked or done check at the start of its usual weeks; an entry with
  only a trimester first in that trimester. Within a day: entries by time, then tasks by time.
- **You are here** («Είσαι εδώ»): a Sky banner and a pulsing Sea node at today, after what has begun and before a
  booking later today, so Journey opens with that booking in view. It always shows, with its trimester heading.
- **Opens at today** every time (from the bar, from Today's Next up on its booking, or after New): the row just above
  "You are here" starts the screen. Coming straight back from a screen opened over it keeps her place.
- **Her tasks** (with "Tasks" on): each task stands on its day, at the start of its week, or first in its trimester
  (after birth under "After birth"), as a tick circle on the line, no card, with its title and a clock line ("Thu 1 Oct
  · 09:00", "Week 26", "3rd trimester"). One saved before tasks needed a when has no place and stays off the
  timeline. Ticking works as on Today; a ticked one stays, struck through. Screen readers hear it as a checkbox, "Tick
  off".
- **Her blood pressure** (always, whatever "Tasks" says): each reading she logged on New stands small at the day and
  time she took it: a 10 dp Sea dot on the line, then a 16 dp Sea heart, "118/76 mmHg" (Label size, Ink) and
  "Wed 30 Sep · 08:30" (Caption, Muted), no card, no chip and no colour for the numbers: Maia never reads anything into
  them. One taken earlier today stands before "You are here". It is never edited: sliding it left or right shows
  "Delete" («Διαγραφή», a Sand pill); tapping it deletes the reading for good. Toast "Blood pressure 118/76 mmHg
  deleted from your journey." Screen readers hear "Blood pressure 118 over 76, Wed 30 Sep · 08:30" and the hint
  "Slide left or right to delete".
- Scrolling stays smooth in the Release build: cards, chips and nodes are `LightBorder`s (README, Motion).

## 6. An entry, opened — [`06-entry-opened.jpg`](screens/06-entry-opened.jpg)

Tapping a card opens it in place (no navigation); the open card has a 1 dp Sage border. The due date doesn't open.

- Its note: from her typical journey ("A detailed look at your baby's organs and growth."), or the one she wrote on New
  or in Edit. With none, nothing shows.
- Two outline buttons sharing the row (one under the other when a word won't fit):
  - **Not booked**: "Book" («Κλείσε») and "Delete" («Διαγραφή»).
  - **Booked, Done, Probably done**: "Edit" («Επεξεργασία») and "Delete".
- **Book** opens a Mist panel: Date and Time, starting at the next whole hour, any day from the first day of the
  pregnancy, past or to come; then "Book" (primary) and "Cancel". Toast "Anomaly scan booked for Tue 6 Oct at 10:30.",
  or "… done on Tue 29 Sep." for a time that has passed.
- **Edit** opens the same panel: "Name" (her words, editable), "Notes" (its note, editable; empty for none, up to 500
  characters) and "When?": a booked one keeps a date and time; a done one may also choose "Trimester" (the three
  trimesters and "After birth") or "Week" (− Week 24 +). "Save" and "Cancel". Toast "Anomaly scan
  updated." Moving a done one to a time ahead makes it Booked; a booked one moved into the past is Done.
- **Delete** asks: "Delete Anomaly scan from your journey?" with "Delete" / "Keep it". It goes for good, with its
  reminders. Toast "Anomaly scan deleted from your journey."
- No results here (they are on Results) and no call button (her care team is in Settings).

## 7. New — [`07-new.jpg`](screens/07-new.jpg)

The third slot (Νέο). One short page that adds one entry, a task or her blood pressure. Screen readers hear the slot as "New, something
for your journey".

- Title "New" («Νέο»).
- **What**: six choice pills right under the title, none chosen at first (screen readers hear the group as "What is
  it?"): "Doctor visit", "Exam or test", "Hospital visit", "Something else", "Task", "Blood pressure" («Επίσκεψη σε
  γιατρό», «Εξέταση», «Επίσκεψη σε νοσοκομείο», «Κάτι άλλο», «Εργασία», «Πίεση»). The rest appears once she picks.
- **Name** (for an entry): empty at first, up to 150 characters. An exam or test offers suggestion chips of Maia's
  usual extras her journey doesn't already have ("+ Extra ultrasound", "+ Iron recheck", "+ Urine culture", "+ Doppler
  scan", "+ Thyroid test", "+ Blood pressure checks"; "✓" once chosen). Maia's own names follow a language switch; her
  words stay as typed.
- **Notes** («Σημειώσεις», for an entry): optional, "Optional, e.g. bring the referral", lines of her own up to 500
  characters. Shown when its card is opened on Journey, as a check's note from a typical journey is; left empty,
  nothing shows.
- **When?** Three pills, none chosen at first:
  - "Booked" («Κλεισμένο»): Date and Time, starting at the next whole hour, any day from the first day of the
    pregnancy, past or to come (a past one is done).
  - "Already done" («Έγινε ήδη»): "Date and time" (one that has passed), "Trimester" or "Week".
  - "Not booked yet" («Δεν έχει κλειστεί ακόμα»): "Trimester" or "Week" (starting at this week).
  - "Trimester" offers the three trimesters and "After birth" («Μετά τη γέννα»), which stands under Journey's "After
    birth" heading.
- **Task**: its words, the "Date and time" switch and, while it is off, "When?" with "Trimester" or "Week", as on
  Today's This week card; one of them is needed.
- **Blood pressure**: "Systolic" and "Diastolic" («Συστολική», «Διαστολική») side by side, two big fields on the
  number pad (up to three digits, "mmHg" as their placeholder), then "When?" with Date and Time starting at now, any
  day from the first day of the pregnancy up to today. Save is ready once both numbers are there; it says "Write both
  numbers.", "Check the two numbers as your monitor shows them." (numbers no monitor shows: the upper 50–300, the lower
  20–200, the upper above the lower) or "Choose a day and time that has passed." Toast "Blood pressure 118/76 mmHg
  added to your journey." No ranges, colours or words about what the numbers mean.
- **Save** («Αποθήκευση») looks muted until what, name and when are there, and stays tappable: with something missing
  it says what in a calm Sand line ("Choose what you're adding.", "Give it a name.", "Choose when it is.", "Choose a day
  and time that has passed.").
- Toasts: "Thyroid test added to your journey for Tue 6 Oct at 10:30.", "… added to your journey, done on Tue 29
  Sep.", "… added to your journey, already done.", "… added to your journey. It's waiting for a date.", or a task's
  "Task added …". She stays on New, which starts afresh so she can add another.
- No files here (they are on Results) and no marking other entries done (that is on Journey).

## 8. Results — [`08-results.jpg`](screens/08-results.jpg)

Every report and photo in one list, apart from the entries on her journey.

- Title "Results", body "Every report and scan photo, in one place and ready to show your doctor."
- "Upload results" (primary, upload icon) and, when the phone has a camera app, an outline "Take a photo" (camera
  icon). Toasts "1 file added to your results." / "3 files added to your results."; files Maia can't keep don't stop the
  others, and one Sand line names each with its reason.
- Once she has a result: a search box "Search by name or month" (name, photo or PDF, the month added; English
  or Greek, with or without accents) and "Select". Empty: "No results yet. Add the report or a photo of it.", or
  "Nothing matches your search."
- Tiles, two to a row, newest first: a small picture of the photo or of the PDF's first page (a file icon until it is
  drawn, or when the phone can't draw it), a "PDF" / "Photo" badge, the name without its file ending ("20-week scan"),
  and "PDF · Sat 3 Oct". A tap opens the viewer (screen 17).
- **Choosing and sharing**: "Select", or holding a tile, starts choosing; a tap then ticks or unticks (a tick circle on
  the tile). A bar at the bottom shows "Choose results to share" / "2 selected", "Select all", × and "Share" (muted
  until something is ticked), with "The app you choose keeps its own copy." Share opens the phone's share sheet
  ("Sharing results") with the chosen files under their names; Back or × ends choosing.
- **Shared into Maia** from another app (the share sheet, or "Open with" for PDFs): added at once; with no journey yet,
  a notice "Set up your journey first, then share the file with Maia again." and setup opens.

## 9. Care team, a card in Settings — [`09-care-team.jpg`](screens/09-care-team.jpg)

- Icon tile (people), "Care team" («Ομάδα φροντίδας»), "Link your doctors from your phone's contacts, to call and
  book." and a chevron. The card is folded to this heading until she taps it (the chevron turns up), and folds again
  with another tap or when she leaves Settings, so a long list of doctors never makes Settings long. Screen readers
  hear "Care team" as an expander, folded or open.
- **OB-GYN** («Γυναικολόγος», stethoscope tile): "Used for bookings and calls". Each linked contact: initials avatar,
  name, "Mobile · ••• 4567", a round call button and a 44 dp × ("Unlink Dr. Eleni Papadaki"), which asks "Unlink Dr.
  Eleni Papadaki? Your bookings stay in your journey." with "Unlink doctor" / "Keep it". A dashed "Link from contacts"
  only until her OB-GYN is linked.
  Toasts "Dr. Eleni Papadaki linked." / "Calling Dr. Eleni Papadaki…".
- **Other doctors** («Άλλοι γιατροί»): one smaller card per doctor she has linked (Dentist, Internist «Παθολόγος»,
  Endocrinologist, Haematologist, Cardiologist, Dermatologist, Gastroenterologist), with the role and its contacts
  (call and ×); then a dashed "Link another doctor", which asks "Which doctor?" in the phone's own list (her OB-GYN
  first, for a second contact, then the seven) and opens the contact picker.
- Only the OB-GYN is used for bookings. No emergency number here for now (the privacy notice names 112).

## 10. Linking a doctor (no image: the phone's own picker)

There is no sheet of Maia's own: linking opens the phone's own contact picker,
one contact per pick, and Maia keeps only the name, label and number. A contact without a number gives a calm note.

## 11. Reminders, a card in Settings — [`11-reminders.jpg`](screens/11-reminders.jpg)

Android only for now. Maia writes nothing to the phone's calendar (ADR 0009).

- Icon tile (clock), "Reminders" («Υπενθυμίσεις»), "Bookings and timed tasks." and a switch. Turning it on asks for
  notifications first and stays off if she says no, with a Sand note "Maia can't show notifications yet. You can allow
  them for Maia in your phone's settings." and "Open phone settings". Left on with notifications since turned off:
  "Reminders are on, but notifications for Maia are off in your phone's settings."
- While on, **When?** («Πότε;») with three equal pills: "24 hours before", "2 hours before", "Both". When they ring
  depends only on the booking's time, for bookings and timed tasks alike:
  - 24 hours before: 24 hours before, or at 20:00 the evening before for a booking with only a day, while that is
    still ahead; nothing rings in its place once it has passed.
  - 2 hours before: 2 hours before; a booking with only a day rings the evening before at 20:00 instead.
  - Both: each of them, while it is still ahead.
- While on, under a thin line, the switch row **Discreet lock screen**: "Notifications say just “Appointment” or
  “Reminder”, with no name, even when your phone is unlocked. The lock screen only ever shows “Maia reminder”."
- Every change sets the reminders again at once; one that can't be saved says so in Sand.

## 12. Timer — [`12-breathing.jpg`](screens/12-breathing.jpg)

Pushed from Today's Timer tile; Back pill, no bottom bar. A quiet helper: one timer for calm breathing or the exercises
her doctor suggested. It times, and suggests nothing.

- Title "Timer" («Χρονόμετρο»), then the body, which follows her times: "4 seconds, then 6, 12 times. For calm
  breathing, or the exercises your doctor suggested." No eyebrow, no choice of mode.
- A large soft sphere (Sky to Sea) with rings, growing in the first part and settling in the second. "Ready?" at
  rest; while running, the part's name, "First" / "Then" («Πρώτα» / «Μετά»), with the seconds left of that part
  counting down over it ("3", Title) and the time left of the session under it ("1:52"; screen readers hear "Time
  left: 1 minute and 52 seconds", and each part's name once as it begins, never every second).
- "Start" (Sea pill), "Pause" while running. Paused: the words read "Paused", the time stays, the pill reads
  "Resume", and a text button "Start over" begins again. Resuming carries on with the part she paused in; the
  vibration and tone stop while paused.
- Caption "Feeling dizzy, in pain or unwell? Stop and rest."
- A card with her times, three rows: "First" and "Then", each with its seconds under it ("4 seconds"), and "Rounds"
  with its number; each row has a round − and + (48 dp, the `RoundButton` look, Pine icon). Seconds 1–30, rounds
  1–50; a button at its limit dims, and all dim while it runs. A change while paused starts afresh. Her times are
  kept on the phone. Screen readers hear "First, 4 seconds" and "Fewer seconds" / "More seconds", "Fewer rounds" /
  "More rounds".
- A card with two switch rows, each only where the phone can do it (Android 8+ for the vibration):
  - "Gentle vibration" (on until she turns it off): "One soft pulse as the first part begins, two as the second, so
    you can close your eyes. It follows your phone's vibration settings, and stops on silent or in Do not disturb." A
    longer pulse marks the end.
  - "Soft sound" (off until she turns it on): "A quiet tone as each part begins, at your media volume. Not on silent
    or in Do not disturb." Tones are made on the phone, with a small chord at the end.
  - Turned on while it isn't running, each gives one sample.
- After the last round: "Well done" and "Start again". The screen stays on while it runs; each part and the end are
  announced. It pauses when Maia goes to the background and ends when she leaves the screen. With reduced motion the
  sphere holds still and only the words change; the vibration and tone stay.

## 13. Questions — [`13-questions.jpg`](screens/13-questions.jpg)

Pushed from Today's Questions tile; Back pill, no bottom bar. Tied to no doctor or appointment.

- Title "Questions" («Ερωτήσεις»), body "Jot them down when they come up, and keep them in one place."
- "Type a question" and a round + ("Add question"), muted and doing nothing until she has written something. A new
  question goes on top of the list, right under the field, above every other.
- Checklist rows: a tick circle ("Tick off once it's answered"), the words ("Tap to change or delete") and a handle
  (three lines; "Drag to move this question"). Ticked ones are struck through. Hold the handle and drag: the question
  follows her finger, faded in its place; held over another for 0.2 s, that one slides out of the way and she drops it
  into the gap. With reduced motion rows move at once.
- Tapping the words opens the question in place: its text, "Save" / "Cancel" and "Delete question" ("Delete this
  question?", "Delete question" / "Keep it").
- "Clear answered" whenever some are ticked: "Clear the answered questions? The ones still to ask stay.", "Clear
  answered" / "Keep them".
- A caption at the end: "No questions yet. Add one when it comes to mind.", "Tick one off once it's answered." or "All
  asked. Add another when one comes up."

## 14. Settings — [`14-settings.jpg`](screens/14-settings.jpg)

The fifth slot. No Back pill: she leaves by the bar. Leaving closes whatever was open in place (the backup form, its
password cleared; an unanswered question; a due-date edit) and folds Care team.

- Title "Settings", body "Your pregnancy, your reminders, your language and your data."
- Every card opens with a 36 dp SageTint icon tile and a Pine icon, left of its title. In order:
- **Pregnancy** («Εγκυμοσύνη», calendar icon): "Due Sun 17 Jan · Week 24 + 3", "If your doctor gives you a new due date,
  for example after a scan, change it here." and an outline "Change due date", which opens step 1's date field in place
  (with the last-period link and the Sky summary), the note "Your checks' weeks move with the new date. Bookings stay on
  the days you booked.", and "Save due date" / "Cancel". Toast "Your due date is now Sun 24 Jan. Your journey moved
  with it."
- **Care team** (screen 9), folded until she taps it.
- **Reminders** (screen 11).
- **Animations** («Κινούμενα εφέ», sparkles): "Gentle movement on the screens. Off, everything stays still." and a
  switch, on by default. Off, nothing on Maia's screens moves, as with the phone's "Remove animations".
- **Language** («Γλώσσα», globe): "English" / "Ελληνικά" pills. Switching redraws Maia and renames checks and reminders.
- **Screen lock** («Κλείδωμα οθόνης», lock; Android): "When you've been away for more than a minute, Maia asks for your
  phone's PIN, pattern or fingerprint." and a switch. Turning it on asks for the screen lock first ("Turning on the
  lock for Maia"); with no screen lock on the phone, a Sand note and "Open phone settings".
- **Backup** («Αντίγραφο ασφαλείας», upload): "One encrypted file with your journey and results, for a new phone. Keep
  it somewhere safe, like your email or Drive.", "Last backup: Thu 1 Oct" or "No backup yet.", and an outline "Create a
  backup". It opens in place: password, password again, a SandDeep hint line (length, match, guessable passwords), a
  SageTint "Good password." / "Strong password." chip, "Keep this password safe…", then "Create and share" ("Making
  your backup…" until the share sheet opens) and "Cancel". After the share sheet a Sand card asks "Did you save the
  file somewhere safe, like your email or Drive? Maia can't see where it went." with "Yes, it's saved" and "Not yet";
  only "Yes" records the backup. A Sand note names any results left out because their files no longer open.
- **Data** («Δεδομένα», phone): "Your journey and results stay on this phone, encrypted." and "Delete all data", which
  asks "Delete your journey and results? This can't be undone." with "Delete everything" / "Keep them". Deleting cancels
  every reminder; setup opens with the toast "Everything is deleted from this phone."
- **Diagnostics** («Διαγνωστικά», a clipboard list): "If something goes wrong, Maia notes an error code on this phone,
  never anything from your journey. Nothing is sent unless you share it, and deleting all data clears it." and an
  outline "Share diagnostics", which hands the file straight to the share sheet, or says "Nothing to share. Maia hasn't
  noted any problems."
- **About Maia** (the logo, a chevron): "Version, privacy notice, terms and licences" → screen 18.

## 15. Backup, on a new phone — [`15-backup.jpg`](screens/15-backup.jpg)

Pushed from setup step 1's link, setup step 2's not-ready state and the start-up screen (screen 16); Back pill, no bar.

- Title "Backup" («Αντίγραφο ασφαλείας»), body "Choose the backup file you saved, then type its password. Your journey
  and results come back on this phone."
- A card: "Choose the backup file" (outline), the chosen file's name, "Backup password", and a SandDeep line when it
  can't be opened (wrong password, not a backup, damaged, saved by a newer Maia).
- "Reminders are set on each phone. Turn them on again in Settings and your bookings ring here too." and a primary
  "Restore". Today opens with "Welcome back. Your journey is restored." The restore counts as her last backup.

## 16. Start-up (no image: reached only when something goes wrong)

Whenever Maia doesn't open straight on Today. Built from setup's parts; no bar.

- **Opening**: a quiet spinner, usually for a moment.
- **Can't open** (the key is gone after moving phones, or both copies are damaged): "Maia can't open your journey",
  "Your journey is saved on this phone, but Maia can't open it. This can happen after moving to a new phone. If you made
  a backup, restore it and your journey and results come back.", a primary "Restore a backup"; a card "No backup?" with
  "You can start again with a new journey on this phone." and "Start again", which asks "Start again? This removes the
  journey Maia can't open and its results from this phone. This can't be undone."; a quiet "Try again".
- **Needs a newer Maia**: "A newer Maia is needed" («Χρειάζεται νεότερη Maia»), "Your journey was saved by a newer
  version of Maia. Update Maia and your journey opens just as you left it.", and a Sky note "Your journey is safe on
  this phone. Nothing has changed." No actions that could change it.
- **Didn't open this time**: "Your journey didn't open this time", "Something got in the way. Maia hasn't changed or
  removed anything, so try again in a moment…", and a primary "Try again".

## 17. Result viewer — [`17-result-viewer.jpg`](screens/17-result-viewer.jpg)

Pushed from a Results tile.

- Back pill, "Rename" and "Remove" (SandDeep outline) in the top row; then the name (`Heading`), "PDF · 2 pages · 410
  KB", and a hint ("Tap a page to zoom in." / "Pinch or double-tap to zoom in.").
- "Rename" opens the name in place with "Save" / "Cancel" (toast "Renamed."); the file stays as it is.
- "Remove" asks "Remove 20-week scan from this phone? This can't be undone." with "Remove result" / "Keep it", then
  goes back with a toast.
- **Photo**: whole; pinch up to 5×, drag, double-tap. **PDF**: pages one under another, up to 60 ("Showing the first 60
  pages. To see them all, open it in another app."); a tap shows a page sharper to zoom into, with "Page 2 of 5" and
  "Back to all pages"; the phone's Back leaves the zoom first.
- **Can't show it here**: a Sand notice, then "Open in another app" with "The other app may keep its own copy, or list
  the file in its recent files."

## 18. About Maia — [`18-about.jpg`](screens/18-about.jpg)

- Back pill, title "About Maia", "Maia organises your pregnancy: your checks, bookings and results, in one calm place.
  Your doctor decides what's right for you.", and "Version 0.1 (123)".
- Three row cards with an icon tile and a chevron: "Privacy notice" (a shield; "What Maia keeps, where, and your
  rights") → screen 19, "Terms of use" (a scroll; "What you accept when you use Maia") → screen 25, and "Licences"
  ("Fonts, icons and libraries in Maia") → screen 20.

## 19. Privacy notice — [`19-privacy-notice.jpg`](screens/19-privacy-notice.jpg)

Pushed from About Maia and from setup step 1's link. The text awaits legal review and the controller's contact (see
[`../compliance.md`](../compliance.md)). Its headings are a document's and may speak to her ("Your rights").

- Back pill, title "Privacy notice", intro, "Last updated 11 October 2026".
- Short parts, each a heading and a few plain sentences: What Maia keeps · Only on this phone, encrypted · No ads, no
  trackers · Reminders on this phone · Your contacts and camera · Files from and to other apps · Backups are yours ·
  Locking Maia · Diagnostics · Your data, in your hands · Your rights · Changes to this notice · Maia organises, your
  doctor decides (with the emergency number).

## 20. Licences — [`20-licences.jpg`](screens/20-licences.jpg)

- Back pill, title "Licences", "Maia is built with these fonts, icons and open-source libraries. Thank you to everyone
  who makes them."
- Sections "Fonts", "Icons" (Lucide, ISC) and "Libraries": one card per licence with its name and "Packages: …", and
  "Show licence" / "Hide licence", which opens the full text in place.

## 21. Lock screen (no image: it needs the phone's screen lock)

Shown over Maia when the screen lock is on, at start and after more than a minute away: a full-screen window, not a
page, so the screen underneath keeps its state.

- Mist, held still. The logo, title "Maia is locked", "Your journey is safe. Unlock with your phone's screen lock to
  carry on.", and a primary "Unlock", which opens the phone's own prompt ("Unlocking Maia" / "Use your phone's screen
  lock").
- After a cancel: a Sand note "Maia stays locked. Tap Unlock when you're ready."
- Back sends Maia to the background, still locked. Unlocking fades the window out (at once with reduced motion).

## 22. Reminder notifications (no image: the phone's own)

In the language Maia shows. Channel "Reminders": "Bookings and timed tasks. The lock screen only shows “Maia
reminder”."

- Small icon: the logo in white, tinted Pine.
- Unlocked: the entry's or task's name as title ("Appointment" or "Reminder" with the discreet lock screen) and the day
  and time ("Tomorrow at 08:30", "Today at 08:30", "At 01:30").
- Locked: only "Maia reminder"; the full one shows in the shade after unlocking.

## 23. All tasks — [`23-all-tasks.jpg`](screens/23-all-tasks.jpg)

Pushed from Today's "All tasks" line, with the Today slot lit; Back pill, no add button (she adds on Today or New).

- Title "All tasks" («Όλες οι εργασίες»); "Nothing on your list." when she has none.
- One virtualised list in groups, each heading in the trimester-heading style (a screen-reader heading), no counts:
  **Past** («Περασμένες»: open, day before today) · **This week** (today and the six days after) · **Later** · **No
  date** (tasks for a trimester or a week). Within a group by day and time; No date by the week they are for (one for a
  trimester first in its first week), then any saved before tasks needed one. Empty groups don't show.
- It opens at the top; coming back to it, or a reload after a change, keeps her place.
- Tasks she ticked off are not kept: they are deleted as the list loads (opening it, coming back, after a change), with
  their reminders. No question and no toast.
- **The task row** (the same control here and on Today): a tick circle ("Tick off"; a ticked task stays, struck
  through, until the list is shown again), the words ("Tap to change or delete") and the clock line in Sea ("Thu 1 Oct ·
  09:00", "Week 26", "3rd trimester", "After birth"). Tapping the words opens it in place: its words, the "Date and
  time" switch with Date and Time, or while it is off "When?" with "Trimester" or "Week" (needed: "Choose when it
  is."), and three equal buttons "Save", "Cancel", "Delete task"; "Delete task" asks "Delete this task?" with
  "Delete task" / "Keep it". Toasts "Task updated." / "Task deleted." One task is open at a time. Task words hold up to
  200 characters, with "20 characters left" / "That's all that fits" near the limit.
- When her list is full, a calm notice: "Your list is full: Maia keeps up to 500 tasks, done ones too. Open “All
  tasks” to clear the done ones."

## 24. Calendar (no image yet: `capture-screens.ps1` saves it as `24-calendar.jpg`)

Her journey as a month, to look at only. Pushed from the week pill on Today, with the Today slot lit; Back pill, no
bottom bar. Nothing here changes her journey (that is on Journey, Today and New), and nothing goes to the phone's
calendar (ADR 0009).

- Title "Calendar" («Ημερολόγιο»).
- **The month** (a card): its name between ‹ and › ("October 2026", «Οκτώβριος 2026», `Heading`), the weekday names
  from the first day of the week where she lives ("Mon … Sun", «Δευ … Κυρ»; `Label` in Muted), and six rows of days,
  always six, so the card never changes height. **Every day shows**, with or without something on it; days of the
  month before and after are faded (Muted at 50 %). Today's number is Sea and Bold; the chosen day is a Pine circle
  with a White number, a Sea circle when it is today. Screen readers hear each day as "Tue 6 Oct, today, Booked, Task,
  button".
- **Dots** under a day's number, one for each kind of thing that day, in the status colours: Sun the due date, Sea
  booked, Sage done (a booking whose time has passed, an entry done on a day, a ticked task), Muted a task still to
  do. No counts.
- **Moving**: ‹ and › (44 dp, Pine chevrons, faded at the ends) or a swipe left or right move a month, from the month
  her pregnancy began to the month of week 52 (further when something of hers is dated further). Another month opens
  on its first day, or on today in this month. Tapping a faded day of the month before or after goes to it. No
  animation, so nothing moves with reduced motion.
- **The chosen day**, under the card: its date ("Tue 6 Oct", `Heading`), then what is on it, one card each: the name
  (Muted once done), its status chip ("Booked", "Done", "Due date", or a neutral "Task") and its time in Sea ("10:30";
  none for a day without a time). Order: the due date, then entries by time, then tasks by time. With nothing on it:
  "Nothing on this day." («Τίποτα αυτή τη μέρα.») in Muted. Nothing here can be tapped.
- What has no day is not here: a check not booked yet, one done in a week or a trimester, a task for a week or a
  trimester. Those are on Journey.
- It opens on this month with today chosen; coming back to it (from the background, or on a new day) keeps the month
  and day she was looking at.

## 25. Terms of use — [`25-terms-of-use.jpg`](screens/25-terms-of-use.jpg)

Pushed from About Maia and from setup step 1's link; built like the privacy notice (screen 19). The text awaits legal
review and the maker's name and contact (see [`../compliance.md`](../compliance.md), Terms of use). Its headings are a
document's, like the notice's ("Your data and backups").

- Back pill, title "Terms of use" («Όροι χρήσης»), "What you and Maia's maker agree to when you use Maia, in plain
  words.", "Last updated 11 October 2026".
- Short parts, each a heading and a few plain sentences: These terms (accepted by setting up or using Maia; adults, for
  her own pregnancy) · What Maia is (organises; not a medical device, no advice, no interpretation; her doctor decides)
  · A typical journey (a common starting point, may not suit hers) · Not for emergencies (with the emergency number) ·
  Reminders and the timer (can come late; the timer only counts time) · What you enter · Your data and backups (only on
  this phone, nobody can bring it back) · Maia as it is · Limits of liability (as far as the law allows, never what the
  law won't let anyone limit, and never her rights as a consumer) · Using Maia · Changes to these terms (each version
  carries its own) · Greek law.
