# =============================================================================
# SIMF - production environment variables for the Website server
#
# ONE script per deployment package, because each package now runs on its OWN
# server. Run this on the SimfWeb box only. The public Website carries no
# secrets of its own: it has no sign-in, registration or account page, stores no
# personal data, and reaches everything it shows through the API.
#
# Deployment is therefore:
#
#     1. the pipeline publishes and deploys this package to this server
#     2. an operator runs THIS script here, as Administrator
#     3. restart the SimfWeb app pool
#
# Naming: SIMF_WEB_ + ASP.NET Core double-underscore (SIMF_WEB_Section__Key). Each app
# registers AddEnvironmentVariables("SIMF_WEB_"), which strips the prefix.
# ASPNETCORE_ENVIRONMENT is host-level and stays UN-prefixed.
#
#     .\deploy\set-env-web.ps1
#
# THIS SCRIPT IS THE WHOLE CONFIGURATION OF THE WEBSITE SERVER. Every setting
# the host can read is declared below - the three it reads in code, plus the
# logging and host-filtering settings that otherwise sit in appsettings.json.
# Running it is the only configuration step on this box.
#
# An empty value is SKIPPED with a warning, so a half-filled script cannot blank
# a working server.
#
# Every name here is SIMF_WEB_-prefixed, so nothing this server sets can collide
# with another SIMF application on the same box. Two settings must still AGREE
# with the Control Panel's copies, and a test enforces it by comparing the part
# after the prefix: Api__BaseUrl (both talk to the same API) and
# DataProtection__KeyRingPath (both must share ONE ring, or a token minted by
# one host is rejected by the other).
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

# Secret: $true  => never commit, never print, never paste into a ticket.
# Gate  : $true  => THIS app refuses to start in Production without it.
$vars = @(

    # --- Host -----------------------------------------------------------------
    # Not prefixed - read before configuration sources load.
    [pscustomobject]@{ Name = "ASPNETCORE_ENVIRONMENT"; Value = 'Production'; Secret = $false; Gate = $false; Note = "required; the same value on every SIMF server" }

    # --- Logs -----------------------------------------------------------------
    [pscustomobject]@{ Name = "SIMF_WEB_Storage__LogDirectory"; Value = 'C:\SIMF\Storage\logs'; Secret = $false; Gate = $false; Note = "per-app logs under {dir}/SIMF.Web on this host" }

    # --- How the Website reaches the API --------------------------------------
    # Api:AllowSelfSignedCertificate is GONE (2026-08-08); setting it now
    # does nothing, and a test fails the build if the key comes back. The
    # consequence, stated plainly: if the API certificate is expired, missing or
    # does not match this address, the Website CANNOT reach the API and there is
    # no switch to make it. Certificate renewal is a commitment this deployment
    # has.
    #
    # api.simrsnf.com is the API's own name and stays that way; it resolves
    # INSIDE the estate only, since the API is not published to the internet.
    # The Website is a server-side caller in the presentation zone, so it
    # reaches the application zone directly rather than through the public edge.
    [pscustomobject]@{ Name = "SIMF_WEB_Api__BaseUrl"; Value = 'https://api.simrsnf.com/'; Secret = $false; Gate = $false; Note = "the API's address, internal to the estate; its certificate must validate; MUST equal the Control Panel's copy" }

    # --- Data Protection key ring ---------------------------------------------
    # It encrypts every antiforgery token this host issues. Unset, each process
    # keeps its own ring on local disk - fine on one node, and silently broken on
    # two, where a token minted on one instance is rejected by the next. The
    # Website therefore REFUSES TO START without it outside Development. Point it
    # at the file server (a UNC path), not at an application host: the ring must
    # outlive any single node, and it must NOT sit under the versioned deploy
    # root, which a release replaces.
    #
    # FIREWALL NOTE, and it is a real one. This is read by the Website and the
    # Control Panel, which sit in the PRESENTATION zone, while the file server
    # sits in the DATA zone and the internal firewall as drawn permits SMB 445
    # from the APPLICATION zone only. On that ruleset the Website cannot reach
    # the ring and, because this is a boot gate, it does not start at all.
    # Settle it with the network team before the tiers are split.
    [pscustomobject]@{ Name = "SIMF_WEB_DataProtection__KeyRingPath"; Value = 'C:\SIMF\Storage\keyring'; Secret = $false; Gate = $true; Note = "shared key ring; back this up - losing it signs every admin out; UNC once the tiers are split, and presentation must be able to reach it" }

    # --- Logging levels -------------------------------------------------------
    # Two settings, because two loggers read two sections and neither reads the
    # other's. The host runs Serilog through ReadFrom.Configuration, so the
    # Serilog:* keys decide what reaches
    # {Storage:LogDirectory}/SIMF.Web/log-{Date}.log. Logging:LogLevel is the
    # framework's own section. Both are declared so neither is a setting that
    # exists only in a file this server no longer configures from.
    [pscustomobject]@{ Name = "SIMF_WEB_Serilog__MinimumLevel__Default"; Value = "Information"; Secret = $false; Gate = $false; Note = "Verbose | Debug | Information | Warning | Error | Fatal" }
    [pscustomobject]@{ Name = "SIMF_WEB_Logging__LogLevel__Default"; Value = "Information"; Secret = $false; Gate = $false; Note = "framework ILogger default level" }

    # The per-category overrides carry a DOT in the key name, which is legal in a
    # Windows environment variable and binds correctly. Note that
    # DeploymentEnvTemplateTests' entry regex matches [A-Za-z0-9_] only, so these
    # two are invisible to it - still set by the loop below, but not covered by
    # its duplicate and prefix checks.
    [pscustomobject]@{ Name = "SIMF_WEB_Serilog__MinimumLevel__Override__Microsoft.AspNetCore"; Value = "Warning"; Secret = $false; Gate = $false; Note = "quietens per-request framework chatter" }
    [pscustomobject]@{ Name = "SIMF_WEB_Logging__LogLevel__Microsoft.AspNetCore"; Value = "Warning"; Secret = $false; Gate = $false; Note = "same, on the framework logger" }

    # --- Host header filtering ------------------------------------------------
    # Pinned to this server's own name rather than the "*" the shipped
    # appsettings.json uses. A request arriving with any other Host is refused
    # with 400, so add any further name this site answers to - separated by a
    # semicolon - including a load-balancer health probe that calls the raw
    # machine name.
    [pscustomobject]@{ Name = "SIMF_WEB_AllowedHosts"; Value = "web.simrsnf.com"; Secret = $false; Gate = $false; Note = "semicolon-separated host list; '*' accepts any Host header" }
)

