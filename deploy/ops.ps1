# =============================================================================
# SIMF - unified operations script for the on-site IIS deployment.
#
# One entry point to install, remove, and control the four SIMF web apps
# (API + Control Panel + Website + Mobile Edge) as IIS sites and application
# pools, plus the background-worker tier.
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
#   .\ops.ps1 -Action Install -Target All -ApiHost api-int.simrsnf.local
#   .\ops.ps1 -Action Install -Target Edge -ApiHost api-int.simrsnf.local
#   .\ops.ps1 -Action Stop -Target Web
#
# Actions: Status | Start | Stop | Restart | Install | Uninstall
# Targets: All (default) | Api | Cp | Web | Edge | Workers
# =============================================================================

#Requires -RunAsAdministrator

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [ValidateSet('Status', 'Start', 'Stop', 'Restart', 'Install', 'Uninstall')]
    [string]$Action,

    [ValidateSet('All', 'Api', 'Cp', 'Web', 'Edge', 'Workers')]
    [string]$Target = 'All',

    # IIS site names + physical paths. Defaults match deploy/README.md; override
    # per server if the sites were created with other names or paths.
    [string]$ApiSiteName = 'SIMF.API',
    [string]$ApiPath     = 'D:\System\v1.0.1\api',
    [string]$CpSiteName  = 'SIMF.CP',
    [string]$CpPath      = 'D:\System\v1.0.1\cp',
    [string]$WebSiteName = 'SIMF.WEB',
    [string]$WebPath     = 'D:\System\v1.0.1\web',
    [string]$EdgeSiteName = 'SIMF.EDGE',
    [string]$EdgePath     = 'D:\System\v1.0.1\edge',

    # Install only: the HTTP port for a newly created site. Ignored otherwise.
    [int]$ApiPort  = 12340,
    [int]$CpPort   = 12341,
    [int]$WebPort  = 12342,
    [int]$EdgePort = 12343,

    # Public host names. Install adds an SNI HTTPS binding for each when
    # -CertThumbprint is supplied. These are the names the certificate must
    # cover: every certificate bypass was removed on 2026-08-08, so the CP and
    # Website will not start if the API's certificate does not validate.
    [string]$ApiHost = 'api.simrsnf.com',
    [string]$CpHost  = 'cp.simrsnf.com',
    [string]$WebHost = 'web.simrsnf.com',

    # The mobile edge TAKES OVER api.simrsnf.com. That is the point of it: the
    # installed Flutter app compiles its base URL in, so the public name has to
    # stay the same or every user needs a store release. At that cutover the API
    # moves to a PRIVATE name and stops being published, so -ApiHost above must
    # be overridden with that private name on the API's own server.
    [string]$EdgeHost = 'api.simrsnf.com',

    # Thumbprint of a certificate in Cert:\LocalMachine\My. Without it Install
    # creates the sites on HTTP only and says so, rather than pretending TLS is
    # configured.
    [string]$CertThumbprint = '',

    # Uploads + logs. Granted to the API pool ONLY - it holds ID documents, and
    # the API's authorization is the only thing meant to gate them.
    [string]$StoragePath = 'C:\SIMF\Storage',

    # Run the pools as a SPECIFIC account instead of ApplicationPoolIdentity.
    # Needed when the content sits on a UNC share, when drive ACLs are managed
    # centrally and cannot grant a virtual account, or when the app must reach a
    # domain resource - a virtual account leaves the machine as MACHINE$ and gets
    # refused. Format: 'DOMAIN\user' or '.\localuser'.
    #
    # Use a DEDICATED service account, not a member of Administrators. This pool
    # runs the internet-facing API, so its identity is what an attacker inherits
    # from any code-execution bug; an admin identity turns that into full control
    # of the server, the encryption keys in the machine environment and the
    # certificate's private key. The ACL grants below give a plain account
    # everything the app needs.
    [string]$PoolIdentity = '',

    # SecureString, not [string]: a plain parameter puts a service-account
    # password into PowerShell history and into any transcript the operator has
    # running. Pass it as (Read-Host -AsSecureString) or from a credential store.
    [securestring]$PoolPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

