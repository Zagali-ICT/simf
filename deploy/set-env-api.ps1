# =============================================================================
# SIMF - SimfAPI production environment variables (TEMPLATE - empty values)
#
# Per SIMF-OPS-001 section 6: production overrides and every secret are applied
# as MACHINE-scope environment variables on the server by this per-service
# script. The committed copy is a TEMPLATE with empty values - NEVER commit real
# secret values. Fill the values on the server, run as Administrator, then
# restart the IIS app pool so w3wp picks them up.
#
# Naming: SIMF_ + ASP.NET Core double-underscore convention (SIMF_Section__Key).
# The apps register AddEnvironmentVariables("SIMF_"), which strips the prefix, so
# SIMF_ConnectionStrings__SimfAppDb binds to ConnectionStrings:SimfAppDb.
# EXCEPTION: ASPNETCORE_ENVIRONMENT is read by the host before configuration
# sources load, so it stays UN-prefixed.
#
# Array values bind by index: SIMF_ReverseProxy__KnownProxies__0, __1, __2, ...
#
# Secret generation (SIMF-OPS-001 section B.3):
#   SIMF_Jwt__SigningKey                      : openssl rand -base64 48
#   SIMF_Storage__UserIdDocumentEncryptionKey : openssl rand -base64 32
#   SIMF_Ai__PromptHash__Secret               : openssl rand -base64 32
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

# name -> value. An empty value is SKIPPED (warned), so running the unedited
# template never clobbers a required secret with an empty string.
$vars = [ordered]@{
    "ASPNETCORE_ENVIRONMENT"                       = "Production"  # [REQUIRED] host-level - NOT prefixed

    # --- Databases (SIMF-OPS-001 B.1) ---
    "SIMF_ConnectionStrings__SimfIdentityDb"       = ""  # [REQUIRED][SECRET] Identity DB
    "SIMF_ConnectionStrings__SimfAppDb"            = ""  # [REQUIRED][SECRET] App DB

    # --- JWT ---
    "SIMF_Jwt__SigningKey"                         = ""  # [REQUIRED][SECRET] openssl rand -base64 48
    # SIMF_Jwt__Issuer / __Audience / __AccessTokenMinutes / __StreamAudience /
    # __StreamTokenMinutes have appsettings defaults - override only if needed.

    # --- Email / SMTP ---
    "SIMF_Email__Host"                             = ""  # [REQUIRED]
    "SIMF_Email__Port"                             = ""  # optional (default 587)
    "SIMF_Email__User"                             = ""  # [REQUIRED]
    "SIMF_Email__Password"                         = ""  # [REQUIRED][SECRET]
    "SIMF_Email__FromAddress"                      = ""  # optional (default no-reply@simf.example)
    "SIMF_Email__FromName"                         = ""  # optional (default SIMF)

    # --- Bootstrap super-admin (rotate post first login - SIMF-OPS-001 B.6) ---
    "SIMF_SuperAdmin__Email"                       = ""  # [REQUIRED]
    "SIMF_SuperAdmin__TempPassword"                = ""  # [REQUIRED][SECRET]
    "SIMF_SuperAdmin__TotpSecret"                  = ""  # [REQUIRED][SECRET]

    # --- Filesystem storage ---
    "SIMF_Storage__AvatarBase"                     = ""  # [REQUIRED] validated at startup
    "SIMF_Storage__UserIdDocumentBase"             = ""  # [REQUIRED] encrypted ID-image store
    "SIMF_Storage__UserIdDocumentEncryptionKey"    = ""  # [REQUIRED][SECRET] openssl rand -base64 32
    "SIMF_Storage__LogDirectory"                   = ""  # optional (default logs)

    # --- AI provider ---
    "SIMF_Ai__DefaultProvider"                     = ""  # optional (default Echo; production should set OpenAi)
    "SIMF_Ai__OpenAi__ApiKey"                      = ""  # [SECRET] required if any prompt uses OpenAi
    "SIMF_Ai__OpenAi__BaseUrl"                     = ""  # optional (default https://api.openai.com/v1)
    "SIMF_Ai__OpenAi__DefaultModel"                = ""  # optional (default gpt-4o-mini)
    "SIMF_Ai__PromptHash__Secret"                  = ""  # [REQUIRED for prod][SECRET] openssl rand -base64 32

    # --- Reverse proxy (trusted hops for X-Forwarded-For) ---
    "SIMF_ReverseProxy__KnownProxies__0"           = ""  # [REQUIRED for prod] first trusted proxy IP; add __1, __2 ...

    # --- Rate limits (defaults exist; tighten for a public-facing deploy) ---
    "SIMF_RateLimit__PermitLimit"                  = ""  # optional (default 20)
    "SIMF_RateLimit__WindowSeconds"                = ""  # optional (default 60)

    # --- Media / presentation / recording storage roots ---
    "SIMF_MediaImageStorage__RootPath"             = ""  # optional (default App_Data/media)
    "SIMF_SpeakerPresentationStorage__RootPath"    = ""  # optional (default App_Data/presentations)
    "SIMF_SessionRecordingStorage__RootPath"       = ""  # optional (default App_Data/recordings)
    "SIMF_SessionRecordingStorage__MaxUploadBytes" = ""  # optional (default 1073741824)
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
