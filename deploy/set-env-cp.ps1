# =============================================================================
# SIMF - SimfCP (Control Panel) production environment variables (TEMPLATE)
#
# Per SIMF-OPS-001 section 6: MACHINE-scope environment variables, set on the
# server by this per-service script. Committed copy is a TEMPLATE with empty
# values - NEVER commit real values. Fill on the server, run as Administrator,
# then restart the IIS app pool.
#
# Naming: ASP.NET Core no-prefix double-underscore convention (Section__Key).
# Note: Machine-scope variables are shared by every app on the box, so
# Api__BaseUrl and ASPNETCORE_ENVIRONMENT are common to SimfCP and SimfWeb.
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

# An empty value is SKIPPED (warned) so the unedited template never sets blanks.
$vars = [ordered]@{
    "ASPNETCORE_ENVIRONMENT" = "Production"  # [REQUIRED]
    "Api__BaseUrl"           = ""            # [REQUIRED] e.g. https://api.simf.example/ - MUST be HTTPS outside Development
    "Storage__LogDirectory"  = ""            # optional (default logs) - per-app logs under {dir}/SIMF.ControlPanel/
}

$set = 0
$skipped = 0
foreach ($name in $vars.Keys) {
    $value = $vars[$name]
    if ([string]::IsNullOrWhiteSpace($value)) {
        Write-Warning "SKIP (empty): $name  - fill this in before a real deploy"
        $skipped++
        continue
    }
    [Environment]::SetEnvironmentVariable($name, $value, [EnvironmentVariableTarget]::Machine)
    Write-Host "set: $name"
    $set++
}

Write-Host ""
Write-Host "SimfCP env: $set set, $skipped skipped (empty)."
Write-Host "Restart the IIS app pool (or the server) so w3wp picks up the new Machine-scope variables."
