# =============================================================================
# SIMF - settings for the Flutter web app, and the build that bakes them in.
#
# This was deploy/app-web/publish-app-web.ps1 (D-376). It is a set-env script in
# the same family as the other four, with one difference that matters: the other
# four write Machine-scope environment variables that a host reads at startup,
# while these values are COMPILED INTO the bundle by `flutter build web`. There
# is no runtime config for a browser bundle, so changing any value here means
# re-running this script.
#
# Run it from any directory:
#   .\deploy\set-env-webapp.ps1
#
# Output: a folder ready to be copied to the IIS site's physical path, holding
# the build output plus webapp-web.config.
# =============================================================================

$ErrorActionPreference = 'Stop'

# Secret: $true  => never commit, never print.
# Gate  : $true  => the build refuses to run without it.
$vars = @(

    # --- The API the bundle calls --------------------------------------------
    # THE EDGE, NOT THE API. This bundle runs in a browser and the API is not
    # published to the internet: api.simrsnf.com resolves inside the estate only.
    # The edge publishes /api/v1/app/**, which is the whole surface this build
    # calls. Pointed at api.simrsnf.com it would compile cleanly, deploy cleanly,
    # and then fail to resolve for every visitor.
    [pscustomobject]@{ Name = "ApiBase"; Value = "https://edge.simrsnf.com/api/v1"; Secret = $false; Gate = $true; Note = "compiled in; must be https and end with /api/v1" }

    # --- Where the assembled bundle lands ------------------------------------
    [pscustomobject]@{ Name = "OutDir"; Value = "D:\System\v1.0.1\appweb"; Secret = $false; Gate = $true; Note = "point the IIS site's physical path here" }
    [pscustomobject]@{ Name = "FlutterBat"; Value = "D:\dev\flutter\bin\flutter.bat"; Secret = $false; Gate = $false; Note = "falls back to flutter on PATH when missing" }

    # --- Optional app settings -----------------------------------------------
    # Listed rather than hidden: an invisible knob is how a build acquires
    # settings nobody can account for.
    #
    # The eight marked Optional are empty on purpose and have been empty since
    # D-369/D-378 shipped - the old publish-app-web.ps1 carried the same eight
    # blanks with placeholder examples, and no real value for any of them exists
    # anywhere in this repository. They are CONTENT, not configuration: the
    # forum's published phone number, support mailbox and official social
    # accounts. Empty is a designed state, not a gap - the tile or button is
    # rendered INERT rather than launching a dead tel:/mailto:/https: intent.
    # Supply them here when the client confirms them, and rebuild.
    #
    # AppKey is different again: no endpoint in this build requires an X-App-Key
    # header (SimfPublicClient), so a value here changes nothing today. It stays
    # declared because the app still compiles one in.
    [pscustomobject]@{ Name = "AppKey"; Value = ""; Secret = $true; Gate = $false; Optional = $true; Note = "no endpoint requires X-App-Key in this build" }
    [pscustomobject]@{ Name = "SupportPhone"; Value = ""; Secret = $false; Gate = $false; Optional = $true; Note = "CONTENT - the tile is inert until the client supplies it" }
    [pscustomobject]@{ Name = "SupportEmail"; Value = ""; Secret = $false; Gate = $false; Optional = $true; Note = "CONTENT - the tile is inert until the client supplies it" }

    # Home page social links (D-378). Empty keeps that button inert.
    [pscustomobject]@{ Name = "SocialX"; Value = ""; Secret = $false; Gate = $false; Optional = $true; Note = "CONTENT - empty keeps the button inert" }
    [pscustomobject]@{ Name = "SocialInstagram"; Value = ""; Secret = $false; Gate = $false; Optional = $true; Note = "CONTENT - empty keeps the button inert" }
    [pscustomobject]@{ Name = "SocialLinkedIn"; Value = ""; Secret = $false; Gate = $false; Optional = $true; Note = "CONTENT - empty keeps the button inert" }
    [pscustomobject]@{ Name = "SocialYouTube"; Value = ""; Secret = $false; Gate = $false; Optional = $true; Note = "CONTENT - empty keeps the button inert" }
    [pscustomobject]@{ Name = "SocialTikTok"; Value = ""; Secret = $false; Gate = $false; Optional = $true; Note = "CONTENT - empty keeps the button inert" }

    # The only one of the group with a knowable value: the app's own compiled-in
    # default, stated here so the build does not depend on a fallback buried in
    # BuildConfig.
    [pscustomobject]@{ Name = "VisitSaudiUrl"; Value = "https://www.visitsaudi.com"; Secret = $false; Gate = $false; Note = "the Visit Saudi discover link on the home screen" }
)

$cfg = @{}
foreach ($v in $vars) { $cfg[$v.Name] = $v.Value }

