#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Fixes the SIMF app-web (Flutter web) "HTTP 500 on every path" failure on IIS.

.DESCRIPTION
    ROOT CAUSE
    ----------
    deploy/app-web/web.config contains a <rewrite> reverse-proxy rule
    (/api/* -> https://localhost:12340) so the browser only ever talks to this
    origin and never sees the API's self-signed cert. That <rewrite> section
    REQUIRES two IIS out-of-band modules:
        * URL Rewrite
        * Application Request Routing (ARR)  -- depends on URL Rewrite
    If either is missing, IIS cannot parse the <rewrite> section at startup and
    returns HTTP 500 for EVERY request -- including non-existent paths and even
    /web.config (which would normally 404). That "500 on every path" signature is
    how you recognise this specific failure.

    WHAT THIS SCRIPT DOES (run ON the prod IIS host, e.g. WIN-MAP9VAMAU4Q, elevated)
    -------------------------------------------------------------------------------
        1. Detects whether URL Rewrite + ARR are installed and whether the ARR
           proxy is enabled.
        2. Installs any missing module from the official Microsoft installer
           (URL Rewrite FIRST, then ARR).
        3. Backs up applicationHost.config, then enables the ARR proxy.
        4. Restarts IIS (brief interruption to ALL sites on the box) and verifies
           the app-web site returns HTTP 200.

    Idempotent: anything already in place is skipped, so it is safe to re-run.

.PARAMETER SiteUrl
    Public URL verified at the end. Default: https://web.simrsnf.com/

.PARAMETER Force
    Skip the interactive confirmation prompt (for unattended runs).

.PARAMETER DryRun
    Detect and report only. Make no changes.

.NOTES
    Installer links below were confirmed against the official Microsoft pages on
    2026-07-25. If a link 404s, download manually from the landing pages printed
    by the script and re-run (the script skips already-installed modules).
        URL Rewrite : https://www.iis.net/downloads/microsoft/url-rewrite
        ARR         : https://www.iis.net/downloads/microsoft/application-request-routing
#>
param(
    [string] $SiteUrl = 'https://web.simrsnf.com/',
    [switch] $Force,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

# --- Official installers (verified 2026-07-25) --------------------------------
$RewriteMsiUrl = 'https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi'
$ArrMsiUrl     = 'https://download.microsoft.com/download/e/9/8/e9849d6a-020e-47e4-9fd0-a023e99b54eb/requestRouter_amd64.msi'
$RewritePage   = 'https://www.iis.net/downloads/microsoft/url-rewrite'
$ArrPage       = 'https://www.iis.net/downloads/microsoft/application-request-routing'

function Write-Step([string] $m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Ok  ([string] $m) { Write-Host "    OK  $m" -ForegroundColor Green }
function Write-Warn2([string] $m) { Write-Host "    !!  $m" -ForegroundColor Yellow }

Import-Module WebAdministration -ErrorAction Stop

function Test-GlobalModule([string] $name) {
    return [bool] (Get-WebGlobalModule | Where-Object { $_.Name -eq $name })
}

function Get-ArrProxyEnabled {
    try {
        $v = Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
                 -filter 'system.webServer/proxy' -name 'enabled' -ErrorAction Stop
        return [bool] $v.Value
    } catch {
        return $false
    }
}

function Install-Msi([string] $url, [string] $fileName, [string] $label, [string] $landingPage) {
    $dest = Join-Path $env:TEMP $fileName
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Write-Host "    downloading $label ..."
        Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
    } catch {
        throw "Could not download $label from $url`n" +
              "    Download it manually from $landingPage, install it, then re-run this script.`n" +
              "    ($($_.Exception.Message))"
    }
    Write-Host "    installing $label ..."
    $p = Start-Process msiexec.exe -ArgumentList "/i `"$dest`" /qn /norestart" -Wait -PassThru
    Remove-Item $dest -ErrorAction SilentlyContinue
    if ($p.ExitCode -ne 0 -and $p.ExitCode -ne 3010) {
        throw "$label install failed (msiexec exit code $($p.ExitCode))."
    }
    Write-Ok "$label installed."
}

function Get-HttpStatus([string] $url) {
    # curl.exe ships with Windows Server 2019+ ; -k tolerates the self-signed cert.
    $code = & curl.exe -k -s -o NUL -w "%{http_code}" $url 2>$null
    return $code
}

# --- 1. Detect ---------------------------------------------------------------
Write-Step "Detecting current state"
$rewriteInstalled = Test-GlobalModule 'RewriteModule'
$arrInstalled     = Test-GlobalModule 'ApplicationRequestRouting'
$proxyEnabled     = if ($arrInstalled) { Get-ArrProxyEnabled } else { $false }

Write-Host ("    URL Rewrite installed : {0}" -f $rewriteInstalled)
Write-Host ("    ARR installed         : {0}" -f $arrInstalled)
Write-Host ("    ARR proxy enabled     : {0}" -f $proxyEnabled)
$statusBefore = Get-HttpStatus $SiteUrl
Write-Host ("    {0} currently returns : HTTP {1}" -f $SiteUrl, $statusBefore)

$needsRewrite = -not $rewriteInstalled
$needsArr     = -not $arrInstalled
$needsProxy   = -not $proxyEnabled

if (-not ($needsRewrite -or $needsArr -or $needsProxy)) {
    Write-Ok "URL Rewrite + ARR are installed and the proxy is enabled -- nothing to change."
    if ($statusBefore -eq '200') { Write-Ok "Site already returns HTTP 200. Done." }
    else { Write-Warn2 "Modules look fine but the site returns HTTP $statusBefore -- investigate the app pool / site binding / IIS logs." }
    return
}

Write-Host ""
Write-Step "Planned actions"
if ($needsRewrite) { Write-Host "    - install URL Rewrite" }
if ($needsArr)     { Write-Host "    - install Application Request Routing (ARR)" }
if ($needsProxy)   { Write-Host "    - enable ARR proxy (applicationHost.config)" }
Write-Host "    - back up applicationHost.config, then iisreset (briefly restarts ALL IIS sites)"

if ($DryRun) { Write-Warn2 "DryRun: no changes made."; return }

if (-not $Force) {
    $ans = Read-Host "Proceed on THIS server? (type YES to continue)"
    if ($ans -ne 'YES') { Write-Warn2 "Aborted by operator."; return }
}

# --- 2. Install missing modules (URL Rewrite before ARR) ---------------------
if ($needsRewrite) {
    Write-Step "Installing URL Rewrite"
    Install-Msi $RewriteMsiUrl 'rewrite_amd64_en-US.msi' 'URL Rewrite 2.1' $RewritePage
}
if ($needsArr) {
    Write-Step "Installing Application Request Routing (ARR)"
    Install-Msi $ArrMsiUrl 'requestRouter_amd64.msi' 'ARR 3.0' $ArrPage
}

# --- 3. Back up config, then enable the ARR proxy ----------------------------
Write-Step "Backing up applicationHost.config"
$backupName = "simf-pre-arr-fix-{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss')
Backup-WebConfiguration -Name $backupName
Write-Ok "Backup created: $backupName  (restore with: Restore-WebConfiguration -Name '$backupName')"

Write-Step "Enabling ARR proxy"
Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter 'system.webServer/proxy' -name 'enabled' -value $true
Write-Ok "ARR proxy enabled."

# --- 4. Restart IIS and verify ----------------------------------------------
Write-Step "Restarting IIS (iisreset) -- brief interruption to all sites"
iisreset | Out-Host

Start-Sleep -Seconds 3
Write-Step "Verifying"
$rewriteInstalled = Test-GlobalModule 'RewriteModule'
$arrInstalled     = Test-GlobalModule 'ApplicationRequestRouting'
$proxyEnabled     = Get-ArrProxyEnabled
$statusAfter      = Get-HttpStatus $SiteUrl

Write-Host ("    URL Rewrite installed : {0}" -f $rewriteInstalled)
Write-Host ("    ARR installed         : {0}" -f $arrInstalled)
Write-Host ("    ARR proxy enabled     : {0}" -f $proxyEnabled)
Write-Host ("    {0} now returns       : HTTP {1}" -f $SiteUrl, $statusAfter)

Write-Host ""
if ($statusAfter -eq '200') {
    Write-Ok "FIXED -- the app-web site now returns HTTP 200."
    Write-Host "    Next: run the sign-in smoke test in docs/tests/e2e/mobile-sign-in.md"
    Write-Host "    (watch the network tab: /api/* calls should proxy to the API and return 2xx)."
} else {
    Write-Warn2 "Site still returns HTTP $statusAfter."
    Write-Warn2 "If 502 on /api/*: ARR is validating the API's self-signed cert (target 'localhost'"
    Write-Warn2 "does not match the cert CN). Trust that cert in Local Machine > Trusted Root, or edit"
    Write-Warn2 "web.config's proxy target to the cert-CN host. Otherwise check the app pool + IIS logs."
    exit 1
}
