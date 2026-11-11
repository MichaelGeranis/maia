# Compliance (DRAFT for legal review)

> **DRAFT. Not legal or regulatory advice.** Written by the product team from what the code does on
> 2026-10-01, to prepare a review by a lawyer (GDPR, consumer law and the terms of use, store terms, DSA) and a
> regulatory adviser (EU MDR). Every answer
> marked *proposed* must be confirmed before a store release. Open decisions for the owner are listed at the end.

What Maia is and its positioning rules are in [`product.md`](product.md); the technical facts behind each answer are
in [`architecture.md`](architecture.md) (Security model). Decisions: [ADR 0008](adr/0008-intended-purpose-and-privacy-role.md)
(intended purpose and privacy role), [ADR 0002](adr/0002-local-first-encrypted-storage.md) (storage),
[ADR 0006](adr/0006-reminders-on-this-phone.md) (reminders), [ADR 0007](adr/0007-diagnostics-without-health-data.md)
(diagnostics).

## 1. Intended purpose and the EU MDR

### Intended purpose (proposed)

> Maia organises the appointments and paperwork of a pregnancy for the expecting mother. It keeps a list of checks,
> their bookings and results, reminders, tasks and questions for her doctor. It does not diagnose, monitor, predict or
> recommend treatment, and it does not interpret results. The plan is a typical starting point, taken from a
> clinically reviewed schedule of common checks, which her doctor changes. Maia is not for emergencies.

Maia is meant as an organiser, **not a medical device** under Regulation (EU) 2017/745 (MDR). This is the product
team's reading of MDCG 2019-11 (guidance on qualification and classification of software; use its current revision)
and must be confirmed by a short regulatory opinion before the paid launch.

### What the software does, feature by feature

| Feature | What the code does | Why it is organising |
| --- | --- | --- |
| Dating | Works out weeks + days and the due date from a date she enters (her due date, or the first day of her last period plus 280 days) | Calendar arithmetic on a date she gives; no measurement, no clinical judgement. Her doctor's date wins: she can change it any time in Settings |
| A typical journey | Lists checks from a static per-country template (`gr.json`), each with a week window, the same for everyone with the same due date | A schedule of common appointments, like a printed leaflet. Nothing is adapted to her health data. She switches checks off; her doctor adds or removes them |
| Status | "Not booked", "Booked", "Done", "Probably done" from today's date, the window and her bookings | Scheduling state, not a health state. "Probably done" only means a check's usual weeks have passed |
| Results | Stores PDFs and photos, encrypted, in one list she can search by name, kind and month, and share through the share sheet | Storage and retrieval only. Maia never reads, parses or interprets a result |
| Reminders | Notifications before bookings and timed tasks | Scheduling |
| New | She adds a doctor visit, an exam or test, a hospital visit, something else or a task, with when it happens (booked, done or not booked yet) | Her doctor decides; Maia records |
| Care team | Calls the OB-GYN and the other doctors she linked (dentist, internist, endocrinologist, haematologist, cardiologist, dermatologist, gastroenterologist). The 112 tile was removed at the owner's request; the privacy notice still says Maia isn't for emergencies and names 112 | Communication |
| Timer | One generic two-part timer with rounds, "First" and "Then" (4 s, 6 s ×12 at first), for calm breathing or her exercises, each part 1–30 s and 1–50 rounds as she sets them, with a countdown, an optional gentle vibration and optional tones made on the phone | Timing only, not therapy or a training programme: Maia names and suggests no exercise ("For calm breathing, or the exercises your doctor suggested."), adapts nothing to her, keeps no record of sessions and sends no reminders. "Feeling dizzy, in pain or unwell? Stop and rest." No audio files or third-party sounds, so nothing to add to the licence notices |

### MDCG 2019-11 decision steps (proposed reading)

1. *Is it software?* Yes.
2. *Is it an accessory, Annex XVI product, or does it drive a device?* No.
3. *Does it act on data beyond storage, archiving, communication, simple search or lossless compression?* Partly:
   the dating arithmetic and the per-week list of checks are calculations.
