#!/usr/bin/env bash
# Stand up a THROWAWAY QA stack for the element sweep and the E2E catalogue runs.
#
# NEVER points at production. Its own LocalDB databases, its own ports, and SMTP
# aimed at a local sink so a run can never email a real person.
#
#   API  http://localhost:5275
#   CP   http://localhost:5278
#   Web  http://localhost:5280
#
# Usage:
#   dotnet build SIMF.slnx -c Release
#   tools/qa/launch-qa-stack.sh api   # each in its own background shell
#   tools/qa/launch-qa-stack.sh cp
#   tools/qa/launch-qa-stack.sh web
#   tools/qa/launch-qa-stack.sh env   # print the exports for another shell
#
# Then point the browser suite at it:
#   export SIMF_QA_ADMIN_EMAIL=superadmin@simf.test
#   export SIMF_QA_ADMIN_PASSWORD="$SIMF_SuperAdmin__TempPassword"
#   export SIMF_QA_ADMIN_TOTP_SECRET=JBSWY3DPEHPK3PXP
#   dotnet test tests/SIMF.E2E.Tests -c Release
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_ID='Server=(localdb)\MSSQLLocalDB;Database=SIMF_QA_Identity;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
QA_APP='Server=(localdb)\MSSQLLocalDB;Database=SIMF_QA_App;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True'

export ASPNETCORE_ENVIRONMENT=Development
export SIMF_ConnectionStrings__SimfIdentityDb="$QA_ID"
export SIMF_ConnectionStrings__SimfAppDb="$QA_APP"

# SMTP to a local sink. Port 2525, nothing listening = mail simply fails; that is
# the intent. A QA run must not be able to reach a real mailbox.
export SIMF_Email__Host=localhost
export SIMF_Email__Port=2525

# --- QA-only throwaway secrets. None of these is a production value. ---
export SIMF_Jwt__SigningKey='qa-only-throwaway-signing-key-not-a-real-secret-0123456789abcdef'

# Both MUST be base64 decoding to EXACTLY 32 bytes (AES-256).
#
# Two traps here, and each fails differently:
#   * A key containing hyphens is not base64 at all — the API refuses to boot.
#   * A key that IS valid base64 but decodes to 31 or 33 bytes is silently
#     DISCARDED (AesGcmPiiEncryptor leaves its key null and only throws when
#     something first decrypts). The stack then boots, appears healthy, and dies
#     mid-seed inside EnsureDemoVisitorInterestsAsync with a stack trace that
#     names neither the key nor the config setting. A 33-byte key cost a boot
#     cycle to diagnose on 2026-07-29.
# Verify with: python -c "import base64;print(len(base64.b64decode('KEY')))"
export SIMF_FileStorage__EncryptionKey='U0lNRi1RQS10aHJvd2F3YXktZmlsZS1rZWstMzJieXQ='
export SIMF_Storage__UserIdDocumentEncryptionKey='U0lNRi1RQS10aHJvd2F3YXktaWQtZG9jLWtlay0zMmI='

# The super-admin temp password MUST NOT contain a monotonic run of characters.
# The seeder's own policy check rejects one, and it then writes NO super-admin
# while only LOGGING the reason — so the stack comes up healthy and every
# signed-in test fails at the login page for no visible reason. An earlier
# launcher used "QaOnly@Temp12345"; the trailing 12345 is exactly that trap.
export SIMF_SuperAdmin__Email='superadmin@simf.test'
export SIMF_SuperAdmin__TempPassword='Nv8@Kq3Rp6Ws'
export SIMF_SuperAdmin__TotpSecret='JBSWY3DPEHPK3PXP'
export SIMF_SuperAdmin__PasswordChangeRequired=false

export SIMF_UploadScanning__Enabled=false

# CRUD endpoints carry RequireRateLimiting("auth"); a sweep hitting ~100 routes
# in a loop trips it and the failures look like auth defects.
export SIMF_RateLimit__PermitLimit=100000
export SIMF_RateLimit__EmailPermitLimit=100000
export SIMF_RateLimit__GlobalPermitLimit=1000000

case "${1:-}" in
  api)
    cd "$REPO/src/Backend/SIMF.Api/bin/Release/net10.0"
    ASPNETCORE_URLS=http://localhost:5275 exec dotnet SIMF.Api.dll
    ;;
  cp)
    cd "$REPO/src/ControlPanel/SIMF.ControlPanel/bin/Release/net10.0"
    SIMF_Api__BaseUrl=http://localhost:5275/ \
      ASPNETCORE_URLS=http://localhost:5278 exec dotnet SIMF.ControlPanel.dll
    ;;
  web)
    cd "$REPO/src/Website/SIMF.Web/bin/Release/net10.0"
    SIMF_Api__BaseUrl=http://localhost:5275/ \
      ASPNETCORE_URLS=http://localhost:5280 exec dotnet SIMF.Web.dll
    ;;
  env)
    env | grep -E '^(SIMF_|ASPNETCORE_)' | sort
    ;;
  *)
    echo "usage: $0 {api|cp|web|env}" >&2
    exit 2
    ;;
esac
