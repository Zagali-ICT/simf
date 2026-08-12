#!/usr/bin/env bash
# D-111 smoke test — admin-create flows + visitor self-signup lifecycle.
# Drives the live API on :5175. Captures evidence in /tmp/smoke-*.json.
set -e

API=${SIMF_SMOKE_API:-http://localhost:5175}
TOTP_TOOL_DIR="${SIMF_SMOKE_TOTP_TOOL_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../totp" && pwd)}"

# Credentials come from the ENVIRONMENT. They used to be three literals right
# here: a super-admin email, its password, and its TOTP seed -- a complete first
# AND second factor for the production super-admin identity, committed to a
# tracked file in every clone of this repository. Removed 2026-07-30.
#
# Fail loudly and immediately if they are absent. A smoke script that silently
# runs unauthenticated reports green while proving nothing, which is worse than
# not running at all.
: "${SIMF_SMOKE_EMAIL:?set SIMF_SMOKE_EMAIL (do not hardcode it -- see the header of this script)}"
: "${SIMF_SMOKE_PASSWORD:?set SIMF_SMOKE_PASSWORD (do not hardcode it)}"
: "${SIMF_SMOKE_TOTP_SECRET:?set SIMF_SMOKE_TOTP_SECRET (do not hardcode it)}"
SUPER_TOTP_SECRET="$SIMF_SMOKE_TOTP_SECRET"
RUN_ID=$(date -u +%Y%m%d-%H%M%S)
LOG="/tmp/smoke-$RUN_ID.log"

log() { echo "[$(date -u +%H:%M:%S)] $*" | tee -a "$LOG"; }

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
totp() { cd "$TOTP_TOOL_DIR" && dotnet run --no-build -- "$1" 2>/dev/null | tail -1; cd - >/dev/null; }

super_jwt() {
  local body mfa code
  body=$(curl -s -X POST "$API/api/v1/auth/sign-in" -H 'Content-Type: application/json' \
    -d "$(python -c 'import json,os; print(json.dumps({"email":os.environ["SIMF_SMOKE_EMAIL"],"password":os.environ["SIMF_SMOKE_PASSWORD"],"audience":1}))')")
  mfa=$(echo "$body" | python -c "import sys,json; print(json.load(sys.stdin)['data']['mfaToken'])")
  code=$(totp "$SUPER_TOTP_SECRET")
  body=$(curl -s -X POST "$API/api/v1/auth/verify-totp" -H 'Content-Type: application/json' \
    -d "{\"mfaToken\":\"$mfa\",\"code\":\"$code\"}")
  echo "$body" | python -c "import sys,json; print(json.load(sys.stdin)['data']['accessToken'])"
}

# ---------------------------------------------------------------------------
# Capture super-admin JWT
# ---------------------------------------------------------------------------
log "Acquiring super-admin JWT..."
ADMIN_JWT=$(super_jwt)
echo "$ADMIN_JWT" > /tmp/smoke-admin-jwt
log "Admin JWT length: $(echo -n "$ADMIN_JWT" | wc -c)"

# ---------------------------------------------------------------------------
# Part A — Admin creates an Admin / a Visitor / an Other via CP-equivalent API
# ---------------------------------------------------------------------------
log "============================================================"
log "PART A: super-admin creates admin/visitor/other (CP path)"
log "============================================================"

CREATE_ADMIN_EMAIL="smoke-admin-$RUN_ID@simf.test"
CREATE_VISITOR_EMAIL="smoke-visitor-$RUN_ID@simf.test"
CREATE_OTHER_EMAIL="smoke-other-$RUN_ID@simf.test"

log "[A1] Create admin: $CREATE_ADMIN_EMAIL"
R=$(curl -s -X POST "$API/api/v1/admin/admins" \
  -H "Authorization: Bearer $ADMIN_JWT" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$CREATE_ADMIN_EMAIL\",\"displayName\":\"Smoke Admin\",\"roles\":[\"Administrator\"]}")
