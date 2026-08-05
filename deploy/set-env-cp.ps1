# =============================================================================
# SIMF - SimfCP (Control Panel) production environment variables (TEMPLATE)
#
# Per SIMF-OPS-001 section 6: MACHINE-scope environment variables, set on the
# server by this per-service script. Committed copy is a TEMPLATE with empty
# values - NEVER commit real values. Fill on the server, run as Administrator,
# then restart the IIS app pool.
#
# Naming: SIMF_ + ASP.NET Core double-underscore (SIMF_Section__Key). The app
# registers AddEnvironmentVariables("SIMF_"), which strips the prefix, so
# SIMF_Api__BaseUrl binds to Api:BaseUrl. ASPNETCORE_ENVIRONMENT is host-level
# and stays UN-prefixed.
# Note: Machine-scope variables are shared by every app on the box, so
# SIMF_Api__BaseUrl and ASPNETCORE_ENVIRONMENT are common to SimfCP and SimfWeb.
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

# An empty value is SKIPPED (warned) so the unedited template never sets blanks.
# Values below are NON-SECRET (loopback URL, flag, log path) - safe to commit.
# Machine scope is shared, so the API overlay you create from
# deploy\set-env-api.template.ps1 may already have set some of these; this
# script fills them so the CP can be provisioned standalone. Running both is
# fine - the values are identical and the last writer wins.
$vars = [ordered]@{
    "ASPNETCORE_ENVIRONMENT"                = "Production"                 # [REQUIRED] host-level - NOT prefixed
    "SIMF_Api__BaseUrl"                     = "https://localhost:12340/"   # API loopback binding (avoids NAT hairpin); MUST be HTTPS outside Development
    "SIMF_Api__AllowSelfSignedCertificate"  = "true"                       # accept the API's self-signed cert (host-mismatch on localhost)
    "SIMF_Storage__LogDirectory"            = "C:\SIMF\Storage\logs"       # per-app logs under {dir}/SIMF.ControlPanel/

    # Auth-cookie IDLE lifetime, in hours (Session:LifetimeHours). Empty = the
    # 8h default. Listed explicitly because it was previously invisible here
    # while the API carries a DIFFERENTLY NAMED knob (Session:TimeoutHours) --
    # two keys, one section, neither documented, which is how a server acquires
    # settings nobody can account for. The CP reads only this one.
    #
    # Raising it cannot extend the real session past the API's absolute cap
    # (Jwt:SessionLifetimeHours, D-443 = 24h): the refresh token still expires
    # there and forces re-login. Use it for a SHORTER idle window.
    "SIMF_Session__LifetimeHours"           = ""                           # default 8; cannot exceed the API's 24h cap
}

# The whole system stores and compares Saudi local wall-clock time (SimfClock,
# +03:00, no DST). A host on another timezone is a deployment defect worth
# seeing at provisioning time rather than discovering from a support ticket:
# scheduled workers, reminder windows and "is this session live" all read a
# Saudi clock, and anything that hands a naive value to a framework that
# converts it inherits the host offset. D-848 removed the one such call site
# that could reject every access token, but the setting is still wrong.
# Reported, never changed -- altering a server's timezone is the operator's call.
$tz = tzutil /g
if ($tz -ne 'Arab Standard Time') {
    Write-Warning "Host timezone is '$tz', not 'Arab Standard Time' (UTC+03:00). SIMF works on the Saudi clock; verify this is intended."
} else {
    Write-Host "host timezone: $tz (UTC+03:00) - correct for SIMF."
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
