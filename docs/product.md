# Maia: product brief

The product decisions behind the code. Read this before building any screen or feature.
Design (look, feel, screens) is in [`design/`](design/README.md). Store, privacy and regulatory matters are in
[`compliance.md`](compliance.md).

## What Maia is

**Maia organises a pregnancy the way a good midwife would.** It proposes the checks she will need, keeps
them booked and reminds her of them, and keeps every result together in one place.

Most pregnancy apps track the baby's size week by week. Maia helps with the actual work: knowing which
exams are coming, booking them, remembering them and keeping the paperwork. At the start she doesn't know
what to expect, and her doctor's plan changes after every visit, so Maia starts from a typical plan and she
adds what changed in a few taps after each appointment.

*μαία* (maia) is Greek for midwife.

## Positioning rules (never break these)

- **Never say or imply that Maia replaces a midwife, doctor or any care professional.** Not in UI text,
  store listings, docs, comments or code names. Maia *organises*; her care team *decides*.
- **Her doctor's advice wins.** Every plan screen makes clear the plan is a typical starting point she can
  change. Maia gives no medical advice and does not interpret results.
- **Maia is its own product.** Don't frame it around fathers or "dads", and don't link it to any other
  app. The user is the expecting mother; a partner is a secondary role.
- **Calm, never alarming.** No red, no warning-style urgency, no countdown pressure (see design).
- **No ads, ever. No third-party trackers.**
- **An organiser, not a medical device.** Maia organises appointments and paperwork; it does not diagnose,
  monitor, predict or recommend treatment. Wording rules for listings and copy are in
  [`compliance.md`](compliance.md) (ADR 0008).

## Who it's for

| Persona | Main job for Maia | Biggest pain today |
|---|---|---|
| First-time mother (**primary**) | Show me which checks are coming and keep them organised | Doesn't know which exams to expect, or when |
| Second-time mother | Book fast and keep results together | Reports scattered across bags, inboxes and lab portals |
| Partner | Know the dates and be there | Hears about appointments late |
| Her OB-GYN | See the right results at the visit | The patient can't find last month's report |

UI copy addresses **her** in the second person ("your journey", "your doctor"). What Maia keeps for her pregnancy is
her **journey** in English and her **πλάνο** in Greek, everywhere (see [design: Words](design/README.md#words)).

## Features

All about organising: the journey, the bookings and the paperwork.

The main screens sit on a bottom bar of five flat, equal slots: **Today · Journey · New · Results · Settings**
(Σήμερα · Πλάνο · Νέο · Αποτελέσματα · Ρυθμίσεις).

1. **Plan setup.** She enters her due date, or the first day of her last period. Maia proposes the common
   checks, dated from her due date, grouped by trimester, each tagged *Routine*, *Offered*, *Often offered*
   or *If advised*. She switches off what doesn't apply. Checks already in the past show as
   "probably done". Then she links her OB-GYN from contacts and turns on "Reminders on this phone" (both skippable), which asks for
   notifications. If Android closes Maia half-way, setup resumes where she was.
2. **Journey and bookings.** Her checks and the entries she added on one timeline, laid out like a calendar, with a
   "You are here" marker; it opens at today every time, so late in the pregnancy she never scrolls to find it. Every
   entry is Not booked, Booked or Done (item 41) and shows its trimester, week and, when booked, day and time (no
   reminder words, item 43); each pregnancy week has its heading ("Week 25", "11–17 Oct"), empty ones too. Not
   booked: **Book** (date and time, past or to come) and **Delete**. Booked or done: **Edit** (name and when) and
   **Delete**. A booking turns Done by itself once its time has passed; a check kept at setup whose weeks passed unbooked
   reads "Probably done". Delete asks first and removes it for good. Once on her journey, a check from setup is hers as
   much as one she added. An entry she added says what it is (Doctor visit, Exam or test, Hospital visit, Something
   else), and opened shows its note: a typical journey's, or the one she wrote. The "Tasks" switch (on until she turns
   it off) puts her tasks on their day, at their week or first in their trimester too. Cards hold no results: those
   are on Results.
3. **New** (Νέο). One short page: **what** it is (Doctor visit · Exam or test · Hospital visit ·
   Something else), its **name** (a visit starts as "Visit with <her OB-GYN>", or "Appointment" with none linked,
   and a hospital visit as "Hospital visit", both editable; an exam offers suggestion chips, Maia's usual extras, or
   her own words), an optional **note** shown when its card is opened on Journey, and **when** (Booked for a date and
   time, past or to come · Already done on a date and time, in a trimester or in a week · Not booked yet, in a
   trimester or a week; "After birth" is a fourth choice beside the trimesters). Save adds one entry: booked gets its
   reminders (done once its time has passed), the others stand at their trimester or week. A fifth pill, **Task**,
   adds a task instead, as the + on Today's This week card does (its words, and a date and time if she turns them on,
   the day may have passed, or else a trimester or a week). New is a tab; once saved she stays on it and it starts afresh, so she can add another. No files
   here (they are on Results) and no marking other entries done (that is on Journey).
4. **Results.** PDFs and photos of paper reports, all in one list, newest first. Lab-portal links are gone (item 33;
   links she had added are dropped from her plan). She can choose several results and send them on through the
   phone's share sheet (email, WhatsApp, Viber…), free for now. Results stand on their own, apart from the entries on her journey (item 30): she adds them here ("Upload
   results", "Take a photo") or shares them into Maia from other apps (the share sheet, and "Open with" for PDFs),
   and finds them with one search box. Results open inside Maia, ready to show at a visit.
5. **Care team** (a card in Settings since item 29, folded until she taps it). Link one or many phone contacts to a
   role: "Link from contacts" for her OB-GYN until one is linked, then "Link another doctor" (her OB-GYN first). Her
   **OB-GYN** is used for bookings and who her next appointment is with. Smaller tiles link her other
   doctors to call: Dentist, Internist (Παθολόγος), Endocrinologist, Haematologist, Cardiologist, Dermatologist and
   Gastroenterologist. The "coming next" roles, the 112 emergency tile and the calendar card are gone from this
   screen at the owner's request; the emergency number is to be added somewhere else later.
6. **No calendar sync** (item 37, ADR 0009). Maia writes nothing to the phone's calendar; its reminders are its own
   notifications (10).
7. **Today** (Σήμερα, the first slot). Where she is (the week, and the journey card with the due date), Next up (her
   next booking, and who it is with when Maia knows; a tap opens Journey on it), this week's tasks (optionally with
   date and time; one "All tasks" line opens **All tasks**, every task by group) and a calm reminder to keep a backup.
   The week pill at the top, with a calendar icon, opens **Calendar**: her journey as a month, like the phone's
   calendar app but inside Maia and read-only. Every day of the month shows, a dot under a day for each kind of thing
   on it (booked, done, a task, the due date), and the day she taps lists what is on it. Nothing there changes her
   journey, and nothing goes to the phone's calendar (6).
   Plus **Questions** (her own checklist, tied to no appointment, a new one on top, in the order she drags them into)
   and the **Timer**, a quiet helper rather than a selling point: one timer of a first part, then a second, a number of
   times (4 seconds, then 6, twelve times at first: two calm minutes), for calm breathing or the exercises her doctor
   suggested, such as pelvic floor exercises, with the seconds and rounds she sets, a countdown, a gentle vibration at each
   part (on until she turns it off) and an optional soft tone (off until she turns it on). It times only: no
   programme, no reminders, no record of what she did. No
   "needs booking" card and no statistics: the owner found them confusing and not what a pregnant woman wants to see.
