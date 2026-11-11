<#
.SYNOPSIS
    Builds a Release Maia, installs it on the emulator and opens it: real speed, without the Debug overhead.

.DESCRIPTION
    Used by `make run-release`, after `make device` has made sure the emulator is up. Builds for the emulator's own
    CPU only (faster), with the draft plan template allowed, as a personal build. The APK is signed with the debug
    key, so it only goes to an emulator: Maia on her phone is signed with her own key, and a debug-signed build could
    only replace it by uninstalling, which deletes her plan. For the phone, use `make release-personal`.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$serial = ((adb devices) -match "^emulator-\d+`tdevice" | Select-Object -First 1) -replace "`t.*", ''
if (-not $serial) {
    throw 'make run-release only installs on the emulator. Unplug the phone and try again, or use make release-personal for your phone.'
}

$abi = (adb -s $serial shell getprop ro.product.cpu.abi).Trim()
$rid = if ($abi -eq 'arm64-v8a') { 'android-arm64' } else { 'android-x64' }
Write-Host "Release build for $serial ($abi)..."

& dotnet build src/Maia.App/Maia.App.csproj -t:Run -f net10.0-android -c Release -nologo `
    "-p:RuntimeIdentifier=$rid" -p:AndroidPackageFormat=apk -p:MaiaAllowDraftTemplates=true "-p:AdbTarget=-s $serial"
if ($LASTEXITCODE -ne 0) {
    throw "The Release build or install failed (exit $LASTEXITCODE)."
}
