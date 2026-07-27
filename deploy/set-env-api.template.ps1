# =============================================================================
# SIMF - SimfAPI production environment variables (TEMPLATE)
#
# Per SIMF-OPS-001 section 6: MACHINE-scope environment variables, set on the
# server by this per-service script. Committed copy is a TEMPLATE with empty
# values - NEVER commit real values. Fill on the server, run as Administrator,
# then restart the IIS app pool.
#
# Naming: SIMF_ + ASP.NET Core double-underscore (SIMF_Section__Key). The app
# registers AddEnvironmentVariables("SIMF_"), which strips the prefix, so
# SIMF_ConnectionStrings__SimfAppDb binds to ConnectionStrings:SimfAppDb.
# ASPNETCORE_ENVIRONMENT is host-level and stays UN-prefixed.
# Note: Machine-scope variables are shared by every app on the box, so
# ASPNETCORE_ENVIRONMENT is common to SimfAPI, SimfCP and SimfWeb.
#
# -----------------------------------------------------------------------------
# WHY THIS FILE IS A SEPARATE ".template.ps1"
# -----------------------------------------------------------------------------
# deploy/set-env-api.ps1 is the FILLED overlay carrying real production secrets
# and is deliberately untracked (.gitignore line 9). Do NOT remove that ignore
# rule to "share the script" - that would commit live credentials. This template
# is the tracked, safe counterpart: copy it to set-env-api.ps1 on the server,
# fill it in there, and it stays out of git.
#
#     Copy-Item .\deploy\set-env-api.template.ps1 .\deploy\set-env-api.ps1
#     # edit set-env-api.ps1, then:
#     .\deploy\set-env-api.ps1
#
# Every value below is EMPTY, and an empty value is SKIPPED with a warning, so
# running this template UNEDITED sets nothing and cannot blank a working server.
# Generate the random keys with deploy\configure-prod-env.ps1 (it creates the
# base64 32-byte AES keys for you and never prints them).
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

