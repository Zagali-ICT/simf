# =============================================================================
# SIMF - CLEAR environment variables  (safe to commit - contains NO secrets)
#
# Removes the Machine-scope SIMF_* SECRETS the set-env-* scripts create (DB creds,
# JWT / encryption keys, super-admin password, AI API keys, prompt-hash, etc.).
#
# By DEFAULT it PRESERVES the non-secret, CP/Website-facing shared config so a
# clear does not knock the Control Panel / Website offline. The one that matters
# most is Api__BaseUrl - without it the CP falls back to http://localhost:5175
# and shows "The SIMF service could not be reached". Use -Full to wipe everything
# (a true scrub / decommission), which also drops those shared values.
#
# Machine scope is shared by every app on the box. On a dedicated server that is
# exactly one package, so clearing the whole SIMF_* namespace is the right
# default. On a box that still runs several packages - the single-server estate
# this deployment is moving away from - use -Target to clear only one package's
# variables and leave the others running.
#
#   Scrub secrets, keep CP reachable:  .\deploy\clear-env.ps1
#   Only the edge's variables:            .\deploy\clear-env.ps1 -Target Edge
#   Wipe EVERYTHING (incl. Api BaseUrl): .\deploy\clear-env.ps1 -Full
#   Also drop the host env:               .\deploy\clear-env.ps1 -IncludeAspNetEnv
#   Then restart the IIS app pools (or the server) so w3wp drops the values.
# =============================================================================

#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    # Which package's variables to clear. 'All' (the default) clears every SIMF_*
    # on the box, which is correct on a dedicated server and is what this script
    # has always done. Naming a package reads that package's template for the set
    # of names it declares and clears only those, so an operator on a shared or
    # transitional box cannot knock the other packages over.
    #
    # ASPNETCORE_ENVIRONMENT is never scoped by this: it is host-level and shared
    # by every app on the box, so it stays behind -IncludeAspNetEnv alone.
    [ValidateSet('All', 'Api', 'Cp', 'Web', 'Edge')]
    [string]$Target = 'All',

    # Wipe EVERY SIMF_* variable, including the non-secret shared config that is
    # normally preserved (Api__BaseUrl, CORS origins, AI routing). Use for a
    # full scrub / decommission - it WILL take the CP + Website offline until
    # set-env is run again.
    [switch]$Full,

    # Also remove ASPNETCORE_ENVIRONMENT (host-level, shared). Without it the host
    # falls back to its default (Production). Off by default so a clear doesn't
    # silently flip the environment.
    [switch]$IncludeAspNetEnv
)

$ErrorActionPreference = "Stop"

# Non-secret, CP/Website-facing shared config that is KEPT unless -Full is given.
# These are public values (URLs / origins / model id), never credentials.
#
# Listed UN-prefixed and matched against the part after the application prefix,
# because each application now reads its own (SIMF_API_, SIMF_CP_, SIMF_WEB_,
# SIMF_EDGE_). Naming SIMF_CP_Api__BaseUrl here would preserve the Control
# Panel's copy and silently wipe the Website's.
$preserveExactSuffix = @(
    "Api__BaseUrl"                     # CP + Website -> API endpoint
    "Ai__DefaultProvider"              # non-secret AI routing
    "Ai__Anthropic__DefaultModel"      # non-secret model id
)

# RETIRED variables: ALWAYS removed - without -Full, and even though one of them
# used to be on the preserve list above.
#
# SIMF_Api__AllowSelfSignedCertificate installed a blanket
# accept-any-certificate handler on the CP/Website API clients, and was deleted
# on 2026-08-08. No application reads it any more, so leaving it set changes
# nothing today - but a box provisioned before that date still carries the
# value, and a stale security toggle sitting in the machine environment is
# exactly what gets "restored" by someone debugging a TLS error later. Removing
# it here is the supported way to clean an already-provisioned server.
# Matched on the suffix, so a pre-split SIMF_Api__AllowSelfSignedCertificate and
# a per-application SIMF_CP_Api__AllowSelfSignedCertificate are both removed.
$retiredSuffix = @(
    "Api__AllowSelfSignedCertificate"
)
$preservePrefixSuffix = @(
    "Cors__WebAppOrigins__"              # public web-app origin(s)
)

# The four live application prefixes, longest-first so SIMF_API_ is stripped
# before the bare SIMF_ of a pre-split variable.
$applicationPrefixes = @('SIMF_EDGE_', 'SIMF_WEB_', 'SIMF_API_', 'SIMF_CP_')

