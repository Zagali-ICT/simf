# =============================================================================
# SIMF - PRODUCTION ENVIRONMENT RUNBOOK  (safe to commit - contains NO secrets)
#
# One-shot, RE-RUNNABLE provisioning of the SimfAPI Machine-scope environment:
#
#   1. GENERATE the cryptographic keys that can be generated (base64 32-byte,
#      System.Security.Cryptography.RandomNumberGenerator) - but ONLY when the
#      key is not already set. An existing key is NEVER overwritten.
#   2. PROMPT for the values that cannot be generated (connection strings, SMTP
#      credentials, the public Website origin, ...) without echoing them.
#   3. VERIFY - report, per key, its NAME and whether it is set. Never a value.
#   4. RESTART the IIS app pools and health-check the API.
#
# Run as Administrator on the SIMF server:
#
#     .\deploy\configure-prod-env.ps1
#     .\deploy\configure-prod-env.ps1 -VerifyOnly          # audit, change nothing
#     .\deploy\configure-prod-env.ps1 -SkipPrompts         # keys + verify only
#
# ############################################################################
# # THE ONE DANGEROUS OPERATION: ROTATING AN ENCRYPTION KEY                  #
# #                                                                          #
# # SIMF_FileStorage__EncryptionKey is the KEK for the centralized file      #
# # store (D-568). SIMF_Storage__UserIdDocumentEncryptionKey encrypts the    #
# # UserProfile PII columns at rest (NCA A2-10). Replacing EITHER makes      #
# # everything already encrypted under the old key PERMANENTLY UNREADABLE -  #
# # every stored file, every national ID / Iqama / passport / mobile.        #
# #                                                                          #
# # This script therefore NEVER overwrites an existing key. It warns and     #
# # skips. There is deliberately no -Force switch: a real rotation needs a   #
# # decrypt-and-re-encrypt migration, not an environment variable edit.      #
# ############################################################################
#
# This script does not print, log or return any secret value. Keys are written
# straight to the Machine environment and the local variable is cleared.
#
# Companion files:
#   deploy\set-env.template.ps1      - the full variable list for API + CP + Web
#   deploy\clear-env.ps1             - remove the Machine-scope SIMF_* variables
#   deploy\ops.ps1                   - install / start / stop the IIS sites
# =============================================================================

#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    # Report what is set and what is missing, then exit. Changes nothing.
    [switch]$VerifyOnly,

    # Generate the missing keys and verify, but do not prompt for the manual
    # values (useful on a box where those were already provisioned).
    [switch]$SkipPrompts,

    # Skip the app-pool restart + health check (e.g. IIS is not up yet).
    [switch]$SkipRestart,

    # The API health endpoint used for the post-restart check. The PUBLIC origin,
    # not loopback: the probe validates the certificate chain like any other
    # client, and the certificate is issued for this name - it cannot match
    # "localhost". There is deliberately no option to skip validation.
    [string]$HealthUrl = "https://api.simrsnf.com/health",

    # IIS app pools to recycle so w3wp picks up the new Machine variables.
    [string[]]$AppPools = @("SIMF.API", "SIMF.CP", "SIMF.WEB")
)

$ErrorActionPreference = "Stop"

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------

function Get-MachineVar([string]$name) {
    return [Environment]::GetEnvironmentVariable($name, [EnvironmentVariableTarget]::Machine)
}

function Test-MachineVarSet([string]$name) {
    return -not [string]::IsNullOrWhiteSpace((Get-MachineVar $name))
}

function Set-MachineVar([string]$name, [string]$value) {
    [Environment]::SetEnvironmentVariable($name, $value, [EnvironmentVariableTarget]::Machine)
}

# Reads a secret interactively and returns it as plain text WITHOUT echoing it
# to the console or the transcript. The caller must clear the result.
function Read-SecretValue([string]$prompt) {
    $secure = Read-Host -Prompt $prompt -AsSecureString
    if ($null -eq $secure -or $secure.Length -eq 0) { return $null }
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        $secure.Dispose()
    }
}

