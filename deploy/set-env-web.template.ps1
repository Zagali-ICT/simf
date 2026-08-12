# =============================================================================
# SIMF - production environment variables for the Website server (TEMPLATE)
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
#     Copy-Item .\deploy\set-env-web.template.ps1 .\deploy\set-env-web.ps1
#     # fill the values in set-env-web.ps1, then:
#     .\deploy\set-env-web.ps1
#
# The filled copy is untracked by .gitignore. An empty value is SKIPPED with a
# warning, so running this template UNEDITED sets nothing and cannot blank a
# working server.
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
    [pscustomobject]@{ Name = "ASPNETCORE_ENVIRONMENT"; Value = "Production"; Secret = $false; Gate = $false; Note = "required; the same value on every SIMF server" }

    # --- Logs -----------------------------------------------------------------
    [pscustomobject]@{ Name = "SIMF_WEB_Storage__LogDirectory"; Value = "C:\SIMF\Storage\logs"; Secret = $false; Gate = $false; Note = "per-app logs under {dir}/SIMF.Web on this host" }

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
    [pscustomobject]@{ Name = "SIMF_WEB_Api__BaseUrl"; Value = "https://api.simrsnf.com/"; Secret = $false; Gate = $false; Note = "the API's address, internal to the estate; its certificate must validate" }

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
    [pscustomobject]@{ Name = "SIMF_WEB_DataProtection__KeyRingPath"; Value = "C:\SIMF\Storage\keyring"; Secret = $false; Gate = $true; Note = "shared key ring; back this up - losing it signs every admin out; UNC once the tiers are split, and presentation must be able to reach it" }
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
Write-Host "SIMF Web env: $set set, $skipped skipped (empty)."

if ($set -eq 0) {
    Write-Warning "Nothing was set - this is the UNEDITED template. Copy it to deploy\set-env-web.ps1 and fill the values there."
}

if ($missingGates.Count -gt 0) {
    Write-Warning ("The Website will REFUSE TO START in Production without: {0}" -f ($missingGates -join ", "))
}

Write-Host "Restart the SimfWeb app pool so w3wp picks up the new Machine-scope variables."
