# =============================================================================
# SIMF - production environment variables for the API server
#
# ONE script per deployment package, because each package now runs on its OWN
# server. Run this on the SimfAPI box only. It sets nothing that belongs to the
# Control Panel, the Website or the mobile edge, so a compromised or mis-copied
# file on this host cannot carry another host's secrets.
#
# Deployment is therefore:
#
#     1. the pipeline publishes and deploys this package to this server
#     2. an operator runs THIS script here, as Administrator
#     3. restart the SimfAPI app pool
#
# HISTORY, so the split is not undone by accident. Until 2026-08-06 there were
# three per-service scripts; they were merged into one because all three wrote
# to the SAME Machine-scope namespace on the SAME box and overlapped on
# ASPNETCORE_ENVIRONMENT, SIMF_API_Api__BaseUrl and SIMF_API_Storage__LogDirectory, each
# saying "running both is fine, the last writer wins" - true only while the
# copies agree. Separate servers remove that collision entirely: each box has
# its own environment, so there is no last writer. The keys that legitimately
# appear in more than one template are pinned by a test that fails the build if
# the copies disagree, which is a better guard than the merge was.
#
# Naming: SIMF_API_ + ASP.NET Core double-underscore (SIMF_API_Section__Key). Each app
# registers AddEnvironmentVariables("SIMF_API_"), which strips the prefix, so
# SIMF_API_ConnectionStrings__SimfAppDb binds to ConnectionStrings:SimfAppDb.
# ASPNETCORE_ENVIRONMENT is host-level and stays UN-prefixed.
#
# -----------------------------------------------------------------------------
# THIS FILE CARRIES LIVE PRODUCTION SECRETS
# -----------------------------------------------------------------------------
# Both connection strings, the JWT signing key, the two encryption keys, the AI
# prompt-hash secret, the AI provider key and the super-admin password are real
# values in the entries below. Treat the file the way you would treat those
# credentials: it is not a sample, and anything it is copied into inherits them.
#
#     .\deploy\set-env-api.ps1
#
# THIS SCRIPT IS THE WHOLE CONFIGURATION OF THE API SERVER. Every setting the
# host can read is declared below, so nothing is left to an appsettings.json
# default. An empty value is SKIPPED with a warning, so a half-filled script
# cannot blank a working server.
#
# ROTATING A KEY IS NOT AN EDIT HERE. Replacing FileStorage__EncryptionKey or
# Storage__UserIdDocumentEncryptionKey makes everything already encrypted under
# the old key permanently unreadable - every stored file, every national ID /
# Iqama / passport / mobile. A real rotation needs a decrypt-and-re-encrypt
# migration. The runbook that used to generate these keys while refusing to
# overwrite an existing one (configure-prod-env.ps1) has been deleted, so
# nothing mechanical protects them now.
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

