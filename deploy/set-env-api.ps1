# =============================================================================
# SIMF - SimfAPI production environment variables (TEMPLATE - empty values)
#
# Per SIMF-OPS-001 section 6: production overrides and every secret are applied
# as MACHINE-scope environment variables on the server by this per-service
# script. The committed copy is a TEMPLATE with empty values - NEVER commit real
# secret values. Fill the values on the server, run as Administrator, then
# restart the IIS app pool so w3wp picks them up.
#
# Naming: ASP.NET Core no-prefix double-underscore convention (Section__Key).
# The API uses the default host configuration (WebApplication.CreateBuilder), so
# there is NO custom env-var prefix - do not prefix names with "SIMF_".
#
# Array values bind by index: ReverseProxy__KnownProxies__0, __1, __2, ...
#
# Secret generation (SIMF-OPS-001 section B.3):
#   Jwt__SigningKey                      : openssl rand -base64 48
#   Storage__UserIdDocumentEncryptionKey : openssl rand -base64 32
#   Ai__PromptHash__Secret               : openssl rand -base64 32
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

# name -> value. An empty value is SKIPPED (warned), so running the unedited
# template never clobbers a required secret with an empty string.
$vars = [ordered]@{
    "ASPNETCORE_ENVIRONMENT"                  = "Production"  # [REQUIRED]

    # --- Databases (SIMF-OPS-001 B.1) ---
    "ConnectionStrings__SimfIdentityDb"       = ""  # [REQUIRED][SECRET] Identity DB
    "ConnectionStrings__SimfAppDb"            = ""  # [REQUIRED][SECRET] App DB

    # --- JWT ---
    "Jwt__SigningKey"                         = ""  # [REQUIRED][SECRET] openssl rand -base64 48
    # Jwt__Issuer / Jwt__Audience / Jwt__AccessTokenMinutes / Jwt__StreamAudience
    # / Jwt__StreamTokenMinutes have appsettings defaults - override only if needed.

    # --- Email / SMTP ---
    "Email__Host"                             = ""  # [REQUIRED]
    "Email__Port"                             = ""  # optional (default 587)
    "Email__User"                             = ""  # [REQUIRED]
    "Email__Password"                         = ""  # [REQUIRED][SECRET]
    "Email__FromAddress"                      = ""  # optional (default no-reply@simf.example)
    "Email__FromName"                         = ""  # optional (default SIMF)

    # --- Bootstrap super-admin (rotate post first login - SIMF-OPS-001 B.6) ---
    "SuperAdmin__Email"                       = ""  # [REQUIRED]
    "SuperAdmin__TempPassword"                = ""  # [REQUIRED][SECRET]
    "SuperAdmin__TotpSecret"                  = ""  # [REQUIRED][SECRET]

    # --- Filesystem storage ---
    "Storage__AvatarBase"                     = ""  # [REQUIRED] validated at startup
    "Storage__UserIdDocumentBase"             = ""  # [REQUIRED] encrypted ID-image store
    "Storage__UserIdDocumentEncryptionKey"    = ""  # [REQUIRED][SECRET] openssl rand -base64 32
    "Storage__LogDirectory"                   = ""  # optional (default logs)

    # --- AI provider ---
    "Ai__DefaultProvider"                     = ""  # optional (default Echo; production should set OpenAi)
    "Ai__OpenAi__ApiKey"                      = ""  # [SECRET] required if any prompt uses OpenAi
    "Ai__OpenAi__BaseUrl"                     = ""  # optional (default https://api.openai.com/v1)
    "Ai__OpenAi__DefaultModel"                = ""  # optional (default gpt-4o-mini)
    "Ai__PromptHash__Secret"                  = ""  # [REQUIRED for prod][SECRET] openssl rand -base64 32

    # --- Reverse proxy (trusted hops for X-Forwarded-For) ---
    "ReverseProxy__KnownProxies__0"           = ""  # [REQUIRED for prod] first trusted proxy IP; add __1, __2 ...

    # --- Rate limits (defaults exist; tighten for a public-facing deploy) ---
    "RateLimit__PermitLimit"                  = ""  # optional (default 20)
    "RateLimit__WindowSeconds"                = ""  # optional (default 60)

    # --- Media / presentation / recording storage roots ---
    "MediaImageStorage__RootPath"             = ""  # optional (default App_Data/media)
    "SpeakerPresentationStorage__RootPath"    = ""  # optional (default App_Data/presentations)
    "SessionRecordingStorage__RootPath"       = ""  # optional (default App_Data/recordings)
    "SessionRecordingStorage__MaxUploadBytes" = ""  # optional (default 1073741824)
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
Write-Host "Restart the IIS app pool (or the server) so w3wp picks up the new Machine-scope variables."
