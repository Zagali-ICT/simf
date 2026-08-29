# =============================================================================
# SIMF - production environment variables for the mobile edge server
#
# ONE script per deployment package, because each package now runs on its OWN
# server. Run this on the SimfEdge box only. The edge is a reverse proxy: it
# holds no connection string, no signing key and no encryption key, and it never
# touches the file store or either database.
#
# Deployment is therefore:
#
#     1. the pipeline publishes and deploys this package to this server
#     2. an operator runs THIS script here, as Administrator
#     3. restart the SimfEdge app pool
#
# THE EDGE IS PUBLISHED AT edge.simrsnf.com. api.simrsnf.com stays the API's own
# name and resolves inside the estate only. The mobile app compiles its base URL
# in (BuildConfig.apiBaseUrl), so pointing the app at the edge needs a rebuild
# with --dart-define and a store release on both platforms; the DNS change and
# that release have to land together, or the installed app has nothing to talk
# to in between.
#
# Naming: SIMF_EDGE_ + ASP.NET Core double-underscore (SIMF_EDGE_Section__Key). The host
# registers AddEnvironmentVariables("SIMF_EDGE_"), which strips the prefix, so
# SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address binds through
# to the YARP cluster. ASPNETCORE_ENVIRONMENT is host-level and stays
# UN-prefixed - the host reads it before configuration sources load, and both
# boot gates below are skipped in Development.
#
#     .\deploy\set-env-edge.ps1
#
# THIS SCRIPT IS THE WHOLE CONFIGURATION OF THE EDGE SERVER. Every setting the
# host can read is declared below - the forwarding destination and proxy
# allowlist it gates on, the YARP route it publishes, and the logging and
# host-filtering settings that otherwise sit in appsettings.json. Running it is
# the only configuration step on this box.
#
# An empty value is SKIPPED with a warning, so a half-filled script cannot blank
# a working server.
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

