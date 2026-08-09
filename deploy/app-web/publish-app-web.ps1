# SIMF — publish the Flutter web app for IIS (D-376).
#
# Builds simf_app for web in release/prod mode with the API base baked in,
# then assembles a deployable folder (build output + web.config) ready to be
# copied to the IIS site's physical path.
#
# Usage (PowerShell, any directory) - both now default to the production estate,
# so it runs with no arguments:
#   .\publish-app-web.ps1
#   .\publish-app-web.ps1 `
#       -ApiBase  "https://api.simrsnf.com/api/v1" `
#       -OutDir   "D:\SIMF\System\V1.0.0\publish" `
#       [-AppKey  "<prod app key>"] `
#       [-SupportPhone "+9665XXXXXXXX"] [-SupportEmail "support@..."] `
#       [-SocialX "https://x.com/..."] [-SocialInstagram "..."] `
#       [-SocialLinkedIn "..."] [-SocialYouTube "..."] [-SocialTikTok "..."] `
#       [-VisitSaudiUrl "https://www.visitsaudi.com"] `
#       [-FlutterBat "D:\dev\flutter\bin\flutter.bat"]
#
# Notes:
# - The API base is COMPILED IN (--dart-define); changing it means re-running
#   this script — there is no runtime config file.
# - If the IIS site's origin differs from the API host, set the API's
#   Cors:WebAppOrigins to the site origin (see docs/deploy/SIMF-AppWeb-IIS-Deploy.md).

param(
    # Defaulted to the production estate (D-868) rather than prompted. It is
    # COMPILED INTO the bundle, so it was mandatory to stop anyone shipping a
    # build pointed at the wrong host - but a prompt is a poor way to enforce
    # that: it stops the script being runnable from CI and teaches people to
    # type past it. The check below enforces the same thing and also catches the
    # mistake a prompt never could, a base missing the /api/v1 route prefix.
    [string] $ApiBase = 'https://api.simrsnf.com/api/v1',
    [string] $OutDir  = 'D:\SIMF\System\V1.0.0\publish',
    [string] $AppKey = '',
    [string] $SupportPhone = '',
    [string] $SupportEmail = '',
    # Home "تابعنا" social links + the روح السعودية URL (D-378) — empty keeps
    # that button inert; VisitSaudiUrl falls back to the in-app default.
    [string] $SocialX = '',
    [string] $SocialInstagram = '',
    [string] $SocialLinkedIn = '',
    [string] $SocialYouTube = '',
    [string] $SocialTikTok = '',
    [string] $VisitSaudiUrl = '',
    [string] $FlutterBat = 'D:\dev\flutter\bin\flutter.bat'
)

$ErrorActionPreference = 'Stop'

# --- validate the compiled-in API base ---------------------------------------
# This value cannot be corrected after the build: there is no runtime config for
# a browser bundle, so a wrong base ships as an app that calls the wrong host and
# only fails in someone's browser. Both mistakes are caught here.
if ($ApiBase -notmatch '^https://') {
    throw "-ApiBase must be https (got '$ApiBase'). The app is served over HTTPS, so a cleartext API base is blocked as mixed content by the browser."
}
if ($ApiBase -notmatch '/api/v1/?$') {
    throw "-ApiBase must end with the /api/v1 route prefix (got '$ApiBase'). The API mounts every endpoint under it, so a bare origin builds an app whose every call 404s."
}

# Flutter is not always at the vendored path; fall back to PATH before failing.
if (-not (Test-Path $FlutterBat)) {
    $onPath = (Get-Command flutter -ErrorAction SilentlyContinue).Source
    if ($onPath) {
        Write-Host "flutter not at $FlutterBat - using $onPath" -ForegroundColor DarkGray
        $FlutterBat = $onPath
    }
    else {
        throw "Flutter SDK not found at '$FlutterBat' and 'flutter' is not on PATH. Pass -FlutterBat with the full path to flutter.bat."
    }
}

Write-Host ""
Write-Host "  ApiBase : $ApiBase   (COMPILED IN - rebuild to change)" -ForegroundColor Cyan
Write-Host "  OutDir  : $OutDir" -ForegroundColor Cyan
Write-Host ""

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$appDir = Join-Path $repoRoot 'src\Mobile\simf_app'
if (-not (Test-Path (Join-Path $appDir 'pubspec.yaml'))) {
    throw "simf_app not found under $appDir"
}

$defines = @(
    "--dart-define=SIMF_BUILD=prod",
    "--dart-define=SIMF_API_BASE_WEB=$ApiBase"
)
if ($AppKey)       { $defines += "--dart-define=SIMF_APP_KEY=$AppKey" }
if ($SupportPhone) { $defines += "--dart-define=SIMF_SUPPORT_PHONE=$SupportPhone" }
if ($SupportEmail) { $defines += "--dart-define=SIMF_SUPPORT_EMAIL=$SupportEmail" }
if ($SocialX)         { $defines += "--dart-define=SIMF_SOCIAL_X=$SocialX" }
if ($SocialInstagram) { $defines += "--dart-define=SIMF_SOCIAL_INSTAGRAM=$SocialInstagram" }
if ($SocialLinkedIn)  { $defines += "--dart-define=SIMF_SOCIAL_LINKEDIN=$SocialLinkedIn" }
if ($SocialYouTube)   { $defines += "--dart-define=SIMF_SOCIAL_YOUTUBE=$SocialYouTube" }
if ($SocialTikTok)    { $defines += "--dart-define=SIMF_SOCIAL_TIKTOK=$SocialTikTok" }
if ($VisitSaudiUrl)   { $defines += "--dart-define=SIMF_VISIT_SAUDI_URL=$VisitSaudiUrl" }

Write-Host "Building simf_app web (release) with API base $ApiBase ..."
Push-Location $appDir
try {
    & $FlutterBat build web --release @defines
    if ($LASTEXITCODE -ne 0) { throw "flutter build web failed ($LASTEXITCODE)" }
}
finally {
    Pop-Location
}

$buildOut = Join-Path $appDir 'build\web'
if (-not (Test-Path (Join-Path $buildOut 'index.html'))) {
    throw "Build output missing at $buildOut"
}

Write-Host "Assembling deploy folder $OutDir ..."
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force $OutDir | Out-Null
Copy-Item (Join-Path $buildOut '*') $OutDir -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'web.config') $OutDir -Force

Write-Host "Done. Point the IIS site's physical path at: $OutDir"
Write-Host "API base compiled in: $ApiBase"