8. **Partner and doctor view** (paid). Read-only view of her plan, bookings and the results she picks;
   invite by link; revocable at any time.
9. **English and Greek.** The phone's language when it is English or Greek, otherwise English; she can choose the
   other on the first setup screen, and switch in Settings at any time, including the plan templates.
10. **Reminders on this phone** (Android), turned on in setup step 3 ("Reminders on this phone") or on the Reminders
    card in Settings. Notifications for upcoming bookings of any entry and timed tasks. "When?": 24 hours before (the evening before for a day without a time), 2 hours before,
    or both; a booking made less than a day ahead rings 2 hours before instead, and a day without a time rings the
    evening before whichever she chose. Journey names no reminders (item 43).
    The lock screen shows only "Maia reminder"; "Discreet lock screen" keeps names out of the notifications too.
11. **Settings** (the fifth slot). Change the due date, her care team, reminders (on or off, when, discreet lock
    screen), animations, language, an
    optional lock with the phone's screen lock, an encrypted backup, delete all data, diagnostics to share, and About Maia (version, privacy notice,
    licences).

### Out of scope (decided, do not add)

- Symptom check-in / mood tracking
- Kick counter
- Weekly baby-size or fruit-comparison content, articles, forums
- Anything that interprets results or gives medical advice
- Anything that reads into her blood pressure: ranges, colours, flags, averages, charts, trends, reminders to measure

## Release scope

**Free v1: organise the pregnancy, offline, one user**

- Setup: due date or last period, a typical journey with switches, OB-GYN from contacts, "Reminders on this phone"
- Journey: book with date and time, edit (with a note), delete; reminders (on Android, as Maia's own phone
  notifications: 24 hours before and/or 2 hours before); her tasks on their day, week or trimester; a heading
  for every week
- New: a doctor visit, an exam or test, a hospital visit, something else or a task, booked, done or not booked yet
- Results in one list (files, photos, camera, shared files), apart from her journey, with one search box; choose
  several and share them (email, WhatsApp, Viber…)
- Care team: her OB-GYN and seven other doctors to call
- No calendar sync: Maia writes nothing to the phone's calendar (ADR 0009)
- Today, tasks with a date (and maybe a time) or a trimester or week, questions list, breathing
- English and Greek; all data encrypted on the phone; optional app lock
- Available in Greece only until another country has a clinically reviewed template

**Paid upgrade, one-time purchase** (never a subscription, never ads)

- Encrypted cloud backup and restore to a new phone (end-to-end: the server stores only ciphertext)
- Partner and doctor view: read-only, invite by link, revocable
- Visit pack: chosen results shared as one PDF with the doctor

**Free, after v1:** fourth trimester, meaning postnatal checks for mother and baby on the same timeline.

**Later, new scope:** more care-team roles in use (midwife, hospital, lab); a new place for the emergency number
(112), which the owner took off Care team; a portal where the OB-GYN or midwife sees shared plans (needs a backend
and consent flows).

## Decisions