# A cryptographically-random base64-encoded 32-byte (256-bit) AES key.
function New-Base64AesKey {
    $bytes = [byte[]]::new(32)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
        return [Convert]::ToBase64String($bytes)
    }
    finally {
        $rng.Dispose()
        [Array]::Clear($bytes, 0, $bytes.Length)
    }
}

# -----------------------------------------------------------------------------
# The variable inventory
# -----------------------------------------------------------------------------

# Generated automatically when missing. Catastrophic = rotating it destroys
# already-encrypted data, so the skip-if-present rule is absolute.
$generatedKeys = @(
    [pscustomobject]@{
        Name         = "SIMF_FileStorage__EncryptionKey"
        Purpose      = "KEK for the centralized file store (D-568)"
        Catastrophic = $true
        Consequence  = "every file already in the store becomes undecryptable"
    }
    [pscustomobject]@{
        Name         = "SIMF_Storage__UserIdDocumentEncryptionKey"
        Purpose      = "AES-256-GCM key for the UserProfile PII columns (NCA A2-10)"
        Catastrophic = $true
        Consequence  = "every stored national ID / Iqama / passport / mobile becomes undecryptable"
    }
    [pscustomobject]@{
        Name         = "SIMF_Ai__PromptHash__Secret"
        Purpose      = "HMAC secret for the AI prompt-hash audit trail"
        Catastrophic = $false
        Consequence  = "historical AI audit hashes stop correlating with new ones"
    }
    [pscustomobject]@{
        Name         = "SIMF_Jwt__SigningKey"
        Purpose      = "symmetric signing key for access / refresh tokens"
        Catastrophic = $false
        Consequence  = "every issued token is invalidated and all users are signed out"
    }
)

# Cannot be generated - prompted for (secret) or typed (non-secret).
$promptedValues = @(
    [pscustomobject]@{ Name = "SIMF_ConnectionStrings__SimfIdentityDb"; Secret = $true;  Prompt = "SIMF_Identity connection string" }
    [pscustomobject]@{ Name = "SIMF_ConnectionStrings__SimfAppDb";      Secret = $true;  Prompt = "SIMF_App connection string" }
    [pscustomobject]@{ Name = "SIMF_Email__Host";                       Secret = $false; Prompt = "SMTP host" }
    [pscustomobject]@{ Name = "SIMF_Email__User";                       Secret = $true;  Prompt = "SMTP user" }
    [pscustomobject]@{ Name = "SIMF_Email__Password";                   Secret = $true;  Prompt = "SMTP password" }
    [pscustomobject]@{ Name = "SIMF_SuperAdmin__TempPassword";          Secret = $true;  Prompt = "bootstrap super-admin password (must NOT be the committed default)" }
    [pscustomobject]@{ Name = "SIMF_SuperAdmin__TotpSecret";            Secret = $true;  Prompt = "bootstrap super-admin TOTP seed (base32)" }
    [pscustomobject]@{ Name = "SIMF_MeetingLinks__PublicWebBaseUrl";    Secret = $false; Prompt = "public Website origin, e.g. https://simf.example.sa" }
    # Storage:AvatarBase and Storage:UserIdDocumentBase were prompted for here
    # until 2026-08-06. They named the roots of bespoke per-asset filesystem
    # stores that D-568 replaced with the unified StoredFile store, and by then
    # nothing read either one - so an operator was being asked for two
    # directories that configured nothing, while never being asked for the one
    # that does. FileStorage:RootPath is that setting: every uploaded avatar,
    # ID document, media image and speaker photo lands under it. Left unset it
    # silently falls back to %ProgramData%\SIMF\files, which is a real location
    # an operator never chose and may not be backing up.
    [pscustomobject]@{ Name = "SIMF_FileStorage__RootPath";             Secret = $false; Prompt = "file-store root for ALL uploads, e.g. C:\SIMF\Storage\files" }
    [pscustomobject]@{ Name = "SIMF_Storage__LogDirectory";             Secret = $false; Prompt = "log directory, e.g. C:\SIMF\Storage\logs" }
    # The Control Panel and Website refuse to start without this outside
    # Development: it is the shared Data Protection key ring behind the auth
    # cookie and every antiforgery token, and a per-node ring breaks the moment a
    # second instance exists. A UNC path once the tiers are separated.
    [pscustomobject]@{ Name = "SIMF_DataProtection__KeyRingPath";       Secret = $false; Prompt = "shared key-ring directory, e.g. C:\SIMF\Storage\keyring" }
    # The mobile edge refuses to start without both of these. The destination is
    # the API's PRIVATE address: pointing it at the public name sends the edge
    # back through the load balancer to itself.
    [pscustomobject]@{ Name = "SIMF_ReverseProxy__Clusters__api__Destinations__primary__Address"; Secret = $false; Prompt = "API PRIVATE address for the mobile edge, e.g. https://api-int.simrsnf.local/" }
    [pscustomobject]@{ Name = "SIMF_ReverseProxy__KnownProxies__0";     Secret = $false; Prompt = "address of the WAF / load balancer in front" }
)