4. *Is that action for the benefit of an individual patient?* Arguably yes: it uses her own dates.
5. *Is the intended purpose a medical purpose in Article 2(1) MDR* (diagnosis, prevention, monitoring, prediction,
   prognosis, treatment or alleviation of disease; of an injury or disability; investigation of a physiological
   process or state; control or support of conception)? **Proposed: no.** Pregnancy is not a disease; Maia does not
   monitor or investigate the pregnancy, does not predict outcomes, supports no conception, and sets no treatment. The
   checks are the ones a typical national schedule lists for every pregnancy; deciding which apply stays with her doctor.

The points a regulator could press are step 5's "monitoring" and "investigation of a physiological state" (dating)
and "prevention" (proposing screening tests). The answer rests on the intended purpose and on the wording below.

### Red lines (would need a new decision and a regulatory review first)

Interpreting or flagging results; risk scores of any kind; adapting the plan to health inputs (symptoms, measurements,
history); dating from ultrasound measurements; fetal growth or weight charts; kick counting; symptom check-in.
Several of these are already out of scope in product.md.

**Blood pressure readings** (product.md item 45, the owner's request of 2026-10-11) stay on the organiser side of
this line as built: she types the two numbers her own monitor shows, and Maia stores them and shows them on Journey
as written, at the time she took them. Maia does not judge them: no normal or high ranges, no colours, flags or
words about what they mean, no charts or trends, no reminders to measure, and nothing on her journey changes because
of them. The only check is that the numbers can be a monitor's (the upper 50–300, the lower 20–200, the upper above
the lower), to catch typing mistakes. Any of those would cross the red line above. To be confirmed by the
regulatory opinion before the paid launch; the store wording rules below still apply ("keep" or "log", never
"track" or "monitor").

### Wording rules (store listings, in-app copy, docs, support replies)

- Say: organise, plan, keep, book, remind, file, "a typical journey", "your doctor may suggest a different set",
  "Maia organises; your doctor decides", "not for emergencies".
- Never say: tracks or monitors your health or your baby, detects, diagnoses, predicts, risk, recommends, "you should
  have this test", personalised medical plan, or anything implying Maia replaces a midwife, doctor or care
  professional. Check names in the template ("Fetal monitoring (CTG)") name what her doctor does, not what Maia does.
- The listing carries "Not medical advice. Your doctor decides." and Google Play's required line for health apps that
  are not medical devices: "Maia is not a medical device and does not diagnose, treat, cure, or prevent any medical
  condition."
- The Settings card for the problem log is called "Diagnostics". It is plainly technical in context; if a reviewer
  reads it as medical, rename it ("Problem log").

### Availability

`AppConfig.DefaultCountry` is "GR": everyone gets the Greek private-practice pathway. Until another country has a
clinically reviewed template:

- Google Play: Production, Countries / regions: **Greece only**. App Store (later): Availability: **Greece only**.
- The listing says "Available in Greece. The typical journey follows common practice in Greece."
- English stays available inside Greece (many residents use it).

## 2. Data protection (GDPR)

### What Maia keeps, and where

| Data | Where | Protection |
| --- | --- | --- |
| Due date, checks, entries she added (kind, name, when), bookings, tasks, questions, notes, blood pressure readings, result tags | `plan/plan.maia` (+ `.bak`) on the phone | AES-256-GCM, key in the Android Keystore |
| Result files (PDFs, photos) | `results/*.blob` on the phone | Same key. Photos over 3000 px, and HEIC, are re-encoded without metadata; smaller JPEG/PNG/WebP are kept as picked, with only the Exif GPS block wiped from JPEGs. Other Exif data, XMP location in a JPEG, location in a PNG and HEIC on Android 7-8 are not stripped (ADR 0002) |
| Her doctors' names, labels, phone numbers (only the contacts she picks, for her OB-GYN and other doctors) | Inside the plan | Same |
| Setup choices until the plan exists | `drafts/setup.maia` | Same key |
| Reminders (only if she turns them on): Maia's own notifications on this phone | The phone's notification system (Android inexact alarms) | The lock screen shows only "Maia reminder"; with "Discreet lock screen" on, no names show even when the phone is unlocked |
| Reminder schedule for after a restart | App preferences | A short list of plain numbers, times and words that name no check or task |
| Problem log | `diagnostics/problems.log` | Codes, types, versions and times only; no health data |
| Language and reminder settings (on/off, when, discreet lock screen), lock on/off, the Timer's switches, mode and times, Journey's "Tasks" switch, two backup timestamps | App preferences | Not health data |
| Backup file | Wherever she sends it | AES-256-GCM, key from her password (PBKDF2, 600,000 rounds) |

