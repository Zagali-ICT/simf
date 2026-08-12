# =============================================================================
# SIMF - deploy ONE package to THIS server (called by the Deploy stage).
#
# Each package now runs on its own box, so each deployment job extracts only its
# own zip and hands only its own site to iis-deploy.ps1. The single job that
# extracted all four zips and deployed them together assumed one machine; on a
# per-server estate it would have had each agent unpack three packages it must
# never install.
#
#   .\deploy\pipeline-deploy-one.ps1 `
#       -Package edge -ZipName SIMF.MobileEdge.zip `
#       -SiteName SIMF.EDGE -SitePath D:\System\v1.0.1\edge `
#       -Drop $(Pipeline.Workspace)\drop -Root $(Pipeline.Workspace)\drop_root
#
# It is a thin wrapper on purpose: the stop / robocopy / start sequence lives in
# iis-deploy.ps1 and is not duplicated here.
# =============================================================================

param (
    # api | cp | web | edge. Names the artifact sub-folder, and selects which
    # -<App>SiteName/-<App>Path pair iis-deploy.ps1 receives.
    [Parameter(Mandatory = $true)]
    [ValidateSet('api', 'cp', 'web', 'edge')]
    [string]$Package,

    [Parameter(Mandatory = $true)] [string]$ZipName,
    [Parameter(Mandatory = $true)] [string]$SiteName,
    [Parameter(Mandatory = $true)] [string]$SitePath,

    # The downloaded artifact, and the directory the extracted output goes to.
    [Parameter(Mandatory = $true)] [string]$Drop,
    [Parameter(Mandatory = $true)] [string]$Root
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$zipPath = Join-Path (Join-Path $Drop $Package) $ZipName
if (!(Test-Path $zipPath)) {
    throw ("$Package zip not found: $zipPath. The Build stage publishes one zip " +
           "per package into the drop; a missing one means the build did not " +
           "produce this package rather than that the server is misconfigured.")
}

$outDir = Join-Path $Root $Package

# Extracted fresh every run: Expand-Archive -Force overwrites files but does not
# remove ones the new build dropped, and a stale DLL left behind is exactly the
# kind of thing that runs fine until it does not.
Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "Extracting $Package : $zipPath -> $outDir"
Expand-Archive -Path $zipPath -DestinationPath $outDir -Force

$fileCount = @(Get-ChildItem $outDir -Recurse -File).Count
Write-Host "$Package extracted: $fileCount file(s)."
if ($fileCount -eq 0) {
    throw "$zipPath extracted to nothing. Refusing to mirror an empty folder over a live site."
}

# Only this package's pair is passed. Every other pair defaults to empty, so
# iis-deploy.ps1 skips it and never touches a site this server does not host.
$deployArgs = @{
    ArtifactRoot = $Root
    "$($Package.Substring(0,1).ToUpperInvariant() + $Package.Substring(1))SiteName" = $SiteName
    "$($Package.Substring(0,1).ToUpperInvariant() + $Package.Substring(1))Path"     = $SitePath
}

Write-Host "Deploying $Package to site '$SiteName' at $SitePath"
& (Join-Path $PSScriptRoot 'iis-deploy.ps1') @deployArgs