# The two go together. Half a pair is the failure this pass exists to prevent:
# an operator passes an account, sees no error, and believes the pool runs as it
# while IIS silently keeps ApplicationPoolIdentity.
if (-not [string]::IsNullOrWhiteSpace($PoolIdentity) -and -not $PoolPassword) {
    throw "-PoolIdentity was given without -PoolPassword. Pass both, e.g. -PoolPassword (Read-Host -AsSecureString), or neither to use ApplicationPoolIdentity."
}
if ([string]::IsNullOrWhiteSpace($PoolIdentity) -and $PoolPassword) {
    throw "-PoolPassword was given without -PoolIdentity. There is no account to apply it to."
}
# A LITERAL check, not a regex: a lone backslash in a pattern is a trailing
# escape, so `-notmatch '\\'` is a parse error rather than a test.
if ($PoolIdentity -and -not $PoolIdentity.Contains('\')) {
    throw "-PoolIdentity must be qualified: 'DOMAIN\user' for a domain account or '.\user' for a local one (got '$PoolIdentity')."
}

# --- the app table -----------------------------------------------------------
# Each app is one IIS site + its application pool (the ASP.NET Core Module hosts
# the .NET app in-process). The API pool additionally runs the background workers.
$apps = [ordered]@{
    Api  = @{ Site = $ApiSiteName;  Path = $ApiPath;  Port = $ApiPort;  Pool = $ApiSiteName;  Host = $ApiHost;  Storage = $true  }
    Cp   = @{ Site = $CpSiteName;   Path = $CpPath;   Port = $CpPort;   Pool = $CpSiteName;   Host = $CpHost;   Storage = $false }
    Web  = @{ Site = $WebSiteName;  Path = $WebPath;  Port = $WebPort;  Pool = $WebSiteName;  Host = $WebHost;  Storage = $false }
    # Storage = $false: the edge proxies, it never touches the file store. It
    # still gets the log grant below, which every app receives.
    Edge = @{ Site = $EdgeSiteName; Path = $EdgePath; Port = $EdgePort; Pool = $EdgeSiteName; Host = $EdgeHost; Storage = $false }
}

# The edge exists to take api.simrsnf.com OFF the API, so the two sharing a
# hostname means the cutover was half-applied. On one box that is an SNI
# collision IIS resolves by luck; across two it is a DNS record pointing at
# whichever answers first. Neither fails loudly on its own.
#
# Only checked on Install, and only when the edge is in scope: hostnames are
# used to create SNI bindings and nothing else, so Status/Start/Stop on a
# single-box estate that has no edge must not trip over the shared default.
if ($Action -eq 'Install' -and $Target -in @('All', 'Edge') -and $ApiHost -eq $EdgeHost) {
    throw ("-ApiHost and -EdgeHost are both '$ApiHost'. The edge takes the public " +
           "name; pass the API's PRIVATE name as -ApiHost (e.g. api-int.simrsnf.local).")
}

function Resolve-Targets([string]$requested) {
    switch ($requested) {
        'All'     { return @('Api', 'Cp', 'Web', 'Edge') }
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

# The ASP.NET Core Module is what actually starts a .NET app under IIS. Without
# the Hosting Bundle every site returns 500.19 and nothing else in this script
# matters, so it is checked once, up front, rather than discovered per site.
function Test-HostingBundle {
    $module = Get-WebGlobalModule | Where-Object Name -like 'AspNetCoreModule*'
    if ($module) {
        Write-Host ("  module ok {0}" -f ($module.Name -join ', ')) -ForegroundColor DarkGray
        return
    }
    Write-Warning "AspNetCoreModuleV2 is NOT installed. Every SIMF site will return 500.19."
    Write-Warning "Install the ASP.NET Core Hosting Bundle for .NET 10, then run 'iisreset'."
}

# The Windows account a pool runs as, in the form icacls and IIS both accept.
#
# ApplicationPoolIdentity is a VIRTUAL account: it exists only as
# "IIS AppPool\<pool>" and cannot be granted rights on a UNC share or reach a
# domain resource (it presents as MACHINE$). -PoolIdentity switches to a real
# account for exactly those cases, and every ACL below must then target THAT
# account rather than the virtual one - granting the virtual account rights a
# custom identity never uses is the silent half of this bug.
function Get-PoolPrincipal([hashtable]$app) {
    if ([string]::IsNullOrWhiteSpace($PoolIdentity)) {
        return "IIS AppPool\$($app.Pool)"
    }
    return $PoolIdentity
}

# Applies -PoolIdentity to a pool. Idempotent: re-running Install on a box whose
# pool already carries the account leaves it alone rather than re-writing the
# password.
function Set-PoolIdentity([hashtable]$app) {
    if ([string]::IsNullOrWhiteSpace($PoolIdentity)) { return }

    $poolPath = "IIS:\AppPools\$($app.Pool)"
    $current = (Get-ItemProperty $poolPath -Name processModel.userName).Value
    if ($current -eq $PoolIdentity) {
        Write-Host ("  ident ok {0} -> {1} (already set)" -f $app.Pool, $PoolIdentity) -ForegroundColor DarkGray
        return
    }

    # SpecificUser = 3. The password is converted here and not held anywhere:
    # IIS stores it encrypted in applicationHost.config.
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringUni(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PoolPassword))
    try {
        Set-ItemProperty $poolPath -Name processModel.identityType -Value 3
        Set-ItemProperty $poolPath -Name processModel.userName -Value $PoolIdentity
        Set-ItemProperty $poolPath -Name processModel.password -Value $plain
    }
    finally {
        $plain = $null
    }
    Write-Host ("  ident +  {0} -> {1}" -f $app.Pool, $PoolIdentity) -ForegroundColor Green
}

# Least privilege, and idempotent so re-running Install repairs a drifted box.
#   site folder -> Read+Execute for its OWN pool identity
#   storage     -> Modify for the API pool only
function Grant-AppAccess([hashtable]$app) {
    $identity = Get-PoolPrincipal $app

    & icacls $app.Path /grant "${identity}:(OI)(CI)RX" /T /Q | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning ("icacls failed on {0}" -f $app.Path) }
    else { Write-Host ("  acl  ok {0} -> {1} (RX)" -f $app.Path, $identity) -ForegroundColor DarkGray }

    # LOGS: all four apps write here. SIMF_Storage__LogDirectory is tagged
    # "API CP Web", and Serilog opens its sink during startup - so a pool that
    # cannot write to it does not log an error, it FAILS TO START. That presents
    # as a 500 under IIS while the same build runs fine from a console, because
    # the console runs as you and w3wp runs as the pool identity.
    $logPath = Join-Path $StoragePath 'logs'
    if (-not (Test-Path $logPath)) { New-Item -ItemType Directory -Force -Path $logPath | Out-Null }
    & icacls $logPath /grant "${identity}:(OI)(CI)M" /T /Q | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning ("icacls failed on {0}" -f $logPath) }
    else { Write-Host ("  acl  ok {0} -> {1} (Modify)" -f $logPath, $identity) -ForegroundColor DarkGray }

    # FILES: uploads and ID documents. API only - the API's authorization is the
    # only thing meant to gate them, so the CP and Website pools stay out.
    if (-not $app.Storage) { return }
    $filePath = Join-Path $StoragePath 'files'
    if (-not (Test-Path $filePath)) { New-Item -ItemType Directory -Force -Path $filePath | Out-Null }
    & icacls $filePath /grant "${identity}:(OI)(CI)M" /T /Q | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning ("icacls failed on {0}" -f $filePath) }
    else { Write-Host ("  acl  ok {0} -> {1} (Modify)" -f $filePath, $identity) -ForegroundColor DarkGray }
}