echo "$R" > /tmp/smoke-create-admin.json
A_USER_ID=$(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin)['data']['userId'])")
log "[A1] admin user id = $A_USER_ID"

log "[A2] Create visitor: $CREATE_VISITOR_EMAIL"
R=$(curl -s -X POST "$API/api/v1/admin/visitors" \
  -H "Authorization: Bearer $ADMIN_JWT" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$CREATE_VISITOR_EMAIL\",\"displayName\":\"Smoke Visitor\"}")
echo "$R" > /tmp/smoke-create-visitor.json
V_USER_ID=$(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin)['data']['userId'])")
log "[A2] visitor user id = $V_USER_ID"

log "[A3] Create other: $CREATE_OTHER_EMAIL"
OTHER_PROFILE_TYPE_ID=$(sqlcmd -S . -d SIMF -Q "SET NOCOUNT ON; SELECT TOP 1 CAST(Id AS NVARCHAR(64)) FROM ProfileTypes WHERE UserType='Other' AND IsActive=1" -h -1 2>&1 | tr -d ' \r\n')
R=$(curl -s -X POST "$API/api/v1/admin/others" \
  -H "Authorization: Bearer $ADMIN_JWT" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$CREATE_OTHER_EMAIL\",\"displayName\":\"Smoke Other\",\"profileTypeId\":\"$OTHER_PROFILE_TYPE_ID\"}")
echo "$R" > /tmp/smoke-create-other.json
O_USER_ID=$(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin)['data']['userId'])")
log "[A3] other user id = $O_USER_ID"

# ---------------------------------------------------------------------------
# Part B — Visitor self-signup (Flutter API simulation)
# ---------------------------------------------------------------------------
log "============================================================"
log "PART B: visitor self-signup full lifecycle (Flutter sim)"
log "============================================================"

FLUTTER_EMAIL="smoke-flutter-$RUN_ID@simf.test"
FLUTTER_PWD="Visitor#Sm0ke!"

log "[B1] POST /auth/sign-up"
R=$(curl -s -X POST "$API/api/v1/auth/sign-up" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$FLUTTER_EMAIL\",\"password\":\"$FLUTTER_PWD\",\"confirmPassword\":\"$FLUTTER_PWD\"}")
echo "$R" > /tmp/smoke-signup.json
log "[B1] sign-up status: $(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin).get('success'))")"

log "[B2] Read verification code from DB; POST /auth/verify-email"
sleep 1
VCODE=$(sqlcmd -S . -d SIMF -Q "SET NOCOUNT ON; SELECT TOP 1 ac.Code FROM AccountCodes ac JOIN Users u ON u.Id=ac.UserId WHERE u.Email='$FLUTTER_EMAIL' AND ac.Purpose='EmailVerification' AND ac.ConsumedAt IS NULL ORDER BY ac.CreatedAt DESC" -h -1 2>&1 | tr -d ' \r\n' | head -c 6)
log "[B2] verification code = $VCODE"
R=$(curl -s -X POST "$API/api/v1/auth/verify-email" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$FLUTTER_EMAIL\",\"code\":\"$VCODE\"}")
echo "$R" > /tmp/smoke-verify.json
log "[B2] verify-email success: $(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin).get('success'))")"

log "[B3] Sign in (Web audience, no 2FA yet)"
R=$(curl -s -X POST "$API/api/v1/auth/sign-in" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$FLUTTER_EMAIL\",\"password\":\"$FLUTTER_PWD\",\"audience\":0}")
echo "$R" > /tmp/smoke-signin-web.json
WEB_JWT=$(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin)['data']['tokens']['accessToken'])")
FLUTTER_USER_ID=$(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin)['data']['tokens']['user']['id'])")
log "[B3] visitor user id = $FLUTTER_USER_ID, JWT captured"

