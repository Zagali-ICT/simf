# =============================================================================
# SIMF - carry pre-split `SIMF_*` Machine variables onto their per-app prefixes.
#
# WHAT PROBLEM THIS SOLVES. Until 2026-08-12 every SIMF app read one shared
# `SIMF_` prefix. Each app now reads its own - `SIMF_API_`, `SIMF_CP_`,
# `SIMF_WEB_`, `SIMF_EDGE_` - and `SimfLegacyEnvironmentGuard` REFUSES to start a
# host that still has the retired names present, rather than booting and
# reporting a missing key whose value is sitting there under its former name.
#
# A server provisioned before the split therefore fails with:
#
#     This server still carries 61 environment variable(s) using the retired
#     'SIMF_' prefix, which this build does not read: ...
#
# Those 61 hold REAL production values - connection strings, the SMTP password,
# the encryption keys. Re-typing them by hand is 61 chances to fat-finger a
# secret, and rotating either data key strands everything already encrypted with
# it. This script copies each value onto its new name instead.
#
# HOW THE MAPPING IS DERIVED. Not from a list written here - from the four
# `set-env-*.template.ps1` files, which are the source of truth for which key
# belongs to which app. `SIMF_Jwt__SigningKey` becomes `SIMF_API_Jwt__SigningKey`
# because the API template declares that name and no other template does.
#
# ONE OLD NAME CAN FEED SEVERAL NEW ONES, and that is the case this exists to get
# right: `SIMF_Storage__LogDirectory` is declared by all four templates, so it is
# written to all four prefixes. `SIMF_Api__BaseUrl` feeds CP and Web.
# `SIMF_ReverseProxy__KnownProxies__0` feeds API and Edge. A hand-written rename
# is exactly where those get missed.
#
# WHAT IT DOES NOT DO:
#   * it never prints a value - only names, and whether each was set;
#   * it never overwrites a new-prefix variable that already holds a value,
#     so it is safe to re-run and safe to run after a partial set-env pass;
#   * it does NOT delete the old variables. Run deploy/clear-env.ps1 -Full for
#     that, AFTER checking this reported no unmapped names. Clearing first
#     leaves the box with no configuration at all if this is interrupted.
#
# USAGE (elevated, on the server):
#
#     .\deploy\migrate-env-prefix.ps1 -WhatIf   # show the plan, change nothing
#     .\deploy\migrate-env-prefix.ps1           # apply
#     .\deploy\clear-env.ps1 -Full              # then remove the old names
#     .\deploy\ops.ps1 -Action Restart -Target All
#
# `-Report` alone lists which REQUIRED and boot-GATE keys are still unset under
# the new prefixes, which is the "what is still missing" question.
# =============================================================================
#Requires -RunAsAdministrator

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # Only report the current state; make no changes at all.
    [switch]$Report,

    # Where the set-env-*.template.ps1 files live. Defaults to this script's
    # own folder, which is where they sit in the repository.
    [string]$TemplateDirectory = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$prefixes = [ordered]@{
    api  = "SIMF_API_"
    cp   = "SIMF_CP_"
    web  = "SIMF_WEB_"
    edge = "SIMF_EDGE_"
}

# --- 1. Read the templates -------------------------------------------------
# The templates are the source of truth for which key belongs to which app.
# A missing template is fatal: a partial map would silently strand whichever
# app's keys were not read.
$declared = @()
foreach ($package in $prefixes.Keys) {
    $path = Join-Path $TemplateDirectory "set-env-$package.template.ps1"
    if (-not (Test-Path $path)) {
        throw ("Template not found: $path. Run this from the repository's deploy " +
               "folder, or pass -TemplateDirectory. Without all four templates " +
               "the mapping would be incomplete and would strand keys.")
    }

    foreach ($line in Get-Content -LiteralPath $path) {
        $match = [regex]::Match($line, 'Name\s*=\s*"(?<name>[^"]+)".*?Gate\s*=\s*\$(?<gate>true|false)')
        if (-not $match.Success) { continue }

        $name = $match.Groups["name"].Value
        # ASPNETCORE_ENVIRONMENT is host-level and un-prefixed by design; it is
        # not part of the rename and must not be touched.
        if (-not $name.StartsWith($prefixes[$package], [StringComparison]::Ordinal)) { continue }

        $declared += [pscustomobject]@{
            Package = $package
            NewName = $name
            Suffix  = $name.Substring($prefixes[$package].Length)
            Gate    = ($match.Groups["gate"].Value -eq 'true')
        }
    }
}