# Reported by the verify pass but never set here (see set-env.template.ps1).
$alsoVerified = @(
    "ASPNETCORE_ENVIRONMENT"
    "SIMF_Ai__DefaultProvider"
    "SIMF_ReverseProxy__KnownProxies__0"
    "SIMF_Cors__WebAppOrigins__0"
)

Write-Host "=== SIMF production environment runbook (Machine scope) ===" -ForegroundColor Cyan
if ($VerifyOnly) {
    Write-Host "VerifyOnly - nothing will be changed." -ForegroundColor Yellow
}

# -----------------------------------------------------------------------------
# 1. Encryption keys - generate ONLY when absent
# -----------------------------------------------------------------------------

if (-not $VerifyOnly) {
    Write-Host ""
    Write-Host "--- 1. Cryptographic keys ---" -ForegroundColor Cyan

    foreach ($key in $generatedKeys) {
        if (Test-MachineVarSet $key.Name) {
            # THE GUARD. Never replace a live key.
            Write-Host ""
            Write-Warning ("PRESERVED (already set): {0}" -f $key.Name)
            Write-Warning ("  Refusing to overwrite. If this key changed, {0}." -f $key.Consequence)
            if ($key.Catastrophic) {
                Write-Warning "  This is IRREVERSIBLE data loss - a real rotation needs a decrypt-and-re-encrypt migration, not an env-var edit."
            }
            continue
        }

        $generated = New-Base64AesKey
        try {
            Set-MachineVar $key.Name $generated
            # Report the NAME only. The value is never printed.
            Write-Host ("  generated + set: {0}  ({1})" -f $key.Name, $key.Purpose) -ForegroundColor Green
        }
        finally {
            $generated = $null
        }
    }
    [GC]::Collect()
}

# -----------------------------------------------------------------------------
# 2. Values that cannot be generated
# -----------------------------------------------------------------------------

if (-not $VerifyOnly -and -not $SkipPrompts) {
    Write-Host ""
    Write-Host "--- 2. Manual values (press Enter to leave a value unchanged) ---" -ForegroundColor Cyan

    foreach ($item in $promptedValues) {
        $already = Test-MachineVarSet $item.Name
        $status = if ($already) { "currently SET" } else { "currently MISSING" }
        $label = "{0} [{1}]" -f $item.Name, $status

        if ($item.Secret) {
            $entered = Read-SecretValue $label
        }
        else {
            $typed = Read-Host -Prompt $label
            $entered = if ([string]::IsNullOrWhiteSpace($typed)) { $null } else { $typed }
        }

        if ($null -eq $entered -or [string]::IsNullOrWhiteSpace($entered)) {
            Write-Host ("  unchanged: {0}" -f $item.Name) -ForegroundColor DarkGray
            continue
        }

        try {
            Set-MachineVar $item.Name $entered
            Write-Host ("  set: {0}" -f $item.Name) -ForegroundColor Green
        }
        finally {
            $entered = $null
        }
    }
    [GC]::Collect()
}

# -----------------------------------------------------------------------------
# 3. Verify - names and set/missing only, never values
# -----------------------------------------------------------------------------

Write-Host ""
Write-Host "--- 3. Verification (name + state only; no value is ever shown) ---" -ForegroundColor Cyan

