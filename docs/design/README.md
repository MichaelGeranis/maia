# Maia design

How Maia looks, moves and speaks. This page holds the rules every screen follows: words, colour, type, icons,
shape and motion. [`screens.md`](screens.md) describes each screen (content, actions, states) and
[`system.html`](system.html) shows the colours, sizes and icons side by side, in English and Greek (live, private to
the owner: https://claude.ai/artifact/Dwwomm7ZUc5oNKGV8LTJ2a).

- **Screenshots** in [`screens/`](screens/) are captured from the app itself (see Screenshots below). Where a
  screenshot and `screens.md` disagree, `screens.md` wins, and the screenshot is retaken.
- The first prototype ([`prototype/maia-prototype.dc.html`](prototype/maia-prototype.dc.html), live:
  https://claude.ai/artifact/UrA7Ht3Z8w96GeyZfEp3h8) is kept for history. It needs the design tool's runtime to
  render, uses the old fonts and predates these rules; it is no longer the reference.

## Feel

Calm, natural, unhurried. A quiet garden by the sea, not a hospital and not a baby-shop.

- **Green, blue and a warm sun, never red.** Attention is Sand with plain words, errors and destructive actions included.
- **Organiser first.** Today answers "what's next, and what's on this week?", not "how big is my baby?".
- **Soft surfaces.** Misty background, white cards with thin lines, generous radii, pill buttons.
- **Few words.** Short, warm, second-person copy. One idea per line.
- **Gentle motion** that makes the app feel alive but never busy (see Motion).
- **One of everything.** One font, 14 colours, 6 text sizes, one icon set, one logo, one word for each thing. A new
  screen is built from what is here; anything new is added here first.

## Words

**One word for each thing, the same everywhere, in both languages.**

| She reads | English | Greek | Never |
|---|---|---|---|
| Everything Maia keeps for her pregnancy, and the timeline screen | Journey ("your journey") | Πλάνο («το πλάνο σου») | "plan" in English, «διαδρομή» in Greek |
| The country's suggested set of checks, at setup | A typical journey | Ένα συνηθισμένο πλάνο | "template" |
| The first screen | Today | Σήμερα | Week, Home, Your week |
| Adding something | New | Νέο | Add (as a name) |
| Two calm minutes of breathing | Breathing | Αναπνοή | Breathe, Ανάσα |
| Her doctors, in Settings | Care team | Ομάδα φροντίδας | Η ομάδα σου |
| A to-do | Task / tasks | Εργασία / εργασίες | Τι να κάνεις |
| What she accepts by using Maia, in About Maia | Terms of use | Όροι χρήσης | Terms and conditions, T&Cs, Όροι και προϋποθέσεις |

In code the journey stays `PregnancyPlan`, `PlanPage`, `PlanViewModel` (see the glossary in
[`../product.md`](../product.md)); only what she reads changes.

**Names are nouns.** Tabs, screen titles, card and section headings, tiles and links name a place or a thing. They
don't tell her what to do ("Add to your journey"), don't announce ("Here's your week") and don't start with "Your".
**A screen's title is the name she tapped** to get there:

| She taps | It opens, titled |
|---|---|
| Today · Journey · New · Results · Settings (the bar) | Today · Journey · New · Results · Settings (Σήμερα · Πλάνο · Νέο · Αποτελέσματα · Ρυθμίσεις) |
| Questions tile, on Today | Questions (Ερωτήσεις) |
| Breathing tile, on Today | Breathing (Αναπνοή) |
| All tasks, on Today | All tasks (Όλες οι εργασίες) |
| The week pill, on Today (it carries the calendar icon) | Calendar (Ημερολόγιο) |
| About Maia, Privacy notice, Terms of use, Licences | the same names |

- **Buttons are verbs**, because there she chooses an action: "Save", "Add task", "Delete task", "Start my journey". A
  link that leads to a place is a noun ("All tasks"); a link that does something is a button and says what it does
  ("Not sure? Use your last period instead", "Restore a backup").
- **Field labels** are a noun ("Name") or a short question ("When?", "When is your baby due?").
- **Messages** (start-up, lock, notes, toasts) say plainly what happened or what is true ("Maia can't open your
  journey", "A newer Maia is needed"). A title never gives an instruction.
- Two exceptions, on purpose: setup's welcome line "Let's organise your pregnancy" (an invitation she sees once), and
  the headings of the privacy notice and the terms of use, which are a document's ("Your rights", "Your data and
  backups").
- Second person, warm, short: "your journey", "your doctor". Make her control explicit: "Your doctor may suggest a
  different set. Switch off what doesn't apply; you can change it any time."
- Never alarming, never medical advice. No "warning", "risk", "urgent", no exclamation marks.
- Never say Maia replaces a midwife or doctor. Never frame the app around fathers.
- Dates like "Tue 6 Oct at 10:30", windows like "Weeks 24–28 · 27 Sep – 31 Oct", age like "Week 24 + 3".
- Every string in English and Greek (`Strings.resx`, `Strings.el.resx`), saying the same thing: the Greek is not a
  different wording.

## Colour

**Fourteen colours: three families of three, and five neutrals.** They are the only keys in
`src/Maia.App/Resources/Styles/Colors.xaml`; no screen, style or drawing uses another hex value.

| Family, and what it means | Dark: text on the light step | Middle: marks, nodes, icons | Light: fills |
|---|---|---|---|
| **Green**: Maia, hers, done | `Pine` `#2E5A4C` | `Sage` `#4E8A70` | `SageTint` `#E1ECE3` |
| **Sea**: booked, "you are here", links, focus | `SkyDeep` `#1F4F5B` | `Sea` `#2F6F7E` | `Sky` `#DCEBEE` |
| **Sun**: the due date, and attention | `SandDeep` `#5C4611` | `Sun` `#E9C979` | `Sand` `#F5EAC8` |

| Neutral | Hex | Use |
|---|---|---|
| `Ink` | `#1E2D29` | Main text |
| `Muted` | `#56665F` | Secondary text, inactive tabs, control outlines (empty tick circles, radio rings, a switch that is off) |
| `Line` | `#DDE7DF` | Card borders, dividers, field and outline-button borders, neutral chips |
| `Mist` | `#F2F5EF` | Page background, the bottom bar, panels inside cards |
| `White` | `#FFFFFF` | Cards, fields, outline buttons |

Rules:

- **Each family keeps its meaning.** Green is Maia and what's done, sea is what's booked and where she is now, sun
  is the due date and anything that needs her attention. Neutrals carry everything else.
- **Text on a light fill uses the dark step of the same family** (SkyDeep on Sky, SandDeep on Sand, Pine on SageTint).
  Text on Pine, Sea or SandDeep is White.
- **Sage, Sun and Line are never text** (Sage is 4 : 1 on white, Sun and Line much less). Green text is Pine.
- **Transparency is allowed, new colours are not:** cards are White at 85 % (done ones 55 %), the bar is Mist at
  90 %, the scrim behind sheets is Ink at 40 %, shadows are Pine at 8 %, the hero's second lines are White at 75 %.
- Illustrations use the same fourteen: the background blobs (Sky, SageTint), falling leaves (Sage, Sky, Sun), setup's
  hills and sun (Sage, SageTint, Sun, Sky), the hero's waves (Sage and Sea, part-transparent), Breathing's sphere
  (Sky to Sea, words in SkyDeep), avatars (SageTint with Pine, Sky with SkyDeep), the logo.

Components:

| Component | Colours |
|---|---|
| Page | Mist, with two drifting blobs (Sky top-left, SageTint bottom-right) |
| Card | White 85 %, 1 dp Line border; an expanded card 1 dp Sage |
| Primary button | Pine, White words |
| Outline button | White, 1 dp Line border, Pine words |
| Confirm button (the action in a confirmation) | SandDeep, White words |
| Keep button | White, 1 dp Line border, Ink words |
| Link, text button | Sea words |
| Field | White, 1 dp Line border; focused 2 dp Sea |
| Choice pill | Chosen: Pine, White words. Not chosen: White, Line border, Ink words |
| Switch | On: Sage track, White thumb. Off: Muted track |
| Tick circle, radio | Empty: Muted ring. Ticked: Sage with a White tick |
| Bottom bar | Mist 90 %, Line along its top; active slot Pine with a Pine dot, the others Muted |
| Next up hero | Pine; "NEXT UP" and the second lines White 75 %, the title White |
| Info banner ("You are here", setup's summary) | Sky, SkyDeep words |
| Notice, confirmation strip, attention card | Sand, SandDeep words, 1 dp Sun border |
| Toast | Pine, White words, a Pine tick on a SageTint circle |
| Icon tile (Settings cards) | SageTint, Pine icon |
| Neutral chip ("Routine", "Not booked") | Line, Muted words |

### Status colours

Status is derived by the domain; the UI maps it (`StatusBackground` / `StatusForeground` converters). Status is never
colour alone: the chip always has its word.

| Status | Chip | Timeline node | Card |
|---|---|---|---|
| Not booked | Line / Muted | White, Muted ring at 50 % | White 85 % |
| Booked | Sky / SkyDeep | Sea | White 85 % |
| Done, Probably done | SageTint / Pine | Sage with a White tick | White 55 %, title in Muted |
| Due date | Sand / SandDeep | Sun | White 85 % |
| You are here | Sky banner, SkyDeep words | Sea, with a soft pulsing Sea ring | |

All cards share the Line border; the chip and the node carry the status. Setup's tags: *Routine* is a neutral chip;
*Offered*, *Often offered* and *If advised* are Sky / SkyDeep. On Journey an entry she added carries its kind as an
eyebrow ("DOCTOR VISIT", "EXAM OR TEST", "HOSPITAL VISIT", "SOMETHING ELSE").

## Type

One font for English and Greek: **M PLUS Rounded 1c** (the owner, 2026-10-02), soft and rounded, with Latin and Greek
drawn alike. Three weights ship, Regular, Medium and Bold (`MPLUSRounded1c-*.ttf`, subset to Latin, Greek and
Cyrillic, about 280 KB each); letters outside them, in a name she types for example, are drawn by Android's own
fallback fonts. SIL OFL; the licence is in `licenses/fonts/` and on the Licences screen. A test keeps the bundled
files, their registrations and the font aliases (`FontTitle`, `FontText`, `FontTextSemiBold`, `FontTextBold`) in step.

**Six sizes**, each a named style in `Resources/Styles/Maia.xaml`. Screens use the styles and never set a size of
their own (a test enforces it).

| Style | Size (dp) | Weight | Use |
|---|---|---|---|
| `Title` | 32 | Bold | Screen titles ("Today", "Journey", "Let's organise your pregnancy") |
| `Hero` | 26 | Bold | The Next up title, Breathing's words in the sphere |
| `Heading` | 16 | Bold | Card titles, check and entry names, primary buttons |
| `Body` | 15 | Regular (Bold on outline and text buttons, Medium for a stressed line) | Body text, fields, buttons |
| `Caption` | 13 | Regular, or Medium (`CaptionStrong`) | Dates, weeks, notes, helping lines |
| `Label` | 12 | Bold | Eyebrows (UPPERCASE, letter-spacing 0.08 em), chips, badges, tab names, Journey's week headings |

- Long words wrap rather than shrink; cards grow with her text size. The one exception is the bar's five names, which
  share one size and shrink together (see Components).
- Weights: Bold for titles, headings, labels and buttons; Regular for reading; Medium only for a caption or line
  that needs a little weight.

## Icons and the logo

**One icon set: [Lucide](https://lucide.dev)** (ISC licence, on the Licences screen), drawn on its 24-unit grid with a
1.8 stroke, round ends and joins, in **one colour at a time**: the colour of the words beside it, Pine when active,
Muted when not. Sizes: 24 dp in the bar, 18 dp in an icon tile, 16 dp beside text. Keys live in
`Resources/Styles/Icons.xaml`, named for what they mean in Maia.

| Key | Lucide icon | Where |
|---|---|---|
| `IconToday` | `sunrise` | Today tab |
| `IconJourney` | `route` | Journey tab |
| `IconNew` | `plus` | New tab; the round + that adds a task or a question |
| `IconResults` | `folder` | Results tab (no tick: Maia doesn't judge results) |
| `IconSettings` | `settings` | Settings tab |
| `IconCheck` | `check` | Ticks, toasts, chosen results |
| `IconBack` / `IconChevronRight` | `chevron-left` / `chevron-right` | Back pill / a row that opens a screen; turned down (up when open) for "Show all" and a card that folds |
| `IconClose` | `x` | Close, stop choosing, unlink |
| `IconClock` | `clock` | Times, Reminders |
| `IconDate` | `calendar` | The due date (Pregnancy card); the week pill on Today, which opens Calendar |
| `IconPhone` | `phone` | Call |
| `IconStethoscope` | `stethoscope` | Her OB-GYN |
| `IconTeam` / `IconUserPlus` | `users` / `user-plus` | Care team / link a doctor |
| `IconQuestions` | `message-circle` | Questions tile |
| `IconLeaf` | `leaf` | Trimester headings |
| `IconPhoto` / `IconFile` | `image` / `file-text` | Photo and PDF results, Licences, Restore |
| `IconUpload` / `IconCamera` / `IconSearch` | `upload` / `camera` / `search` | Results, Backup |
| `IconGrip` | `grip-horizontal` | A question's drag handle |
| `IconLess` / `IconMore` | `minus` / `plus` | The Timer's round − and + for her times |
| `IconSparkle` / `IconGlobe` / `IconLock` / `IconDevice` | `sparkles` / `globe` / `lock` / `smartphone` | Settings: Animations, Language, Screen lock, Data |
| `IconDiagnostics` | `clipboard-list` | Diagnostics: the error codes Maia notes (never a heartbeat or pulse: Maia monitors nothing) |
| `IconPrivacy` | `shield` | Privacy notice (About Maia) |
| `IconBloodPressure` | `heart` | Beside a blood pressure reading on Journey (a plain heart, never a pulse line) |
| `IconTerms` | `scroll-text` | Terms of use (About Maia) |

A new icon comes from Lucide too, with a key named for its meaning: its SVG elements become one path of lines and
cubic curves only (M, L, C, Z), since Android's path reader draws some of Lucide's arcs wrongly or not at all. A test keeps every
icon in this set, at the one stroke and the three sizes.

**The logo** is Maia's sprout: a stem and two leaves, filled, the drawing in `Resources/AppIcon/appiconfg.svg`. It is
the same drawing everywhere, in two colourings: **on Pine** (the app icon, the splash, and in white alone for the
notification icon) the stem and right leaf are Mist and the left leaf Mist at 60 %; **on a light background** (the
header beside the "maia" wordmark, About Maia, the lock screen, start-up, setup, the About row in Settings) the stem
and right leaf are Pine and the left leaf Sage. The wordmark "maia" is the only lower-case use of the name.

**Illustrations** are Maia's own drawings, not icons: the journey card's path, setup's hills and rising sun, the
hero's waves, the falling leaves, Breathing's sphere and the dandelion seed on the Breathing tile.

## Shape, spacing, depth

- Radii: **pill** for every button, chip, badge and pill; **22** for cards, sheets and Today's tiles; **14** for fields,
  icon tiles, result tiles, notes and panels inside cards; circles stay circles (avatars, tick circles, round buttons).
- Spacing: 16 page gutter at phone width, 12–16 inside cards, 8 between chips.
- Borders: 1 dp Line on cards; a chosen option gets a 2 dp Pine border.
- Shadows: soft and green-tinted only (Pine at 8 %, e.g. `0 16 34`). No grey drop shadows.
- Touch targets at least 44 × 44 dp.

## Components

- **Bottom bar**: five flat slots of the same size, each an icon and a name: **Today · Journey · New · Results ·
  Settings** (Σήμερα · Πλάνο · Νέο · Αποτελέσματα · Ρυθμίσεις). No raised centre button, no ring, no gear anywhere.
  Active slot: Pine icon and name plus a small dot under it. Screen readers hear New as "New, something for your
  journey". The five names share one size: 12, unless two neighbours would come closer than 4 dp or a name would run
  past its slot; then all shrink together, but not below what looks like 9 at her text size (where they may come as
  close as 2 dp). Each slot fills its column and is at least 52 dp high.
- **Header** (Today only): the logo and the "maia" wordmark (`FontTitle`, Pine), and a glass pill with the week and
  a 16 dp Pine calendar icon, "Week 24 + 3": the pill is the way into Calendar (its touch area 44 dp high). No gear,
  and no separate calendar button.
- **Screen tops**: the title comes first, with no eyebrow above it (setup's "Welcome to Maia" is the one eyebrow, over
  its welcome line). The week and the due date show only on Today, in its pill and its journey card.
- **Journey card** (Today): a hand-drawn curve through the trimester nodes, "you are here" glowing in Sea, a dotted
  path to the Sun dot of the due date; captions: trimester · due date.
- **Next up hero**: Pine, eyebrow "NEXT UP", the title in `Hero`, the day and time, the doctor when Maia knows; a
  layered wave along the bottom edge. One tap opens Journey on that booking.
- **This week card** (Today): "This week" with a round 44 dp Pine + (add a task), round tick-circle rows with a clock
  line in Sea ("Thu 1 Oct · 09:00"), at most 5 rows ("Show all" opens it to 20), then one "All tasks" line with a
  chevron. The task row is one control (`TaskRowView`), the same on All tasks.
- **Timeline** (Journey): trimester headings (leaf icon, eyebrow), a vertical line with status nodes, one card per
  entry; a "You are here" Sky banner where today is. A booked entry stands at its day. With "Tasks" on, a task stands
  on its day, at its week or first in its trimester as a tick circle with its title and clock line, no card. Tapping a
  card expands it in place. **Week headings**: each pregnancy week has a heading across the timeline above its first
  row (`WeekRow`): "Week 25" in the Label size in Pine, a 1 dp Line hairline, its days ("11–17 Oct") in Caption Muted;
  the line pauses for it. Every week up to the due date shows, and an empty one says "Nothing this week" (Caption
  Muted), so the weeks read down Journey as sections, like a calendar's.
- **Month grid** (Calendar): a card with the month's name between ‹ › chevrons, the weekday names (`Label`, Muted)
  and six rows of seven days (`CalendarDayView`, the same 42 for every month). A day is its number (`Body`) over a
  row of 6 dp dots in the status colours (Sun due date, Sea booked, Sage done, Muted a task); today in Sea and Bold;
  the chosen day in a 36 dp circle, Pine (Sea when it is today) with a White number; days of the months around it
  Muted at 50 %. The chosen day's entries are cards under it, read-only, with their status chip and time.
- **Folding card** (Care team in Settings): its heading (icon tile, title, caption) is one tap with a Sea chevron on
  the right, turned down while folded and up while open; screen readers hear an expander. It starts folded.
- **Toggle rows** (setup, Settings, Timer): title, caption, switch on the right.
- **Round icon buttons** (`RoundIconButton`, the Timer's − and +): 48 dp circles, White 85 %, 1 dp Line border, an 18 dp
  Pine icon; dimmed while they can't act.
- **Choice tiles**: a tile with a small badge and a radio in the corner, a 2 dp Pine border and a filled dot when
  chosen (setup step 3's "Reminders on this phone").
- **Chips**: status and tag chips (a word on a light fill); suggestion chips with "+" ("+ Growth scan").
- **Confirmations** ask once more in place on a Sand strip, never in a dialog and never in red: the question names
  what goes, then two buttons in words, the action first (`ConfirmButton`, repeating the question's verb: "Delete
  task") and "Keep it" / "Keep them" (`KeepButton`; «Να μείνει» / «Να μείνουν»).
- **Doctor cards** (Care team): one per doctor she has linked, smaller and plainer than the OB-GYN part (the `Card`
  style, padding 12).
- **Toast**: a short confirmation after an action ("Thyroid test added to your journey, done on Tue 29 Sep."), Pine
  with a tick. When something did not happen it is a calm notice instead: Sand, SandDeep words, a Sun outline, no tick.
- **Icon tiles**: each Settings card opens with a 36 dp SageTint tile (radius 14) holding an 18 dp Pine icon.

## Motion

Subtle, slow, and always off when the phone asks for reduced motion or Maia's own "Animations" switch is off.

| Where | What | Timing |
|---|---|---|
| Screen content | Cards rise 16 dp and fade in, staggered, the first time a screen shows | 0.7 s, ease-out `(.2,.7,.2,1)` |
| New item / chip | Pop from 0.9 scale | 0.5 s |
| A task just added on Today | Its row glows SageTint and fades to clear | 1.6 s once |
| Marking done | Tick draws itself in the node | 0.7 s |
| Timeline | Line grows downward on first show | 1.4 s |
| "You are here" | Soft pulsing ring (Sea) | 2.4 s loop |
| Background | Blobs drift, occasional leaf falls | 18–22 s loops |
| Hero card | Wave layers slide | 9 s / 14 s loops |
| Setup welcome | Sun rises over hills | 2.4 s once |
| Breathing | Sphere grows (in 4 s) and shrinks (out 6 s), words fade; the vibration and tone are not motion and stay with reduced motion | 10 s loop |
| Breathing tile | The dandelion seed drifts up with the breath and down | 4 s / 6 s |
| Sheets | Slide up | 0.45 s |
| Buttons, tiles, rows | Dip to 0.96 while pressed, spring back | 0.1 s / 0.3 s |
| Bottom bar | Active tab springs up, its dot grows in, as a screen arrives | 0.65 s |
| Ticking something off | Tick pops; a few leaves (Sage, Sky, Sun) lift out and drift down | 0.9 s |

Ambient loops (background, waves, glows, ripples) run only while their screen is on screen, all on one 20-steps-a-
second clock, and keep moving while a list scrolls (build order item 31); they hold still only while Battery Saver is
on. They move finished drawings (translate, scale, fade) instead of redrawing them. See `AmbientLoop` in `Maia.App`.

What sits still while a list scrolls is drawn once: the page background and the bottom bar are kept in a layer of
their own, falling leaves and the "You are here" ring fade without being painted offscreen first, and a line icon is
one shape. Journey's cards, chips and dots are `LightBorder`s, which look like the design's but cost much less to
draw while scrolling.

## Accessibility

- Text contrast at least 4.5 : 1, placeholders included (Muted on Mist passes; don't go lighter). Control outlines
  (switch tracks, empty tick circles, radio rings) at least 3 : 1 on their card, which is why they are Muted.
- Touch targets at least 44 × 44 dp, in fact and not only declared: no negative margins on anything she taps.
- Status is never colour-only: the chip always has a word.
- Support dynamic type; cards grow, button words wrap, nothing truncates important text except file names.
- Screen titles and section names are headings. Drawn controls (switches, pills, tick circles, tabs) tell the screen
  reader their role, name and state, in the language Maia shows. A question or form that opens in place takes the
  focus; calm notes are read out as they appear.
- Light theme only for v1 (`UserAppTheme = Light`); a dark theme later, with its own fourteen.

## Screenshots

The images in [`screens/`](screens/) are **captured from the app**, so they can't fall behind what she sees.
`scripts/capture-screens.ps1` takes them all:

- On the `Maia_Capture` emulator, a phone 390 × 844 dp wide (1080 × 2337 px at 443 dpi; `-CreateAvd` makes it once),
  in English, with the phone's animations off, saved as 780 px wide JPEGs.
- With the sample journey, made fresh by a Debug-only hook (`Platforms/Android/CaptureSample.cs`, never in a store
  build): today is week 24 + 3, her OB-GYN is "Dr. Eleni Papadaki", with a booking, entries she added, this week's
  tasks, questions and two results. Nothing in it is anyone's.
- One image per screen, named like the sections of `screens.md` (`04-today.jpg`); a long screen gets a second
  (`04-today-lower.jpg`). The phone's own screens (the contact picker, notifications) and screens reached only when
  something goes wrong (start-up, the lock screen) are described in words.
- Retaken whenever a screen changes, in the same change.