if ($declared.Count -eq 0) {
    throw "No variables parsed from the templates - the template format has changed and this script's regex no longer matches. Fix the regex rather than proceeding, or the migration would silently do nothing."
}

# --- 2. Read the machine environment ---------------------------------------
$machine = [Environment]::GetEnvironmentVariables('Machine')

function Test-HasValue([string]$name) {
    if (-not $machine.Contains($name)) { return $false }
    return -not [string]::IsNullOrWhiteSpace([string]$machine[$name])
}

$newPrefixes = @($prefixes.Values)
$legacy = @($machine.Keys |
    Where-Object { $_ -like 'SIMF_*' } |
    Where-Object {
        $candidate = [string]$_
        -not ($newPrefixes | Where-Object { $candidate.StartsWith($_, [StringComparison]::Ordinal) })
    } |
    Sort-Object)

Write-Host ""
Write-Host "Legacy 'SIMF_' variables present : $($legacy.Count)"
Write-Host "Keys declared by the templates   : $($declared.Count)"
Write-Host ""

# --- 3. Plan ---------------------------------------------------------------
$plan = @()
$unmapped = @()

foreach ($old in $legacy) {
    $suffix = ([string]$old).Substring("SIMF_".Length)
    $targets = @($declared | Where-Object { $_.Suffix -eq $suffix })

    if ($targets.Count -eq 0) {
        $unmapped += $old
        continue
    }

    foreach ($target in $targets) {
        $plan += [pscustomobject]@{
            OldName = [string]$old
            NewName = $target.NewName
            Skip    = (Test-HasValue $target.NewName)
        }
    }
}

foreach ($entry in $plan) {
    $state = if ($entry.Skip) { "already set, leaving alone" } else { "will copy" }
    Write-Host ("  {0,-52} -> {1,-56} [{2}]" -f $entry.OldName, $entry.NewName, $state)
}

if ($unmapped.Count -gt 0) {
    Write-Host ""
    Write-Warning ("These legacy variables match no key in any template, so nothing " +
                   "reads them under any prefix. They are almost certainly retired " +
                   "settings and clear-env.ps1 -Full will remove them - but read the " +
                   "list before running it:")
    $unmapped | ForEach-Object { Write-Host "  $_" }
}

# --- 4. What is still missing ----------------------------------------------
$missingGates = @($declared | Where-Object { $_.Gate -and -not (Test-HasValue $_.NewName) })
$plannedNames = @($plan | Where-Object { -not $_.Skip } | Select-Object -ExpandProperty NewName)
$stillMissingGates = @($missingGates | Where-Object { $plannedNames -notcontains $_.NewName })

Write-Host ""
if ($stillMissingGates.Count -eq 0) {
    Write-Host "Boot gates: all satisfied (or covered by the plan above)."
} else {
    Write-Warning "BOOT GATES still unset after this migration - these hosts will refuse to start:"
    $stillMissingGates | ForEach-Object { Write-Host ("  {0}  [{1}]" -f $_.NewName, $_.Package) }
    Write-Host ""
    Write-Host "Generate the encryption keys with deploy/configure-prod-env.ps1 -Target <package>."
    Write-Host "It never overwrites an existing key, because rotating one strands the data it protects."
}

if ($Report) {
    Write-Host ""
    Write-Host "-Report given: nothing was changed."
    return
}

# --- 5. Apply --------------------------------------------------------------
$copied = 0
foreach ($entry in $plan) {
    if ($entry.Skip) { continue }

    $value = [string]$machine[$entry.OldName]
    if ($PSCmdlet.ShouldProcess($entry.NewName, "set from $($entry.OldName)")) {
        [Environment]::SetEnvironmentVariable($entry.NewName, $value, 'Machine')
        $copied++
    }
}

Write-Host ""
Write-Host "Copied $copied variable(s) onto their new prefix."
Write-Host ""
Write-Host "NEXT, in this order:"
Write-Host "  1. .\deploy\clear-env.ps1 -Full          # remove the retired SIMF_ names"
Write-Host "  2. .\deploy\ops.ps1 -Action Restart -Target All"
Write-Host ""
Write-Host "Step 1 AFTER this one, never before: clearing first leaves the box with no configuration at all."
