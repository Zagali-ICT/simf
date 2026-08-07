# =============================================================================
# SIMF - production environment variables for ALL THREE apps (TEMPLATE)
#
# ONE script provisions SimfAPI, SimfCP and SimfWeb. Deployment is therefore:
#
#     1. the pipeline publishes the three sites
#     2. an operator runs THIS script once, as Administrator
#     3. restart the app pools
#
# It replaces set-env-api.template.ps1, set-env-cp.ps1 and set-env-web.ps1
# (removed 2026-08-06). Those three wrote to the SAME Machine-scope namespace
# and overlapped on ASPNETCORE_ENVIRONMENT, SIMF_Api__BaseUrl,
# SIMF_Api__AllowSelfSignedCertificate and SIMF_Storage__LogDirectory, each
# saying "running both is fine, the last writer wins". That is only true while
# the copies agree; edit one and the box silently takes whichever script ran
# last. One file cannot disagree with itself.
#
# Naming: SIMF_ + ASP.NET Core double-underscore (SIMF_Section__Key). Each app
# registers AddEnvironmentVariables("SIMF_"), which strips the prefix, so
# SIMF_ConnectionStrings__SimfAppDb binds to ConnectionStrings:SimfAppDb.
# ASPNETCORE_ENVIRONMENT is host-level and stays UN-prefixed.
#
# -----------------------------------------------------------------------------
# WHY THIS IS A ".template.ps1"
# -----------------------------------------------------------------------------
# deploy/set-env.ps1 is the FILLED overlay carrying real production secrets and
# is deliberately untracked. Do NOT remove that .gitignore rule to "share the
# script" - an earlier attempt to do exactly that would have committed a live
# SQL connection string, an SMTP app password and several production keys.
#
#     Copy-Item .\deploy\set-env.template.ps1 .\deploy\set-env.ps1
#     # fill the Secret entries in set-env.ps1, then:
#     .\deploy\set-env.ps1
#
# EVERY entry marked Secret = $true ships EMPTY here and must stay empty in the
# committed copy; a test enforces it. Non-secret values that are identical on
# every SIMF production box ARE pre-filled, so an operator fills roughly a dozen
# secrets rather than sixty variables. Non-secret values that differ per site
# (public origins, proxy IPs, SMTP host) also ship empty - they are prompts, not
# secrets.
#
# An empty value is SKIPPED with a warning, so running this template UNEDITED
# sets nothing and cannot blank a working server.
#
# Generate the random keys with deploy\configure-prod-env.ps1 - it creates the
# base64 32-byte AES keys, never prints them, and refuses to overwrite one that
# already exists.
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