| Topic | Decision |
|---|---|
| First market | Greece. English default + Greek from v1 serves Greek speakers and the many foreign residents, and makes expanding abroad quick. |
| Store availability | Greece only (Play country targeting, App Store availability) until another country's template is clinically reviewed. |
| Intended purpose | An organiser of appointments and paperwork, not a medical device; to be confirmed by a regulatory opinion before the paid launch (ADR 0008). |
| Business model | Free app + one paid upgrade bought once. No ads, ever. |
| Source code | Open source under the Apache License 2.0 since November 2026. The licence grants no rights to the Maia name or logo. |
| Storage | Everything on the phone, encrypted (AES-256-GCM, key in the platform keystore). Not copied to cloud backup or a new phone. Paid tier adds encrypted cloud backup. Nothing goes to the phone's calendar. |
| Lost data key | A calm start-up screen: restore a backup or start again. A new key is never made over her files (ADR 0002). |
| Backup | One encrypted file she sends where she chooses (ADR 0005). Password of 10 or more characters, guessable ones turned away. It counts as kept only once she says the file is saved. |
| Sharing | Read-only view for partner and doctors, part of the paid upgrade. |
| Platforms | Android and iOS with .NET MAUI. Android first; iOS later. Windows target is for quick local UI checks only. |
| Clinical content | Each country's template is written or reviewed by a licensed midwife or OB-GYN, versioned and dated (`reviewedBy`, `reviewedOn`). Greek template is **Draft** until then; store builds can't start a plan from it. |
| Template updates | Existing plans take a newer template at start-up: its wording, weeks and categories. Checks it adds join switched off, so nothing new to book appears without her; she brings them back from Add if her doctor asks. Bookings, results, checks she has acted on and her own entries are untouched (ADR 0003). |
| Check lifecycle | Marking a check done before its booked day drops the unused booking. A done check is reopened ("Not done yet") before it can be marked not needed, so its record stays. Saving the same booking again changes nothing. |
| Plan language | The plan follows the app's language at every start, so check titles and reminders match the screens. |
| Calendar | No calendar sync (the owner, 2026-10-04; ADR 0009, superseding ADR 0004): Maia writes nothing to the phone's calendar and asks for no calendar permission. Her journey has its own read-only month view inside Maia instead, Calendar, opened from Today's week pill (item 47), so she sees her dates without leaving Maia and nothing leaves the phone. Maia is only on test phones, so nothing was migrated or cleaned up: calendars and events an earlier build wrote stay as they are. |
| Navigation | Five flat, equal slots: Today · Journey · New · Results · Settings (Σήμερα · Πλάνο · Νέο · Αποτελέσματα · Ρυθμίσεις; item 42). New and Settings are tabs like the others, with no gear on any screen; her care team is a card in Settings (item 29). The five names share one size and shrink together in Greek. |
| Words | One word for each thing, the same in every place and in both languages (the owner, 2026-10-04, item 42). What Maia keeps for her pregnancy is her **journey** (English) and her **πλάνο** (Greek), never "plan" or «διαδρομή». Tabs, titles, headings, tiles and links are nouns, and a screen's title is the name she tapped ("instructions feel commanding"); buttons are verbs. Rules and the full list in [design: Words](design/README.md#words). |
| Look | One of everything (the owner, 2026-10-04, item 42): one font, 14 colours (three families, Green · Sea · Sun, of three steps, and five neutrals), 6 text sizes, one icon set (Lucide), one logo (the filled sprout). Screens use only these, through the styles and keys in `Resources/Styles`; tests keep it that way. Screenshots are captured from the app. See [design](design/README.md). |
| Today | The first screen (once "Your week"; renamed with item 42, as it shows more than the week: Next up may be weeks away). Title "Today" with the week in its pill, no greeting. No "needs booking" card and no statistics (the owner: the booking it showed was not the full picture, and no pregnant woman wants these stats). Next up is her next booked entry, never a task; "with …" shows only when Maia knows who it is with; no call button and no questions pill; a tap opens Journey on it. It is the one screen that shows the week and the due date. Its This week card shows this week only (5 rows, 20 after "Show all"; then one "All tasks" line; item 38), so it is as quick with 900 tasks as with 5 (item 26). |
| Journey | The timeline is called Journey in English and Πλάνο in Greek (item 42; once «Διαδρομή»). It opens at today every time, unless she comes straight back from a screen opened over it. The "Tasks" switch is on until she turns it off, and remembered on the phone (not in the journey or the backup). The first row of each pregnancy week has the week's number on the line (item 43, the owner's "something subtle"); cards name no reminders. |
| Entries she adds | One page, New: what, name, when, then Save. An optional note she writes is shown when its card is opened, as a typical journey's note is (item 43). Nothing is chosen at first, so no day she didn't pick is booked. A booked day can be any day from the start of the pregnancy, past or to come; a done day from the start of the pregnancy up to today. A task's day can have passed too (up to 44 weeks back); a task without a date is for a trimester (or after birth) or a week, chosen as an entry's, and stands there on Journey and under "No date" on All tasks (item 43). An entry stands at its day on Journey and moves with it. Her old "next visit" became an ordinary Doctor-visit entry (stored format 5). |
| Blood pressure | Strictly a logger (the owner, 2026-10-11, item 45): Maia shows the two numbers she typed and when she took them, nothing more. No normal or high ranges, colours, flags, words about what they mean, averages, charts or trends, no reminders to measure, and nothing on her journey changes because of a reading. The only check catches typing mistakes (numbers no monitor shows). |
| Results | Results stand on their own, apart from the entries on her journey: no entry holds results and Journey cards have no results panel (item 30, stored format 6, which moved every result filed under an entry into the one list). |
| Care team | A card in Settings (once a tab). Folded until she taps it, and again each time she leaves Settings (item 43). "Link from contacts" only until her OB-GYN is linked; "Link another doctor" lists her OB-GYN first. Her OB-GYN plus seven other doctors (Dentist, Internist, Endocrinologist, Haematologist, Cardiologist, Dermatologist, Gastroenterologist), in smaller tiles. Only the OB-GYN is used for bookings. No "coming next" roles, no 112 tile for now. |
| Timer | One timer (items 46 and 48, the owner, 2026-10-11: Breathing and Exercise were the same thing, so they merged): two parts, "First" and "Then" («Πρώτα», «Μετά»), and rounds that she sets (1–30 s each, 1–50 rounds), remembered on the phone; it starts at 4/6 ×12. The sphere counts down each part's seconds. A "hidden" helper, not a selling point: it names no exercise, suggests none, keeps no record and sends no reminder. A gentle vibration as each part begins (on by default) and an optional soft tone made on the phone (off by default; no sound files, so no licences), a countdown of the session, nothing on silent or in Do not disturb, and it pauses when Maia goes to the background. Android only for now. |
| Reminders | Maia's own phone notifications on Android, off until she turns them on (setup step 3 or Settings). They depend only on the booking's time: 24 hours before (the evening before at 20:00 for a day without a time) while ahead, and nothing in its place once that has passed (a booking made less than a day ahead gets none); 2 hours before (the evening before for a day without a time); or both, each while ahead (ADR 0009). Inexact alarms (can arrive up to an hour late), no exact-alarm permission, so the words name the day and time. Only "Maia reminder" on the lock screen. iOS later (ADR 0006). |
| Home time zone | The plan keeps the zone it was made in; bookings and reminders stay at their time at home when she travels. There is no setting to change it yet. |
| App lock | Optional, off by default, Android: the phone's own screen lock after more than a minute away; the recents thumbnail is hidden. |
| Viewing results | PDFs and photos open inside Maia. A file Maia can't draw (a protected PDF, or a HEIC photo on Android 7 and 8), or a PDF over 60 pages, goes to another app only when she asks, with a note that the app may keep a copy. |
| Sharing into Maia | PDFs and photos shared from other apps are added to Results at once; her choice in the share sheet is the confirmation, and × removes one she didn't mean to keep. Maia needs no camera or storage permission. |
| Photos | Photos over 3000 px, and HEIC, are re-encoded without metadata; smaller JPEG/PNG/WebP are kept as picked, with the GPS block wiped from JPEGs (XMP/PNG location and HEIC on Android 7-8 are not stripped; ADR 0002). |
| Diagnostics | A problem log on the phone with error codes only; shared only when she taps "Share diagnostics", which hands the file (codes, times, Maia's and Android's versions) straight to the share sheet, once (ADR 0007, amended). No crash-reporting SDK, no analytics. |

## Privacy and safety

- Health data (GDPR Art. 9): plans and results stay on the phone, encrypted at rest. One-tap export (an
  encrypted backup) and delete. The analysis and the open questions for the owner are in
  [`compliance.md`](compliance.md).
- The free app has no internet permission, no account and no server.
- The privacy notice is in the app (About Maia, and a link at setup step 1), in English and Greek. A hosted copy,
  the controller's contact and a legal review are still needed before a store release.
- The terms of use are in the app too, in the same two places and both languages: setup step 1 says that using Maia
  means accepting them. They limit the maker's liability only as far as the law allows (compliance.md, Terms of use),
  and need the maker's name and contact and a legal review before a store release.
- Contacts: Maia reads only the contacts she picks, and stores just name, label and number.
- Calendar: Maia neither reads nor writes the phone's calendar, and has no calendar permission (ADR 0009).
- Reminders: notification permission is asked only when she turns reminders on. "Discreet lock screen" keeps names out
  of the notifications even on an unlocked phone. For after a restart the phone
  keeps only plain running numbers, times and words that name no check or task.
- Optional app lock with the phone's own screen lock; Maia never sees the PIN, pattern or fingerprint. The recents
  screen never shows her plan (from Android 13 always; earlier, while the lock is on).
- The problem log stays on the phone, holds codes only (with their times and the Maia and Android versions), is
  shared only when she taps "Share diagnostics", and Delete everything removes it.
- Not for emergencies: the privacy notice says so and names the emergency number (112 in Greece/EU). Care team no
  longer shows it, at the owner's request ("we will add it somewhere else in the future"); for now the notice is the
  only place in the app that does, and a new place for it is an open item in the build order.
- Never log health data (see `CLAUDE.md`).

## Glossary (use these words in code and UI)