Nothing is copied to cloud backup or a new phone (`allowBackup="false"`, `dataExtractionRules`). The Release app has
no internet permission; there is no account, server, analytics, advertising or tracking SDK. Maia never writes to the
phone's calendar, so plan data leaves the phone only through the share sheet, when she chooses.

### Who is the controller? (owner to decide, with legal advice)

- **Her own use.** She keeps her own pregnancy data for herself. That is likely a purely personal activity
  (Art. 2(2)(c) GDPR), outside the GDPR for her.
- **The developer, free app (proposed position).** The developer receives nothing from the plan: it never leaves the
  phone except in an encrypted file she sends where she chooses, and the developer has no key, or in results she
  shares herself to an app she picks. On that reading the developer is neither controller nor processor of the plan data. Recital 18 brings providers of the means for personal
  processing into scope only when they act as controller or processor, so the point must be confirmed by legal review.
  Whatever the answer, Maia follows data protection by design: local-only, encrypted, minimal, deletable, with a
  notice in the app.
- **Where the developer does receive personal data**, it is a controller for that processing:
  - support emails she sends (they may mention her pregnancy, so special category data can arrive);
  - a problem log she chooses to share (error codes, versions, times; no health data by design, but the email that
    carries it identifies her);
  - Google Play Console data (statistics, and Android vitals crash reports from users who let Google share them).
  The owner needs: the controller's legal name and contact, a support address, a short retention rule for support
  mail, and a record of processing for it (Art. 30; the SME exemption may not apply because special category data
  can arrive).
- **Paid cloud features (later).** Encrypted backup and the partner/doctor view put the developer in the role of
  controller or processor of encrypted health data. Before building them: a DPIA (Art. 35; large-scale special
  category data, and the Hellenic DPA's list of processing that needs one), a legal basis under Art. 9(2)(a) (explicit
  consent), processor contracts and transfer checks for the hosting, a decision on a DPO (Art. 37), network permissions
  back in the manifest, and new store answers.

### Her rights, in the app

| Right | How she uses it in the free app |
| --- | --- |
| Information (Art. 13/14) | Privacy notice in the app (About Maia, and a link at setup step 1), English and Greek. A hosted copy is still needed |
| Access, rectification | Everything is on Maia's screens and can be changed there |
| Erasure | Settings, Delete all data: plan, results, the problem log and the data key, and Maia's reminders are cancelled. Maia writes nothing to the phone's calendar, so there is nothing there to remove. Uninstalling removes the app's data too (architecture.md, "Language and deleting her data") |
| Portability | Settings, Backup: one encrypted file in Maia's own format. ADR 0005 rejected a readable export because it would put health data in plain form wherever she saves it. Art. 20 binds a controller; revisit ADR 0005 only if legal review concludes the developer is a controller of the plan data |
| Restriction, objection | Nothing is processed by the developer in the free app |
| Complaint | The notice names the Hellenic Data Protection Authority |

### The in-app notice, and its gaps (for the owner and legal; strings in both languages)