function Get-KeySuffix([string]$name) {
    foreach ($prefix in $applicationPrefixes) {
        if ($name.StartsWith($prefix, [StringComparison]::Ordinal)) {
            return $name.Substring($prefix.Length)
        }
    }
    # A pre-split SIMF_ variable: strip the bare prefix so the preserve list
    # still recognises it on a server that has not been re-provisioned yet.
    if ($name.StartsWith('SIMF_', [StringComparison]::Ordinal)) {
        return $name.Substring('SIMF_'.Length)
    }
    return $name
}

function Test-Preserved([string]$name) {
    $suffix = Get-KeySuffix $name
    if ($preserveExactSuffix -contains $suffix) { return $true }
    foreach ($p in $preservePrefixSuffix) { if ($suffix -like ($p + "*")) { return $true } }
    return $false
}

$mode = if ($Full) { "FULL wipe (incl. shared config)" } else { "secrets only (shared config kept)" }
$scope = if ($Target -eq 'All') { "every SIMF_* on this box" } else { "the $Target package only" }
Write-Host ("=== SIMF clear env (Machine scope) - {0}, {1} ===" -f $mode, $scope) -ForegroundColor Cyan

# Enumerate the live Machine environment and pick our whole namespace.
$machineVars = [Environment]::GetEnvironmentVariables([EnvironmentVariableTarget]::Machine)
$simfNames   = @($machineVars.Keys | Where-Object { $_ -like 'SIMF_*' } | Sort-Object)

# Scoped to one package: intersect the live namespace with the names that
# package's template declares. Read by regex rather than by dot-sourcing - the
# template carries #Requires and an apply loop, so running it to inspect it
# would SET the variables this script exists to remove.
if ($Target -ne 'All') {
    $templateName = "set-env-$($Target.ToLowerInvariant()).template.ps1"
    $templatePath = Join-Path $PSScriptRoot $templateName
    if (-not (Test-Path $templatePath)) {
        throw "Cannot scope to '$Target': $templateName was not found beside this script."
    }
    $declared = [regex]::Matches(
        (Get-Content -Raw -LiteralPath $templatePath),
        'Name\s*=\s*"(?<name>[^"]+)"') | ForEach-Object { $_.Groups['name'].Value }

    $before = $simfNames.Count
    $simfNames = @($simfNames | Where-Object { $declared -contains $_ })
    Write-Host ("  scoped to {0}: {1} of {2} live SIMF_* variable(s) belong to this package." `
        -f $templateName, $simfNames.Count, $before) -ForegroundColor DarkGray
}

$removed = 0
$kept    = 0
if ($simfNames.Count -eq 0) {
    Write-Host "  No SIMF_* Machine variables found - nothing to clear." -ForegroundColor DarkGray
} else {
    foreach ($name in $simfNames) {
        if ($retiredSuffix -contains (Get-KeySuffix $name)) {
            [Environment]::SetEnvironmentVariable($name, $null, [EnvironmentVariableTarget]::Machine)
            Write-Host ("  removed {0} (RETIRED - no application reads it)" -f $name) -ForegroundColor Yellow
            $removed++
            continue
        }
        if (-not $Full -and (Test-Preserved $name)) {
            Write-Host ("  keep    {0} (non-secret shared config)" -f $name) -ForegroundColor DarkCyan
            $kept++
            continue
        }
        # Setting a variable to $null deletes it from the Machine environment.
        [Environment]::SetEnvironmentVariable($name, $null, [EnvironmentVariableTarget]::Machine)
        Write-Host ("  removed {0}" -f $name) -ForegroundColor Yellow
        $removed++
    }
}

if ($IncludeAspNetEnv) {
    $hostEnv = [Environment]::GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", [EnvironmentVariableTarget]::Machine)
    if (-not [string]::IsNullOrWhiteSpace($hostEnv)) {
        [Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", $null, [EnvironmentVariableTarget]::Machine)
        Write-Host "  removed ASPNETCORE_ENVIRONMENT (host reverts to its default = Production)" -ForegroundColor Yellow
        $removed++
    } else {
        Write-Host "  ASPNETCORE_ENVIRONMENT was not set at Machine scope - skipped." -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host ("{0} variable(s) removed, {1} kept." -f $removed, $kept) -ForegroundColor Green
if (-not $Full -and $kept -gt 0) {
    Write-Host "Kept the non-secret shared config (Api__BaseUrl etc., under each application prefix) so the CP + Website stay reachable." -ForegroundColor Green
    Write-Host "Use -Full to wipe those too." -ForegroundColor DarkGray
}
Write-Host "Restart the IIS app pools (or the server) so w3wp drops the cleared values." -ForegroundColor Green
if ($Target -eq 'All') {
    Write-Host "Re-provision with the template for this server:  .\deploy\set-env-{api|cp|web|edge}.ps1" -ForegroundColor Green
} else {
    Write-Host ("Re-provision with:  .\deploy\set-env-{0}.ps1" -f $Target.ToLowerInvariant()) -ForegroundColor Green
}
