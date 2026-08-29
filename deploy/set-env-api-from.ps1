# =============================================================================
# SIMF - change ONLY the outbound From address on the API server.
#
#     .\set-env-api-from.ps1 -FromAddress "no-reply@simrsnf.com"
#
# Run on the API box, as Administrator. NO PASSWORD NEEDED.
#
# Use this when no-reply@simrsnf.com is a VERIFIED SEND-AS ALIAS on the Zoho
# mailbox already configured here. The SMTP login and password stay exactly as
# they are - they are already set on this server and this script never reads,
# prints or changes them.
#
# If instead you created a NEW Zoho mailbox for simrsnf.com, this is the wrong
# script: the login changes too, so use set-env-api-email.ps1.
#
# STILL REQUIRED BEFORE THIS WORKS, and neither is optional:
#   * the alias must be VERIFIED in Zoho, or Zoho refuses the send outright;
#   * simrsnf.com DNS must authorise Zoho, or the mail sends and then fails
#     authentication at the recipient:
#         SPF   v=spf1 include:_spf.google.com include:zoho.com ~all
#               (today: include:_spf.google.com twice, no Zoho)
#         DKIM  the zoho._domainkey.simrsnf.com TXT record Zoho issues
#               (today: NXDOMAIN)
#     Skipping DNS does not error. It sends the mail to spam - including the
#     sign-in code a Play reviewer needs.
# =============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FromAddress,

    [string]$FromName = "SIMF",

    [string]$PoolName = "SimfAPI",

    [switch]$NoRestart
)

$ErrorActionPreference = "Stop"

$identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this as Administrator - Machine-scope variables cannot be written otherwise."
}

if ($FromAddress -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') {
    throw "FromAddress '$FromAddress' is not a valid email address."
}

# The login must already exist, or there is nothing to send AS the alias with.
$existingUser = [Environment]::GetEnvironmentVariable('SIMF_API_Email__User', 'Machine')
$existingPass = [Environment]::GetEnvironmentVariable('SIMF_API_Email__Password', 'Machine')
if ([string]::IsNullOrWhiteSpace($existingUser) -or [string]::IsNullOrWhiteSpace($existingPass)) {
    throw "SIMF_API_Email__User / __Password are not set on this machine. Run set-env-api.ps1 (or set-env-api-email.ps1) first - there is no login for the alias to send through."
}

Write-Host ""
Write-Host "SMTP login stays: $existingUser   (password untouched)"
Write-Host "Sending as:       $FromAddress"
Write-Host ""

[Environment]::SetEnvironmentVariable('SIMF_API_Email__FromAddress', $FromAddress, [EnvironmentVariableTarget]::Machine)
[Environment]::SetEnvironmentVariable('SIMF_API_Email__FromName',    $FromName,    [EnvironmentVariableTarget]::Machine)
Write-Host "set: SIMF_API_Email__FromAddress = $FromAddress"
Write-Host "set: SIMF_API_Email__FromName    = $FromName"

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
Write-Host "VERIFY, do not assume - this fails silently:" -ForegroundColor Cyan
Write-Host '  curl.exe -s -X POST "https://edge.simrsnf.com/api/v1/app/auth/sign-up" -H "Content-Type: application/json" -d "{\"email\":\"<your gmail>\",\"password\":\"Test@12345\",\"confirmPassword\":\"Test@12345\"}"'
Write-Host ""
Write-Host "Gmail -> open the message -> Show original. Need SPF: PASS and DKIM: PASS with"
Write-Host "simrsnf.com signing. If Zoho rejects the alias you get no mail at all; if DNS is"
Write-Host "incomplete you get mail that lands in spam. Both look like 'it worked' from here."
