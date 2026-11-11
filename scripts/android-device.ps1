<#
.SYNOPSIS
    Makes sure a phone or the emulator is ready for an install. Used by `make run` and `make release-emulator`.

.DESCRIPTION
    With no phone or emulator connected, starts the emulator and waits until it has booted. Then checks that the
    device's package manager answers: an emulator restored from a snapshot taken while an install was stuck can boot
    with its package manager hung, and an install then waits forever. When that happens on the emulator, it is
    restarted without loading its snapshot (a cold boot, about half a minute), which clears it.

.EXAMPLE
    ./scripts/android-device.ps1 -Avd Pixel_API_34
#>
[CmdletBinding()]
param(
    [string] $Avd = 'Pixel_API_34'
)

$ErrorActionPreference = 'Stop'

function Test-Connected {
    [bool]((adb devices) -match "`tdevice")
}

function Wait-Booted {
    adb wait-for-device
    while ((adb shell getprop sys.boot_completed) -ne '1') { Start-Sleep -Seconds 2 }
}

function Test-PackageManager {
    $job = Start-Job { adb shell pm path android }
    $answered = [bool](Wait-Job $job -Timeout 20)
    Stop-Job $job -ErrorAction SilentlyContinue
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    $answered
}

if (-not (Test-Connected)) {
    Write-Host "No phone or emulator connected: starting $Avd..."
    Start-Process emulator -ArgumentList '-avd', $Avd
    Wait-Booted
}

if (Test-PackageManager) {
    return
}

$serial = ((adb devices) -match "^emulator-\d+`tdevice" | Select-Object -First 1) -replace "`t.*", ''
if (-not $serial) {
    throw 'The connected phone is not answering installs. Unplug it, plug it back in and try again.'
}

Write-Host "The emulator is stuck (its package manager does not answer): restarting it without its snapshot..."
adb -s $serial emu kill | Out-Null
Start-Sleep -Seconds 5
Start-Process emulator -ArgumentList '-avd', $Avd, '-no-snapshot-load'
Wait-Booted
if (-not (Test-PackageManager)) {
    throw "The emulator is still stuck. Wipe it in Android Studio's Device Manager (Wipe Data), then try again."
}
Write-Host 'Emulator ready.'