$missingGates = @($vars | Where-Object { $_.Gate -and [string]::IsNullOrWhiteSpace($_.Value) } |
    ForEach-Object { $_.Name })
if ($missingGates.Count -gt 0) {
    throw ("Required value(s) empty: {0}. Fill them in the `$vars block above." -f ($missingGates -join ", "))
}

# --- Validate the compiled-in API base ---------------------------------------
# This value cannot be corrected after the build: there is no runtime config for
# a browser bundle, so a wrong base ships as an app that calls the wrong host and
# only fails in someone's browser. Both mistakes are caught here.
if ($cfg.ApiBase -notmatch '^https://') {
    throw "ApiBase must be https (got '$($cfg.ApiBase)'). The app is served over HTTPS, so a cleartext API base is blocked as mixed content by the browser."
}
if ($cfg.ApiBase -notmatch '/api/v1/?$') {
    throw "ApiBase must end with the /api/v1 route prefix (got '$($cfg.ApiBase)'). The API mounts every endpoint under it, so a bare origin builds an app whose every call 404s."
}

# Flutter is not always at the configured path; fall back to PATH before failing.
$flutterBat = $cfg.FlutterBat
if (-not (Test-Path $flutterBat)) {
    $onPath = (Get-Command flutter -ErrorAction SilentlyContinue).Source
    if ($onPath) {
        Write-Host "flutter not at $flutterBat - using $onPath" -ForegroundColor DarkGray
        $flutterBat = $onPath
    }
    else {
        throw "Flutter SDK not found at '$flutterBat' and 'flutter' is not on PATH. Set FlutterBat above to the full path to flutter.bat."
    }
}

Write-Host ""
Write-Host "  ApiBase : $($cfg.ApiBase)   (COMPILED IN - rebuild to change)" -ForegroundColor Cyan
Write-Host "  OutDir  : $($cfg.OutDir)" -ForegroundColor Cyan
Write-Host ""

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$appDir = Join-Path $repoRoot 'src\Mobile\simf_app'
if (-not (Test-Path (Join-Path $appDir 'pubspec.yaml'))) {
    throw "simf_app not found under $appDir"
}

$defines = @(
    "--dart-define=SIMF_BUILD=prod",
    "--dart-define=SIMF_API_BASE_WEB=$($cfg.ApiBase)"
)
if ($cfg.AppKey)          { $defines += "--dart-define=SIMF_APP_KEY=$($cfg.AppKey)" }
if ($cfg.SupportPhone)    { $defines += "--dart-define=SIMF_SUPPORT_PHONE=$($cfg.SupportPhone)" }
if ($cfg.SupportEmail)    { $defines += "--dart-define=SIMF_SUPPORT_EMAIL=$($cfg.SupportEmail)" }
if ($cfg.SocialX)         { $defines += "--dart-define=SIMF_SOCIAL_X=$($cfg.SocialX)" }
if ($cfg.SocialInstagram) { $defines += "--dart-define=SIMF_SOCIAL_INSTAGRAM=$($cfg.SocialInstagram)" }
if ($cfg.SocialLinkedIn)  { $defines += "--dart-define=SIMF_SOCIAL_LINKEDIN=$($cfg.SocialLinkedIn)" }
if ($cfg.SocialYouTube)   { $defines += "--dart-define=SIMF_SOCIAL_YOUTUBE=$($cfg.SocialYouTube)" }
if ($cfg.SocialTikTok)    { $defines += "--dart-define=SIMF_SOCIAL_TIKTOK=$($cfg.SocialTikTok)" }
if ($cfg.VisitSaudiUrl)   { $defines += "--dart-define=SIMF_VISIT_SAUDI_URL=$($cfg.VisitSaudiUrl)" }

Write-Host "Building simf_app web (release) with API base $($cfg.ApiBase) ..."
Push-Location $appDir
try {
    & $flutterBat build web --release @defines
    if ($LASTEXITCODE -ne 0) { throw "flutter build web failed ($LASTEXITCODE)" }
}
finally {
    Pop-Location
}

$buildOut = Join-Path $appDir 'build\web'
if (-not (Test-Path (Join-Path $buildOut 'index.html'))) {
    throw "Build output missing at $buildOut"
}

Write-Host "Assembling deploy folder $($cfg.OutDir) ..."
if (Test-Path $cfg.OutDir) { Remove-Item $cfg.OutDir -Recurse -Force }
New-Item -ItemType Directory -Force $cfg.OutDir | Out-Null
Copy-Item (Join-Path $buildOut '*') $cfg.OutDir -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'webapp-web.config') (Join-Path $cfg.OutDir 'web.config') -Force

Write-Host "Done. Point the IIS site's physical path at: $($cfg.OutDir)"
Write-Host "API base compiled in: $($cfg.ApiBase)"