Code keeps its names; the UI uses the words she reads (English / Greek), as the design's
[Words](design/README.md#words) list them.

| Term (code) | She reads | Meaning |
|---|---|---|
| Plan (`PregnancyPlan`) | journey / πλάνο | Her list of checks and entries for this pregnancy, with her tasks, questions and results |
| Journey (route `plan`, `PlanPage`, `PlanViewModel`) | Journey / Πλάνο | The screen that shows her journey as a timeline (once "Your plan", then «Διαδρομή» in Greek) |
| Template (`gr.json`) | a typical journey / ένα συνηθισμένο πλάνο | Her country's suggested checks, offered at setup |
| Today (route `week`, `WeekPage`, `WeekViewModel`) | Today / Σήμερα | The first screen (once "Your week") |
| New (route `add`, `AddEntryPage`, `AddEntryViewModel`) | New / Νέο | The screen that adds one entry or a task (once "Add to your journey"; it replaced After your visit) |
| Timer (`BreathePage`, `BreatheViewModel`) | Timer / Χρονόμετρο | One timer with times she sets, for breathing or exercises (once "Breathing" and "Exercise", before that "Breathing", and before that "Breathe") |
| Calendar (route `calendar`, `CalendarPage`, `CalendarViewModel`) | Calendar / Ημερολόγιο | Her journey as a month, read-only, from Today's week pill |

| Term | Meaning |
|---|---|
| Check | One exam, scan, test or visit on her journey (`PlannedCheck`) |
| Entry | Anything on her journey; in particular one she added on New, which has a kind (`EntryKind`: Doctor visit, Exam or test, Hospital visit, Something else). Checks from her country's typical journey have no kind |
| When | How an entry stands: Booked (date and time), or Not booked yet or Already done in a trimester (or after birth) or a week (`EntryWhen`). A booked one stands at its day on Journey and moves with it |
| Window | The weeks in which a check usually happens (`WeekWindow`) |
| Booking / appointment | An entry with a date and time (`Appointment`). Her "next visit" is no longer a thing of its own: it is her next booked entry |
| Task | A to-do ("Call the clinic…") for a date (and maybe a time), or with no date for a trimester or a week (`PlanTask.Trimester`, `Week`; item 43) |
| Result | A PDF or a photo (`ResultAttachment`), kept in her results (`PregnancyPlan.Results`), apart from the entries on her journey |
| Reading | A blood pressure reading she logged on New (`BloodPressureReading`, `PregnancyPlan.BloodPressure`): the two numbers as her monitor shows them, and the day and time she took it. Shown small on Journey; deleted, never edited; never interpreted |
| Care team / role | Linked contacts per role (Ομάδα φροντίδας): her OB-GYN, and seven other doctors to call |
| Reminder | One of Maia's own notifications on this phone (`PhoneReminder`); when it rings follows `ReminderTimes` and her `ReminderSettings` ("When?", "Discreet lock screen") |
| Status | Derived at a moment: Not booked, Booked, Done (a booking whose time has passed, or one done in a trimester or week), Probably done, Due date. Never stored; what is stored is the state (`CheckState`: Not booked, Booked, Done). "Probably done" never applies to entries she added |
| Home time zone | The zone her journey's times are in (`TimeZoneId`) |

## Build order

What exists and what comes next. Keep this list current when a slice lands. "Review #N" is the code-review finding
the commits cite.

1. ✅ Core: domain, use cases, encrypted storage, Greek template (draft)
2. ✅ Setup (due date, suggested plan), Your week, plan timeline (read-only), English + Greek
3. ✅ **App shell to match the design:** five-slot bottom bar (Week, Plan, **Update** as the raised centre
   button, Results, Team), Fraunces + Figtree fonts, background and card styles from `docs/design` (now six flat
   slots, item 22)
4. ✅ Plan actions: tap a check to expand it (see screen 06); book with date and time, move, done, not needed (adding a check moves to After your visit, now Add)
5. ✅ Setup step 3: OB-GYN from contacts + calendar choice
6. ✅ After your visit (done, results, new checks, next appointment); replaced by Add in item 22
7. ✅ Results: per-check attach (file, photo, link) and the Results tab (one list with tags since item 22)
8. ✅ Care team: OB-GYN contacts, call button, "coming next" roles, emergency number (coming next and 112 removed,
   other doctors added, item 22)
9. ✅ Calendar sync (removed in item 37; Plugin.Maui.CalendarStore) and reminders, with the Calendar sync screen (now a card in Settings,
   item 22)
10. ✅ Tasks with times on Your week, Questions, Breathe
11. ✅ Settings: language, generic calendar titles (on Calendar sync), delete all data, export as an encrypted backup with restore on a new phone (ADR 0005)
12. ✅ Release pipeline: offline manifest (no network permissions), smaller Release APK (R8, resource linking, unused
    fonts removed), CI Release APK with an emulator smoke test, trim analysis and the store-gate check, `release.yml`
    for a signed AAB and APK, the personal draft-template build and setup step 2's blocked state (review #1, #7, #25,
    #41, #43, #46, #79, #86)
13. ✅ Storage, data key and recovery: the key is never replaced over her files, start-up recovery screen, durable
    writes and `.bak` rules, plans from a newer Maia refused, broken records repaired on load, Delete everything
    forgets the key, no cloud backup or device transfer (review #2, #44, #55, #61, #63, #64, #84)
14. ✅ Calendar reliability (removed with calendar sync in item 37): repair and honest messages, one lock for all calendar work, a local calendar on Android,
    encrypted links, discreet calendar name, delete-all removes the calendar (at once, or without access the next
    time Maia has it, never reusing it, and Calendar sync says so until then), a calendar of hers that took the id of
    Maia's never touched, past appointments and her own events kept, next visit in the agenda (review #12, #18, #28,
    #29, #32, #42, #48, #49, #52, #53, #59, #60, #69, #80)
15. ✅ Check lifecycle: Not done yet, Cancel booking, Add back to my plan, "How did it go?" for past bookings, her
    doctor's checks never "probably done", finishing early drops the unused booking, removing a result or a contact
    asks once more, the plan follows the app's language (review #3, #4, #9, #10, #17, #24, #54, #56, #57, #65, #66)
16. ✅ Results: take a photo, share PDFs and photos into Maia, the in-app viewer, smaller photos (location wiped
    where Maia can), no plaintext copies left in the cache, files read one at a time, unreferenced files tidied, a
    backup that skips files it can't read (review #3, #14, #23, #36, #45, #50, #62, #75, #85)
17. ✅ Setup and visits: "Booked / Not booked yet" for the next appointment, choosing whom to call, Breathe's two
    minutes, setup that resumes after Android closes Maia, template sign-off fields and newer wording for existing
    plans, dates and numbers in the shown language, picker ranges (review #8, #11, #14, #19, #44, #67, #68, #70, #71,
    #72, #81, #82, #83)
18. ✅ Settings and privacy: change the due date, the backup password rule, About Maia with the privacy notice and
    licences, the setup privacy line, the optional app lock and recents hiding (review #5, #6, #30, #37, #63, #64)
19. ✅ Robustness: screen loads and taps that never close Maia, the problem log and Share diagnostics, Your week and
    Your plan that keep up with the day, one navigation at a time, opening straight on Your week (review #15, #16,
    #31, #33, #34, #35, #44, #73, #74)
20. ✅ Your week and Questions: change or delete tasks and questions, tasks without a date, clear answered questions,
    the backup reminder, Reminders on this phone, one home time zone on the plan (review #13, #14, #21, #22, #44, #58)
21. ✅ Test fakes that behave like the phone (review #51); docs, compliance draft and release checklist (review #39,
    #40)
22. ✅ **The owner's change request** after she used the Release build on her phone (2026-10-01, "make the app
    simpler and more intuitive"; "change request #N" in the commits):
    - Six flat slots Week · Journey · Add · Results · Team · Settings; no gear; Settings is a tab; "Your plan" is
      now Journey (Διαδρομή) and its button reads Add (#4, #12)
    - Your week without the needs-booking card and the plan stats; Next up is her next booked entry or timed task,
      and says who it is with only when Maia knows (#1)
    - Calendar sync is a card in Settings, without the agenda; she chooses Maia's calendar on this phone or one of
      her Google calendars; events carry Maia's mark, so a reinstall or a new phone finds them again, and the new
      phone puts back bookings an old phone took out; changing phones: choose the same Google calendar again and
      just uninstall Maia on the old phone (#2, #3)
    - Add to your journey: one page, what, name and when; stored format 5 (entry kinds, untagged results, her next
      visit made a Doctor-visit entry, entries at their day) (#5)
    - Journey opens at today, can show her dated tasks ("Show my tasks"), labels what each entry she added is, and
      scrolls smoothly in the Release build (`LightBorder`, layers, ambient motion held while scrolling); the card's
      button reads "Upload result" (#6, #7, #8, #13)
    - Results: "Upload results"; one list, newest first; untagged uploads and shares, with a question to tag them;
      filters by kind and by tag (#9, #10)
    - Care team: tiles for seven other doctors; coming next, 112 and the calendar card removed (#11)
    - Breathe: a gentle vibration at each breath, a countdown and an optional soft tone (#14)
23. ✅ **Simpler, with less text** (the owner's second round on her phone):
    - Your week and Journey without their round calendar buttons (calendar sync is in Settings); the "In calendar"
      pill and the calendar notes stay
    - Lab links can no longer be added, from a card or on Results; links added before still show and open, and
      Results shows its "Links" filter only while she has one
    - Journey cards: "Book" (Κλείσε) whether or not sync is on, "Save new time" when moving; "Upload" (Ανέβασε)
    - Add: no visible "What is it?" above the kind pills (screen readers still hear it)
    - Settings: "Delete all data" asks one short question answered by a round check (delete) and × (keep);
      "Share diagnostics" hands the file straight to the share sheet, or says in a toast that there is nothing to
      share (no preview panel)
24. ✅ **One font** (the owner, 2026-10-02): M PLUS Rounded 1c, soft and rounded, for English and Greek alike, titles
    in Bold. It replaces Fraunces, Figtree and the Noto fonts Greek needed; the bundled fonts go from about 2.8 MB to
    about 850 KB. On Your week, the journey card's three labels no longer run into each other in Greek.
25. ✅ **Fewer words at the top of each screen** (the owner's third round on her phone, 2026-10-02):
    - Your week: no "Good morning" greeting, and no "In calendar" pill on Next up (with sync on, everything goes to
      her calendar, so it said nothing new); a timed task's "in calendar" and Journey's "In calendar" pill stay
    - Journey: no eyebrow with the week and due date; "You are here" (Είσαι εδώ) without the week; "Show my tasks" is
      now a "Tasks" (Εργασίες) switch under Add
    - Add, Results and Care team: no eyebrow above the title (the week, "6 files", "Linked from your phone")
    - The week and the due date show only on Your week, in its pill and journey card
26. ✅ **A big plan stays quick: Your week's tasks collapsed** (a load test of the Release build on the emulator,
    2026-10-02; the owner chose to collapse Week's tasks):
    - Your week's task card shows this week only (due today or in the six days after, with or without a time): at
      most 5 rows, "Show all 12" up to 20, then "All 34 in Your tasks". Open tasks from before today are one calm Sand
      line below them ("12 from earlier"), never above; Later, No date and Done are a line each with how many. Each
      line opens Your tasks at its group. The count says what it counts: "5 this week" ("5 εργασίες"). A round + in
      the card's header opens "Add a task" (the row at the bottom is gone); a task added for this week appears in the
      card with a short glow (none with reduced motion). While Your week first loads it shows calm blank cards, never
      "Nothing booked yet".
    - **Your tasks** (new, pushed from Your week with its slot lit, *awaiting design sign-off*): every task in one
      virtualised list grouped Earlier · This week · Later · No date · Done, each row ticked, changed and deleted as
      on Your week (one row control and one set of row behaviour for both screens). "Clear done" asks once more in
      place, answered with a round check or ×, and clears every ticked task (`PregnancyPlan.RemoveDoneTasks`,
      `PlanService.ClearDoneTasksAsync`); the calendar and the reminders on the phone follow as for a deleted task.
    - Why: each of Your week's task rows cost about 12 ms and 70 JNI global references; 500 open tasks froze the
      screen for 3.9 s, and past about 640 Maia reached .NET for Android's ceiling of 46,080 references and never
      started again (a restored backup is not capped). Your week now builds at most 20 task rows. Measured on the
      emulator (Release x64): start to Your week drawn about 2.5 s / 2.8 s / 3.2 s for the small, 500-task and
      1,000-task plans, from about 3.3 s / 7.1 s / a hang, with about 1,400 / 1,750 / 1,700 global references, from
      about 2,000 / 23,700 / past 46,000; the 1,000-task backup now restores onto Your week.
    - Fixes the load test found: a refusal or error is a calm Sand toast with no tick (`INotifier.ShowNotice`), and
      "Your list is full" says how to make room; Next up no longer shows a timed task whose time has passed at home;
      on Journey, today's tasks whose time has passed stand before "You are here", like bookings; Restore goes on to
      Your week once her plan is restored, even if the file won't close or the backup day can't be noted.
27. ✅ **Animations can be turned off** (the owner, 2026-10-02): an "Animations" switch in Settings, on by default.
    Off, nothing on Maia's screens moves, as with the phone's "Remove animations"; it takes effect at once.

28. ✅ **Simpler again, after the owner opened the app on the emulator** (2026-10-02; *awaiting design sign-off*):
    - No statistics anywhere but the due date and the week and day: Journey's "3 of 3 done", Your week's "5 this week"
      and "140 days to go", the counts on the task lines, on Your tasks' headings, on the Questions pill and footer and
      on a card's results pill ("Results") are gone
    - Next up is a Journey booking only; a task is never next up (it is in "To do this week")
    - Next up reads just "Next up" (no "in 126 days"); setup has no "14 checks in your plan" chip and its toast no
      "2 checks to book now"; booking a check starts at now, or at its booking while that is still ahead
    - Add: the name starts empty (no "Visit with …" / "Appointment" / "Hospital visit" filled in, no hint); "Booked" starts
      at now; Save looks muted until what, name and when are there; a saved entry is scrolled to on Journey but left
      closed. A new task, or a date given to one without, also starts at now
    - Every date field reads "Sat 3 Oct" / "Σάβ 3 Οκτ", never the phone's short format (from item 34, with the year
      when the day is more than half a year from today: "Fri 3 Oct 2031")
    - Journey: no "+ Add" button (the bar's Add is there); a check's card keeps one filled button ("Book"), and its
      Results panel waits behind a quiet "Add a result" until the check has results, is done or its day has passed
    - Results: one search box (name, the visit it belongs to, photo or report, the month added; English or Greek, with
      or without accents) instead of the kind and tag filters and the "Tag it?" question; tiles show the day added;
      removing a result happens in the viewer; an old lab link offers Open or Remove
    - Care team: only the doctors she has linked show, under one "Link another doctor" that asks which doctor; no "0
      linked" badge; the intro no longer promises sharing results
    - Settings: one Reminders card ("Remind me" for the calendar and the phone alike, then "On this phone too")
    - Questions, Breathe and Your tasks have a Back pill and no bottom bar, as About Maia
    - Text without extra line spacing (M PLUS Rounded 1c already spaces its lines), so every screen is shorter

29. ✅ **Next up, Settings and Add** (the owner's fourth round, 2026-10-03; *awaiting design sign-off*):
    - Five flat slots Week · Journey · Add · Results · Settings: the Team tab is gone and her care team (OB-GYN and
      other doctors, linking, calling, unlinking) is a card in Settings, after Your pregnancy; Add is a tab like the
      others rather than a screen pushed over the one she is on, and starts afresh after each save
    - Your week: Next up has no call button and no "Questions ready" pill; tapping it opens Journey scrolled to that
      booking (left closed), or at today with nothing booked. "Add a task" starts with its date and time off
    - No calendar words outside Settings: no "In calendar" pill on Journey, no "in calendar" on task rows, no "Added
      to your calendar" or calendar problem line after a change, no "Goes to your calendar" / "Stays in Maia" under a
      task and no calendar note on Add (bookings and tasks still reach her calendar; Settings says if something is wrong)
    - Add: a fifth pill, Task, adds a task as Your week does
    - Every text field but passwords opens a sentence-case keyboard, so what she writes starts with a capital
    - Reminders: "24 hours before" replaces "Evening before" (a day booked without a time is still reminded the evening
      before; the stored choice keeps its number, so nothing is migrated)
    - Results: "Rename" next to "Remove" in the viewer, for photos and PDFs (`PregnancyPlan.RenameResult`; the file
      stays as it is)
    - Questions: "Your questions", tied to no doctor or appointment; drag a question by its handle onto another to move
      it (`PregnancyPlan.MoveQuestion`); the + is faded and does nothing until she writes one

30. ✅ **Add stays, past days, results apart** (the owner's fifth round, 2026-10-03; *awaiting design sign-off*):
    - Add: saving an entry or a task keeps her on Add (it starts afresh, with the toast), so she can add another
    - Past days: Add's "Booked" and a task's date (Add, Your week, Your tasks) can be a day that has passed, and so can
      a check's booking on Journey (entries and bookings back to the first day of the pregnancy, tasks 44 weeks back)
    - Results apart from Journey: no Results panel, "Add a result", "Upload" or results pill on a check card; Results
      tiles show no entry and search doesn't look at entries; Journey's line under its title no longer mentions
      results. Stored format 6 moves every result into the plan's one list (`PregnancyPlan.Results`); the domain no
      longer links results to checks
    - Breathe tile on Your week: a dandelion seed drifting with the breath instead of the breathing dot
31. ✅ **Fixes from the owner's phone** (2026-10-03; *awaiting design sign-off*):
    - A task opened for changes: Save, Cancel and Delete task share the row's whole width in three equal parts
    - Choice pills never shrink to fit a line (Add's "Task" read "Tas"); a pill that doesn't fit moves to the next line
    - Ambient motion keeps moving while she scrolls (it held still from item 22, for smooth scrolling in Release; to
      watch on a slower phone)
    - Tasks from before today are "Past" (Περασμένες) on Your week and in Your tasks, rather than "From earlier"
    - Questions: the whole question follows her finger as she drags it by its handle (`RowDragShadow`, Android), it
      shows faded in its place meanwhile, and the question it would land on is outlined in Primary
32. ✅ **Calmer words on Journey** (the owner, 2026-10-04; a calming app that never urges her):
    - "Book now" reads "Book" (the Greek "Κλείσε ραντεβού" stays)
    - A booking whose day has passed keeps reading "Booked", drawn as any booking, rather than "How did it go?"; its
      card still offers Mark done, Move and Cancel booking
    - No helping text under a card: "· mark it done or move it" and "· needs a date" are gone ("Week 24" alone)
33. ✅ **Share results, no lab links** (the owner, 2026-10-04; *awaiting design sign-off*):
    - Results: "Select" beside the search box, or holding a tile, starts choosing; a tap then ticks or unticks. A
      bar at the bottom shows "2 selected", "Select all", × and Share, with "The app you choose keeps its own copy."
      Share opens the phone's share sheet with the chosen files (`ResultFiling.ShareAsync`, `ResultSharer` on
      Android), each under its name with its file ending; Back or × ends choosing
    - Free for now; the owner decides later what goes into the paid plan
    - Lab links are gone as a feature: stored format 7 drops any link she had kept (files stay), and with them the
      link kind, its strings, `AttachLinkAsync` and the Custom Tabs query
34. ✅ **Fixes from a user test** (an overwhelmed first-time user, played on the emulator, 2026-10-04):
    - Leaving Maia with Back while a field is focused no longer ends it (the field lets go before MAUI disposes the
      window's services, `MainActivity`); Enter in a field runs its return and keeps the focus there (`FieldHandler`),
      so a question typed after an empty Enter isn't lost; when the keyboard goes down the field lets go, so Your
      week no longer jumps under her next tap (`SoftKeyboard`)
    - Next up drops a booking whose time has passed today (one with only a day stays all day)
    - Dates more than half a year away read with their year (rows, toasts, date fields); the calendar a date field
      opens speaks Greek when Maia does (`DateFieldHandler`; in English it stays the phone's own)
    - Renaming a task that has only a day no longer gives it the current minute as its time
    - Task names hold up to 200 characters (as the plan does), with "20 characters left" / "That's all that fits"
      once she nears the limit, rather than letters dropped without a word
    - "Create and share" says "Making your backup…" with a spinner until the share sheet opens
    - A quick tap no longer leaves a button pressed smaller (Add's Save stayed narrow); the language pills are one size
    - Large text: the bottom bar's names stop growing past 130% and keep a whole gap from the screen's edge; labels and
      buttons hyphenate in Maia's language; Journey's Mark done and Not needed stand one under the other when a word
      would not fit its half (`ButtonRow`)
    - Results tiles show a small picture of the photo or of the PDF's first page (`ResultThumbnails`; in memory only,
      drawn once per tile, the icon stays for a file the phone can't draw)
35. ✅ **Design calls after the user test** (the owner, 2026-10-04; *awaiting design sign-off*):
    - Every confirmation reads the same way: the question names what goes (a second sentence only for what stays, or
      "This can't be undone." where files or all her data go), then two buttons in words, the action repeating the
      question's verb ("Delete task", "Remove result", "Delete everything") and "Keep it" / "Keep them" («Να μείνει» /
      «Να μείνουν»), in shared `ConfirmButton` / `KeepButton` styles. Delete all data and Clear done lose their
      icon-only tick and cross
    - Next up says "with …" only once she has linked her OB-GYN (no "with your OB-GYN" before)
    - "Mark done" is white like "Not needed" (its tinted fill read as already done)
    - "Welcome to Maia" with a capital M; only the logo is lower case
    - Android's own date and time dialogs and the text cursor are pine (Primary) rather than sea blue
36. ✅ **A clean start, clean names, past tasks folded** (the owner, 2026-10-04; *awaiting design sign-off*):
    - A new setup starts its past empty: setup lists, and a new plan holds, only the checks still to come (no "Probably
      done" rows); she can add what she has done herself. The due date starts 40 weeks from today
    - Result names read without their file ending ("20-week scan"); the kept name is unchanged, and sharing or opening
      a file elsewhere adds the ending back
    - Your week: "Past" is a folded line like "Later", "No date" and "Done", after "No date"
37. ✅ **No calendar sync; reminders from Maia only** (the owner, 2026-10-04; ADR 0009, superseding ADR 0004):
    - Maia writes nothing to the phone's calendar: calendar sync, its stores, the Android calendar gateway, the
      calendar permissions, the iOS calendar texts and Plugin.Maui.CalendarStore are gone. No migration or clean-up on
      the test phones
    - Reminders depend only on the booking's time (`ReminderTimes`): 24 hours before (the evening before at 20:00 for
      a day without a time) while ahead, and nothing in its place once that has passed (a booking made less than a day
      ahead gets none, so no booking ever rings twice); 2 hours before (the evening before for a day without a time);
      both, each while ahead
    - Settings: one Reminders card (the switch, "Remind me", "Discreet lock screen"); the choices made under calendar
      sync are kept until she chooses again
    - Setup step 3: "Remind me" in place of the calendar card, asking for notifications; Delete all data is a plain
      toast
    - Journey names exactly the reminders that will ring, only while reminders are on
38. ✅ **One way into all tasks; done tasks go** (the owner, 2026-10-04; *awaiting design sign-off*):
    - Your week: under "To do this week" one "All tasks" line (Greek «Όλες οι εργασίες») opens Your tasks from the
      top; the lines per group ("Later", "No date", "Past", "Done") and "All 34 in Your tasks" are gone
    - Your tasks: no Done group and no "Clear done". Tasks she ticked off are deleted as the list loads (opening it,
      coming back to it, after a change on it), with their reminders, as `PlanService.ClearDoneTasksAsync` did
39. ✅ **An icon on every Settings card** (the owner, 2026-10-04; *awaiting design sign-off*): each card has a smaller
    icon tile (36 dp, was 44) by its title; new line icons for the date, animations, language, lock, her data and
    diagnostics
40. ✅ **Questions make room as she drags** (the owner, 2026-10-04; *awaiting design sign-off*): held over another
    question for 0.2 s, that one slides out of the way and the dragged question's faded place moves into the gap, where
    it lands when she lets go (rows slide by translation; the list takes the new order only on the drop). The
    drop-target outline is gone
41. ✅ **Journey entries: Not booked, Booked, Done, Delete** (the owner, 2026-10-04; *awaiting design sign-off*):
    - Three stored states: Not booked, Booked, Done (`CheckState`). A booking is shown as Done once its time has passed,
      without her (derived, `PlannedCheck.StatusAt`); "Probably done" stays only for checks kept at setup, never booked,
      whose weeks have passed, and behaves as Done. "Book", "Later", "Not needed" and "Mark done" are gone
    - Every entry shows its trimester, its week (or usual weeks) and, when booked, its day and time; the day sets the
      week and the trimester. One with only a trimester stands first in that trimester
    - Journey card: Not booked has "Book" (a day and a time, past or to come; a past one makes it done) and "Delete";
      Booked has "Edit" (name, day and time) and "Delete"; Done and Probably done have "Edit" (name, and a day and time,
      a trimester or a week) and "Delete". Delete asks once more ("Delete … from your journey?") and removes it for
      good
    - Setup creates only the checks she keeps; a check switched off is not stored, so it no longer comes back as a chip
      on Add (the chips are Maia's usual extras). Template updates add no checks, only milestones
    - Add, "When?": Booked (a day and a time), Already done (a day and a time that has passed, a trimester or a week),
      Not booked yet (a trimester or a week)
    - Not booked entries get no reminders ("time to book" reminders are gone) and are not on Your week
    - No migration (not in production): Delete all data starts clean; an older saved plan still opens (its switched-off
      checks are left out, a booking marked done keeps its day and time, and a booking stands at its own week)
    - A check she booked, moved, marked or renamed is hers (`PlannedCheck.IsEdited`, stored): a newer template or setup
      run again leaves it as she has it
    - A booking turns Done at its time in her home zone, wherever the phone is; at that minute it stops being Next up
    - Booked starts at the next whole hour (on Add and Journey's Book), so saving straight away books a time to come
42. ✅ **One Maia: one look, one set of words** (the owner, 2026-10-04, after a consistency review found names,
    sizes, colours and icons chosen screen by screen; the docs were updated first, then the app; *awaiting design
    sign-off* on the new screenshots):
    - Today (Σήμερα) replaces Week and "Here's your week": the tab, the title, a Lucide `sunrise` icon in place of the
      house; the week stays in its pill. New (Νέο) replaces Add on the tab and as the title ("Add to your journey"
      goes). Breathing (Αναπνοή) replaces Breathe and "Breathe with Maia"; All tasks, Questions and Care team (Greek
      «Ομάδα φροντίδας») are the titles of the screens they open, and This week is the task card's heading. Setup's
      steps read "A typical journey" and "Doctor and reminders"; Restore reads "Backup"; Today's backup card "Backup"
    - Journey in English and Πλάνο in Greek, everywhere: "plan" leaves the English strings and «Διαδρομή» the Greek
    - Titles, headings, tiles and links are nouns; buttons are verbs; messages say what happened ("A newer Maia is
      needed"); no title starts with "Here's" or "Your". Every Greek string says what its English says (the task
      toasts, "Add task", "Task" / «Τι να κάνεις», "Start breathing", "If advised" and a dozen more did not). "Remind
      me" became "Reminders on this phone" (setup) and "When?" (Settings); the Breathing screen lost its "Calm" eyebrow,
      the only one above a title; the results search no longer says "visit" (results aren't tied to visits)
    - Stale words go: "when it's time to book" in the Reminders card, the notification channel and the privacy notice
      (the "time to book" reminder went in item 41)
    - 14 colours (Pine, Sage, SageTint · SkyDeep, Sea, Sky · SandDeep, Sun, Sand · Ink, Muted, Line, Mist, White);
      the other 55 keys, the template's greys among them, go; `Primary` and `Tertiary` become `Pine` and `Sea`. Card
      borders are all Line (status shows in the chip and the node); Settings' icon tiles are all SageTint with a Pine
      icon; the default control styles use Maia's colours
    - 6 text sizes as styles (Title 32, Hero 26, Heading 16, Body 15, Caption 13, Label 12); no size set on a screen
    - Every button a pill; cards 22, fields, icon tiles and notes 14; questions tick the same round circle as tasks
    - Lucide icons throughout (ISC and Feather's MIT licence on the Licences screen, in a new "Icons" card), named
      for their meaning, at one stroke and three sizes; Results' folder without its tick; a shield for the privacy
      notice; a clipboard list for Diagnostics (the pulse line read as monitoring her health, which Maia never does);
      the filled sprout is the only logo (`Controls/Logo`; the header's line sprout goes). Every icon is drawn in
      lines and curves only: Android drew some of Lucide's arcs wrongly or not at all (Diagnostics and the shield were
      blank), and a test keeps arcs out
    - Tests (`DesignResourceTests`): every colour key is one of the fourteen at some opacity, no hex or colour of its
      own in a screen or a drawing, no `FontSize` on a screen, every icon from the set at one stroke and three sizes,
      every button style a pill
    - Screenshots captured from the app (`scripts/capture-screens.ps1` on the 390 × 844 dp `Maia_Capture` emulator,
      with a Debug-only sample journey, `CaptureSample`), 20 images in place of the first prototype's
43. ✅ **Fixes from the owner, 2026-10-08** (the choices on an options page, in English and Greek; *awaiting design
    sign-off*):
    - Journey cards name no reminders ("· reminder 24 hours before" is gone; reminders ring as Settings says)
    - Notes: New has an optional "Notes" field for an entry, shown when its card is opened on Journey as a typical
      journey's note is (nothing with none); Edit changes it (`PregnancyPlan.Describe`, `PlannedCheck.MaxNoteLength`
      500). An entry's note is its `Description`, so nothing new is stored for it
    - "After birth" is a fourth choice beside the trimesters wherever a trimester is chosen (New, Edit, tasks):
      `Trimester.AfterBirth`, weeks 41 to 52, under Journey's "After birth" heading
    - A task without a date is for a trimester or a week, chosen as an entry's "When?" and needed ("Choose when it
      is."; `task.when.missing`), on Today, New and All tasks. It stands at its week, or first in its trimester, on
      Journey, and under "No date" on All tasks by its week; a task saved before keeps neither until she changes it.
      Stored format 8 (`trimester`, `week` on a task; `AfterBirth`), so an older Maia refuses rather than drops them
    - Care team folds: one tap on its heading opens it, and it folds again when she leaves Settings
    - "Link from contacts" shows only until her OB-GYN is linked, and on no other doctor's card; "Link another doctor"
      lists her OB-GYN first, for a second contact
    - Weeks on Journey: the first row of each pregnancy week carries the week's number on the line above its node
      (`WeekMark`), chosen from three options (a number on the line, a line per week, only "You are here"); a
      heading per week since item 49
44. ✅ **A new question on top** (the owner, 2026-10-08): a question she writes goes to the top of Questions, right
    under the field, above every other (`PregnancyPlan.AddQuestion`); the order she drags them into stays as she
    leaves it. Nothing stored changes: questions are kept in their list's order
45. ✅ **Blood pressure on New and Journey** (the owner, 2026-10-11; *awaiting design sign-off and new screenshots*):
    - New: a sixth pill, "Blood pressure" («Πίεση»): "Systolic" and "Diastolic" on the number pad, and the day and time
      she took it, starting at now (up to now, from the first day of the pregnancy). Numbers no monitor shows are
      refused as mistyped; nothing is read into the rest (`PregnancyPlan.LogBloodPressure`, `PlanService`)
    - Journey: each reading small on the line at the day and time she took it, like a week's number ("118/76 mmHg ·
      Wed 30 Sep · 08:30"), shown whether or not "Tasks" is on; sliding it left or right shows "Delete", which deletes
      it for good (`PregnancyPlan.RemoveBloodPressure`). No editing
    - Stored format 9 (`bloodPressure`), so an older Maia refuses the plan rather than drop her readings
    - Organiser, not monitor: no ranges, colours, flags, charts or trends, and no reminders to measure (compliance.md)
46. ✅ **Timer: Breathing and Exercise** (the owner, 2026-10-11; *awaiting design sign-off*): Breathing became the
    Timer («Χρονόμετρο»; the Today tile reads "Breathing and exercises"), with two choice pills, Breathing and Exercise
    (hold, then rest, "For the exercises your doctor suggested."; "Feeling pain or unwell? Stop and rest."), and a
    card where she sets each part's seconds and the rounds with round − and + buttons (`Rhythm`), kept per mode in
    Preferences. Non-medical: no exercise named or suggested, no reminders, no record. Screenshots 04 and 12 to retake
47. ✅ **Calendar** (the owner, 2026-10-11; *awaiting design sign-off and its screenshot*): her journey as a month,
    read-only, like the phone's calendar app but inside Maia, so she sees her dates without leaving Maia and nothing
    is synced anywhere (ADR 0009 stands). Today's week pill carries a calendar icon and opens it (pushed, Back pill).
    Every day of the month shows, six rows always; a dot under a day for each kind of thing on it (Sun due date, Sea
    booked, Sage done, Muted a task); the chosen day's entries listed under the grid with their chip and time, or
    "Nothing on this day."; ‹ › or a swipe move a month, from the month the pregnancy began to week 52. Only what has a
    day is on it. `CalendarViewModel` reads `PlanQueries.GetTimelineAsync`; the grid is the same 42 day views for
    every month (`CalendarDayView`). Nothing stored changes

48. ✅ **One Timer** (the owner, 2026-10-11: "They are the same thing"): the Breathing and Exercise pills are gone.
    One rhythm, "First" / "Then" («Πρώτα» / «Μετά»), 4 s then 6 s ×12 at first; the body reads "4 seconds, then 6, 12
    times. For calm breathing, or the exercises your doctor suggested."; one caption, "Feeling dizzy, in pain or unwell?
    Stop and rest."; "Start" / "Start again". The sphere counts down the seconds left of each part over its name. Her
    Breathing times carry over (`timer.breathing.*`). Screenshot 12 to retake
49. ✅ **Week headings on Journey** (the owner, 2026-10-11, option C of four): the number on the line gave way to a
    heading for each week across the timeline, "Week 25" («Εβδομάδα 25»), a hairline and its days ("11–17 Oct";
    `WeekRow`, `DisplayFormat.DayRange`). Every week from the first on the timeline to the due date shows, an empty one
    with "Nothing this week" («Τίποτα αυτή την εβδομάδα»); after birth only weeks with something in them. A row with
    only a trimester stands before its week's heading. Nothing stored changes. Screenshot 05 to retake
50. ✅ **Terms of use, and the privacy notice brought up to date** (the owner, 2026-10-11; *awaiting legal review and
    design sign-off*):
    - Terms of use («Όροι χρήσης», `TermsViewModel`, `TermsPage`, screen 25), in the privacy notice's layout, pushed
      from About Maia (a third row card, Lucide `scroll-text`) and from setup step 1 ("By using Maia, you accept the
      Terms of use")
    - Twelve plain parts: the agreement (adults, her own pregnancy), what Maia is (not a medical device, no advice, her
      doctor decides), a typical journey is a starting point, not for emergencies (112), reminders can come late and
      the timer only counts time, she checks what she enters, her data can't be brought back without a backup, Maia as
      it is, limits of liability as far as the law allows, her licence to use it, changes, Greek law
    - The privacy notice gains "Changes to this notice" and says her settings (language, the timer's times) are kept
      apart in the phone's settings
    - The maker's name and contact, and a legal review, are still needed (compliance.md, Terms of use)

Next:

- Clinical review of `gr.json` (blocks every store release).
- Owner and legal: controller and contact, hosted privacy policy, store declarations, listing, DSA status, and a
  lawyer's review of the terms of use (item 50) with the maker's name and contact in them
  ([`compliance.md`](compliance.md)); regulatory opinion before the paid launch.
- Design sign-off on items 42, 43, 45, 46 and 47 ([`design/screens/`](design/screens/)), and on a phone in Greek; retake
  `05-journey.jpg` and `07-new.jpg` with the blood pressure readings (the capture sample has two), and `04-today.jpg`
  and `12-breathing.jpg` with the Timer; capture Calendar's
  screenshot (`24-calendar.jpg`, added to `capture-screens.ps1`).
- Regulatory adviser: confirm that logging blood pressure without interpretation keeps Maia an organiser (item 45,
  [`compliance.md`](compliance.md)). Screen readers can't slide a reading aside to delete it yet.
- Device checks on real phones: the Timer's pulse strength and tone
  loudness, the Greek tab names on 320–360 dp phones, Journey's first-open jump, and the big-journey check on a real
  phone.
- Owner: where the emergency number (112) goes now that Care team no longer shows it.
- Paid upgrade.
