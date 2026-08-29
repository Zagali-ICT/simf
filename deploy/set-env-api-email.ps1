# =============================================================================
# SIMF - change the outbound mail sender on the API server
#
# Run this ON THE API BOX, as Administrator.
#
#     .\set-env-api-email.ps1 -FromAddress "no-reply@simrsnf.com"
#
# It changes ONLY the four SIMF_API_Email__* variables and restarts the app
# pool. It touches no other setting, so it cannot disturb the JWT key, the
# encryption keys or the connection strings the way re-running the full
# set-env-api.ps1 would.
#
# The SMTP password comes from ONE of two places, in this order:
#   1. $SmtpPassword at the top of this file, if you fill it in;
#   2. otherwise an interactive prompt.
#
# Leave it empty and the file carries no secret, so it can sit on disk and be
# read by anyone. Fill it in and it does - delete the file from the server
# afterwards, or blank the line again.
#
# ---------------------------------------------------------------------------
# BEFORE YOU RUN IT - the variable is the smallest part of the change
#
# Zoho refuses to send from an address it has not verified, and a receiver
# rejects (or spam-folders) mail whose SPF does not authorise the sender. So:
#
#   1. Add simrsnf.com as a domain in Zoho Mail and complete verification.
#      This uses a TXT/CNAME record and does NOT touch MX, so Google Workspace
#      keeps RECEIVING mail for the domain.
#   2. Create the mailbox (or a verified send-as alias) for the From address.
#   3. Generate a Zoho APP-SPECIFIC password for it. Zoho blocks plain account
#      passwords on SMTP.
#   4. DNS on simrsnf.com - both records, not one:
#        SPF   v=spf1 include:_spf.google.com include:zoho.com ~all
#              (today it reads include:_spf.google.com twice and no Zoho)
#        DKIM  the zoho._domainkey.simrsnf.com TXT record Zoho issues
#              (today: NXDOMAIN)
#
# Skip step 4 and the mail still sends - it just fails authentication at
# Gmail and lands in spam. The Play reviewer's sign-in code goes to a Gmail
# address, so that failure mode costs you the store review.
# =============================================================================

[CmdletBinding()]
param(
    # The address recipients see. Must be a verified Zoho sender.
    [Parameter(Mandatory = $true)]
    [string]$FromAddress,

    # The SMTP login. Defaults to the From address, which is right when the
    # mailbox sends as itself; pass it explicitly when From is an ALIAS of a
    # different mailbox, because then the login stays the underlying account.
    [string]$SmtpUser,

    # Display name on outbound mail.
    [string]$FromName = "SIMF",

    [string]$PoolName = "SimfAPI",

    # Skip the pool restart (the variables are then live only after the next
    # recycle - w3wp reads Machine-scope environment at process start).
    [switch]$NoRestart
)

# =============================================================================
# FILL THIS IN, THEN RUN. Leave it empty and the script prompts instead.
#
# Paste the Zoho APP-SPECIFIC password for the mailbox below. Not the Zoho
# account password - Zoho refuses those on SMTP.
#
# If you fill it in: this file now holds a live credential. It is git-ignored,
# but delete it from the server when you are done, or blank this line again.
# =============================================================================
$SmtpPassword = "Gt60uPjeCr5b"   # Zoho app password for no-reply@simrsnf.com

$ErrorActionPreference = "Stop"

# --- Administrator check. SetEnvironmentVariable at Machine scope silently
#     needs elevation; without this you get a confusing access error later.
$identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this as Administrator - Machine-scope variables cannot be written otherwise."
}

if (-not $SmtpUser) { $SmtpUser = $FromAddress }

# --- Sanity, not policy. A typo here is silent: mail keeps sending from the
#     old address and nobody notices until someone reads a header.
if ($FromAddress -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') {
    throw "FromAddress '$FromAddress' is not a valid email address."
}
$domain = $FromAddress.Split('@')[1]
if ($domain -ne 'simrsnf.com') {
    Write-Warning "FromAddress is on '$domain', not simrsnf.com. Continuing - but confirm that is deliberate."
}

if ([string]::IsNullOrWhiteSpace($SmtpPassword)) {
    $secure = Read-Host -AsSecureString "Zoho APP password for $SmtpUser"
    $plain  = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
} else {
    $plain = $SmtpPassword
    Write-Host "Using the password set at the top of this script."
}
if ([string]::IsNullOrWhiteSpace($plain)) {
    throw "No password supplied. Nothing was changed."
}

$vars = @(
    @{ Name = "SIMF_API_Email__FromAddress"; Value = $FromAddress; Secret = $false }
    @{ Name = "SIMF_API_Email__FromName";    Value = $FromName;    Secret = $false }
    @{ Name = "SIMF_API_Email__User";        Value = $SmtpUser;    Secret = $true  }
    @{ Name = "SIMF_API_Email__Password";    Value = $plain;       Secret = $true  }
)

Write-Host ""
foreach ($v in $vars) {
    [Environment]::SetEnvironmentVariable(
        $v.Name, $v.Value, [EnvironmentVariableTarget]::Machine)
    # Echo the NAME only. A secret printed to a console is a secret in the
    # scrollback, the transcript and whatever is recording the session.
    if ($v.Secret) {
        Write-Host ("set: {0}  = <secret>" -f $v.Name)
    } else {
        Write-Host ("set: {0}  = {1}" -f $v.Name, $v.Value)
    }
}

# Drop the plaintext copy as soon as it has been written.
$plain = $null
[GC]::Collect()

Write-Host ""
Write-Host "Host (unchanged): $([Environment]::GetEnvironmentVariable('SIMF_API_Email__Host', 'Machine'))"
Write-Host "Port (unchanged): $([Environment]::GetEnvironmentVariable('SIMF_API_Email__Port', 'Machine'))"

if ($NoRestart) {
    Write-Warning "Pool NOT restarted. w3wp reads Machine-scope environment at process start, so the old sender stays live until it recycles."
    return
}

Import-Module WebAdministration -ErrorAction Stop
if (-not (Test-Path "IIS:\AppPools\$PoolName")) {
    Write-Warning "App pool '$PoolName' not found. Variables ARE set; restart the correct pool by hand."
    return
}
Restart-WebAppPool -Name $PoolName
Write-Host "Restarted app pool '$PoolName'."

Write-Host ""
Write-Host "VERIFY IT, do not assume it. Trigger a real mail and read the headers:" -ForegroundColor Cyan
Write-Host '  curl.exe -s -X POST "https://edge.simrsnf.com/api/v1/app/auth/sign-up" -H "Content-Type: application/json" -d "{\"email\":\"<your gmail>\",\"password\":\"Test@12345\",\"confirmPassword\":\"Test@12345\"}"'
Write-Host ""
Write-Host "In Gmail: open the message -> Show original. You need SPF: PASS and DKIM: PASS"
Write-Host "with simrsnf.com as the signing domain. NEUTRAL or FAIL means step 4 above is"
Write-Host "incomplete or has not propagated - mail will be spam-foldered until it is."
