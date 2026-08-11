# =============================================================================
# SIMF - IIS deploy script (called by the Deploy stage of azure-pipelines.yml)
#
# Mirrors the V10 ERP iis-deploy.ps1, generalised from two sites to the three
# SIMF web apps (API + Control Panel + Website). For each app it: stops the IIS
# site and its app pool, releases file locks by killing the worker process,
# mirrors the published files with robocopy (retried), then restarts the pool
# and the site.
#
# Artifact layout expected under -ArtifactRoot (produced by the Extract step):
#   <ArtifactRoot>\api   <- SimfAPI published output
#   <ArtifactRoot>\cp    <- SimfCP  published output
#   <ArtifactRoot>\web   <- SimfWeb published output
# =============================================================================

param (
    [Parameter(Mandatory = $true)] [string]$ArtifactRoot,

    [Parameter(Mandatory = $true)] [string]$ApiSiteName,
    [Parameter(Mandatory = $true)] [string]$ApiPath,

    [Parameter(Mandatory = $true)] [string]$CpSiteName,
    [Parameter(Mandatory = $true)] [string]$CpPath,

    [Parameter(Mandatory = $true)] [string]$WebSiteName,
    [Parameter(Mandatory = $true)] [string]$WebPath,

    # The mobile edge. Optional on purpose: it exists only where the tiers have
    # been separated, so an estate without it must still deploy.
    [string]$EdgeSiteName = "",
    [string]$EdgePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module WebAdministration

function Ensure-Directory([string]$Path) {
    if (!(Test-Path $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
    }
}

function Stop-IisSiteIfExists([string]$SiteName) {
    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
    if ($null -ne $site) {
        Write-Host "Stopping IIS site: $SiteName"
        Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
    }
    else {
        Write-Warning "IIS site not found (skip stop): $SiteName"
    }
}

function Start-IisSiteIfExists([string]$SiteName) {
    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
    if ($null -ne $site) {
        Write-Host "Starting IIS site: $SiteName"
        Start-Website -Name $SiteName -ErrorAction SilentlyContinue
    }
    else {
        Write-Warning "IIS site not found (skip start): $SiteName"
    }
}

function Get-RootAppPoolName([string]$SiteName) {
    try {
        return (Get-Item "IIS:\Sites\$SiteName").applications["/"].applicationPool
    }
    catch {
        return $null
    }
}

function Stop-AppPoolIfExists([string]$AppPoolName) {
    if ([string]::IsNullOrWhiteSpace($AppPoolName)) { return }

    $pool = Get-Item "IIS:\AppPools\$AppPoolName" -ErrorAction SilentlyContinue
    if ($null -ne $pool) {
        Write-Host "Stopping AppPool: $AppPoolName"
        Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    }
    else {
        Write-Warning "AppPool not found (skip stop): $AppPoolName"
    }
}

function Start-AppPoolIfExists([string]$AppPoolName) {
    if ([string]::IsNullOrWhiteSpace($AppPoolName)) { return }

    $pool = Get-Item "IIS:\AppPools\$AppPoolName" -ErrorAction SilentlyContinue
    if ($null -ne $pool) {
        Write-Host "Starting AppPool: $AppPoolName"
        Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    }
    else {
        Write-Warning "AppPool not found (skip start): $AppPoolName"
    }
}

function Try-KillW3wpForAppPool([string]$AppPoolName) {
    if ([string]::IsNullOrWhiteSpace($AppPoolName)) { return }

    # Best-effort: locate worker processes for this app pool and terminate them
    # so file locks are released before the robocopy mirror.
    try {
        $workers = Get-CimInstance -Namespace "root\WebAdministration" -ClassName "WorkerProcess" -ErrorAction Stop
        foreach ($w in $workers) {
            if ($w.AppPoolName -eq $AppPoolName) {
                Write-Warning "Killing w3wp PID $($w.ProcessId) for AppPool '$AppPoolName' (release file locks)"
                Stop-Process -Id $w.ProcessId -Force -ErrorAction SilentlyContinue
            }
        }
    }
    catch {
        Write-Warning "Could not query WorkerProcess via WMI (skip killing w3wp). Reason: $($_.Exception.Message)"
    }
}

function Resolve-ArtifactFolder([string]$Root, [string]$FolderName) {
    $direct = Join-Path $Root $FolderName
    if (Test-Path $direct) { return $direct }

    $found = Get-ChildItem $Root -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ieq $FolderName } |
        Select-Object -First 1

    if ($null -eq $found) {
        throw "Missing '$FolderName' folder. Not found at '$direct' and not found anywhere under '$Root'."
    }

    return $found.FullName
}

function Invoke-RobocopyMirror([string]$From, [string]$To) {
    if (!(Test-Path $From)) { throw "Source path does not exist: $From" }
    Ensure-Directory $To

    $args = @(
        "`"$From`"",
        "`"$To`"",
        "/MIR",
        "/R:10",
        "/W:3",
        "/NP",
        "/NFL",
        "/NDL"
    )

    Write-Host "Robocopy: $From -> $To"
    $p = Start-Process -FilePath "robocopy.exe" -ArgumentList $args -Wait -PassThru -NoNewWindow
    return $p.ExitCode
}

function Robocopy-MirrorWithRetry([string]$From, [string]$To, [int]$MaxAttempts = 3) {
    for ($i = 1; $i -le $MaxAttempts; $i++) {
        $code = Invoke-RobocopyMirror -From $From -To $To

        # robocopy exit codes 0-7 indicate success (8+ means at least one failure)
        if ($code -lt 8) {
            Write-Host "Robocopy succeeded with exit code $code"
            return
        }

        Write-Warning "Robocopy attempt $i/$MaxAttempts failed with exit code $code."
        if ($i -lt $MaxAttempts) {
            Write-Host "Waiting 5 seconds before retry..."
            Start-Sleep -Seconds 5
        }
        else {
            throw "Robocopy failed after $MaxAttempts attempts. Last exit code: $code"
        }
    }
}

# The SIMF apps, in deploy order.
$apps = @(
    @{ Name = "API"; Site = $ApiSiteName; Path = $ApiPath; Folder = "api" },
    @{ Name = "CP";  Site = $CpSiteName;  Path = $CpPath;  Folder = "cp"  },
    @{ Name = "WEB"; Site = $WebSiteName; Path = $WebPath; Folder = "web" }
)

# The mobile edge is OPTIONAL, and deliberately not a mandatory parameter: it
# exists only on an estate whose tiers have been separated, and making it
# required would break the deployment of every estate that has not been. Pass
# -EdgeSiteName and -EdgePath once the site exists and it joins the same
# stop, copy, start sequence as the other three.
if ($EdgeSiteName -and $EdgePath) {
    $apps += @{ Name = "EDGE"; Site = $EdgeSiteName; Path = $EdgePath; Folder = "edge" }
}
else {
    Write-Host "EDGE skipped (no -EdgeSiteName/-EdgePath supplied)."
}

Write-Host "=== SIMF IIS Deploy ==="
Write-Host "ArtifactRoot: $ArtifactRoot"
if (!(Test-Path $ArtifactRoot)) { throw "ArtifactRoot does not exist: $ArtifactRoot" }

# Resolve each app's artifact folder and root app pool up front.
foreach ($app in $apps) {
    $app.Artifact = Resolve-ArtifactFolder -Root $ArtifactRoot -FolderName $app.Folder
    $app.Pool     = Get-RootAppPoolName $app.Site
    Write-Host ("{0,-3} site={1} pool={2} artifact={3} -> {4}" -f $app.Name, $app.Site, $app.Pool, $app.Artifact, $app.Path)
}

# Stop every site and app pool, then release file locks.
foreach ($app in $apps) { Stop-IisSiteIfExists $app.Site }
foreach ($app in $apps) { Stop-AppPoolIfExists $app.Pool }
foreach ($app in $apps) { Try-KillW3wpForAppPool $app.Pool }

Start-Sleep -Seconds 2

# Mirror the published files for each app.
foreach ($app in $apps) {
    Write-Host "Deploying $($app.Name)..."
    Robocopy-MirrorWithRetry -From $app.Artifact -To $app.Path -MaxAttempts 3
}

# Start app pools, then sites.
foreach ($app in $apps) { Start-AppPoolIfExists $app.Pool }
foreach ($app in $apps) { Start-IisSiteIfExists $app.Site }

Write-Host "Deployment completed successfully."