log "[B4] Seed an Interest via super-admin so the profile upsert validator passes"
R=$(curl -s -X POST "$API/api/v1/admin/interests" \
  -H "Authorization: Bearer $ADMIN_JWT" -H 'Content-Type: application/json' \
  -d "{\"name\":\"Smoke Interest $RUN_ID\",\"nameArabic\":\"اهتمام\",\"displayOrder\":1,\"isActive\":true}")
echo "$R" > /tmp/smoke-interest.json
INTEREST_ID=$(echo "$R" | python -c "import sys,json; d=json.load(sys.stdin); print(d['data']['id'] if d.get('data') else d.get('error',{}).get('code','no-id'))")
log "[B4] interest id = $INTEREST_ID"

log "[B5] POST /account/user-profile (upsert) — auto-transitions to PendingApproval"
R=$(curl -s -X POST "$API/api/v1/account/user-profile" \
  -H "Authorization: Bearer $WEB_JWT" -H 'Content-Type: application/json' \
  -d "{\"interestIds\":[\"$INTEREST_ID\"],\"arabicName\":\"زائر تجريبي\",\"englishName\":\"Smoke Flutter\",\"nationalityCode\":\"SA\",\"dateOfBirth\":\"1990-01-01\",\"placeOfBirth\":\"Riyadh\",\"isSaudi\":true,\"nationalId\":\"1234567890\"}")
echo "$R" > /tmp/smoke-upsert.json
log "[B5] upsert success: $(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin).get('success'))")"

log "[B6] Admin approves the visitor (re-using the JWT from start)"
R=$(curl -s -X POST "$API/api/v1/admin/visitors/$FLUTTER_USER_ID/approve" \
  -H "Authorization: Bearer $ADMIN_JWT" -H 'Content-Type: application/json' -d '{}')
echo "$R" > /tmp/smoke-approve.json
log "[B6] approve success: $(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin).get('success'))")"

log "[B7] Visitor signs in on the APP audience — verify token carries Approved + Visitor"
R=$(curl -s -X POST "$API/api/v1/auth/sign-in" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$FLUTTER_EMAIL\",\"password\":\"$FLUTTER_PWD\",\"audience\":2}")
echo "$R" > /tmp/smoke-signin-app.json
APP_JWT=$(echo "$R" | python -c "import sys,json; print(json.load(sys.stdin)['data']['tokens']['accessToken'])")
log "[B7] App JWT captured (length $(echo -n "$APP_JWT" | wc -c))"

log "[B8] Decode App-audience JWT claims"
python -c "
import base64,json,sys
jwt='$APP_JWT'
payload=jwt.split('.')[1]
payload += '=' * (-len(payload) % 4)
claims=json.loads(base64.urlsafe_b64decode(payload))
for k in ['email','account_state','user_type']:
    print(f'  jwt.{k} = {claims.get(k)}')
"

log "[B9] Visitor reads their own notifications via /account/notifications/list"
R=$(curl -s -X POST "$API/api/v1/account/notifications/list" \
  -H "Authorization: Bearer $APP_JWT" -H 'Content-Type: application/json' \
  -d '{"top":50,"skip":0}')
echo "$R" > /tmp/smoke-notifications.json
echo "$R" | python -c "
import sys,json
d=json.load(sys.stdin)
items=d['data']['items']
print(f'  visitor has {len(items)} notifications:')
for i in items:
    print(f'    - {i[\"kind\"]:<40} \"{i[\"title\"]}\"  isRead={i[\"isRead\"]}')
"

log "============================================================"
log "SMOKE COMPLETE — see DB + /tmp/smoke-*.json for full evidence"
log "============================================================"
echo "RUN_ID=$RUN_ID"
echo "CREATE_ADMIN_USER_ID=$A_USER_ID"
echo "CREATE_VISITOR_USER_ID=$V_USER_ID"
echo "CREATE_OTHER_USER_ID=$O_USER_ID"
echo "FLUTTER_USER_ID=$FLUTTER_USER_ID"
