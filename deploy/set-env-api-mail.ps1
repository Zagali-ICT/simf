# =============================================================================
# SIMF - set the email credentials on the API server.
#
#   1. copy this file to the API box
#   2. right-click PowerShell -> Run as Administrator
#   3. cd to the folder, then:  .\set-email-credentials.ps1
#
# It sets the six SIMF_API_Email__* variables and restarts the app pool.
# Nothing else is touched - the JWT key, the encryption keys and the connection
# strings are left exactly as they are.
#
# -----------------------------------------------------------------------------
# EDIT THESE SIX LINES, THEN RUN.
# -----------------------------------------------------------------------------
$Host_       = "smtp.zoho.com"
$Port        = "587"
$User        = "no-reply@simrsnf.com"      # the SMTP LOGIN
$Password    = "Gt60uPjeCr5b"              # Zoho APP-SPECIFIC password
$FromAddress = "no-reply@simrsnf.com"      # what recipients see
$FromName    = "SIMF"

$PoolName    = "SimfAPI"
# =============================================================================

$ErrorActionPreference = "Stop"

# --- Must be elevated: Machine-scope variables cannot be written otherwise.
$id = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run PowerShell as Administrator."
}

# --- Prove the credential BEFORE writing it.
#
# This is the whole reason this script is worth running rather than setting the
# variables by hand. A bad SMTP credential does not throw, log, or show up in
# the UI - the API simply stops delivering. Every OTP, email verification and
# password reset silently fails, so nobody can sign up or sign in, and the
# first sign of it is users reporting they got no code.
#
# Tested 2026-08-24 from this repo: no-reply@simrsnf.com with the password
# above was REJECTED (535) by smtp.zoho.com, .sa, .eu and .in, while the
# existing ammn.com.sa credential authenticated on the same host in the same
# run. If that is still true, this script stops here and changes nothing.
Write-Host "Testing $User against $Host_ : $Port ..." -ForegroundColor Cyan

Add-Type -AssemblyName System.Net.Mail -ErrorAction SilentlyContinue
$smtp = New-Object System.Net.Mail.SmtpClient($Host_, [int]$Port)
$smtp.EnableSsl   = $true
$smtp.Credentials = New-Object System.Net.NetworkCredential($User, $Password)
$smtp.Timeout     = 30000

$probe = New-Object System.Net.Mail.MailMessage
$probe.From = $FromAddress
$probe.To.Add($FromAddress)          # to itself - never leaves the mailbox
$probe.Subject = "SIMF SMTP credential check"
$probe.Body    = "Sent by set-email-credentials.ps1 to prove the login works."

try {
    $smtp.Send($probe)
    Write-Host "SMTP OK - the credential authenticated and a test mail was accepted." -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "SMTP TEST FAILED - NOTHING HAS BEEN CHANGED." -ForegroundColor Red
    Write-Host $_.Exception.Message
    if ($_.Exception.InnerException) { Write-Host $_.Exception.InnerException.Message }
    Write-Host ""
    Write-Host "Most likely, in order:" -ForegroundColor Yellow
    Write-Host "  1. the mailbox does not exist in Zoho yet (simrsnf.com has no Zoho DKIM"
    Write-Host "     record and its SPF names Google only, which is what you would expect"
    Write-Host "     of a domain never set up there);"
    Write-Host "  2. that is an ACCOUNT password - Zoho refuses those on SMTP, it must be"
    Write-Host "     an app-specific password;"
    Write-Host "  3. it is a send-as ALIAS, not a mailbox - then `$User must stay the"
    Write-Host "     underlying mailbox and only `$FromAddress changes."
    Write-Host ""
    Write-Host "The live credential is untouched, so mail keeps working. Fix the value"
    Write-Host "above and run this again." -ForegroundColor Yellow
    exit 1
}
finally {
    $probe.Dispose()
    $smtp.Dispose()
}

# --- Only now write anything.
$vars = [ordered]@{
    "SIMF_API_Email__Host"        = $Host_
    "SIMF_API_Email__Port"        = $Port
    "SIMF_API_Email__User"        = $User
    "SIMF_API_Email__Password"    = $Password
    "SIMF_API_Email__FromAddress" = $FromAddress
    "SIMF_API_Email__FromName"    = $FromName
}
$secretNames = @("SIMF_API_Email__User", "SIMF_API_Email__Password")

Write-Host ""
foreach ($name in $vars.Keys) {
    [Environment]::SetEnvironmentVariable(
        $name, $vars[$name], [EnvironmentVariableTarget]::Machine)
    # Names only for the secrets - a password echoed to a console lives on in
    # the scrollback and in whatever is recording the session.
    if ($secretNames -contains $name) {
        Write-Host ("set: {0} = <secret>" -f $name)
    } else {
        Write-Host ("set: {0} = {1}" -f $name, $vars[$name])
    }
}

# --- w3wp reads Machine-scope environment at process start, so without this the
#     OLD sender stays live and you would conclude the change did not work.
Import-Module WebAdministration -ErrorAction Stop
if (Test-Path "IIS:\AppPools\$PoolName") {
    Restart-WebAppPool -Name $PoolName
    Write-Host ""
    Write-Host "Restarted app pool '$PoolName'." -ForegroundColor Green
} else {
    Write-Warning "App pool '$PoolName' not found. Variables ARE set - restart the right pool by hand."
}

Write-Host ""
Write-Host "Done. Now confirm DELIVERABILITY, which is a separate thing from AUTH:" -ForegroundColor Cyan
Write-Host "  simrsnf.com DNS must authorise Zoho, or the mail sends and then fails"
Write-Host "  authentication at the recipient and lands in spam:"
Write-Host "    SPF   v=spf1 include:_spf.google.com include:zoho.com ~all"
Write-Host "    DKIM  the zoho._domainkey.simrsnf.com TXT record Zoho issues"
Write-Host ""
Write-Host "  Then sign up with a Gmail address and open the message -> Show original."
Write-Host "  You need SPF: PASS and DKIM: PASS with simrsnf.com signing."
Write-Host "  This matters for the Play review: the reviewer's sign-in code goes to a"
Write-Host "  Gmail inbox, and a spam-foldered OTP fails the review."
