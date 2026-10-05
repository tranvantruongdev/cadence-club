# One-time release setup for this game repo. Run it yourself in a terminal: it asks for passwords,
# so no tool or agent ever sees them.
#   powershell -ExecutionPolicy Bypass -File Tools/setup-release-secrets.ps1
#
# Steps (each one asks first, so you can skip any):
#   1. Android signing keystore, created OUTSIDE the repo in %USERPROFILE%\.keystores\
#      -> GitHub secrets ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME, ANDROID_KEYALIAS_PASS
#   2. Unity login for CI builds (Personal licence: the CI signs in with your account)
#      -> GitHub secrets UNITY_EMAIL, UNITY_PASSWORD
#   3. itch.io (needs an itch.io account and an API key from itch.io > Settings > API keys)
#      -> GitHub secret BUTLER_API_KEY, variables ITCH_USER, ITCH_GAME
#
# Values go to GitHub through stdin, never on a command line. Needs gh logged in (gh auth status).
#
# Typed once, reused for every game: the Unity login, the itch.io key and username, and each game's
# keystore password are kept in %USERPROFILE%\.release-secrets\, encrypted with Windows DPAPI (only
# your Windows account on this machine can read them). -Forget deletes that store first.
param(
    [string]$Repo,
    [switch]$Forget
)

$ErrorActionPreference = "Stop"
$OutputEncoding = New-Object System.Text.UTF8Encoding $false # PS 5.1 pipes ASCII to native tools by default
$project = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $project

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "GitHub CLI (gh) not found. Install it: winget install GitHub.cli" }
if (-not $Repo) { $Repo = (gh repo view --json nameWithOwner -q .nameWithOwner).Trim() }
$game = ($Repo -split "/")[1].ToLowerInvariant()
Write-Host "Repository: $Repo" -ForegroundColor Cyan

function Ask([string]$question) {
    $answer = Read-Host "$question [y/N]"
    return $answer -match "^(y|yes)$"
}

function Plain([System.Security.SecureString]$secure) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

function ReadSecret([string]$prompt, [int]$minLength = 1, [switch]$Confirm) {
    while ($true) {
        $first = Plain (Read-Host $prompt -AsSecureString)
        if ($first.Length -lt $minLength) { Write-Host "  At least $minLength characters, please." -ForegroundColor Yellow; continue }
        if (-not $Confirm) { return $first }
        $second = Plain (Read-Host "Type it again" -AsSecureString)
        if ($first -ceq $second) { return $first }
        Write-Host "  They don't match. Try again." -ForegroundColor Yellow
    }
}

$store = Join-Path $env:USERPROFILE ".release-secrets"
if ($Forget -and (Test-Path -LiteralPath $store)) { Remove-Item -LiteralPath $store -Recurse -Force; Write-Host "Saved values deleted." }

# Returns the saved value for $name, or runs $prompt once and saves its answer (DPAPI via Export-Clixml).
function Remembered([string]$name, [scriptblock]$prompt) {
    $file = Join-Path $store "$name.xml"
    if (Test-Path -LiteralPath $file) {
        Write-Host "  using saved $name" -ForegroundColor DarkGray
        return (Import-Clixml -LiteralPath $file).GetNetworkCredential().Password
    }
    $value = & $prompt
    New-Item -ItemType Directory -Force $store | Out-Null
    New-Object System.Management.Automation.PSCredential($name, (ConvertTo-SecureString $value -AsPlainText -Force)) |
        Export-Clixml -LiteralPath $file
    return $value
}

function SetSecret([string]$name, [string]$value) {
    $value | gh secret set $name --repo $Repo
    if ($LASTEXITCODE -ne 0) { throw "gh secret set $name failed" }
    Write-Host "  secret $name set"
}

