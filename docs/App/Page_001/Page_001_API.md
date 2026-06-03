# Page 001 — API (البداية · Splash)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Boot rules are
in [Page_001_Logic.md](Page_001_Logic.md).

> **Status:** **no new endpoint.** The splash reuses two **already-shipped** App
> endpoints for silent session resume + identity. The version/update check is
> **store-native (NOT a SIMF API)** — see the note below. No schema change, no
> enum change, no migration.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split shipped,
> D-247) — so the routes below are `POST /api/v1/app/auth/refresh` and
> `GET /api/v1/app/account/profile`.

## Version / update check — store-native (NO SIMF API)
There is **no** SIMF endpoint for the launch update check. The app queries the **native
app store** (Play Store / App Store in-app-update APIs) for the latest version and the
hard/soft-update decision — see [Page_001_Logic.md](Page_001_Logic.md) L-2. Do **not**
add a SIMF version endpoint; the store is the source of truth.

## E1 — `POST /app/auth/refresh`  (silent session resume)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/refresh` |
| Access | The stored **refresh token** (no access token required); anonymous-callable by design (refresh exchange). |
| App privilege | None required at call time — runs before privilege is known |
| Status | **Exists (shipped).** |
| Returns | `ApiResult<AuthTokens>` |

```jsonc
// Request
{
  "refreshToken": "string"   // from secure local storage (Page_001_Logic L-4)
}
```

```jsonc
// ApiResult<AuthTokens>  (success)
{
  "success": true,
  "data": {
    "accessToken":  "string",   // new JWT — carries the user's roles/privilege claims
    "refreshToken": "string",   // rotated refresh token — persist back to secure storage
    "expiresAtUtc": "2026-09-13T08:00:00Z"
  },
  "error": null
}
```

On failure (expired/revoked/invalid refresh token) the envelope is `success: false` with
an error code below; the app clears the stored session and routes to the signed-out entry.

## E2 — `GET /app/account/profile`  (identity → derive privilege)
| | |
|---|---|
| Route | `GET /api/v1/app/account/profile` |
| Access | Authenticated with the **access token** from E1 (own `sub`). No new permission code. |
| App privilege | Resolved **from** this response (roles → Guest/Visitor/Moderator/Staff) |
| Status | **Exists (shipped).** |
| Returns | `ApiResult<AccountProfile>` |

```jsonc
// ApiResult<AccountProfile>  (success) — shape per the shipped endpoint
{
  "success": true,
  "data": {
    "userId":      "guid",
    "displayName": "string",
    "email":       "string",
    "accountState":"string",   // e.g. Approved / Pending — gates effective privilege
    "roles":       ["string"]  // maps to the app privilege used for route-out (L-5)
  },
  "error": null
}
```

> The exact field set is owned by the shipped endpoint; the splash only needs
> `accountState` + `roles` to derive privilege. If the shape differs from the above,
> the shipped contract wins — this page does not change it.

## Error codes
Standard envelope errors apply (see SIMF-API-001 error model). The splash treats them as:
| Condition | HTTP | Envelope | Splash handling (Page_001_Logic) |
|---|---|---|---|
| Refresh token expired / revoked / invalid | 401 | `success:false` | L-4: clear session → signed-out entry |
| Access token rejected on profile read | 401 | `success:false` | L-4: clear session → signed-out entry |
| Server unreachable / timeout (either call) | — | network error | L-4/L-6: offline-degraded resume on cached identity |
| Server error | 500 | `success:false` | L-6: fall back to entry; never strand on splash |

## No new endpoint
This page introduces **no** new or `(TO BUILD)` SIMF endpoint. It composes the launch flow
from the two shipped reads above plus the **store-native** update check.