# SNI, because three host names share port 443 on one box.
function Add-HttpsBinding([hashtable]$app) {
    if ([string]::IsNullOrWhiteSpace($CertThumbprint)) {
        Write-Host ("  https -- {0} skipped (no -CertThumbprint); site is HTTP only" -f $app.Host) -ForegroundColor Yellow
        return
    }
    $cert = Get-Item "Cert:\LocalMachine\My\$CertThumbprint" -ErrorAction SilentlyContinue
    if (-not $cert) {
        Write-Warning ("certificate {0} not found in Cert:\LocalMachine\My - HTTPS not bound for {1}" -f $CertThumbprint, $app.Host)
        return
    }

    $existing = Get-WebBinding -Name $app.Site -Protocol https -ErrorAction SilentlyContinue |
                Where-Object { $_.bindingInformation -like "*:443:$($app.Host)" }
    if (-not $existing) {
        New-WebBinding -Name $app.Site -Protocol https -Port 443 -HostHeader $app.Host -SslFlags 1
        Write-Host ("  https +  {0}:443 -> {1}" -f $app.Host, $app.Site) -ForegroundColor Green
    }
    else {
        Write-Host ("  https ok {0}:443 (exists)" -f $app.Host) -ForegroundColor DarkGray
    }

    $sslPath = "IIS:\SslBindings\!443!$($app.Host)"
    if (-not (Test-Path $sslPath)) { $cert | New-Item $sslPath -SslFlags 1 | Out-Null }
    Write-Host ("  cert ok  {0} -> {1}" -f $app.Host, $CertThumbprint.Substring(0, 8)) -ForegroundColor DarkGray
}

