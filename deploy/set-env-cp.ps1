# =============================================================================
# SIMF - production environment variables for the Control Panel server
#
# ONE script per deployment package, because each package now runs on its OWN
# server. Run this on the SimfCP box only. It carries none of the API's secrets:
# the Control Panel holds no connection string, no signing key and no encryption
# key, because it reaches data only through the API.
#
# Deployment is therefore:
#
#     1. the pipeline publishes and deploys this package to this server
#     2. an operator runs THIS script here, as Administrator
#     3. restart the SimfCP app pool
#
# Naming: SIMF_CP_ + ASP.NET Core double-underscore (SIMF_CP_Section__Key). Each app
# registers AddEnvironmentVariables("SIMF_CP_"), which strips the prefix.
# ASPNETCORE_ENVIRONMENT is host-level and stays UN-prefixed.
#
#     .\deploy\set-env-cp.ps1
#
# THIS SCRIPT IS THE WHOLE CONFIGURATION OF THE CONTROL PANEL SERVER. Every
# setting the host can read is declared below - the four it reads in code, the
# upload cap its BFF reads, and the logging + host-filtering settings that
# otherwise sit in appsettings.json. Running it is the only configuration step
# on this box; nothing is left to a file default.
#
# An empty value is SKIPPED with a warning, so a half-filled script cannot blank
# a working server.
#
# Every name here is SIMF_CP_-prefixed, so nothing this server sets can collide
# with another SIMF application on the same box. Two settings must still AGREE
# with the Website's copies, and a test enforces it by comparing the part after
# the prefix: Api__BaseUrl (both talk to the same API) and
# DataProtection__KeyRingPath (both must share ONE ring, or a cookie minted by
# one host is rejected by the other). Storage__LogDirectory is deliberately
# free to differ - that is the point of a per-application namespace.
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
    [pscustomobject]@{ Name = "SIMF_CP_Storage__LogDirectory"; Value = 'C:\SIMF\Storage\logs'; Secret = $false; Gate = $false; Note = "per-app logs under {dir}/SIMF.ControlPanel on this host" }

    # --- How the Control Panel reaches the API --------------------------------
    # Api:AllowSelfSignedCertificate is GONE (2026-08-08). It installed
    # DangerousAcceptAnyServerCertificateValidator on the clients that carry the
    # admin password, the TOTP code and the perm:* bearer token, so anything that
    # answered for the configured host received them. It existed because the old
    # underscore hostnames could not get a public CA certificate; D-868 moved the
    # estate to simrsnf.com and D-872 installed a real one, which removed the
    # reason. Setting it now does nothing - the hosts no longer read it, and a
    # test fails the build if the key comes back.
    #
    # The consequence, stated plainly: if the API certificate is expired, missing
    # or does not match this address, the Control Panel CANNOT reach the API and
    # there is no switch to make it. That is an outage to fix on the server.
    # Certificate renewal is now a commitment this deployment has.
    #
    # api.simrsnf.com is the API's own name and stays that way; it resolves
    # INSIDE the estate only, since the API is not published to the internet.
    # Do NOT point this at the mobile edge: the edge publishes /api/v1/app/**
    # only, while the Control Panel calls /api/v1/admin/** through
    # SimfAdminClient, so every admin page would 404 through it.
    [pscustomobject]@{ Name = "SIMF_CP_Api__BaseUrl"; Value = 'https://api.simrsnf.com/'; Secret = $false; Gate = $false; Note = "the API's address, internal to the estate; its certificate must validate" }

    # --- Session --------------------------------------------------------------
    # The CP's own auth-cookie IDLE lifetime. A DIFFERENT setting from the API's
    # SIMF_API_Session__TimeoutHours - two keys under one section name, on two
    # servers, and the CP reads only this one. Before the per-application
    # prefixes these two shared a namespace, which is exactly the confusion the
    # split removes. Raising it cannot extend the real session past the API's
    # absolute cap; the refresh token still expires there and forces a re-login.
    [pscustomobject]@{ Name = "SIMF_CP_Session__LifetimeHours"; Value = "8"; Secret = $false; Gate = $false; Note = "default 8; cannot exceed the API's 24h cap" }

    # --- Data Protection key ring ---------------------------------------------
    # It encrypts the CP auth cookie (which carries the API tokens) and every
    # antiforgery token this host issues. Unset, each process keeps its own ring
    # on local disk - fine on one node, and silently broken on two, where a
    # cookie minted on one instance is rejected by the next and the admin is
    # bounced to /login at random. The CP therefore REFUSES TO START without it
    # outside Development. Point it at the file server (a UNC path), not at an
    # application host: the ring must outlive any single node, and it must NOT
    # sit under the versioned deploy root, which a release replaces.
    #
    # FIREWALL NOTE, and it is a real one. This is read by the CP and the
    # Website, which sit in the PRESENTATION zone, while the file server sits in
    # the DATA zone and the internal firewall as drawn permits SMB 445 from the
    # APPLICATION zone only. On that ruleset the CP cannot reach the ring and,
    # because this is a boot gate, it does not start at all. Settle it with the
    # network team before the tiers are split: either permit SMB from
    # presentation to the file server, or host the ring on a share presentation
    # can already reach.
    [pscustomobject]@{ Name = "SIMF_CP_DataProtection__KeyRingPath"; Value = 'C:\SIMF\Storage\keyring'; Secret = $false; Gate = $true; Note = "shared key ring; back this up - losing it signs every admin out; UNC once the tiers are split, and presentation must be able to reach it" }

    # --- Session-recording upload cap -----------------------------------------
    # The Control Panel's own limit on the recording upload it proxies to the API
    # (AccountEndpoints.Programme.cs). It has a compiled fallback of the same
    # value, so the CP works without it - but then raising the API's
    # SIMF_API_SessionRecordingStorage__MaxUploadBytes leaves this host still
    # rejecting the larger file, with the size named nowhere an operator looks.
    # The two must be kept equal by hand; no test compares them.
    [pscustomobject]@{ Name = "SIMF_CP_SessionRecordingStorage__MaxUploadBytes"; Value = "1073741824"; Secret = $false; Gate = $false; Note = "default 1073741824 (1 GiB); keep equal to the API's copy" }

    # --- Logging levels -------------------------------------------------------
    # Two settings, because two loggers read two sections and neither reads the
    # other's. The host runs Serilog through ReadFrom.Configuration, so the
    # Serilog:* keys are what actually decide what reaches
    # {Storage:LogDirectory}/SIMF.ControlPanel/log-{Date}.log. Logging:LogLevel is
    # the framework's own section, still honoured by anything logging through
    # ILogger before Serilog takes over. Both are declared so neither is a
    # setting that exists only in a file this server no longer configures from.
    #
    # Debug on a production Control Panel is a real cost, not just noise: the
    # request log grows fast and the admin actions it records are the audit trail
    # you would be searching. Leave these at Information/Warning unless
    # diagnosing, and put them back afterwards.
    [pscustomobject]@{ Name = "SIMF_CP_Serilog__MinimumLevel__Default"; Value = "Information"; Secret = $false; Gate = $false; Note = "Verbose | Debug | Information | Warning | Error | Fatal" }
    [pscustomobject]@{ Name = "SIMF_CP_Logging__LogLevel__Default"; Value = "Information"; Secret = $false; Gate = $false; Note = "framework ILogger default level" }

    # The per-category overrides carry a DOT in the key name, which is legal in a
    # Windows environment variable and binds correctly
    # (Logging:LogLevel:Microsoft.AspNetCore). Note that
    # DeploymentEnvTemplateTests' entry regex matches [A-Za-z0-9_] only, so these
    # two entries are invisible to it - they are still set by the loop below, but
    # its duplicate and prefix checks do not cover them.
    [pscustomobject]@{ Name = "SIMF_CP_Serilog__MinimumLevel__Override__Microsoft.AspNetCore"; Value = "Warning"; Secret = $false; Gate = $false; Note = "quietens per-request framework chatter" }
    [pscustomobject]@{ Name = "SIMF_CP_Logging__LogLevel__Microsoft.AspNetCore"; Value = "Warning"; Secret = $false; Gate = $false; Note = "same, on the framework logger" }

    # --- Host header filtering ------------------------------------------------
    # Pinned to this server's own name rather than the "*" the shipped
    # appsettings.json uses. It is the cheapest defence against a host-header
    # redirect, and the CP mints sign-in and password-reset redirects. A request
    # arriving with any other Host is refused with 400.
    #
    # If the CP is reached by a second name - a load-balancer probe on the raw
    # machine name, an internal alias - add it here separated by a semicolon, or
    # that caller gets a 400 nobody can explain from the application log.
    [pscustomobject]@{ Name = "SIMF_CP_AllowedHosts"; Value = "cp.simrsnf.com"; Secret = $false; Gate = $false; Note = "semicolon-separated host list; '*' accepts any Host header" }
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
Write-Host "SIMF CP env: $set set, $skipped skipped (empty)."

if ($set -eq 0) {
    Write-Warning "Nothing was set - every value in this script is empty. Fill them in the `$vars block above and run it again."
}

if ($missingGates.Count -gt 0) {
    Write-Warning ("The Control Panel will REFUSE TO START in Production without: {0}" -f ($missingGates -join ", "))
}

Write-Host "Restart the SimfCP app pool so w3wp picks up the new Machine-scope variables."