# --- 1. Android keystore -------------------------------------------------------------------------
if (Ask "1/3  Create the Android signing keystore and upload it to GitHub secrets?") {
    $keytool = Get-ChildItem "C:\Program Files\Unity\Hub\Editor\*\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" -ErrorAction SilentlyContinue | Select-Object -Last 1
    if (-not $keytool) { throw "keytool not found. Install Android Build Support (with OpenJDK) in Unity Hub." }

    $folder = Join-Path $env:USERPROFILE ".keystores"
    New-Item -ItemType Directory -Force $folder | Out-Null
    $keystore = Join-Path $folder "$game.keystore"
    $alias = $game

    if (Test-Path -LiteralPath $keystore) {
        Write-Host "  $keystore already exists; using it (it is never overwritten)." -ForegroundColor Yellow
        $password = Remembered "keystore-$game" { ReadSecret "Keystore password" }
    } else {
        Write-Host "  The keystore signs every update of the game forever. Lose it or its password and you can"
        Write-Host "  never update the app on Google Play. Save the password in your password manager now."
        $password = ReadSecret "New keystore password (12+ characters)" 12 -Confirm
        $env:TW_KEYSTORE_PASS = $password
        try {
            & $keytool.FullName -genkeypair -v -keystore $keystore -alias $alias -keyalg RSA -keysize 2048 -validity 10000 `
                -dname "CN=Tran Van Truong, C=VN" -storepass:env TW_KEYSTORE_PASS -keypass:env TW_KEYSTORE_PASS | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "keytool failed" }
        } finally {
            Remove-Item Env:TW_KEYSTORE_PASS -ErrorAction SilentlyContinue
        }
        Write-Host "  Created $keystore (alias $alias)" -ForegroundColor Green
        $null = Remembered "keystore-$game" { $password }
    }

    SetSecret "ANDROID_KEYSTORE_BASE64" ([Convert]::ToBase64String([IO.File]::ReadAllBytes($keystore)))
    SetSecret "ANDROID_KEYSTORE_PASS" $password
    SetSecret "ANDROID_KEYALIAS_NAME" $alias
    SetSecret "ANDROID_KEYALIAS_PASS" $password # PKCS12 keystores use one password for store and key
    $password = $null
    Write-Host "  BACK UP $keystore (e.g. to your cloud drive) together with the password." -ForegroundColor Yellow
}

# --- 2. Unity login for CI -----------------------------------------------------------------------
# CI signs in with the account (GameCI's `personal` method). No .ulf: Unity removed offline activation
# for Personal seats, and the Hub's Unity_lic.ulf fails on the runner ("TimeStamp validation failed").
if (Ask "2/3  Upload your Unity login (email + password) for CI builds?") {
    SetSecret "UNITY_EMAIL" (Remembered "unity-email" { Read-Host "Unity account email" })
    SetSecret "UNITY_PASSWORD" (Remembered "unity-password" { ReadSecret "Unity account password" })
    Write-Host "  If your Unity account uses two-factor sign-in, CI activation can fail; see https://game.ci/docs/github/activation" -ForegroundColor Yellow
}

# --- 3. itch.io ----------------------------------------------------------------------------------
if (Ask "3/3  Set up itch.io uploads (needs an itch.io account and API key)?") {
    SetSecret "BUTLER_API_KEY" (Remembered "itch-api-key" { ReadSecret "itch.io API key" })
    $user = Remembered "itch-user" { Read-Host "itch.io username (the part before .itch.io)" }
    $itchGame = Read-Host "itch.io game slug [$game]"
    if (-not $itchGame) { $itchGame = $game }
    gh variable set ITCH_USER --repo $Repo --body $user | Out-Null
    gh variable set ITCH_GAME --repo $Repo --body $itchGame | Out-Null
    Write-Host "  variables ITCH_USER=$user ITCH_GAME=$itchGame set"
    Write-Host "  Create the game page on itch.io first (Upload new project, slug $itchGame), as a draft." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done. Secrets now on GitHub:" -ForegroundColor Cyan
gh secret list --repo $Repo
Write-Host "Release: git tag v0.1.0 ; git push origin v0.1.0   (CI builds APK, AAB, Windows zip and publishes them)"