# Secret: $true  => never commit, never print, never paste into a ticket.
# Gate  : $true  => THIS app refuses to start in Production without it. The flag
#         is per-app on purpose: a key shared with another template may be a
#         gate there and not here, because it records that host's own boot
#         behaviour rather than a property of the value.
$vars = @(

    # --- Host -----------------------------------------------------------------
    # Not prefixed - read before configuration sources load.
    [pscustomobject]@{ Name = "ASPNETCORE_ENVIRONMENT"; Value = 'Production'; Secret = $false; Gate = $false; Note = "required; the same value on every SIMF server" }

    # --- Databases (D-157: two physically separate databases) -----------------
    # Missing => every request that touches data fails, and the startup migration
    # cannot run (Program.cs migrates App then Identity).
    #
    # The API is the ONLY package that holds these. No presentation project
    # references a DbContext, and the data tier accepts TCP 1433 from the
    # application zone only.
    # Initial Catalog is NOT optional and was not in the string supplied. D-157
    # requires two physically separate databases; without a catalog both contexts
    # open the login's DEFAULT database, so SimfAppDbContext and
    # SimfIdentityDbContext would migrate their schemas into the same one. The
    # names come from this file's previous values and appsettings.Development.json.
    [pscustomobject]@{ Name = "SIMF_API_ConnectionStrings__SimfIdentityDb"; Value = ""; Secret = $true; Gate = $false; Note = "SIMF_Identity - users, roles, permissions, 2FA, tokens" }
    [pscustomobject]@{ Name = "SIMF_API_ConnectionStrings__SimfAppDb"; Value = ""; Secret = $true; Gate = $false; Note = "SIMF_App - everything else" }

    # --- Tokens ---------------------------------------------------------------
    # Missing => no access or refresh token can be signed or validated, so every
    # sign-in fails and every authenticated call is rejected.
    [pscustomobject]@{ Name = "SIMF_API_Jwt__SigningKey"; Value = ""; Secret = $true; Gate = $false; Note = "symmetric signing key" }
    [pscustomobject]@{ Name = "SIMF_API_Jwt__Issuer"; Value = "SIMF"; Secret = $false; Gate = $false; Note = "default SIMF" }
    [pscustomobject]@{ Name = "SIMF_API_Jwt__Audience"; Value = "SIMF"; Secret = $false; Gate = $false; Note = "default SIMF" }
    [pscustomobject]@{ Name = "SIMF_API_Jwt__AccessTokenMinutes"; Value = "5"; Secret = $false; Gate = $false; Note = "default 5 (NCA cap, D-443)" }
    [pscustomobject]@{ Name = "SIMF_API_Jwt__SessionLifetimeHours"; Value = "24"; Secret = $false; Gate = $false; Note = "default 24 (NCA cap, D-443)" }

    # Ops override for the access-token lifetime, in HOURS. Clamped to
    # SessionLifetimeHours, so it can never breach the 24h ceiling. Left empty on
    # purpose: empty keeps the NCA default of 5 minutes. It is listed rather than
    # hidden, because an invisible knob is how a server acquires settings nobody
    # can account for. Note that permission codes are baked into the JWT, so a
    # revoked role keeps working until the token lapses - a longer window is a
    # real security trade, not just convenience.
    #
    # The CP has its OWN cookie idle lifetime, SIMF_CP_Session__LifetimeHours, in
    # set-env-cp.ps1. Two settings under one section name, on two servers; do
    # not confuse them.
    [pscustomobject]@{ Name = "SIMF_API_Session__TimeoutHours"; Value = "0"; Secret = $false; Gate = $false; Note = "empty => 5 min; max = SessionLifetimeHours" }

    # --- Encryption keys (base64, decoding to EXACTLY 32 bytes) ---------------
    # A key that is valid base64 but decodes to 31 or 33 bytes is silently
    # discarded and only fails later, at first decrypt. Verify with:
    #   python -c "import base64;print(len(base64.b64decode('KEY')))"
    #
    # ROTATION WARNING for the two data keys below: changing either strands
    # everything already encrypted with it. Set once, back up, never rotate
    # casually - and back the key up somewhere other than the store it protects,
    # because a backup that loses both together restores nothing.
    [pscustomobject]@{ Name = "SIMF_API_FileStorage__EncryptionKey"; Value = ""; Secret = $true; Gate = $true; Note = "KEK for the centralized file store; rotating it makes every stored file undecryptable" }
    [pscustomobject]@{ Name = "SIMF_API_Storage__UserIdDocumentEncryptionKey"; Value = ""; Secret = $true; Gate = $true; Note = "encrypts the national ID / Iqama / passport / mobile columns at rest (NCA A2-10)" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__PromptHash__Secret"; Value = ""; Secret = $true; Gate = $true; Note = "HMAC secret; the dev fallback is publicly derivable and would poison the AI audit trail" }
    [pscustomobject]@{ Name = "SIMF_API_FileStorage__KekVersion"; Value = "1"; Secret = $false; Gate = $false; Note = "default 1" }

    # --- Filesystem paths -----------------------------------------------------
    # RootPath is the ONE setting that decides where every uploaded avatar, ID
    # document, media image and speaker photo lands. Left unset it falls back to
    # %ProgramData%\SIMF\files - a real location an operator never chose and may
    # not be backing up, which is why it is pre-filled here.
    #
    # Storage:AvatarBase, VipPhotoBase and UserIdDocumentBase were removed on
    # 2026-08-05. They named per-asset stores that the unified file store
    # replaced; setting them now has no effect at all.
    #
    # A LOCAL path only holds while one API node serves every request. With the
    # API tier scaled out, a file uploaded through one node has to be readable
    # from the others, so this becomes a UNC path on the file server
    # (\\fs.simrsnf.local\simf\files) and the pool identity needs write access to
    # the share as well as the folder. Left local on a multi-node tier, uploads
    # succeed and then 404 for the next request that lands elsewhere.
    [pscustomobject]@{ Name = "SIMF_API_FileStorage__RootPath"; Value = "C:\SIMF\Storage\files"; Secret = $false; Gate = $false; Note = "root for ALL uploads; back this up; UNC to the file server once the API tier scales out" }
    [pscustomobject]@{ Name = "SIMF_API_Storage__LogDirectory"; Value = 'C:\SIMF\Storage\logs'; Secret = $false; Gate = $false; Note = "per-app logs under {dir}/SIMF.Api and /SIMF.Workers on this host" }
    [pscustomobject]@{ Name = "SIMF_API_SessionRecordingStorage__MaxUploadBytes"; Value = "1073741824"; Secret = $false; Gate = $false; Note = "default 1073741824 (1 GiB)" }

    # --- Meeting confirmation links -------------------------------------------
    # Empty => the speaker double-opt-in email cannot be built, and the Approve /
    # Resend actions are refused UP FRONT with a bilingual 409
    # MEETING_LINKS_NOT_CONFIGURED before any action token is minted.
    [pscustomobject]@{ Name = "SIMF_API_MeetingLinks__PublicWebBaseUrl"; Value = 'https://web.simrsnf.com'; Secret = $false; Gate = $false; Note = "public Website origin - the speaker follows this link from an email, so it must resolve from OUTSIDE the estate" }
    [pscustomobject]@{ Name = "SIMF_API_MeetingLinks__TokenTtlHours"; Value = "72"; Secret = $false; Gate = $false; Note = "default 72" }

    # --- Bootstrap super admin ------------------------------------------------
    # Missing TempPassword => the super-admin is seeded without a usable password
    # and nobody can sign in to the CP on a fresh database. The API additionally
    # REFUSES TO BOOT in Production if this is left at the old committed default,
    # so it must be a NEW value. It must also avoid a monotonic run of characters
    # such as "12345": the seeder's own policy check rejects one, then writes no
    # super-admin while only LOGGING the reason, so the stack comes up healthy and
    # every sign-in fails at the login page for no visible cause.
    [pscustomobject]@{ Name = "SIMF_API_SuperAdmin__Email"; Value = "superadmin@simrsnf.com"; Secret = $false; Gate = $false; Note = "SITE-SPECIFIC" }
    [pscustomobject]@{ Name = "SIMF_API_SuperAdmin__TempPassword"; Value = ""; Secret = $true; Gate = $false; Note = "rotate after first sign-in; no monotonic runs" }
    # Gate = $true, because Program.cs refuses to start in Production without it:
    # the second factor is not optional for an admin account.
    #
    # Generated 2026-08-16 - 160 random bits, base32, which is what an
    # authenticator app expects. Enrol it once by typing it into the app as a
    # manual key; after that the super-admin's codes come from the app. Changing
    # it invalidates the enrolment and every admin has to re-enrol, so treat it
    # as set-once.
    [pscustomobject]@{ Name = "SIMF_API_SuperAdmin__TotpSecret"; Value = ""; Secret = $true; Gate = $true; Note = "base32 TOTP seed for the admin second factor" }
    [pscustomobject]@{ Name = "SIMF_API_SuperAdmin__PasswordChangeRequired"; Value = "true"; Secret = $false; Gate = $false; Note = "true => the seeded password must be changed at first sign-in" }

    # --- Demo seed ------------------------------------------------------------
    # Empty is the correct PRODUCTION posture: the demo-account seed is skipped
    # with a warning. Set it only in a demo or QA environment.
    [pscustomobject]@{ Name = "SIMF_API_Seed__DemoPassword"; Value = ""; Secret = $true; Gate = $false; Note = "non-production only - leave empty in Production" }

    # --- Outbound email -------------------------------------------------------
    # Missing => no OTP, verification, password-reset or meeting-confirmation
    # mail is delivered, which blocks visitor sign-up and the whole speaker flow.
    [pscustomobject]@{ Name = "SIMF_API_Email__Host"; Value = "smtp.zoho.com"; Secret = $false; Gate = $false; Note = "SMTP host" }
    [pscustomobject]@{ Name = "SIMF_API_Email__Port"; Value = "587"; Secret = $false; Gate = $false; Note = "STARTTLS submission port" }
    # The Zoho account is the mailbox the mail is sent AS, so the user is the
    # same address as FromAddress below - the one tested live against the relay.
    # The password is a Zoho APP password, not the mailbox login password, and it
    # is the one value in this file nobody can derive: generate it in the Zoho
    # console under the no-reply@ammn.com.sa account and paste it here.
    [pscustomobject]@{ Name = "SIMF_API_Email__User"; Value = ""; Secret = $true; Gate = $false; Note = "SMTP user - the Zoho mailbox" }
    [pscustomobject]@{ Name = "SIMF_API_Email__Password"; Value = ""; Secret = $true; Gate = $false; Note = "Zoho APP password for that mailbox - without it no OTP, verification or reset mail is sent" }
    # The From domain must be a VERIFIED sender on the Zoho account. Tested
    # 2026-08-07 against the live relay: no-reply@apexium.com.sa was refused with
    # "Sender is not allowed to relay emails", while no-reply@ammn.com.sa sent
    # fine on the same credentials. The send FAILS - it does not fall back - and
    # on this system that stops sign-up codes, password resets and 2FA. So do not
    # change this until apexium.com.sa is a verified domain in Zoho with its
    # SPF/DKIM records published, then re-run the same test before committing.
    [pscustomobject]@{ Name = "SIMF_API_Email__FromAddress"; Value = "no-reply@ammn.com.sa"; Secret = $false; Gate = $false; Note = "sending address - must be a verified Zoho sender (D-873)" }
    [pscustomobject]@{ Name = "SIMF_API_Email__FromName"; Value = "SIMF"; Secret = $false; Gate = $false; Note = "display name on outbound mail" }

    # --- AI providers ---------------------------------------------------------
    # DefaultProvider stays Echo until a real key is supplied; a feature whose
    # provider has no key returns 503 AI_PROVIDER_NOT_CONFIGURED. Reaching a cloud
    # provider also needs an approved outbound firewall exception - see
    # docs/deploy/SIMF-MobileEdge-Deploy.md for the egress the estate permits.
    [pscustomobject]@{ Name = "SIMF_API_Ai__DefaultProvider"; Value = 'Anthropic'; Secret = $false; Gate = $false; Note = "Echo | Gemini | Anthropic | OpenAi" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__Gemini__ApiKey"; Value = ""; Secret = $true; Gate = $false; Optional = $true; Note = "needed only if DefaultProvider is switched to Gemini" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__Anthropic__ApiKey"; Value = ""; Secret = $true; Gate = $false; Note = "" }
    # Non-secret Anthropic routing. Carried over from the superseded set-env-api.ps1
    # (2026-08-08) - the consolidated template never declared them, so they would
    # have been silently dropped when that file was deleted and the provider would
    # have fallen back to its compiled defaults.
    [pscustomobject]@{ Name = "SIMF_API_Ai__Anthropic__BaseUrl"; Value = "https://api.anthropic.com"; Secret = $false; Gate = $false; Note = "provider endpoint" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__Anthropic__DefaultModel"; Value = "claude-haiku-4-5-20251001"; Secret = $false; Gate = $false; Note = "model id" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__Anthropic__AnthropicVersion"; Value = "2023-06-01"; Secret = $false; Gate = $false; Note = "API version header" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__Anthropic__DefaultMaxTokens"; Value = "2048"; Secret = $false; Gate = $false; Note = "response cap" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__OpenAi__ApiKey"; Value = ""; Secret = $true; Gate = $false; Optional = $true; Note = "needed only if DefaultProvider is switched to OpenAi" }

    # --- Proxy / CORS ---------------------------------------------------------
    # KnownProxies missing => the API sees the reverse proxy's IP as the client
    # IP, so rate limiting and every audit row record the wrong address. It is
    # NOT a gate here: the API still starts and serves, it just attributes badly.
    # On the edge the same key IS a gate, because an unverified X-Forwarded-For
    # there lets any caller spoof its source address into this API's rate limiter.
    #
    # Every hop that fronts this API belongs in the list: the WAF / load balancer,
    # and the mobile edge, whose forwarded requests arrive from the presentation
    # zone rather than from the client.
    # The hop that fronts this API is the mobile edge, named here by its domain
    # at the owner's instruction (2026-08-16).
    #
    # On record, because it is not visible from the value: Program.cs:644 reads
    # each entry through IPAddress.TryParse with no else branch, so a name is
    # dropped rather than resolved, and the boot gate counts config strings
    # rather than parsed addresses. Until that parse also accepts a hostname,
    # this list contributes no trusted proxy and X-Forwarded-For is not honoured.
    [pscustomobject]@{ Name = "SIMF_API_ReverseProxy__KnownProxies__0"; Value = 'edge.simrsnf.com'; Secret = $false; Gate = $false; Note = "the mobile edge (repeat __1, __2, ... for further proxies)" }
    # Every BROWSER origin that calls this API. The Flutter web build is on the
    # list even though its requests arrive through the edge: CORS is decided by
    # the origin in the browser's request, not by the hop it travelled, so
    # webapp.simrsnf.com is refused without an entry of its own.
    [pscustomobject]@{ Name = "SIMF_API_Cors__WebAppOrigins__0"; Value = 'https://web.simrsnf.com'; Secret = $false; Gate = $false; Note = "public Website origin" }
    [pscustomobject]@{ Name = "SIMF_API_Cors__WebAppOrigins__1"; Value = 'https://cp.simrsnf.com'; Secret = $false; Gate = $false; Note = "Control Panel origin" }
    [pscustomobject]@{ Name = "SIMF_API_Cors__WebAppOrigins__2"; Value = 'https://webapp.simrsnf.com'; Secret = $false; Gate = $false; Note = "Flutter web build origin. Add __3, __4, ... for further origins" }
    [pscustomobject]@{ Name = "SIMF_API_RateLimit__PermitLimit"; Value = "20"; Secret = $false; Gate = $false; Note = "default 20" }
    [pscustomobject]@{ Name = "SIMF_API_RateLimit__WindowSeconds"; Value = "60"; Secret = $false; Gate = $false; Note = "default 60" }

    # --- Walk-in mode ---------------------------------------------------------
    # STANDBY capability for an event day when a large crowd arrives who never
    # registered online. Everything here defaults to OFF and fails closed: blank
    # means the system behaves exactly as it does today. Arm only when needed,
    # and disarm afterwards. Enabled is the master switch - every other switch is
    # inert without it. ExpiresAt is evaluated on read, so a mode left armed
    # closes itself.
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__Enabled"; Value = "false"; Secret = $false; Gate = $false; Note = "default false (master switch)" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__ExpiresAt"; Value = ""; Secret = $false; Gate = $false; Optional = $true; Note = "blank = no expiry, which is correct while walk-in mode is off" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__QuickRegister"; Value = "false"; Secret = $false; Gate = $false; Note = "default false - reduced desk field set" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__QuickRegisterRequiresIdentityDocument"; Value = "true"; Secret = $false; Gate = $false; Note = "default true - KEEP true; the id number is what makes duplicate detection possible and cannot be reconstructed after the event" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__AutoApprove"; Value = "false"; Secret = $false; Gate = $false; Note = "default false - skips the approval queue" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__SessionWalkIn"; Value = "false"; Secret = $false; Gate = $false; Note = "default false - admits unregistered attendees to a hall" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__ArrivalGraceMinutes"; Value = "15"; Secret = $false; Gate = $false; Note = "default 15, clamped 0..240" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__AcceptOfflineBadges"; Value = "false"; Secret = $false; Gate = $false; Note = "default false - needs BadgeKey" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__OfflineUpload"; Value = "false"; Secret = $false; Gate = $false; Note = "default false" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__AllowBadgeActivation"; Value = "false"; Secret = $false; Gate = $false; Note = "default false - leave OFF unless deliberately decided; a badge in open circulation could be photographed and claimed as a full app account" }
    # Generated 2026-08-16, base64 decoding to exactly 32 bytes. It signs the
    # offline badges the BadgeDesk tool prints, so every desk's appsettings.json
    # must carry THIS value in its BadgeKey with the same BadgeKeyVersion - a
    # desk on a different key prints badges the gates will not open. Dormant
    # while WalkInMode__AcceptOfflineBadges is false.
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__BadgeKey"; Value = ""; Secret = $true; Gate = $false; Note = "base64 32-byte AES key; copy to every BadgeDesk" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__BadgeKeyVersion"; Value = "0"; Secret = $false; Gate = $false; Note = "default 0 (0..31)" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__PreviousBadgeKey"; Value = ""; Secret = $true; Gate = $false; Optional = $true; Note = "set ONLY during a key rotation, to keep badges signed with the old key working" }
    [pscustomobject]@{ Name = "SIMF_API_WalkInMode__PreviousBadgeKeyVersion"; Value = "0"; Secret = $false; Gate = $false; Note = "the version PreviousBadgeKey matches" }

    # --- Swagger --------------------------------------------------------------
    # Pre-filled false on purpose. If it is ever turned on, the username and
    # password below are the ONLY thing standing in front of the API surface.
    [pscustomobject]@{ Name = "SIMF_API_Swagger__AllowSwagger"; Value = "false"; Secret = $false; Gate = $false; Note = "leave OFF in Production" }
    [pscustomobject]@{ Name = "SIMF_API_Swagger__Username"; Value = ""; Secret = $true; Gate = $false; Optional = $true; Note = "needed only if AllowSwagger is turned on" }
    [pscustomobject]@{ Name = "SIMF_API_Swagger__Password"; Value = ""; Secret = $true; Gate = $false; Optional = $true; Note = "needed only if AllowSwagger is turned on" }

    # --- Misc -----------------------------------------------------------------
    # MUST include the /api/v1 route prefix. OrganizationHeroVideoRoutes.ServedUrl
    # prepends the prefix ONLY on the request-derived fallback; when this value is
    # set it is used verbatim, so a bare origin composes a 404 URL. The upload
    # still reports success and persists that unplayable URL, so it surfaces later
    # as a blank hero rather than as an error at upload time.
    #
    # It must be a PUBLIC address, not this server's own. The composed URL is
    # persisted into OrganizationProfile.BackgroundVideoUrl and then fetched by
    # browsers and by the mobile app, which cannot resolve an internal name. The
    # stream route is /app/organization/hero-video.mp4, which sits on the app
    # surface the edge publishes, so point this at the edge.
    [pscustomobject]@{ Name = "SIMF_API_OrganizationHeroVideo__PublicApiBaseUrl"; Value = 'https://edge.simrsnf.com/api/v1'; Secret = $false; Gate = $false; Note = "PUBLIC API base INCLUDING the /api/v1 prefix; the edge, because clients cannot reach the API's internal name" }
    [pscustomobject]@{ Name = "SIMF_API_OrganizationHeroVideo__MaxUploadBytes"; Value = "209715200"; Secret = $false; Gate = $false; Note = "default 209715200 (200 MiB)" }
    [pscustomobject]@{ Name = "SIMF_API_UploadScanning__Enabled"; Value = "true"; Secret = $false; Gate = $false; Note = "leave ON in Production" }

    # --- Stream tokens --------------------------------------------------------
    # A separate short-lived token for the live-session stream, so a stream URL
    # handed to a player is not the caller's ordinary access token.
    [pscustomobject]@{ Name = "SIMF_API_Jwt__StreamAudience"; Value = "simf-stream"; Secret = $false; Gate = $false; Note = "default simf-stream" }
    [pscustomobject]@{ Name = "SIMF_API_Jwt__StreamTokenMinutes"; Value = "180"; Secret = $false; Gate = $false; Note = "default 180" }

    # --- Device keys ----------------------------------------------------------
    # The app's device-key sign-in. RequireStepUpForEnrol keeps enrolment behind
    # a second factor: with it off, anyone holding a live session can silently
    # add a device key that then signs in on its own.
    [pscustomobject]@{ Name = "SIMF_API_DeviceKey__RequireStepUpForEnrol"; Value = "true"; Secret = $false; Gate = $false; Note = "default true - KEEP true" }
    [pscustomobject]@{ Name = "SIMF_API_DeviceKey__MaxActiveKeysPerUser"; Value = "5"; Secret = $false; Gate = $false; Note = "default 5" }

    # --- Credential lifecycle (NCA A7-13) -------------------------------------
    # Zero means the control is OFF, which is what ships. These are the settings
    # an NCA review asks about, so decide them deliberately rather than leaving
    # them at zero by default: 0 password age = passwords never expire, 0 history
    # = a user may re-use the password they just changed, 0 dormant days =
    # abandoned admin accounts stay enabled forever.
    [pscustomobject]@{ Name = "SIMF_API_IdentityLifecycle__PasswordMaxAgeDays"; Value = "0"; Secret = $false; Gate = $false; Note = "0 = passwords never expire" }
    [pscustomobject]@{ Name = "SIMF_API_IdentityLifecycle__PasswordHistoryCount"; Value = "0"; Secret = $false; Gate = $false; Note = "0 = no re-use check" }
    [pscustomobject]@{ Name = "SIMF_API_IdentityLifecycle__DormantAccountDisableDays"; Value = "0"; Secret = $false; Gate = $false; Note = "0 = dormant accounts are never disabled" }
    [pscustomobject]@{ Name = "SIMF_API_IdentityLifecycle__RequireControlPanelTwoFactorEnrolment"; Value = "true"; Secret = $false; Gate = $false; Note = "default true - KEEP true" }

    # --- Face detection -------------------------------------------------------
    [pscustomobject]@{ Name = "SIMF_API_FaceDetection__Enabled"; Value = "true"; Secret = $false; Gate = $false; Note = "default true" }
    [pscustomobject]@{ Name = "SIMF_API_FaceDetection__MinConfidence"; Value = "0.5"; Secret = $false; Gate = $false; Note = "default 0.5" }

    # --- Non-secret AI routing for the other two providers --------------------
    # Declared even though DefaultProvider is Anthropic, so switching provider is
    # a one-value change here rather than a hunt for endpoints that live in a
    # file this server no longer configures from.
    [pscustomobject]@{ Name = "SIMF_API_Ai__Gemini__BaseUrl"; Value = "https://generativelanguage.googleapis.com"; Secret = $false; Gate = $false; Note = "provider endpoint" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__Gemini__DefaultModel"; Value = "gemini-2.5-flash"; Secret = $false; Gate = $false; Note = "model id" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__OpenAi__BaseUrl"; Value = "https://api.openai.com/v1"; Secret = $false; Gate = $false; Note = "provider endpoint" }
    [pscustomobject]@{ Name = "SIMF_API_Ai__OpenAi__DefaultModel"; Value = "gpt-4o-mini"; Secret = $false; Gate = $false; Note = "model id" }

    # --- Logging levels -------------------------------------------------------
    # Serilog reads the Serilog:* keys (ReadFrom.Configuration) and writes
    # {Storage:LogDirectory}/SIMF.Api/log-{Date}.log, which the CP /admin/logs
    # page reads. Logging:LogLevel is the framework's own section. Debug here is
    # expensive on the API in particular - it logs every request.
    [pscustomobject]@{ Name = "SIMF_API_Serilog__MinimumLevel__Default"; Value = "Information"; Secret = $false; Gate = $false; Note = "Verbose | Debug | Information | Warning | Error | Fatal" }
    [pscustomobject]@{ Name = "SIMF_API_Logging__LogLevel__Default"; Value = "Information"; Secret = $false; Gate = $false; Note = "framework ILogger default level" }

    # Dotted key names bind correctly but are invisible to
    # DeploymentEnvTemplateTests' entry regex, which matches [A-Za-z0-9_] only.
    # They are still set by the loop below.
    [pscustomobject]@{ Name = "SIMF_API_Serilog__MinimumLevel__Override__Microsoft.AspNetCore"; Value = "Warning"; Secret = $false; Gate = $false; Note = "quietens per-request framework chatter" }
    [pscustomobject]@{ Name = "SIMF_API_Logging__LogLevel__Microsoft.AspNetCore"; Value = "Warning"; Secret = $false; Gate = $false; Note = "same, on the framework logger" }

    # --- Host header filtering ------------------------------------------------
    # The API answers on its internal name only. The CP, the Website and the
    # edge all reach it as api.simrsnf.com, so anything arriving under another
    # Host is refused with 400 - add a name here if a health probe uses one.
    [pscustomobject]@{ Name = "SIMF_API_AllowedHosts"; Value = "api.simrsnf.com"; Secret = $false; Gate = $false; Note = "semicolon-separated host list; '*' accepts any Host header" }
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
        # Optional = $true means empty is the CORRECT state - a provider that is
        # not in use, a switch that is off, a rotation key needed only during a
        # rotation. Warning about those buries the empty values that are real
        # gaps, which is the only reason anyone reads this output.
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
Write-Host "SIMF API env: $set set, $skipped skipped (empty)."

if ($set -eq 0) {
    Write-Warning "Nothing was set - every value in this script is empty. Fill them in the `$vars block above and run it again."
}

if ($missingGates.Count -gt 0) {
    Write-Warning ("The API will REFUSE TO START in Production without: {0}" -f ($missingGates -join ", "))
    Write-Warning "Fill them in the `$vars block above. The two encryption keys must be base64 that decodes to EXACTLY 32 bytes, and replacing one that is already in use destroys everything encrypted under it."
}

Write-Host "Restart the SimfAPI app pool so w3wp picks up the new Machine-scope variables."

