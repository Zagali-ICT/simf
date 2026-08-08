# =============================================================================
# SIMF - unified operations script for the on-site IIS deployment.
#
# One entry point to install, remove, and control the three SIMF web apps
# (API + Control Panel + Website) as IIS sites and application pools, plus the
# background-worker tier.
#
# The 10 background workers (session reminders, seat-hold expiry, rating
# prompts, the daily NCA sweeps, the email queue drain, and so on) run
# IN-PROCESS inside the SIMF API application pool. There is no separate worker
# service yet, so -Target Workers maps to the API pool: stopping the workers
# means stopping the API pool, and their live health is on the Control Panel
# "Background services" page and the /health "workers" check. The Workers block
# below is isolated so that, when the workers move to a dedicated Windows
# Service (planned), only that block changes.
#
# Usage (run AS ADMINISTRATOR):
#   .\ops.ps1 -Action Status
#   .\ops.ps1 -Action Restart -Target Api
#   .\ops.ps1 -Action Restart -Target Workers
#   .\ops.ps1 -Action Install -Target All
#   .\ops.ps1 -Action Stop -Target Web
#
# Actions: Status | Start | Stop | Restart | Install | Uninstall
# Targets: All (default) | Api | Cp | Web | Workers
# =============================================================================

#Requires -RunAsAdministrator

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [ValidateSet('Status', 'Start', 'Stop', 'Restart', 'Install', 'Uninstall')]
    [string]$Action,

    [ValidateSet('All', 'Api', 'Cp', 'Web', 'Workers')]
    [string]$Target = 'All',

    # IIS site names + physical paths. Defaults match deploy/README.md; override
    # per server if the sites were created with other names or paths.
    [string]$ApiSiteName = 'SIMF.API',
    [string]$ApiPath     = 'D:\System\v1.0.1\api',
    [string]$CpSiteName  = 'SIMF.CP',
    [string]$CpPath      = 'D:\System\v1.0.1\cp',
    [string]$WebSiteName = 'SIMF.WEB',
    [string]$WebPath     = 'D:\System\v1.0.1\web',

    # Install only: the HTTP port + optional host header for a newly created site.
    # TLS bindings and the CA certificate are configured separately (see the HLD /
    # SIMF-OPS-001). Ignored for the other actions.
    [int]$ApiPort = 12340,
    [int]$CpPort  = 12341,
    [int]$WebPort = 12342
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

# --- the app table -----------------------------------------------------------
# Each app is one IIS site + its application pool (the ASP.NET Core Module hosts
# the .NET app in-process). The API pool additionally runs the background workers.
$apps = [ordered]@{
    Api = @{ Site = $ApiSiteName; Path = $ApiPath; Port = $ApiPort; Pool = $ApiSiteName }
    Cp  = @{ Site = $CpSiteName;  Path = $CpPath;  Port = $CpPort;  Pool = $CpSiteName  }
    Web = @{ Site = $WebSiteName; Path = $WebPath; Port = $WebPort; Pool = $WebSiteName }
}

function Resolve-Targets([string]$requested) {
    switch ($requested) {
        'All'     { return @('Api', 'Cp', 'Web') }
        'Workers' { return @('Api') }   # workers run inside the API pool
        default   { return @($requested) }
    }
}

function Resolve-Pool([hashtable]$app) {
    # Prefer the site's actual application pool so control still works when the
    # pool is not named after the site; fall back to the site name (Install
    # creates pool == site, and the site does not exist yet at Install time).
    try {
        $pool = (Get-Item "IIS:\Sites\$($app.Site)" -ErrorAction Stop).applications["/"].applicationPool
        if (-not [string]::IsNullOrWhiteSpace($pool)) { return $pool }
    }
    catch { }
    return $app.Pool
}

function Get-SiteState([string]$site) {
    $s = Get-Website -Name $site -ErrorAction SilentlyContinue
    if ($null -eq $s) { return 'absent' }
    return $s.State
}

function Get-PoolState([string]$pool) {
    if (-not (Test-Path "IIS:\AppPools\$pool")) { return 'absent' }
    return (Get-WebAppPoolState -Name $pool).Value
}

