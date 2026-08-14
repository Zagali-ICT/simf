# =============================================================================
# SIMF - production environment variables for the mobile edge server (TEMPLATE)
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
#     Copy-Item .\deploy\set-env-edge.template.ps1 .\deploy\set-env-edge.ps1
#     # fill the SITE-SPECIFIC values in set-env-edge.ps1, then:
#     .\deploy\set-env-edge.ps1
#
# The filled copy is untracked by .gitignore. An empty value is SKIPPED with a
# warning, so running this template UNEDITED sets nothing and cannot blank a
# working server.
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
    [pscustomobject]@{ Name = "ASPNETCORE_ENVIRONMENT"; Value = "Production"; Secret = $false; Gate = $false; Note = "required; the same value on every SIMF server" }

    # --- Logs -----------------------------------------------------------------
    # This host is internet-facing and the first thing a mobile request touches,
    # so its log is where an incident starts. Leaving it on default console
    # logging means nothing survives an app-pool recycle.
    [pscustomobject]@{ Name = "SIMF_EDGE_Storage__LogDirectory"; Value = "C:\SIMF\Storage\logs"; Secret = $false; Gate = $false; Note = "per-app logs under {dir}/SIMF.MobileEdge on this host" }

    # --- Where the edge forwards ----------------------------------------------
    # The ONE setting that decides where the mobile surface goes. Point it at the
    # API's address inside the estate. Never point it at edge.simrsnf.com, which
    # is this host: the edge would forward to the load balancer and back to
    # itself.
    #
    # An empty value 502s every app user, which is why it is a gate: the edge
    # refuses to start rather than accept traffic it can only fail.
    [pscustomobject]@{ Name = "SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address"; Value = ""; Secret = $false; Gate = $true; Note = "SITE-SPECIFIC; the API's https address inside the estate, e.g. https://api.simrsnf.com/" }

    # --- Who is allowed to set X-Forwarded-For --------------------------------
    # This host sits behind the WAF and load balancer, so the client address
    # arrives in X-Forwarded-For. The API rate-limits and audits on that address;
    # if it is not forwarded, every mobile request appears to come from this one
    # host and the per-caller limits collapse into a single shared bucket.
    #
    # A gate here, unlike on the API, because an unverified X-Forwarded-For on
    # the internet-facing hop lets any caller spoof its source address straight
    # past the API's rate limiter and into the audit log.
    [pscustomobject]@{ Name = "SIMF_EDGE_ReverseProxy__KnownProxies__0"; Value = ""; Secret = $false; Gate = $true; Note = "SITE-SPECIFIC; address of the WAF / load balancer in front" }
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
        $label = if ($v.Secret) { "SECRET" } else { "value" }
        Write-Warning ("SKIP (empty {0}): {1}  {2}" -f $label, $v.Name, $v.Note)
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
    Write-Warning "Nothing was set - this is the UNEDITED template. Copy it to deploy\set-env-edge.ps1 and fill the values there."
}

if ($missingGates.Count -gt 0) {
    Write-Warning ("The mobile edge will REFUSE TO START in Production without: {0}" -f ($missingGates -join ", "))
}

Write-Host "Restart the SimfEdge app pool so w3wp picks up the new Machine-scope variables."