# -----------------------------------------------------------------------------
# Host timezone
# -----------------------------------------------------------------------------
# The whole system stores and compares Saudi local wall-clock time (SimfClock,
# +03:00, no DST). A host on another timezone is a deployment defect worth
# seeing now rather than discovering from a support ticket. Reported, never
# changed - altering a server's timezone is the operator's call.
$tz = tzutil /g
if ($tz -ne 'Arab Standard Time') {
    Write-Warning "Host timezone is '$tz', not 'Arab Standard Time' (UTC+03:00). SIMF works on the Saudi clock; verify this is intended."
} else {
    Write-Host "host timezone: $tz (UTC+03:00) - correct for SIMF."
}

# -----------------------------------------------------------------------------
# Apply
# -----------------------------------------------------------------------------
$set = 0
$skipped = 0
$missingGates = @()

foreach ($v in $vars) {
    if ([string]::IsNullOrWhiteSpace($v.Value)) {
        # Optional = $true means empty is the CORRECT state. Warning about those
        # buries the empty values that are real gaps.
        if ($v.Optional) {
            Write-Host ("not set, and correct: {0}  ({1})" -f $v.Name, $v.Note) -ForegroundColor DarkGray
        }
        else {
            $label = if ($v.Secret) { "SECRET" } else { "value" }
            Write-Warning ("SKIP (empty {0}): {1}  {2}" -f $label, $v.Name, $v.Note)
        }
        if ($v.Gate) { $missingGates += $v.Name }
        $skipped++
        continue
    }
    [Environment]::SetEnvironmentVariable($v.Name, $v.Value, [EnvironmentVariableTarget]::Machine)
    # Never echo the value - only the name.
    Write-Host ("set: {0}" -f $v.Name)
    $set++
}

Write-Host ""
Write-Host "SIMF Web env: $set set, $skipped skipped (empty)."

if ($set -eq 0) {
    Write-Warning "Nothing was set - every value in this script is empty. Fill them in the `$vars block above and run it again."
}

if ($missingGates.Count -gt 0) {
    Write-Warning ("The Website will REFUSE TO START in Production without: {0}" -f ($missingGates -join ", "))
}

Write-Host "Restart the SimfWeb app pool so w3wp picks up the new Machine-scope variables."

