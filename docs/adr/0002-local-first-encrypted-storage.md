# 0002 · Local-first, encrypted storage

- Status: Accepted
- Date: 2026-09-30
- Updated: 2026-10-01 (see Updates)

## Context

Plans and results are health data (GDPR Art. 9). The free app should work without accounts or servers. The paid
upgrade adds cloud backup and a shared view.

## Decision

- Store the plan as one encrypted file and each result as its own encrypted blob, in the app's private folder.
- Use AES-256-GCM (built into .NET, no dependencies) with a random nonce per write and the storage slot bound as
  associated data. Version the envelope format.
- Keep the 256-bit data key in the platform keystore (MAUI `SecureStorage`).
- Keep the stored JSON shape separate from the domain model, versioned, with step-by-step migrations.
- Write atomically and keep one backup copy.

## Consequences

- No personal data leaves the phone in the free app; the server for the paid features only ever stores ciphertext.
- Losing the key (for example, restoring a phone without keystore backup) makes local data unreadable. The paid
  backup must therefore carry a key the parent can recover (to be designed with the sharing model).
- Result files are read into memory whole; fine up to the 25 MB cap. Streaming encryption is a later option.

## Updates

### 2026-10-01 · Losing the key, copies on the phone, photos

- **A lost key is a calm screen, never a crash or a new key** (review #2, #63). A key is made only while none of her
  encrypted files exists, and is read back after writing (Android's SecureStorage can drop a write silently). If her
  files are there and the key is missing or unreadable, or the keystore fails, nothing is touched: the start-up screen
  says Maia can't open her plan and offers Restore a backup or Start again (which asks once more). A key is never
  regenerated over existing files, because it could never open them and saving with it would mix two keys.
- **No copies leave the phone by the back door.** `allowBackup="false"` covers Android 11 and older; from Android 12,
  `dataExtractionRules` exclude everything from cloud backup and device-to-device transfer. Files arriving on a new
  phone without their key would only reach the screen above. Moving phones goes through the encrypted backup file
  (ADR 0005).
- **Delete everything forgets the key.** Result files go first, then every plan file, and the key last, so no copy left
  behind can be opened again. Her next plan gets a new key. The setup draft (`drafts/setup.maia`) uses the same
  key and becomes unreadable with it.
- **Durable writes.** Temp files are flushed to disk before the rename; after a fallback to `plan.maia.bak` the next
  save keeps that good copy (review #61). A plan saved by a newer Maia is refused, never treated as damage (review #84).
- **Decrypted copies.** PDFs and photos open inside Maia; a PDF is decrypted into memory (Android 11+) or into a
  private file unlinked as soon as it is opened. Files go to another app only on request, and that copy is removed when
  she comes back. The whole cache is emptied at start-up and on Delete everything; picked and shared files are read
  straight from their provider, never copied (review #36, #85). What another app keeps of a file she opened there is
  outside Maia's control, and the viewer says so before she opens it there, for every file Maia can't draw itself
  (a HEIC photo on Android 7 and 8 included).
- **Photos are kept smaller, with location removed where Maia can** (review #75). A photo over 3000 px on its long
  edge, or any HEIC/HEIF, is stored as a new JPEG: long edge 3000 px, quality 90, orientation applied, no metadata. A
  smaller JPEG, PNG or WebP keeps its picture as it is, without its location: the Exif GPS block is wiped and XMP
  (and a PNG's text chunks) left out; its other Exif data stays. HEIC is converted even when small, so its
  location goes and any phone can show it later; Android 7 and 8 can't decode HEIC, so there it is kept as picked,
  location included. PDFs are never changed. Legibility of real paper reports at 3000 px is still to be confirmed on
  a phone.
- **Files are no longer buffered together** (review #45). Each picked file is checked for type and known size, then
  read whole up to the 25 MB cap and released before the next. Decrypting a stored file whole stays the accepted
  trade-off.
