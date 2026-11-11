# 0005 · Encrypted backup file

- Status: Accepted
- Date: 2026-09-30
- Updated: 2026-10-01 (see Updates)

## Context

Settings needs "export" (GDPR: her data is hers) and a way to move Maia to a new phone. The data key lives in the
phone's keystore and never leaves it (ADR 0002), so the encrypted files on disk cannot simply be copied. A readable
export (JSON or PDF) would put health data in plain form wherever she saves it. The paid cloud backup comes later
and is designed with the sharing model.

## Decision

- Export is one encrypted file, `Maia-backup-<date>.maia`, holding the plan and every result file. She saves or
  sends it through the phone's share sheet. Reminders are not included: they are
  set on each phone, once she turns them on there.
- The key comes from a **backup password** she chooses (the rule is under Updates): PBKDF2-HMAC-SHA256, 600,000
  iterations, a random 16-byte salt, 32-byte key. The device key never leaves the keystore. A forgotten password
  cannot be recovered; the screens say so.
- Layout (all integers big-endian):
  - Header: `MAIABAK` magic (7) · format version (1) · iterations (4) · salt (16) · nonce prefix (8).
  - Body: chunks of at most 64 KiB plaintext, each `length (4) · ciphertext · tag (16)`, encrypted with AES-256-GCM.
    The nonce is the prefix plus the chunk counter; the associated data is the header, the counter and a "final" flag.
    Reordered, swapped, truncated or extended files fail to decrypt.
  - Plaintext: records `kind (1) · name length (2) · name (UTF-8) · payload length (8) · payload`, ending with an
    end record. Kind 1 is a result file (name: its blob id), kind 2 is the plan (`PlanDocument` JSON, current schema).
- Restore is offered only on a phone without a plan she can open (see Updates for where). The plan goes through
  `PlanDocumentMigrator`, so older backups upgrade and backups from a newer app are refused. Files get new blob ids;
  the plan is remapped. If anything fails, the files already written are deleted and nothing is kept.

## Consequences

- No personal data leaves the phone in readable form; where she keeps the file is her choice.
- Streaming chunks keeps memory flat for large backups; result files are still read whole (25 MB cap, ADR 0002).
- The format is versioned in the header; a change needs a new version and a reader for the old one.

## Updates

### 2026-10-01 · Password rule, where restore is offered, unreadable files

- **Password rule** (review #30), `BackupPasswordRules`, in the style of NIST SP 800-63B: at least 10 characters and no
  composition rules ("one capital, one symbol"). It turns away, in this order:
  - repeated passwords ("aaaaaaaaaa", "passpasspass");
  - runs, including ones that wrap round: each keyboard row, the digit row and its shifted row, and the Latin and
    Greek alphabets ("abcdefghij", "1234567890123456", "qwertyuiopqwerty");
  - digits only, with symbols and separators ignored, under 14 digits (dates, phone numbers such as
    "+306912345678");
  - runs or repeats with up to 3 characters added at either end, as long as at least 7 are left ("qwertyuiop12");
  - common English, Greek and Greeklish passwords, also with decorations or leetspeak.

  Symbols at the ends are removed before the run and digits-only checks. `PlanService` enforces the same rule when it
  writes a backup; restore accepts any password, so a backup made under an older rule still opens. The backup travels
  by email or Drive, so anyone holding the file can guess offline; the rule turns away what is guessed first.
- **Where restore is offered** (review #1, #2). At setup step 1 ("Moving from another phone?"), at setup step 2 when the
  build can't start a new plan (no reviewed template), and on the start-up screen when the plan on the phone is known
  to be unreadable (`key.unavailable` or both copies damaged). There it replaces that plan only once the password has
  opened the backup, and the restored plan gets a new data key. It never replaces a plan saved by a newer Maia, and
  never one that failed to open for another reason.
- **A result that no longer opens is left out** (review #62). If a stored file can't be read or decrypted, the backup
  leaves it out together with its result, so the reader can stay strict and the rest still restores. Settings names
  what was left out in a Sand note.
- **A newer template** (ADR 0003) and the phone's time zone are applied to a restored plan that lacks them. A
  backup carries the plan's home time zone (schema 4); an older Maia refuses such a backup as one from a newer app.
- **A backup counts once she says it is saved** (review #21). The share sheet can close without the file going
  anywhere, so after it Settings asks "Did you save the file somewhere safe, like your email or Drive?". Only "Yes,
  it's saved" records the time, shown as "Last backup: <date>". A restore records it too: the file she restored from
  is a copy of her plan as it now stands, kept off this phone (a wrong password or a damaged file records nothing).
  Your week offers a calm "Keep a copy of your plan" card
  once the plan is a week old and holds something of hers, when there is no backup or the last is 30 or more days old
  ("Not now" hides it for 30 days). A real save-file flow could replace the question later.
