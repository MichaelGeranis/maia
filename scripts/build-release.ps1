<#
.SYNOPSIS
    Builds a Release APK of Maia signed with your own key, ready to install on a phone.

.DESCRIPTION
    Publishes the Android app in Release, signs it with your keystore and checks the signature.

    The keystore password comes from MAIA_KEY_PASS when it is set (for CI); otherwise it is asked for
    without echo. It reaches the build through the environment, never the command line, and is cleared
    afterwards.

    Always sign with the same keystore. Android only updates an installed app when the new APK has the
    same key, and uninstalling Maia deletes the plan on the phone (allowBackup is off and the data key
    lives in the Android Keystore).

    Until gr.json is clinically reviewed, a normal Release build refuses the Draft plan template, so it
    cannot start a new plan: setup step 2 says so and offers "Restore a backup". Existing plans and
    restoring a backup work.

    -AllowDraftTemplates builds a personal APK, for your own phone only, that can start a plan from the
    draft template (ADR 0003). Never share it or upload it anywhere. The build refuses the switch for an
    AAB and on CI (MaiaStoreGate, error MAIA0001), so store builds keep reviewed templates only.

.EXAMPLE
    ./scripts/build-release.ps1

.EXAMPLE
    ./scripts/build-release.ps1 -Install

.EXAMPLE
    ./scripts/build-release.ps1 -Version 40 -RuntimeIdentifier android-x64

.EXAMPLE
    ./scripts/build-release.ps1 -AllowDraftTemplates -Install

    A personal APK that can start a plan from the draft template. Only for your own phone.
#>
[CmdletBinding()]
param(
    # Android versionCode. Defaults to the number of commits on HEAD, so it rises with every commit.
    [int] $Version = 0,

    [string] $KeyStore = (Join-Path $env:USERPROFILE 'keys\maia.keystore'),

    [string] $KeyAlias = 'maia',

    # android-arm64 for phones, android-x64 for the emulator.
    [ValidateSet('android-arm64', 'android-x64')]
    [string] $RuntimeIdentifier = 'android-arm64',

    # Install over the app on the connected phone with adb. Never uninstalls, so her data stays.
    [switch] $Install,

    # Personal build only: let this APK use Draft plan templates (not clinically reviewed). Never distribute it.
    [switch] $AllowDraftTemplates
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src\Maia.App\Maia.App.csproj'
$publishDir = Join-Path $repo "src\Maia.App\bin\Release\net10.0-android\$RuntimeIdentifier\publish"
$sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { Join-Path $env:LOCALAPPDATA 'Android\Sdk' }

if (-not (Test-Path -LiteralPath $KeyStore)) {
    throw "Keystore not found: $KeyStore. Pass -KeyStore with the path to your maia.keystore."
}

if ($Version -le 0) {
    $Version = [int](git -C $repo rev-list --count HEAD)
    if ($LASTEXITCODE -ne 0) { throw 'Could not count the commits on HEAD; pass -Version.' }
}

if (git -C $repo status --porcelain) {
    Write-Warning "Uncommitted changes: this APK does not match a commit, and it shares versionCode $Version with the last commit."
}

$draftArgs = @()
if ($AllowDraftTemplates) {
    Write-Warning ('PERSONAL BUILD: this APK uses the DRAFT plan template, which is not clinically reviewed. ' +
        'Install it only on your own phone. Do not share it, send it or upload it to a store.')
    $draftArgs = @('-p:MaiaAllowDraftTemplates=true')
}

$ownsPassword = -not $env:MAIA_KEY_PASS
if ($ownsPassword) {
    $secure = Read-Host 'Keystore password' -AsSecureString
    $env:MAIA_KEY_PASS = [System.Net.NetworkCredential]::new('', $secure).Password
}

try {
    & dotnet publish $project -f net10.0-android -c Release -r $RuntimeIdentifier -nologo `
        -p:AndroidPackageFormat=apk "-p:ApplicationVersion=$Version" `
        -p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=$KeyStore" "-p:AndroidSigningKeyAlias=$KeyAlias" `
        -p:AndroidSigningKeyPass=env:MAIA_KEY_PASS -p:AndroidSigningStorePass=env:MAIA_KEY_PASS @draftArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed (exit $LASTEXITCODE). A wrong keystore password or alias fails at the signing step."
    }
}
finally {
    if ($ownsPassword) { $env:MAIA_KEY_PASS = $null }
}

$apk = Get-ChildItem -LiteralPath $publishDir -Filter '*-Signed.apk' | Select-Object -First 1
if (-not $apk) { throw "No signed APK in $publishDir." }

# Check who signed it: a debug-signed APK could never be updated by a build signed with your key.
$buildTools = Get-ChildItem -LiteralPath (Join-Path $sdk 'build-tools') -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^\d+(\.\d+)+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if ($buildTools) {
    $certs = & (Join-Path $buildTools.FullName 'apksigner.bat') verify --print-certs $apk.FullName
    if ($LASTEXITCODE -ne 0) { throw 'The APK signature does not verify.' }
    $dn = $certs | Select-String 'certificate DN: (.+)$' | Select-Object -First 1
    $sha = $certs | Select-String 'certificate SHA-256 digest: (.+)$' | Select-Object -First 1
    if (-not $dn) { throw 'apksigner printed no signer.' }
    $signer = $dn.Matches[0].Groups[1].Value
    if ($signer -like '*Android Debug*') { throw 'The APK is signed with the debug key, not your keystore.' }
}
else {
    $signer = '(not checked: Android build-tools not found)'
    $sha = $null
}

Write-Host ''
Write-Host "Maia versionCode $Version, $RuntimeIdentifier" -ForegroundColor Green
if ($AllowDraftTemplates) {
    Write-Host 'Templates  DRAFT allowed: personal build for your own phone. Do not distribute this APK.' -ForegroundColor Yellow
}
Write-Host "Signed by  $signer"
if ($sha) { Write-Host "Cert SHA-256  $($sha.Matches[0].Groups[1].Value)" }
Write-Host ("APK        {0} ({1:N1} MB)" -f $apk.FullName, ($apk.Length / 1MB))

if (-not $Install) {
    Write-Host 'Install    run again with -Install, or copy the APK to the phone and open it.'
    return
}

# adb reports failures on stderr; in Windows PowerShell that must not become a terminating error.
$adb = Join-Path $sdk 'platform-tools\adb.exe'
$ErrorActionPreference = 'Continue'
$result = (& $adb install -r $apk.FullName 2>&1 | ForEach-Object { "$_" }) -join [Environment]::NewLine
$exitCode = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
Write-Host $result

if ($exitCode -ne 0) {
    if ($result -match 'INSTALL_FAILED_UPDATE_INCOMPATIBLE') {
        Write-Warning ('The Maia on the phone is signed with a different key. Switch once, in this order: ' +
            'Settings > Backup in Maia and save the file off the phone; uninstall Maia; install this APK; ' +
            'choose "Restore a backup" at setup step 1. Uninstalling deletes the plan, so back up first.')
    }
    elseif ($result -match 'INSTALL_FAILED_VERSION_DOWNGRADE') {
        Write-Warning "The phone has a newer versionCode than $Version. Pass a higher -Version."
    }
    elseif ($result -match 'no devices|device offline|unauthorized') {
        Write-Warning 'No phone ready: plug it in, turn on USB debugging and accept the prompt on the phone.'
    }
    throw 'adb install failed.'
}
