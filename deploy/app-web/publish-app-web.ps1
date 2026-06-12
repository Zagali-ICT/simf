# SIMF — publish the Flutter web app for IIS (D-376).
#
# Builds simf_app for web in release/prod mode with the API base baked in,
# then assembles a deployable folder (build output + web.config) ready to be
# copied to the IIS site's physical path.
#
# Usage (PowerShell, any directory):
#   .\publish-app-web.ps1 `
#       -ApiBase  "https://simf_api.zagali-ict.com/api/v1" `
#       -OutDir   "D:\SIMF\Publish\simf-app-web" `
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
    [Parameter(Mandatory = $true)] [string] $ApiBase,
    [Parameter(Mandatory = $true)] [string] $OutDir,
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
