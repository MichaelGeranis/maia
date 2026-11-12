# Everyday commands for Maia. Run `make` (or `make help`) to list them.
#
# Works from PowerShell or cmd (make then runs recipes with cmd.exe) and from Git Bash, so recipes stick to what
# both understand. Needs GNU make: winget install ezwinports.make
# adb and the emulator are found on PATH (the Android SDK's platform-tools and emulator folders).
#
# Options, e.g. `make release VERSION=130`:
#   AVD       emulator to start            (default Pixel_API_34)
#   VERSION   Android versionCode           (default: the number of commits)
#   KEYSTORE  your signing keystore         (default: %USERPROFILE%\keys\maia.keystore)

AVD ?= Pixel_API_34
VERSION ?=
KEYSTORE ?=

APP := src/Maia.App
ANDROID := net11.0-android
WINDOWS := net11.0-windows10.0.19041.0
TESTS := $(wildcard tests/*.Tests)
PHONE_APK := src/Maia.App/bin/Release/net11.0-android/android-arm64/publish/com.michailgeranis.maia-Signed.apk
PHONE_APK_FOLDER := src\Maia.App\bin\Release\net11.0-android\android-arm64\publish

RELEASE := powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-release.ps1
RELEASE_OPTIONS := $(if $(VERSION),-Version $(VERSION)) $(if $(KEYSTORE),-KeyStore "$(KEYSTORE)")

.DEFAULT_GOAL := help
.PHONY: help test build build-release build-windows ci run run-release run-windows emulator device devices \
        release release-personal release-emulator install apk-folder clean

help:
	$(info Maia - everyday commands)
	$(info )
	$(info   make test               run every test project)
	$(info   make build              Debug build for Android)
	$(info   make build-release      Release build for Android, as CI builds it)
	$(info   make build-windows      Debug build for the Windows window)
	$(info   make ci                 tests, then the Release build: what CI checks)
	$(info )
	$(info   make emulator           start the Android emulator and wait until it has booted)
	$(info   make run                build, install and open Maia - starts the emulator if no phone is connected)
	$(info   make run-release        Release build on the emulator: real speed, draft plan template, debug key)
	$(info   make run-windows        open Maia in a Windows window - no reminders, contacts or camera)
	$(info   make devices            list the phones and emulators adb can see)
	$(info )
	$(info   make release            signed Release APK for a phone, store rules: no draft plan template)
	$(info   make release-personal   signed Release APK for YOUR phone, with the draft plan template)
	$(info   make release-emulator   signed Release APK for the emulator, installed on it)
	$(info   make install            install the last phone APK on a connected phone or emulator)
	$(info   make apk-folder         open the folder with the phone APK, to copy it to your phone)
	$(info )
	$(info   make clean              delete every bin and obj folder)
	@cd .

test:
	$(foreach p,$(TESTS),dotnet test $(p) --nologo &&) echo All tests passed.

build:
	dotnet build $(APP) -f $(ANDROID) -nologo

build-release:
	dotnet build $(APP) -f $(ANDROID) -c Release -nologo

build-windows:
	dotnet build $(APP) -f $(WINDOWS) -nologo

ci: test build-release

run: device
	dotnet build $(APP) -t:Run -f $(ANDROID) -nologo

run-release: device
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-release.ps1

run-windows:
	dotnet build $(APP) -t:Run -f $(WINDOWS) -nologo

emulator:
	powershell -NoProfile -Command "Start-Process emulator -ArgumentList '-avd','$(AVD)'; adb wait-for-device; while ((adb shell getprop sys.boot_completed) -ne '1') { Start-Sleep -Seconds 2 }; Write-Host 'Emulator $(AVD) is ready.'"

# Starts the emulator only when no phone or emulator is connected, waits until it has booted, and restarts a stuck
# emulator cold (see scripts/android-device.ps1).
device:
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts/android-device.ps1 -Avd $(AVD)

devices:
	adb devices -l

# The release targets sign with your own keystore and ask for its password (see scripts/build-release.ps1).
release:
	$(RELEASE) $(RELEASE_OPTIONS)

release-personal:
	$(RELEASE) -AllowDraftTemplates $(RELEASE_OPTIONS)

release-emulator: device
	$(RELEASE) -AllowDraftTemplates -RuntimeIdentifier android-x64 -Install $(RELEASE_OPTIONS)

install:
	adb install -r "$(PHONE_APK)"

# Explorer always exits with 1, hence the leading dash.
apk-folder:
	-explorer "$(PHONE_APK_FOLDER)"

clean:
	powershell -NoProfile -Command "Get-ChildItem src,tests -Directory -Recurse -Include bin,obj | Remove-Item -Recurse -Force"