# Secret: $true  => never commit, never print, never paste into a ticket.
# Gate  : $true  => THIS app refuses to start in Production without it. Both of
#         the edge's own settings are gates, and deliberately so: it fails at
#         boot rather than serving traffic it cannot serve safely.
$vars = @(

    # --- Host -----------------------------------------------------------------
    # Not prefixed - read before configuration sources load. It also decides
    # whether the two boot gates below apply: they are skipped in Development.
    [pscustomobject]@{ Name = "ASPNETCORE_ENVIRONMENT"; Value = 'Production'; Secret = $false; Gate = $false; Note = "required; the same value on every SIMF server" }

    # --- Logs -----------------------------------------------------------------
    # This host is internet-facing and the first thing a mobile request touches,
    # so its log is where an incident starts. Leaving it on default console
    # logging means nothing survives an app-pool recycle.
    [pscustomobject]@{ Name = "SIMF_EDGE_Storage__LogDirectory"; Value = 'C:\SIMF\Storage\logs'; Secret = $false; Gate = $false; Note = "per-app logs under {dir}/SIMF.MobileEdge on this host" }

    # --- Where the edge forwards ----------------------------------------------
    # The ONE setting that decides where the mobile surface goes. Point it at the
    # API's address inside the estate. Never point it at edge.simrsnf.com, which
    # is this host: the edge would forward to the load balancer and back to
    # itself.
    #
    # An empty value 502s every app user, which is why it is a gate: the edge
    # refuses to start rather than accept traffic it can only fail.
    [pscustomobject]@{ Name = "SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address"; Value = 'https://api.simrsnf.com/'; Secret = $false; Gate = $true; Note = "the API's https address inside the estate; never this host's own public name" }

    # --- Who is allowed to set X-Forwarded-For --------------------------------
    # This host sits behind the WAF and load balancer, so the client address
    # arrives in X-Forwarded-For. The API rate-limits and audits on that address;
    # if it is not forwarded, every mobile request appears to come from this one
    # host and the per-caller limits collapse into a single shared bucket.
    #
    # A gate here, unlike on the API, because an unverified X-Forwarded-For on
    # the internet-facing hop lets any caller spoof its source address straight
    # past the API's rate limiter and into the audit log.
    # Named by domain at the owner's instruction (2026-08-16).
    #
    # On record, because it is not visible from the value: Program.cs:151 reads
    # each entry through IPAddress.TryParse with no else branch, so a name is
    # dropped rather than resolved, and the gate above counts config strings
    # rather than parsed addresses. Until that parse also accepts a hostname,
    # this list contributes no trusted proxy and X-Forwarded-For is not honoured.
    [pscustomobject]@{ Name = "SIMF_EDGE_ReverseProxy__KnownProxies__0"; Value = 'edge.simrsnf.com'; Secret = $false; Gate = $true; Note = "the public name in front of this host" }

    # --- The route this host publishes ----------------------------------------
    # The mobile surface, and deliberately only that: /api/v1/app/** forwards to
    # the api cluster and nothing else is routed, so the admin surface is not
    # reachable through the internet-facing hop even by mistake. Declared here
    # rather than left in appsettings.json so this script is the whole
    # configuration of the box, but treat it as fixed - widening the path is what
    # would publish /api/v1/admin/** to the internet.
    [pscustomobject]@{ Name = "SIMF_EDGE_ReverseProxy__Routes__mobile-app-surface__ClusterId"; Value = "api"; Secret = $false; Gate = $false; Note = "the cluster whose destination is set above" }
    [pscustomobject]@{ Name = "SIMF_EDGE_ReverseProxy__Routes__mobile-app-surface__Match__Path"; Value = '/api/v1/app/{**catch-all}'; Secret = $false; Gate = $false; Note = "the mobile surface ONLY; widening this exposes the admin API" }

    # --- Logging levels -------------------------------------------------------
    # This host is internet-facing and the first thing a mobile request touches,
    # so its log is where an incident starts. Serilog reads the Serilog:* keys
    # (ReadFrom.Configuration); Logging:LogLevel is the framework's own section.
    [pscustomobject]@{ Name = "SIMF_EDGE_Serilog__MinimumLevel__Default"; Value = "Information"; Secret = $false; Gate = $false; Note = "Verbose | Debug | Information | Warning | Error | Fatal" }
    [pscustomobject]@{ Name = "SIMF_EDGE_Logging__LogLevel__Default"; Value = "Information"; Secret = $false; Gate = $false; Note = "framework ILogger default level" }

    # Dotted key names are legal in a Windows environment variable and bind
    # correctly, but DeploymentEnvTemplateTests' entry regex matches
    # [A-Za-z0-9_] only, so these two are invisible to its duplicate and prefix
    # checks. They are still set by the loop below.
    [pscustomobject]@{ Name = "SIMF_EDGE_Serilog__MinimumLevel__Override__Microsoft.AspNetCore"; Value = "Warning"; Secret = $false; Gate = $false; Note = "quietens per-request framework chatter" }
    [pscustomobject]@{ Name = "SIMF_EDGE_Logging__LogLevel__Microsoft.AspNetCore"; Value = "Warning"; Secret = $false; Gate = $false; Note = "same, on the framework logger" }

    # --- Host header filtering ------------------------------------------------
    # This is the internet-facing host, so the "*" the shipped appsettings.json
    # uses is the weakest place to leave it. A Host that is not on this list is
    # refused with a 400 that never reaches the application log, so the spelling
    # has to be exactly the public DNS name - including the load balancer's
    # probe name, if it calls this host by anything else.
    #
    # NOTE: the owner's host list of 2026-08-16 wrote this one as
    # "edage.simrsnf.com". Every other reference in the repo says
    # "edge.simrsnf.com", and the other four names he gave match the repo
    # exactly, so this is set to edge and flagged rather than silently taking
    # the odd one out. Confirm which is the real DNS record.
    [pscustomobject]@{ Name = "SIMF_EDGE_AllowedHosts"; Value = "edge.simrsnf.com"; Secret = $false; Gate = $false; Note = "semicolon-separated host list; '*' accepts any Host header" }
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
Write-Host "SIMF Edge env: $set set, $skipped skipped (empty)."

if ($set -eq 0) {
    Write-Warning "Nothing was set - every value in this script is empty. Fill them in the `$vars block above and run it again."
}

if ($missingGates.Count -gt 0) {
    Write-Warning ("The mobile edge will REFUSE TO START in Production without: {0}" -f ($missingGates -join ", "))
}

Write-Host "Restart the SimfEdge app pool so w3wp picks up the new Machine-scope variables."