$allNames = @()
$allNames += $generatedKeys.Name
$allNames += $promptedValues.Name
$allNames += $alsoVerified

$missing = 0
foreach ($name in ($allNames | Select-Object -Unique)) {
    if (Test-MachineVarSet $name) {
        Write-Host ("  [SET]     {0}" -f $name) -ForegroundColor Green
    }
    else {
        Write-Host ("  [MISSING] {0}" -f $name) -ForegroundColor Yellow
        $missing++
    }
}

Write-Host ""
if ($missing -eq 0) {
    Write-Host "All tracked variables are set." -ForegroundColor Green
}
else {
    Write-Warning "$missing variable(s) still missing - see deploy\set-env.template.ps1 for what each one does."
}

if ($VerifyOnly) {
    Write-Host "VerifyOnly - done." -ForegroundColor Cyan
    return
}

# -----------------------------------------------------------------------------
# 4. Restart the app pools + health check
# -----------------------------------------------------------------------------

if ($SkipRestart) {
    Write-Host ""
    Write-Host "SkipRestart - restart the IIS app pools yourself so w3wp picks up the new values." -ForegroundColor Yellow
    return
}

Write-Host ""
Write-Host "--- 4. Restart app pools + health check ---" -ForegroundColor Cyan

try {
    Import-Module WebAdministration -ErrorAction Stop
}
catch {
    Write-Warning "WebAdministration module unavailable - skipping the restart. Restart the IIS app pools manually."
    return
}

foreach ($pool in $AppPools) {
    if (-not (Test-Path "IIS:\AppPools\$pool")) {
        Write-Warning ("app pool not found, skipped: {0}" -f $pool)
        continue
    }
    # Restart-WebAppPool throws on a STOPPED pool, and $ErrorActionPreference
    # is Stop, so fall back to starting it rather than aborting the runbook.
    try {
        Restart-WebAppPool -Name $pool -ErrorAction Stop
        Write-Host ("  restarted: {0}" -f $pool) -ForegroundColor Green
    }
    catch {
        try {
            Start-WebAppPool -Name $pool -ErrorAction Stop
            Write-Host ("  started (was stopped): {0}" -f $pool) -ForegroundColor Green
        }
        catch {
            Write-Warning ("could not restart or start app pool: {0}" -f $pool)
        }
    }
}

Write-Host ""
Write-Host ("Health check: {0}" -f $HealthUrl)

# NO certificate-validation override. This probe uses ordinary chain validation,
# so "healthy" means the API answered over TLS that actually authenticates it. A
# probe that trusted any certificate would report a deployment healthy on an
# answer from anything at all, which defeats the only check this script performs.
#
# That is also why HealthUrl defaults to the PUBLIC origin rather than loopback:
# the deployment's certificate is issued for that name and cannot match
# "localhost", so a loopback probe would fail validation and there is no longer
# an allowance to let it through. A TLS failure here is a real finding - the
# Control Panel and Website reach the API over the same certificate and will not
# start if it does not validate.
$ok = $false
foreach ($attempt in 1..10) {
    try {
        $response = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -eq 200) {
            Write-Host ("  healthy (HTTP 200) after {0} attempt(s)." -f $attempt) -ForegroundColor Green
            $ok = $true
            break
        }
        Write-Host ("  attempt {0}: HTTP {1}" -f $attempt, $response.StatusCode) -ForegroundColor DarkGray
    }
    catch {
        Write-Host ("  attempt {0}: not ready yet ({1})" -f $attempt, $_.Exception.Message) -ForegroundColor DarkGray
    }
    Start-Sleep -Seconds 5
}

if (-not $ok) {
    Write-Warning "The API did not report healthy. Check the API log under {Storage:LogDirectory}\SIMF.Api\."
    Write-Warning "A boot gate refuses to start Production without SIMF_FileStorage__EncryptionKey, SIMF_Storage__UserIdDocumentEncryptionKey or SIMF_Ai__PromptHash__Secret."
    Write-Warning "If the failure is a TLS/trust error, the API certificate does not validate. FIX THE CERTIFICATE - there is deliberately no option to skip validation."
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
