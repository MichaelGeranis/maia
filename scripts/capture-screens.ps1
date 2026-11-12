<#
.SYNOPSIS
    Captures every screen of Maia with the sample journey into docs/design/screens (docs/design/README.md, Screenshots).

.DESCRIPTION
    Runs on the capture emulator, a phone 390 × 844 dp wide (1080 × 2337 px at 443 dpi), so the images are at the
    design's width; -CreateAvd makes it once. Builds a Debug APK with its assemblies inside, installs it with its data
    cleared, walks setup, then makes the sample journey (the Debug-only hook CaptureSample: today is week 24 + 3, OB-GYN
    "Dr. Eleni Papadaki") and opens each screen in English with the phone's animations off. Each image is saved as a
    780 px wide JPEG named after its section in screens.md. Retake them whenever a screen changes, in the same change.

    Screens that are the phone's own (the contact picker, notifications) or hard to reach on purpose (the start-up
    states, the lock screen) are not captured; screens.md describes them in words.

.EXAMPLE
    ./scripts/capture-screens.ps1 -CreateAvd     # once
    ./scripts/capture-screens.ps1                # starts the emulator if needed, then captures
#>
[CmdletBinding()]
param(
    [string] $Avd = 'Maia_Capture',
    [string] $Serial = 'emulator-5556',
    [switch] $CreateAvd,
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$package = 'com.michailgeranis.maia'
$out = Join-Path $root 'docs/design/screens'
$work = Join-Path ([System.IO.Path]::GetTempPath()) 'maia-capture'
$sdk = Join-Path $env:LOCALAPPDATA 'Android/Sdk'
New-Item -ItemType Directory -Force $work | Out-Null
Add-Type -AssemblyName System.Drawing

# adb writes progress to stderr, which Windows PowerShell would take for a failure.
function Invoke-Adb { $ErrorActionPreference = 'Continue'; & adb -s $Serial @args 2>$null }

if ($CreateAvd) {
    'no' | & (Join-Path $sdk 'cmdline-tools/latest/bin/avdmanager.bat') create avd -n $Avd -k 'system-images;android-34;google_apis;x86_64' -d pixel_7 | Out-Null
    $config = Join-Path $env:USERPROFILE ".android/avd/$Avd.avd/config.ini"
    (Get-Content $config) -replace '^hw.lcd.width=.*', 'hw.lcd.width=1080' -replace '^hw.lcd.height=.*', 'hw.lcd.height=2337' `
        -replace '^hw.lcd.density=.*', 'hw.lcd.density=443' -replace '^showDeviceFrame=.*', 'showDeviceFrame=no' | Set-Content -Encoding ascii $config
    Write-Host "Made the $Avd emulator (390 x 844 dp)."
}

# The capture emulator, started if it isn't running.
if (-not ((& { $ErrorActionPreference = 'Continue'; adb devices 2>$null }) -match "^$Serial\s+device")) {
    $port = $Serial -replace '^emulator-', ''
    Start-Process (Join-Path $sdk 'emulator/emulator.exe') -ArgumentList '-avd', $Avd, '-port', $port, '-no-snapshot-save', '-no-boot-anim', '-no-audio'
    Invoke-Adb wait-for-device
}
while ((Invoke-Adb shell getprop sys.boot_completed) -ne '1') { Start-Sleep -Seconds 2 }
foreach ($scale in 'window_animation_scale', 'transition_animation_scale', 'animator_duration_scale') {
    Invoke-Adb shell settings put global $scale 0
}

# A Debug build that runs without the IDE: its assemblies inside the APK.
if (-not $NoBuild) {
    dotnet build (Join-Path $root 'src/Maia.App') -f net11.0-android -c Debug -p:EmbedAssembliesIntoApk=true -nologo -v q
}
$apk = Join-Path $root "src/Maia.App/bin/Debug/net11.0-android/$package-Signed.apk"
Invoke-Adb install -r $apk | Out-Null
Invoke-Adb shell pm clear $package | Out-Null
$activity = (Invoke-Adb shell cmd package resolve-activity --brief $package | Select-Object -Last 1).Trim()

function Save-Screen([string] $name) {
    Start-Sleep -Milliseconds 1500
    $png = Join-Path $work "$name.png"
    Invoke-Adb shell screencap -p /sdcard/maia-shot.png
    Invoke-Adb pull /sdcard/maia-shot.png $png | Out-Null
    $image = [System.Drawing.Image]::FromFile($png)
    try {
        $height = [int]($image.Height * 780 / $image.Width)
        $small = New-Object System.Drawing.Bitmap 780, $height
        $graphics = [System.Drawing.Graphics]::FromImage($small)
        $graphics.InterpolationMode = 'HighQualityBicubic'
        $graphics.DrawImage($image, 0, 0, 780, $height)
        $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -EQ 'image/jpeg'
        $quality = New-Object System.Drawing.Imaging.EncoderParameters 1
        $quality.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 88L
        $small.Save((Join-Path $out "$name.jpg"), $codec, $quality)
        $graphics.Dispose(); $small.Dispose()
    }
    finally { $image.Dispose() }
    Write-Host "  $name.jpg"
}

# The centre of the first element whose words or screen-reader name are $text, scrolling down to find it.
function Read-Screen {
    # The dump can come back empty while the screen is still settling; it is asked again a few times.
    $file = Join-Path $work 'ui.xml'
    for ($try = 0; $try -lt 5; $try++) {
        Remove-Item $file -ErrorAction SilentlyContinue
        Invoke-Adb shell uiautomator dump /sdcard/maia-ui.xml | Out-Null
        Invoke-Adb pull /sdcard/maia-ui.xml $file | Out-Null
        if ((Test-Path $file) -and (Get-Item $file).Length -gt 0) { return [xml](Get-Content $file -Raw -Encoding UTF8) }
        Start-Sleep -Seconds 2
    }
    throw 'The screen could not be read (uiautomator dump).'
}

function Find-Text([string] $text, [int] $scrolls = 10) {
    for ($i = 0; $i -le $scrolls; $i++) {
        $ui = Read-Screen
        $node = $ui.SelectNodes('//node') | Where-Object { $_.text -eq $text -or $_.'content-desc' -eq $text } | Select-Object -First 1
        if ($node) {
            $b = [regex]::Matches($node.bounds, '\d+') | ForEach-Object { [int]$_.Value }
            return @((($b[0] + $b[2]) / 2), (($b[1] + $b[3]) / 2))
        }
        Invoke-Adb shell input swipe 540 1700 540 900 300
        Start-Sleep -Milliseconds 700
    }
    throw "Couldn't find '$text' on the screen."
}

function Invoke-Tap([string] $text) {
    $at = Find-Text $text
    Invoke-Adb shell input tap $at[0] $at[1]
    Start-Sleep -Seconds 2
}

# Scrolls until $text stands near the top of the screen.
function Show-Text([string] $text) {
    $at = Find-Text $text
    if ($at[1] -gt 420) {
        Invoke-Adb shell input swipe 540 ([int]$at[1]) 540 330 1500
        Start-Sleep -Milliseconds 800
    }
}

function Open-Screen([string] $where) {
    Invoke-Adb shell am start -n $activity --es maia.capture $where | Out-Null
    Start-Sleep -Seconds 3
}

function Invoke-Scroll([int] $times = 1) {
    for ($i = 0; $i -lt $times; $i++) { Invoke-Adb shell input swipe 540 1800 540 700 400; Start-Sleep -Milliseconds 800 }
}

Write-Host 'Capturing into docs/design/screens:'
Invoke-Adb shell monkey -p $package -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep -Seconds 6
Find-Text 'Welcome to Maia' 0 | Out-Null
Save-Screen '01-setup-due-date'
Invoke-Scroll 2
Save-Screen '01-setup-due-date-lower'
Invoke-Tap 'Suggest my journey'
Save-Screen '02-setup-a-typical-journey'
Invoke-Tap 'Looks good'
Save-Screen '03-setup-doctor-and-reminders'

Open-Screen 'sample'
Start-Sleep -Seconds 3
Save-Screen '04-today'
Invoke-Scroll 2
Save-Screen '04-today-lower'

Open-Screen '//main/plan'
Save-Screen '05-journey'
Invoke-Tap 'Growth scan'
Invoke-Adb shell input swipe 540 1500 540 1000 400
Save-Screen '06-entry-opened'

Open-Screen '//main/add'
Invoke-Tap 'Exam or test'
Save-Screen '07-new'

Open-Screen '//main/results'
Save-Screen '08-results'
Invoke-Tap '20-week scan'
Save-Screen '17-result-viewer'

Open-Screen '//main/settings'
Save-Screen '14-settings'
Show-Text 'Care team'
Invoke-Tap 'Care team'
Save-Screen '09-care-team'
Show-Text 'Reminders'
Save-Screen '11-reminders'

Open-Screen '//main/week'
Open-Screen 'questions'
Save-Screen '13-questions'
Open-Screen '//main/week'
Open-Screen 'breathe'
Save-Screen '12-breathing'
Open-Screen '//main/week'
Open-Screen 'tasks'
Save-Screen '23-all-tasks'
Open-Screen '//main/week'
Open-Screen 'calendar'
Save-Screen '24-calendar'
Open-Screen '//main/week'
Open-Screen 'restore'
Save-Screen '15-backup'

Open-Screen '//main/settings'
Open-Screen 'about'
Save-Screen '18-about'
Open-Screen 'privacy'
Save-Screen '19-privacy-notice'
Open-Screen '//main/settings'
Open-Screen 'about'
Open-Screen 'licences'
Save-Screen '20-licences'
Open-Screen '//main/settings'
Open-Screen 'about'
Open-Screen 'terms'
Save-Screen '25-terms-of-use'

Write-Host 'Done. Check the images, then update screens.md if a screen moved on.'