function Install-App([hashtable]$app) {
    if (-not (Test-Path $app.Path)) {
        New-Item -ItemType Directory -Force -Path $app.Path | Out-Null
        Write-Host ("  mkdir   {0}" -f $app.Path) -ForegroundColor DarkGray
    }
    if (-not (Test-Path "IIS:\AppPools\$($app.Pool)")) {
        New-WebAppPool -Name $app.Pool | Out-Null
        Write-Host ("  pool +  {0}" -f $app.Pool) -ForegroundColor Green
    }
    else {
        Write-Host ("  pool ok {0} (exists)" -f $app.Pool) -ForegroundColor DarkGray
    }

    # ALWAYS, not just on create. A pool made by hand (or by an older run of this
    # script) defaults to v4.0, which loads the .NET Framework CLR into a process
    # that is meant to host the ASP.NET Core Module - the app never starts and the
    # error does not say why. Enforcing it every run repairs those pools.
    $runtime = (Get-ItemProperty "IIS:\AppPools\$($app.Pool)" -Name managedRuntimeVersion).Value
    if ($runtime -ne '') {
        Set-ItemProperty "IIS:\AppPools\$($app.Pool)" -Name managedRuntimeVersion -Value ''
        Write-Host ("  pool !  {0} runtime '{1}' -> No Managed Code" -f $app.Pool, $runtime) -ForegroundColor Yellow
    }

    if ((Get-SiteState $app.Site) -eq 'absent') {
        New-Website -Name $app.Site -PhysicalPath $app.Path -ApplicationPool $app.Pool -Port $app.Port | Out-Null
        Write-Host ("  site +  {0} on port {1} -> {2}" -f $app.Site, $app.Port, $app.Path) -ForegroundColor Green
    }
    else {
        Write-Host ("  site ok {0} (exists)" -f $app.Site) -ForegroundColor DarkGray
    }

    # Identity FIRST: Grant-AppAccess grants to whatever the pool runs as, so
    # setting it afterwards would leave the ACLs pointing at the virtual account
    # the pool no longer uses.
    Set-PoolIdentity $app
    Grant-AppAccess $app
    Add-HttpsBinding $app

    # An empty folder is the commonest cause of a 403 on a fresh box: with no
    # web.config IIS has no app to start, falls back to directory browsing and
    # denies it. Say so here rather than leaving it to be diagnosed from a
    # browser error page.
    if (-not (Test-Path (Join-Path $app.Path 'web.config'))) {
        Write-Warning ("{0} has no web.config - nothing is deployed to {1} yet." -f $app.Site, $app.Path)
        Write-Warning "  Until iis-deploy.ps1 copies the published output there, this site returns 403."
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
        'Install'   { if (-not $script:bundleChecked) { Test-HostingBundle; $script:bundleChecked = $true }; Install-App $app }
        'Uninstall' { Uninstall-App $app }
    }
}

Write-Host "Done." -ForegroundColor Cyan