The notice (`Privacy_*` strings, thirteen parts, "Last updated 11 October 2026") covers what Maia keeps; that her plan
and results stay on the phone, encrypted; no internet or trackers; Reminders on this phone (only if she turns them on,
in setup or in Settings, the lock screen shows only "Maia reminder", the short list of times in plain words that name
no check or task, kept for after a restart, and that Maia never writes to her calendar);
contacts and camera; files shared into Maia (only the file she shares, kept encrypted with her results, where she can
remove it) and "Open in another app" (that app may keep a copy or list it in its recent files);
backups, with the two backup dates kept in the phone's settings; the app lock (her phone checks the PIN, pattern or
fingerprint, Maia never sees them) and the recents screen (always hidden from Android 13, only while the lock is on
before that); diagnostics (error codes with times and Maia's version, shared only when she taps Share diagnostics,
which hands those codes, times and the Maia and Android versions to the share sheet, cleared by Delete all data);
control, rights, how the notice changes (it ships with the app, so a new one comes with a new version, dated) and
"Maia organises, your doctor decides". "What Maia keeps" also says her settings (the language, the timer's times) are
kept apart in the phone's settings. Each sentence was checked against the code it describes. Not yet in it:

- the controller's identity and contact, and the URL of the hosted policy.

## 3. Google Play

### Data safety form (proposed answers)

Play's definitions: *collection* is transmitting data from the app off the device; *sharing* is transferring it to a
third party, including on the device to another app. Data processed only on the device is not collected. Even an app
that collects nothing must complete the form and link a privacy policy.

- **Does your app collect or share any of the required user data types? Proposed: No.**
  - Health info, files and docs, photos: stored on the device only, encrypted. Maia never transmits them off the
    phone. The transfers to other apps on the phone are listed below.
  - Open in another app: when Maia can't draw a file (a PDF that is password-protected or damaged, or a HEIC photo
    on Android 7 and 8) or a PDF has more than 60 pages, the viewer offers "Open in another app". Maia decrypts that
    one file into its private cache (`cache/open`) and gives the app she picks in the chooser read access to it;
    Maia's copy is removed when she comes back. That app may keep or upload its own copy, and the screen says so
    before she taps ("The other app may keep its own copy, or list the file in its recent files."). This is a
    user-initiated transfer, on the device, of a decrypted result file, with a clear disclosure before it happens
    (Play's exemption for user-initiated transfers). *Legal to confirm.*
  - Sharing results: she chooses results on Results and taps Share. Maia decrypts just those files into
    `cache/open` and opens the phone's share sheet; the app she picks (email, WhatsApp, Viber…) gets read access to
    them, and Maia's copies are removed when she comes back. That app keeps and may send on its own copy, and the
    screen says so before she taps ("The app you choose keeps its own copy."). A user-initiated transfer, as above.
    *Legal to confirm.*
  - Contacts: the system picker returns only the contact she picks; Maia stores name, label and number on the device.
    Tapping Call hands that contact's number to the phone app's dialer; no call is placed until she places it there.
  - Calendar: Maia neither reads nor writes the phone's calendar and has no calendar permission, so no *Calendar
    events* to declare.
  - Reminders: Maia's own notifications on the phone, only if she turns them on. The lock screen shows only "Maia
    reminder"; the schedule kept for after a restart names no check or task. Nothing leaves the phone.
  - The backup file leaves only through the share sheet when she sends it, encrypted with a password the developer
    never knows.
  - The problem log leaves only through the share sheet, when she taps Share diagnostics in Settings (one tap, one
    share; there is no preview any more, so the file holds only error codes, their times and the Maia and Android
    versions, as the Settings card and the privacy notice say), and she chooses where it goes.
- **Alternative if support will ask people to send the problem log to the developer:** declare *App info and
  performance, Diagnostics* as collected, optional, for app functionality (troubleshooting), not shared, not used for
  tracking. Owner to choose.
- With no data collected, the security-practice questions (encryption in transit, deletion requests) do not apply.

### Health apps declaration (proposed)

- **Medical: Healthcare services and management** ("scheduling medical appointments, reminders").
- **Health and fitness: Stress management, relaxation, mental acuity**, if the Timer is counted (owner to decide).
- Not: period tracking (the last period is asked once, for dating), reproductive and sexual health (fertility),
  medication, clinical decision support, disease management.
- **Medical device: No.** The listing carries the "not a medical device" disclaimer.

### Other Play Console items

- **Account type.** Google requires an organisation account (D-U-N-S number) for "health apps, such as medical apps".
  A Medical declaration above may bring Maia under that rule. A personal account created after 13 November 2023 must
  also run a closed test with at least 12 testers opted in for 14 days before it gets production access. Owner to
  decide which account publishes Maia.
- **App access.** All functions work without a login. A store build can't start a new plan until `gr.json` is
  clinically reviewed, so do not submit for review before that.
- **Ads:** none. **Target audience:** adults (18+); not designed for children. **Content rating:** IARC questionnaire
  (no violence, sexual content, gambling or user-generated sharing; medical topic).
- **Permissions:** no restricted permissions (no SMS, call log, all-files, background location, exact alarms or
  photo/video media permissions; Maia uses the system pickers). No calendar permissions. `POST_NOTIFICATIONS` is
  runtime, asked only when she turns reminders on; `USE_BIOMETRIC`, `RECEIVE_BOOT_COMPLETED`
  and `VIBRATE` (Breathing's gentle vibration) are normal, granted at install. Target SDK is 36 (the .NET 10 Android
  default).
- **Privacy policy:** a public, non-geofenced URL (not a PDF) in the Console, and the policy (or a link) in the app.
- **App category:** owner to choose (Health & Fitness, Medical or Parenting); "Medical" makes the account-type question
  more likely to bite.
- **Store assets:** the design JPGs use fallback fonts and can't be used. Capture screenshots from a Release build on a
  phone, in English and Greek, with invented sample data and invented names (no real doctor or clinic). Also a
  1024 × 500 feature graphic and a 512 × 512 icon from `appicon.svg`.
- **Support contact:** an email address (required) and, ideally, a support web page.

### Store listing draft (legal and owner to review)

**Title** (30 max): "Maia: Pregnancy organiser" / "Maia: Οργάνωση εγκυμοσύνης"

**Short description** (80 max):
"Pregnancy checks, bookings and results, organised. Your doctor decides." /
"Εξετάσεις, ραντεβού και αποτελέσματα εγκυμοσύνης σε τάξη. Αποφασίζει ο γιατρός."

**Full description, English:**

> Maia organises your pregnancy: the checks most pregnancies need, your bookings and your results, in one calm place.
>
> • A typical journey of checks, dated from your due date. Switch off what doesn't apply; your doctor may suggest a
> different set.
> • Book each check with a date and time.
> • Keep reports and photos of paper results together, ready to show your doctor.
> • Add a doctor visit, an exam or a hospital visit to your journey in a few taps.
> • Tasks, questions for your doctor, and a calm timer for breathing.
> • Reminders on this phone before your bookings, if you want them.
> • English and Greek.
>
> Private by design: your journey and results stay on your phone, encrypted. No account, no ads, no trackers. The free
> app doesn't use the internet.
>
> Maia organises; your doctor decides. Not medical advice. Maia is not a medical device and does not diagnose, treat,
> cure, or prevent any medical condition. Always ask your doctor or midwife for medical advice. Not for emergencies:
> call 112.
>
> Available in Greece. The typical journey follows common practice in Greece.

**Full description, Greek:**

> Η Maia οργανώνει την εγκυμοσύνη σου: τις εξετάσεις που χρειάζονται οι περισσότερες εγκυμοσύνες, τα ραντεβού και τα
> αποτελέσματά σου, σε ένα ήρεμο σημείο.
>
> • Ένα συνηθισμένο πλάνο εξετάσεων, με ημερομηνίες από την πιθανή ημερομηνία τοκετού. Απενεργοποίησε ό,τι δεν σε
> αφορά· ο γιατρός σου μπορεί να προτείνει κάτι διαφορετικό.
> • Κλείσε κάθε εξέταση με ημερομηνία και ώρα.
> • Κράτα γνωματεύσεις και φωτογραφίες από χάρτινα αποτελέσματα μαζί, έτοιμες για τον γιατρό σου.
> • Πρόσθεσε μια επίσκεψη σε γιατρό, μια εξέταση ή μια επίσκεψη σε νοσοκομείο στο πλάνο σου με λίγα αγγίγματα.
> • Εργασίες, ερωτήσεις για τον γιατρό σου και δύο ήρεμα λεπτά αναπνοής.
> • Υπενθυμίσεις σε αυτό το κινητό πριν από τα ραντεβού σου, αν τις θέλεις.
> • Ελληνικά και αγγλικά.
>
> Ιδιωτικότητα από τον σχεδιασμό: το πλάνο και τα αποτελέσματά σου μένουν στο κινητό σου, κρυπτογραφημένα. Χωρίς
> λογαριασμό, χωρίς διαφημίσεις, χωρίς trackers. Η δωρεάν εφαρμογή δεν χρησιμοποιεί το διαδίκτυο.
>
> Η Maia οργανώνει· αποφασίζει ο γιατρός σου. Δεν παρέχει ιατρικές συμβουλές. Η Maia δεν είναι ιατροτεχνολογικό
> προϊόν και δεν διαγιγνώσκει, δεν θεραπεύει, δεν γιατρεύει και δεν προλαμβάνει καμία πάθηση. Για ιατρικές
> συμβουλές, ρώτα πάντα τον γιατρό ή τη μαία σου. Δεν είναι για επείγοντα: κάλεσε το 112.
>
> Διαθέσιμη στην Ελλάδα. Το συνηθισμένο πλάνο ακολουθεί την πρακτική στην Ελλάδα.

### EU Digital Services Act

Both stores require every developer distributing in the EU to declare trader or non-trader status, whatever the
price. Declare a status before the first EU release; a trader's contact details (address, phone, email) are shown
publicly on the listing. Switch to trader at the latest when the paid upgrade ships. Owner and legal to decide the
status for the free app.

## 4. App Store (when iOS is in scope)

- **App Privacy (proposed):** "Data Not Collected", on the same facts as the Play answers (Apple counts data as
  collected when it is transmitted off the device and kept beyond the request). Recheck the iOS build first:
  notifications, and any SDK. Maia writes nothing to the calendar on iOS either, and asks for no calendar access.
- Privacy policy URL in App Store Connect and an easy-to-find link in the app (guideline 5.1.1(i)).
- Health guidelines: no health data for advertising or data mining (5.1.3); medical-adjacent apps remind people to
  check with a doctor (1.4.1), which "Your doctor decides" already does.
- Availability: Greece only, as on Play. DSA trader status as above.

## 5. Terms of use

The owner asked (2026-10-11) for terms that keep the maker from being held liable when someone blames the app. The
terms (`Terms_*` strings, twelve parts, "Last updated 11 October 2026", English and Greek) are in the app beside the
privacy notice: About Maia, and setup step 1, which says "By using Maia, you accept the Terms of use" with the terms
one tap away. They limit the maker's liability as far as a contract can, and say plainly where that stops. A clause
claiming more would not hold for consumers and could be struck out whole, taking the lawful part with it.

### What they say

1. **These terms**: an agreement with "Maia's maker"; setting up or using Maia accepts them; for adults (18+, as the
   Play target audience), for her own pregnancy.
2. **What Maia is**: organises; not a medical device; doesn't diagnose, treat, cure or prevent; no medical advice; no
   interpretation of results or readings; her doctor or midwife decides, even where Maia shows something different.
3. **A typical journey**: a common starting point from practice in Greece, the same for every pregnancy with the same
   due date; may not suit hers, may be incomplete or out of date; weeks come from the date she gives.
4. **Not for emergencies**: don't wait for a reminder or a booking; call the doctor, or 112.
5. **Reminders and the timer**: reminders can come up to an hour late, and battery saving, Do Not Disturb or turned-off
   notifications can stop them; the timer only counts time, for calm breathing or exercises her doctor suggested.
6. **What you enter**: she is responsible for checking it; blood pressure is shown as typed, with nothing about what it
   means.
7. **Your data and backups**: only on this phone; lost without a backup or its password; the maker has no copy.
8. **Maia as it is**: free, as is and as available; may change or stop.
9. **Limits of liability**: as far as the law allows, no liability for loss, damage or harm from using Maia or not
   being able to (acting on what it shows, late or missing reminders, mistakes, lost data); nothing limits what the
   law won't let anyone limit (intent, gross negligence) or takes away her rights as a consumer.
10. **Using Maia**: a free personal licence; the words, design and code are the maker's; third-party parts under their
    own licences (Licences screen).
11. **Changes to these terms**: they ship with the app, so each version carries its own, dated.
12. **Greek law**: Greek courts, unless the law lets her go to the courts where she lives; the rest stands if a part
    can't apply.

They follow the wording rules in section 1: nothing says Maia replaces a doctor, tracks or monitors (a test checks
both languages).

### What a contract can't take away (proposed reading, for the lawyer)

- **Intent and gross negligence.** Greek Civil Code art. 332: an advance agreement excluding or limiting liability for
  intent or gross negligence is void.
- **Unfair consumer terms.** Directive 93/13/EEC and Law 2251/1994 (art. 2): a term excluding or limiting a trader's
  liability for death or personal injury, or one that is unclear or one-sided, can be held unfair and void. Plain,
  short parts in both languages are part of the defence (the transparency rule).
- **Product liability.** Law 2251/1994 (art. 6, Directive 85/374/EEC) can't be limited by contract. The new Product
  Liability Directive (EU) 2024/2853 covers software, apps included, placed on the market after 9 December 2026, and
  can't be limited by contract either; it leaves out free and open-source software supplied outside a commercial
  activity. Maia's source has been open (Apache 2.0) since November 2026, but with a paid upgrade planned it is
  likely still supplied in a commercial activity: one more question for the lawyer.
- **Courts.** Under Regulation (EU) 1215/2012 (arts. 17–19) a consumer can always sue where she lives, so the terms
  keep that.
- **Digital content.** The conformity rules of Directive (EU) 2019/770 apply when she pays or provides personal data.
  The free app takes neither, so "as is" is proposed to stand; it must be revisited when the paid upgrade ships.

So the real protection is the product as much as the terms: an organiser, not a medical device (section 1), no
advice and no interpretation, a clinically reviewed template (ADR 0003), honest words about reminders and data, and
the wording rules everywhere. Liability insurance for the maker is the owner's choice.

### Open points for the owner and the lawyer

- **Who "Maia's maker" is**: the terms don't name the maker yet. Consumer and e-commerce rules need the provider's
  name, geographic address and email; add them (both languages) once section 6, item 1 is decided.
- **Acceptance**: a line with a link at setup step 1 ("By using Maia, you accept the Terms of use"). A tick box would
  be stronger evidence; the lawyer to say whether it's needed. The stores' own terms apply alongside.
- **Age**: 18+ matches the Play target audience; the lawyer to confirm, given minors' capacity under Greek law.
- **Changes**: each version carries its own terms, accepted by using that version. The lawyer to say whether a change
  that matters needs her to accept again in the app (a small feature).
- **Hosted copy**: beside the hosted privacy policy, at a public URL.

## 6. What is still needed from the owner

1. **Controller and contact:** who publishes Maia (person or company), legal name, postal address and a privacy and
   support email. Put them in the hosted policy, the in-app notice and terms (`Privacy_*`, `Terms_*`, `About_*`
   strings, English and Greek) and the store listings.
2. **Hosted privacy policy:** a bilingual policy at a URL you control (public, not a PDF, not geofenced), entered in
   Play Console (and App Store Connect later), and linked from About Maia.
3. **Legal review:** of this document, the in-app notice in both languages (including the gaps in section 2), the
   terms of use in both languages and their acceptance at setup (section 5), the store answers, the listing text and
   the DSA status. In particular the Data safety answers for "Open in another app" and sharing results (section 3),
   the transfers she starts on the phone.
4. **Regulatory opinion:** a short written opinion confirming the intended purpose and the non-device reading before
   the paid launch.
5. **Clinical review of `gr.json`:** a licensed midwife or OB-GYN reviews content and Greek wording (for example the
   OGTT fasting note and the anti-D line); then set `ClinicallyReviewed` with `reviewedBy` and `reviewedOn` and bump
   the template version (ADR 0003). Store builds can't start a plan until then.
6. **Play account and declarations:** account type (organisation or personal, see above), closed test if personal,
   Data safety (choose the diagnostics answer), Health apps declaration (Breathing or not), app category, target
   audience, content rating, Greece-only availability, support email, store assets.
7. **DPIA:** planned before any paid cloud feature, with the paid-tier legal basis, processors and DPO decision.
