# Security Policy

Maia holds information about a pregnancy, so security and privacy problems are
taken seriously.

## Supported versions

Only the latest commit on `main` receives security fixes.

## Reporting a vulnerability

**Please do not report security or privacy problems in public issues,
discussions or pull requests.**

Report them privately through GitHub:
[**Report a vulnerability**](https://github.com/MichaelGeranis/maia/security/advisories/new).

Please include what the problem is, steps to reproduce, and the impact you
expect. Never include real health data. Maia is maintained by one person in
their own time. The aim is to acknowledge a report within 7 days and agree a
disclosure date once a fix is understood. You will be credited in the advisory
unless you prefer not to be.

## Scope

Of particular interest:

- reading the plan, results or drafts without the key in the platform keystore;
- weaknesses in the AES-256-GCM storage or the encrypted backup format, or
  opening a backup without its password;
- getting past the app lock;
- health data reaching logs, diagnostics, notifications on the lock screen, or
  anything that leaves the phone;
- files shared into Maia being able to read or overwrite other files.

How storage, keys and backups work is in [docs/architecture.md](docs/architecture.md)
and the ADRs in [docs/adr/](docs/adr/).
