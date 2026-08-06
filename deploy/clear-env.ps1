# =============================================================================
# SIMF - CLEAR environment variables  (safe to commit - contains NO secrets)
#
# Removes the Machine-scope SIMF_* SECRETS the set-env-* scripts create (DB creds,
# JWT / encryption keys, super-admin password, AI API keys, prompt-hash, etc.).
#
# By DEFAULT it PRESERVES the non-secret, CP/Website-facing shared config so a
# clear does not knock the Control Panel / Website offline. The one that matters
# most is SIMF_Api__BaseUrl — without it the CP falls back to http://localhost:5175
# and shows "The SIMF service could not be reached". Use -Full to wipe everything
# (a true scrub / decommission), which also drops those shared values.
#
# Machine scope is shared by every app on the box, so this affects the API,
# Control Panel and Website at once.
#
#   Scrub secrets, keep CP reachable:  .\deploy\clear-env.ps1
#   Wipe EVERYTHING (incl. Api BaseUrl): .\deploy\clear-env.ps1 -Full
#   Also drop the host env:               .\deploy\clear-env.ps1 -IncludeAspNetEnv
#   Then restart the IIS app pools (or the server) so w3wp drops the values.
# =============================================================================

#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    # Wipe EVERY SIMF_* variable, including the non-secret shared config that is
    # normally preserved (SIMF_Api__BaseUrl, CORS origins, AI routing). Use for a
    # full scrub / decommission — it WILL take the CP + Website offline until
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
$preserveExact = @(
    "SIMF_Api__BaseUrl"                     # CP + Website -> API endpoint (public URL)
    "SIMF_Api__AllowSelfSignedCertificate"  # CP/Web accept self-signed API cert (non-secret toggle)
    "SIMF_Ai__DefaultProvider"              # non-secret AI routing
    "SIMF_Ai__Anthropic__DefaultModel"      # non-secret model id
)
$preservePrefix = @(
    "SIMF_Cors__WebAppOrigins__"         # public web-app origin(s)
)
function Test-Preserved([string]$name) {
    if ($preserveExact -contains $name) { return $true }
    foreach ($p in $preservePrefix) { if ($name -like ($p + "*")) { return $true } }
    return $false
}

$mode = if ($Full) { "FULL wipe (incl. shared config)" } else { "secrets only (shared config kept)" }
Write-Host ("=== SIMF clear env (Machine scope) - {0} ===" -f $mode) -ForegroundColor Cyan

# Enumerate the live Machine environment and pick our whole namespace.
$machineVars = [Environment]::GetEnvironmentVariables([EnvironmentVariableTarget]::Machine)
$simfNames   = @($machineVars.Keys | Where-Object { $_ -like 'SIMF_*' } | Sort-Object)

$removed = 0
$kept    = 0
if ($simfNames.Count -eq 0) {
    Write-Host "  No SIMF_* Machine variables found - nothing to clear." -ForegroundColor DarkGray
} else {
    foreach ($name in $simfNames) {
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
    Write-Host "Kept the non-secret shared config (SIMF_Api__BaseUrl etc.) so the CP + Website stay reachable." -ForegroundColor Green
    Write-Host "Use -Full to wipe those too." -ForegroundColor DarkGray
}
Write-Host "Restart the IIS app pools (or the server) so w3wp drops the cleared values." -ForegroundColor Green
Write-Host "Re-provision with:  .\deploy\set-env.ps1" -ForegroundColor Green
