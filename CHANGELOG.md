# Changelog

All notable changes are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed

- Maia is open source under the Apache License 2.0 (it was proprietary).
- .NET 11 and .NET MAUI 11 (from .NET 10 and MAUI 10.0.20): SDK 11.0.100, MAUI workload set 11.0.100.1 with
  `Microsoft.Maui.Controls` 11.0.0. Android builds compile against API 37 with JDK 21; the target stays API 36 and the
  minimum API 24, and the permissions are unchanged.
- Packages: `Microsoft.Extensions.Logging.Debug` 11.0.0, `Microsoft.NET.Test.Sdk` 18.12.0,
  `xunit.runner.visualstudio` 4.0.1, ReportGenerator 5.6.0, and the AndroidX, Kotlin and Glide libraries MAUI 11
  brings. The third-party notices and the Licences screen list them.
- CI: JDK 21, the Android SDK platform installed by the build, newer pinned actions. Dependabot runs weekly with one
  pull request per ecosystem and leaves `Microsoft.Maui.Controls` to move with the workload set.

### Added

- Contributing guide, security policy, code of conduct, and issue templates,
  including one for plan template reviews.