# Apps  : which sites read the variable. Machine scope is shared, so a variable
#         is visible to all three regardless; this records who actually binds it.
# Secret: $true  => never commit, never print, never paste into a ticket.
# Gate  : $true  => the API REFUSES TO START in Production without it.
$vars = @(

    # --- Host -----------------------------------------------------------------
    # Not prefixed - read before configuration sources load.
    [pscustomobject]@{ Name = "ASPNETCORE_ENVIRONMENT"; Value = "Production"; Secret = $false; Gate = $false; Apps = "API CP Web"; Note = "required; the same value for all three sites" }

    # --- Databases (D-157: two physically separate databases) -----------------
    # Missing => every request that touches data fails, and the startup migration
    # cannot run (Program.cs migrates App then Identity).
    [pscustomobject]@{ Name = "SIMF_ConnectionStrings__SimfIdentityDb"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "SIMF_Identity - users, roles, permissions, 2FA, tokens" }
    [pscustomobject]@{ Name = "SIMF_ConnectionStrings__SimfAppDb"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "SIMF_App - everything else" }

    # --- Tokens ---------------------------------------------------------------
    # Missing => no access or refresh token can be signed or validated, so every
    # sign-in fails and every authenticated call is rejected.
    [pscustomobject]@{ Name = "SIMF_Jwt__SigningKey"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "symmetric signing key" }
    [pscustomobject]@{ Name = "SIMF_Jwt__Issuer"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default SIMF" }
    [pscustomobject]@{ Name = "SIMF_Jwt__Audience"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default SIMF" }
    [pscustomobject]@{ Name = "SIMF_Jwt__AccessTokenMinutes"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 5 (NCA cap, D-443)" }
    [pscustomobject]@{ Name = "SIMF_Jwt__SessionLifetimeHours"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 24 (NCA cap, D-443)" }

    # Ops override for the access-token lifetime, in HOURS. Clamped to
    # SessionLifetimeHours, so it can never breach the 24h ceiling. Left empty on
    # purpose: empty keeps the NCA default of 5 minutes. It is listed rather than
    # hidden, because an invisible knob is how a server acquires settings nobody
    # can account for. Note that permission codes are baked into the JWT, so a
    # revoked role keeps working until the token lapses - a longer window is a
    # real security trade, not just convenience.
    [pscustomobject]@{ Name = "SIMF_Session__TimeoutHours"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "empty => 5 min; max = SessionLifetimeHours" }

    # The CP's own auth-cookie IDLE lifetime. A DIFFERENT key from the API's
    # Session:TimeoutHours above - two keys, one section name, and the CP reads
    # only this one. Raising it cannot extend the real session past the API's
    # absolute cap; the refresh token still expires there and forces a re-login.
    [pscustomobject]@{ Name = "SIMF_Session__LifetimeHours"; Value = ""; Secret = $false; Gate = $false; Apps = "CP"; Note = "default 8; cannot exceed the API's 24h cap" }

    # --- Encryption keys (base64, decoding to EXACTLY 32 bytes) ---------------
    # A key that is valid base64 but decodes to 31 or 33 bytes is silently
    # discarded and only fails later, at first decrypt. Verify with:
    #   python -c "import base64;print(len(base64.b64decode('KEY')))"
    #
    # ROTATION WARNING for the two data keys below: changing either strands
    # everything already encrypted with it. Set once, back up, never rotate
    # casually.
    [pscustomobject]@{ Name = "SIMF_FileStorage__EncryptionKey"; Value = ""; Secret = $true; Gate = $true; Apps = "API"; Note = "KEK for the centralized file store; rotating it makes every stored file undecryptable" }
    [pscustomobject]@{ Name = "SIMF_Storage__UserIdDocumentEncryptionKey"; Value = ""; Secret = $true; Gate = $true; Apps = "API"; Note = "encrypts the national ID / Iqama / passport / mobile columns at rest (NCA A2-10)" }
    [pscustomobject]@{ Name = "SIMF_Ai__PromptHash__Secret"; Value = ""; Secret = $true; Gate = $true; Apps = "API"; Note = "HMAC secret; the dev fallback is publicly derivable and would poison the AI audit trail" }
    [pscustomobject]@{ Name = "SIMF_FileStorage__KekVersion"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 1" }

    # --- Filesystem paths -----------------------------------------------------
    # RootPath is the ONE setting that decides where every uploaded avatar, ID
    # document, media image and speaker photo lands. Left unset it falls back to
    # %ProgramData%\SIMF\files - a real location an operator never chose and may
    # not be backing up, which is why it is pre-filled here.
    #
    # Storage:AvatarBase, VipPhotoBase and UserIdDocumentBase were removed on
    # 2026-08-05. They named per-asset stores that the unified file store
    # replaced; setting them now has no effect at all.
    [pscustomobject]@{ Name = "SIMF_FileStorage__RootPath"; Value = "C:\SIMF\Storage\files"; Secret = $false; Gate = $false; Apps = "API"; Note = "root for ALL uploads; back this up" }
    [pscustomobject]@{ Name = "SIMF_Storage__LogDirectory"; Value = "C:\SIMF\Storage\logs"; Secret = $false; Gate = $false; Apps = "API CP Web"; Note = "per-app logs under {dir}/SIMF.Api, /SIMF.Workers, /SIMF.ControlPanel, /SIMF.Web" }
    [pscustomobject]@{ Name = "SIMF_SessionRecordingStorage__MaxUploadBytes"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 1073741824 (1 GiB)" }

    # --- How CP and Web reach the API -----------------------------------------
    # Loopback on purpose: it avoids a NAT hairpin through the public name. The
    # self-signed allowance exists because the loopback certificate cannot match
    # "localhost"; it applies to that hop only, never to a public origin.
    [pscustomobject]@{ Name = "SIMF_Api__BaseUrl"; Value = "https://api.simrsnf.com/"; Secret = $false; Gate = $false; Apps = "CP Web"; Note = "must be HTTPS outside Development" }
    [pscustomobject]@{ Name = "SIMF_Api__AllowSelfSignedCertificate"; Value = "true"; Secret = $false; Gate = $false; Apps = "CP Web"; Note = "accepts the API loopback cert only" }

    # --- Meeting confirmation links -------------------------------------------
    # Empty => the speaker double-opt-in email cannot be built, and the Approve /
    # Resend actions are refused UP FRONT with a bilingual 409
    # MEETING_LINKS_NOT_CONFIGURED before any action token is minted.
    [pscustomobject]@{ Name = "SIMF_MeetingLinks__PublicWebBaseUrl"; Value = "https://web.simrsnf.com"; Secret = $false; Gate = $false; Apps = "API"; Note = "public Website origin (D-868)" }
    [pscustomobject]@{ Name = "SIMF_MeetingLinks__TokenTtlHours"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 72" }

    # --- Bootstrap super admin ------------------------------------------------
    # Missing TempPassword => the super-admin is seeded without a usable password
    # and nobody can sign in to the CP on a fresh database. The API additionally
    # REFUSES TO BOOT in Production if this is left at the old committed default,
    # so it must be a NEW value. It must also avoid a monotonic run of characters
    # such as "12345": the seeder's own policy check rejects one, then writes no
    # super-admin while only LOGGING the reason, so the stack comes up healthy and
    # every sign-in fails at the login page for no visible cause.
    [pscustomobject]@{ Name = "SIMF_SuperAdmin__Email"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "SITE-SPECIFIC" }
    [pscustomobject]@{ Name = "SIMF_SuperAdmin__TempPassword"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "rotate after first sign-in; no monotonic runs" }
    [pscustomobject]@{ Name = "SIMF_SuperAdmin__TotpSecret"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "base32 TOTP seed for the admin second factor" }

    # --- Demo seed ------------------------------------------------------------
    # Empty is the correct PRODUCTION posture: the demo-account seed is skipped
    # with a warning. Set it only in a demo or QA environment.
    [pscustomobject]@{ Name = "SIMF_Seed__DemoPassword"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "non-production only - leave empty in Production" }

    # --- Outbound email -------------------------------------------------------
    # Missing => no OTP, verification, password-reset or meeting-confirmation
    # mail is delivered, which blocks visitor sign-up and the whole speaker flow.
    [pscustomobject]@{ Name = "SIMF_Email__Host"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "SITE-SPECIFIC: SMTP host" }
    [pscustomobject]@{ Name = "SIMF_Email__Port"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 587" }
    [pscustomobject]@{ Name = "SIMF_Email__User"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "SMTP user" }
    [pscustomobject]@{ Name = "SIMF_Email__Password"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "SMTP app password" }
    [pscustomobject]@{ Name = "SIMF_Email__FromAddress"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "SITE-SPECIFIC: e.g. no-reply@simf.example.sa" }
    [pscustomobject]@{ Name = "SIMF_Email__FromName"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default SIMF" }

    # --- AI providers ---------------------------------------------------------
    # DefaultProvider stays Echo until a real key is supplied; a feature whose
    # provider has no key returns 503 AI_PROVIDER_NOT_CONFIGURED.
    [pscustomobject]@{ Name = "SIMF_Ai__DefaultProvider"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "Echo | Gemini | Anthropic | OpenAi" }
    [pscustomobject]@{ Name = "SIMF_Ai__Gemini__ApiKey"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "" }
    [pscustomobject]@{ Name = "SIMF_Ai__Anthropic__ApiKey"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "" }
    [pscustomobject]@{ Name = "SIMF_Ai__OpenAi__ApiKey"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "" }

    # --- Edge / proxy / CORS --------------------------------------------------
    # KnownProxies missing => the API sees the reverse proxy's IP as the client
    # IP, so rate limiting and every audit row record the wrong address.
    [pscustomobject]@{ Name = "SIMF_ReverseProxy__KnownProxies__0"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "SITE-SPECIFIC: reverse-proxy IP (repeat __1, __2, ...)" }
    [pscustomobject]@{ Name = "SIMF_Cors__WebAppOrigins__0"; Value = "https://web.simrsnf.com"; Secret = $false; Gate = $false; Apps = "API"; Note = "browser origin allowed to call the API — Website + the Flutter web build (D-868)" }
    [pscustomobject]@{ Name = "SIMF_Cors__WebAppOrigins__1"; Value = "https://cp.simrsnf.com"; Secret = $false; Gate = $false; Apps = "API"; Note = "Control Panel origin. Add __2, __3, … for further origins" }
    [pscustomobject]@{ Name = "SIMF_RateLimit__PermitLimit"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 20" }
    [pscustomobject]@{ Name = "SIMF_RateLimit__WindowSeconds"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 60" }

    # --- Walk-in mode ---------------------------------------------------------
    # STANDBY capability for an event day when a large crowd arrives who never
    # registered online. Everything here defaults to OFF and fails closed: blank
    # means the system behaves exactly as it does today. Arm only when needed,
    # and disarm afterwards. Enabled is the master switch - every other switch is
    # inert without it. ExpiresAt is evaluated on read, so a mode left armed
    # closes itself.
    [pscustomobject]@{ Name = "SIMF_WalkInMode__Enabled"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default false (master switch)" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__ExpiresAt"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "Saudi local ISO-8601; blank = no expiry" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__QuickRegister"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default false - reduced desk field set" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__QuickRegisterRequiresIdentityDocument"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default true - KEEP true; the id number is what makes duplicate detection possible and cannot be reconstructed after the event" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__AutoApprove"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default false - skips the approval queue" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__SessionWalkIn"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default false - admits unregistered attendees to a hall" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__ArrivalGraceMinutes"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 15, clamped 0..240" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__AcceptOfflineBadges"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default false - needs BadgeKey" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__OfflineUpload"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default false" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__AllowBadgeActivation"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default false - leave OFF unless deliberately decided; a badge in open circulation could be photographed and claimed as a full app account" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__BadgeKey"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "base64 32-byte AES key" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__BadgeKeyVersion"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "default 0 (0..31)" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__PreviousBadgeKey"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "accepted during rotation only" }
    [pscustomobject]@{ Name = "SIMF_WalkInMode__PreviousBadgeKeyVersion"; Value = ""; Secret = $false; Gate = $false; Apps = "API"; Note = "the version PreviousBadgeKey matches" }

    # --- Swagger --------------------------------------------------------------
    # Pre-filled false on purpose. If it is ever turned on, the username and
    # password below are the ONLY thing standing in front of the API surface.
    [pscustomobject]@{ Name = "SIMF_Swagger__AllowSwagger"; Value = "false"; Secret = $false; Gate = $false; Apps = "API"; Note = "leave OFF in Production" }
    [pscustomobject]@{ Name = "SIMF_Swagger__Username"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "only if AllowSwagger=true" }
    [pscustomobject]@{ Name = "SIMF_Swagger__Password"; Value = ""; Secret = $true; Gate = $false; Apps = "API"; Note = "only if AllowSwagger=true" }

    # --- Misc -----------------------------------------------------------------
    [pscustomobject]@{ Name = "SIMF_OrganizationHeroVideo__PublicApiBaseUrl"; Value = "https://api.simrsnf.com"; Secret = $false; Gate = $false; Apps = "API"; Note = "public API origin used to build hero-video URLs (D-868)" }
    [pscustomobject]@{ Name = "SIMF_UploadScanning__Enabled"; Value = "true"; Secret = $false; Gate = $false; Apps = "API"; Note = "leave ON in Production" }
)

# -----------------------------------------------------------------------------
# Host timezone
# -----------------------------------------------------------------------------
# The whole system stores and compares Saudi local wall-clock time (SimfClock,
# +03:00, no DST). A host on another timezone is a deployment defect worth
# seeing now rather than discovering from a support ticket: scheduled workers,
# reminder windows and "is this session live" all read a Saudi clock. Reported,
# never changed - altering a server's timezone is the operator's call.
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
        Write-Warning ("SKIP (empty {0}): {1}  [{2}]  {3}" -f $label, $v.Name, $v.Apps, $v.Note)
        if ($v.Gate) { $missingGates += $v.Name }
        $skipped++
        continue
    }
    [Environment]::SetEnvironmentVariable($v.Name, $v.Value, [EnvironmentVariableTarget]::Machine)
    # Never echo the value - only the name.
    Write-Host ("set: {0}  [{1}]" -f $v.Name, $v.Apps)
    $set++
}

Write-Host ""
Write-Host "SIMF env: $set set, $skipped skipped (empty)."

if ($set -eq 0) {
    Write-Warning "Nothing was set - this is the UNEDITED template. Copy it to deploy\set-env.ps1 and fill the values there."
}

if ($missingGates.Count -gt 0) {
    Write-Warning ("The API will REFUSE TO START in Production without: {0}" -f ($missingGates -join ", "))
    Write-Warning "Generate those keys with deploy\configure-prod-env.ps1 (it never prints them and never overwrites an existing one)."
}

Write-Host "Restart the IIS app pools (SimfAPI, SimfCP, SimfWeb) so w3wp picks up the new Machine-scope variables."
