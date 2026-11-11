# Releasing

How to sign and ship Maia: the one-time signing setup, the one-time key switch and the steps for each release. CI
already builds a Release APK, starts it on an emulator and refuses draft templates on store builds (see
[`architecture.md`](architecture.md), Build, CI and release). Store and privacy items are in
[`compliance.md`](compliance.md).

## One-time setup: signing

1. Create the upload keystore once (outside the repo, for example `%USERPROFILE%\keys\maia.keystore`) and keep an
   **offline copy**. Without it no update can ever be signed. `.gitignore` covers `*.keystore`, `*.jks`, `*.p12`,
   `*.pfx`.
2. Add the repository secrets (README, "Signed releases from CI"): `ANDROID_KEYSTORE_BASE64`, `MAIA_KEY_PASS` and,
   optionally, `MAIA_KEY_ALIAS` (default `maia`). Pipe the base64 into `gh secret set` so it never lands in a file.
3. In Play Console, enrol in **Play App Signing** and decide which key signs the app on phones:
   - *Upload your own key as the app signing key* (export it with the tool Play Console offers): Play installs and
     APKs you build with `build-release.ps1` or `release.yml` are signed alike and can update each other.
   - *Let Google generate the app signing key*: safer if the upload key leaks, but a Play install can't update an APK
     you signed yourself, or the other way round. Anyone moving between the two does the key switch below.
4. Optional: a GitHub `release` environment with required approval for `release.yml` (needs a paid plan on a private
   repository).

## One-time key switch (from the debug key, or between differently signed builds)

Android updates an app only when the new build is signed with the same key, and uninstalling Maia deletes her plan
(backup is off and the data key lives in the phone's Keystore). Builds from Visual Studio and the CI artifact
`maia-android-release-apk` are signed with the **debug key**. To move a phone from such a build (or between a Play
install and a self-signed APK) to the release-signed build, once:

1. In Maia, Settings, Backup: create a backup, send it off the phone (email or Drive) and answer "Yes, it's saved".
   Keep the password.
2. Uninstall Maia. This deletes the plan on the phone.
3. Install the release-signed build (`./scripts/build-release.ps1 -Install`, the APK from `release.yml`, or Play).
4. At setup step 1, "Moving from another phone? Restore a backup": choose the file, type the password.
5. In Settings, turn Reminders and the lock back on if she used them (neither is in the backup).

`build-release.ps1 -Install` prints these steps when `adb` reports `INSTALL_FAILED_UPDATE_INCOMPATIBLE`.

## Each release

1. `main` is green in CI: core tests, the Android Release APK job (store-gate check, 16 KB alignment) and the emulator
   smoke test. Read the trim-analysis summary.
2. If template content changed: `version` bumped, sign-off fields set, the template test passes. Installed over the
   last build, a plan from the older version takes the new wording, and checks it adds wait switched off (ADR 0003).
3. If the stored format changed: `schemaVersion` bumped with a migrator step and a test (CLAUDE.md).
4. Try the change on a real phone, and check that installing the new build over the last one keeps her journey.
5. Tag: `git tag v<x.y> && git push origin v<x.y>` (or run `release.yml` by hand). versionCode is the commit count,
   so it rises with every commit. The tag gives the version name people see (`v1.2` gives 1.2); a run by hand off a
   branch keeps `ApplicationDisplayVersion` from `Maia.App.csproj`, and a tag that isn't a version stops the release.
6. From the run, download the AAB (with `mapping.txt`) and the arm64 APK. Check the job summary: version name,
   versionCode, signer, certificate SHA-256.
7. Play Console: upload the AAB to **internal testing** first and install it from Play on a test phone. Then closed
   testing (for a new personal account: at least 12 testers opted in for 14 days before production access), then
   production, Greece only. Upload `mapping.txt` if Play doesn't pick it up from the bundle.
8. Keep the artifacts: GitHub deletes them after 30 days.