function Start-App([hashtable]$app) {
    if ((Get-PoolState $app.Pool) -ne 'absent') { Start-WebAppPool -Name $app.Pool -ErrorAction SilentlyContinue }
    if ((Get-SiteState $app.Site) -ne 'absent') { Start-Website -Name $app.Site -ErrorAction SilentlyContinue }
    Write-Host ("  started  site={0} pool={1}" -f $app.Site, $app.Pool) -ForegroundColor Green
}

function Stop-App([hashtable]$app) {
    if ((Get-SiteState $app.Site) -ne 'absent') { Stop-Website -Name $app.Site -ErrorAction SilentlyContinue }
    if ((Get-PoolState $app.Pool) -ne 'absent') { Stop-WebAppPool -Name $app.Pool -ErrorAction SilentlyContinue }
    Write-Host ("  stopped  site={0} pool={1}" -f $app.Site, $app.Pool) -ForegroundColor Yellow
}

function Restart-App([hashtable]$app) {
    Stop-App $app
    Start-Sleep -Seconds 2
    Start-App $app
}

function Install-App([hashtable]$app) {
    if (-not (Test-Path $app.Path)) {
        New-Item -ItemType Directory -Force -Path $app.Path | Out-Null
        Write-Host ("  mkdir   {0}" -f $app.Path) -ForegroundColor DarkGray
    }
    if (-not (Test-Path "IIS:\AppPools\$($app.Pool)")) {
        New-WebAppPool -Name $app.Pool | Out-Null
        # No Managed Code: the ASP.NET Core Module hosts the .NET runtime itself.
        Set-ItemProperty "IIS:\AppPools\$($app.Pool)" -Name managedRuntimeVersion -Value ''
        Write-Host ("  pool +  {0}" -f $app.Pool) -ForegroundColor Green
    }
    else {
        Write-Host ("  pool ok {0} (exists)" -f $app.Pool) -ForegroundColor DarkGray
    }
    if ((Get-SiteState $app.Site) -eq 'absent') {
        New-Website -Name $app.Site -PhysicalPath $app.Path -ApplicationPool $app.Pool -Port $app.Port | Out-Null
        Write-Host ("  site +  {0} on port {1} -> {2}" -f $app.Site, $app.Port, $app.Path) -ForegroundColor Green
        Write-Host ("          add the HTTPS binding + CA certificate separately (see the HLD / SIMF-OPS-001)." ) -ForegroundColor DarkGray
    }
    else {
        Write-Host ("  site ok {0} (exists)" -f $app.Site) -ForegroundColor DarkGray
    }
}

function Uninstall-App([hashtable]$app) {
    if ((Get-SiteState $app.Site) -ne 'absent') {
        Stop-Website -Name $app.Site -ErrorAction SilentlyContinue
        Remove-Website -Name $app.Site
        Write-Host ("  site -  {0}" -f $app.Site) -ForegroundColor Yellow
    }
    if (Test-Path "IIS:\AppPools\$($app.Pool)") {
        Stop-WebAppPool -Name $app.Pool -ErrorAction SilentlyContinue
        Remove-WebAppPool -Name $app.Pool
        Write-Host ("  pool -  {0}" -f $app.Pool) -ForegroundColor Yellow
    }
}

function Show-Status([hashtable]$app, [string]$key) {
    $siteState = Get-SiteState $app.Site
    $poolState = Get-PoolState $app.Pool
    $note = if ($key -eq 'Api') { '  (hosts the background workers)' } else { '' }
    Write-Host ("  {0,-4} site={1,-9} [{2,-7}]  pool={3,-9} [{4,-7}]{5}" -f `
        $key, $app.Site, $siteState, $app.Pool, $poolState, $note)
}

# --- dispatch ----------------------------------------------------------------
$targets = Resolve-Targets $Target

Write-Host ("=== SIMF ops : {0} -> {1} ===" -f $Action, $Target) -ForegroundColor Cyan
if ($Target -eq 'Workers') {
    Write-Host "  Workers run in-process in the API pool; acting on the API app." -ForegroundColor DarkGray
}

foreach ($key in $targets) {
    $app = $apps[$key]
    $app.Pool = Resolve-Pool $app
    switch ($Action) {
        'Status'    { Show-Status $app $key }
        'Start'     { Start-App $app }
        'Stop'      { Stop-App $app }
        'Restart'   { Restart-App $app }
        'Install'   { Install-App $app }
        'Uninstall' { Uninstall-App $app }
    }
}

Write-Host "Done." -ForegroundColor Cyan