# An empty value is SKIPPED (warned) so the unedited template never sets blanks.
# [REQUIRED] = the API will not work correctly without it.
# [BOOT GATE] = the API REFUSES TO START in Production without it.
# [SECRET]   = never commit, never print, never paste into a ticket.
$vars = [ordered]@{

    # --- Host -----------------------------------------------------------------
    # NOT prefixed - read before configuration sources load.
    "ASPNETCORE_ENVIRONMENT"                    = ""   # [REQUIRED] "Production"

    # --- Databases (D-157: two physically separate databases) -----------------
    # Missing => every request that touches data fails; migrations cannot run at
    # startup (Program.cs migrates App then Identity).
    "SIMF_ConnectionStrings__SimfIdentityDb"    = ""   # [REQUIRED][SECRET] SIMF_Identity - users, roles, permissions, 2FA, tokens
    "SIMF_ConnectionStrings__SimfAppDb"         = ""   # [REQUIRED][SECRET] SIMF_App - everything else

    # --- Tokens ---------------------------------------------------------------
    # Missing => no access/refresh token can be signed or validated, so every
    # sign-in fails and every authenticated call is rejected.
    "SIMF_Jwt__SigningKey"                      = ""   # [REQUIRED][SECRET] symmetric signing key
    "SIMF_Jwt__Issuer"                          = ""   # default "SIMF"
    "SIMF_Jwt__Audience"                        = ""   # default "SIMF"
    "SIMF_Jwt__AccessTokenMinutes"              = ""   # default 5   (NCA cap, D-443)
    "SIMF_Jwt__SessionLifetimeHours"            = ""   # default 24  (NCA cap, D-443)

    # --- Encryption keys (base64 32-byte AES) ---------------------------------
    # SIMF_FileStorage__EncryptionKey missing => THE API DOES NOT BOOT. It is the
    # KEK for the centralized file store (D-568). Two guards fire: the Production
    # boot gate EnsureFileStorageEncryptionConfigured
    # (SIMF.Infrastructure/DependencyInjection.cs:69) and, in any environment as
    # soon as the cipher is constructed, AesGcmEnvelopeCipher
    # (SIMF.Infrastructure/Files/AesGcmEnvelopeCipher.cs:158) with:
    #   InvalidOperationException: Configuration value 'FileStorage:EncryptionKey'
    #   is required but was not found. Provide a base64-encoded 32-byte AES key.
    # ROTATION WARNING: changing this key makes every ALREADY-STORED file
    # undecryptable. Set it once, back it up, never rotate it casually.
    "SIMF_FileStorage__EncryptionKey"           = ""   # [BOOT GATE][SECRET] base64 32-byte AES KEK
    "SIMF_FileStorage__RootPath"                = ""   # default App_Data/files
    "SIMF_FileStorage__KekVersion"              = ""   # default 1

    # Missing => the API refuses to start in Production
    # (EnsurePiiEncryptionConfigured, DependencyInjection.cs:52); it encrypts the
    # UserProfile national ID / Iqama / passport / mobile columns at rest
    # (NCA A2-10). Same rotation warning: rotating it strands existing PII.
    "SIMF_Storage__UserIdDocumentEncryptionKey" = ""   # [BOOT GATE][SECRET] base64 32-byte AES key

    # Missing => the API refuses to start in Production
    # (EnsureAiPromptHashSecretConfigured, DependencyInjection.cs:36) because the
    # dev-fallback HMAC key is publicly derivable, which would poison the AI
    # audit trail.
    "SIMF_Ai__PromptHash__Secret"               = ""   # [BOOT GATE][SECRET] base64 32-byte HMAC secret

    # --- Filesystem paths -----------------------------------------------------
    # Storage:AvatarBase is validated with ValidateOnStart
    # (DependencyInjection.cs:207) => missing means the host fails to build.
    "SIMF_Storage__AvatarBase"                  = ""   # [BOOT GATE] e.g. C:\SIMF\Storage\avatars
    "SIMF_Storage__VipPhotoBase"                = ""   # optional - defaults to a vip-photos sibling of AvatarBase
    "SIMF_Storage__UserIdDocumentBase"          = ""   # [REQUIRED] e.g. C:\SIMF\Storage\visitor-ids
    "SIMF_Storage__LogDirectory"                = ""   # [REQUIRED] e.g. C:\SIMF\Storage\logs - per-app logs under {dir}/SIMF.Api/ and {dir}/SIMF.Workers/
    "SIMF_SessionRecordingStorage__MaxUploadBytes" = "" # default 1073741824 (1 GiB) - the recording upload ceiling

    # --- Meeting confirmation links (D-717) -----------------------------------
    # EMPTY => the speaker double-opt-in email cannot be built. On THIS build the
    # Approve / Resend actions are REFUSED UP FRONT with a bilingual 409
    # MEETING_LINKS_NOT_CONFIGURED (QA A24 -
    # SpeakerMeetingRequestService.EnsureSpeakerConfirmationIsDeliverableAsync,
    # line 749), which runs BEFORE any action token is minted. The older
    # behaviour - silently skipping the email while the tokens were still minted,
    # leaving the request parked in AwaitingSpeaker until it expired - is gone;
    # the residual no-URL branch now logs at ERROR (line 715) instead.
    "SIMF_MeetingLinks__PublicWebBaseUrl"       = ""   # [REQUIRED] public Website origin, e.g. https://simf.example.sa (no trailing slash needed)
    "SIMF_MeetingLinks__TokenTtlHours"          = ""   # default 72

    # --- Bootstrap super admin ------------------------------------------------
    # Missing TempPassword => the bootstrap super-admin is seeded without a
    # usable password and nobody can sign in to the Control Panel on a fresh DB.
    # Program.cs additionally REFUSES TO BOOT in Production if this is left at
    # the old committed default, so it must be a NEW value.
    "SIMF_SuperAdmin__Email"                    = ""   # default superadmin@zagali-ict.com
    "SIMF_SuperAdmin__TempPassword"             = ""   # [REQUIRED][SECRET] bootstrap password - rotate after first sign-in
    "SIMF_SuperAdmin__TotpSecret"               = ""   # [REQUIRED][SECRET] base32 TOTP seed for the admin second factor

    # --- Demo seed ------------------------------------------------------------
    # EMPTY => the demo-account seed is SKIPPED with a warning
    # (IdentitySeeder.cs:641). That is the correct production posture: leave it
    # empty in Production, set it only in a demo / QA environment.
    "SIMF_Seed__DemoPassword"                   = ""   # [SECRET] non-production only

    # --- Outbound email -------------------------------------------------------
    # Missing => no OTP, verification, password-reset or meeting-confirmation
    # email is delivered, which blocks visitor sign-up and the speaker flow.
    "SIMF_Email__Host"                          = ""   # [REQUIRED] SMTP host
    "SIMF_Email__Port"                          = ""   # default 587
    "SIMF_Email__User"                          = ""   # [REQUIRED][SECRET]
    "SIMF_Email__Password"                      = ""   # [REQUIRED][SECRET] SMTP app password
    "SIMF_Email__FromAddress"                   = ""   # e.g. no-reply@simf.example
    "SIMF_Email__FromName"                      = ""   # default "SIMF"

    # --- AI providers ---------------------------------------------------------
    # DefaultProvider stays "Echo" until a real key is supplied; a feature whose
    # provider has no key returns 503 AI_PROVIDER_NOT_CONFIGURED.
    "SIMF_Ai__DefaultProvider"                  = ""   # Echo | Gemini | Anthropic | OpenAi
    "SIMF_Ai__Gemini__ApiKey"                   = ""   # [SECRET]
    "SIMF_Ai__Anthropic__ApiKey"                = ""   # [SECRET]
    "SIMF_Ai__OpenAi__ApiKey"                   = ""   # [SECRET]

    # --- Edge / proxy / CORS --------------------------------------------------
    # KnownProxies missing => the API sees the reverse proxy's IP as the client
    # IP, so rate limiting and audit rows record the wrong address.
    "SIMF_ReverseProxy__KnownProxies__0"        = ""   # reverse-proxy IP (repeat __1, __2, ... as needed)
    "SIMF_Cors__WebAppOrigins__0"               = ""   # browser origin allowed to call the API (repeat __1, ...)
    "SIMF_RateLimit__PermitLimit"               = ""   # default 20
    "SIMF_RateLimit__WindowSeconds"             = ""   # default 60

    # --- Swagger --------------------------------------------------------------
    # Keep AllowSwagger empty/false in Production. If it is ever enabled, the
    # username + password below are the ONLY thing in front of the API surface.
    "SIMF_Swagger__AllowSwagger"                = ""   # default false - leave OFF in Production
    "SIMF_Swagger__Username"                    = ""   # [SECRET] only if AllowSwagger=true
    "SIMF_Swagger__Password"                    = ""   # [SECRET] only if AllowSwagger=true

    # --- Misc -----------------------------------------------------------------
    "SIMF_OrganizationHeroVideo__PublicApiBaseUrl" = "" # public API origin used to build hero-video URLs
    "SIMF_UploadScanning__Enabled"              = ""   # default true - leave ON in Production
}

$set = 0
$skipped = 0
foreach ($name in $vars.Keys) {
    $value = $vars[$name]
    if ([string]::IsNullOrWhiteSpace($value)) {
        Write-Warning "SKIP (empty): $name  - fill this in before a real deploy"
        $skipped++
        continue
    }
    [Environment]::SetEnvironmentVariable($name, $value, [EnvironmentVariableTarget]::Machine)
    Write-Host "set: $name"
    $set++
}

Write-Host ""
Write-Host "SimfAPI env: $set set, $skipped skipped (empty)."
if ($set -eq 0) {
    Write-Warning "Nothing was set - this is the UNEDITED template. Copy it to set-env-api.ps1 and fill the values there."
}
Write-Host "Restart the IIS app pool (or the server) so w3wp picks up the new Machine-scope variables."
