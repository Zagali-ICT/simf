# Page 001 — API (البداية · Splash)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Boot rules are
in [Page_001_Logic.md](Page_001_Logic.md). Last updated 2026-06-13 (conformance pass on
the D-361 as-built; the redesign changed visuals only — the API surface is unchanged).

> **Status:** **no new endpoint.** The splash reuses two **already-shipped** App
> endpoints for silent session resume + identity. The version/update check is
> **store-native (NOT a SIMF API)** — see the note below. No schema change, no
> enum change, no migration.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split shipped,
> D-247) — so the routes below are `POST /api/v1/app/auth/refresh` and
> `GET /api/v1/app/users/me`.

## Version / update check — store-native (NO SIMF API)
There is **no** SIMF endpoint for the launch update check. The app queries the **native
app store** (Play Store / App Store in-app-update APIs) for the latest version and the
hard/soft-update decision — see [Page_001_Logic.md](Page_001_Logic.md) L-2. Do **not**
add a SIMF version endpoint; the store is the source of truth. As-built the active
checker is the pre-launch `NoopAppUpdateChecker` (`lib/core/startup/app_update_checker.dart`),
which always reports up-to-date; the store-plugin implementation is wired at
store-submission time by overriding `appUpdateCheckerProvider`.

## E1 — `POST /app/auth/refresh`  (silent session resume)
| | |
|---|---|
| Route | `POST /api/v1/app/auth/refresh` |
| Access | The stored **refresh token** (no access token required); `AllowAnonymous` by design (refresh exchange), behind the `"auth"` rate-limit policy. |
| App privilege | None required at call time — runs before privilege is known |
| Status | **Exists (shipped).** `RefreshEndpoint` (FastEndpoints). |
| Returns | `ApiResult<AuthTokens>` |
| When called | **Only when the cached access token is missing or expired.** A still-valid cached token skips E1 entirely (fast path — Logic L-4). |

```jsonc
// Request (RefreshRequest)
{
  "refreshToken": "string"   // from secure local storage (Page_001_Logic L-4)
}
```

```jsonc
// ApiResult<AuthTokens>  (success) — the shape the app's TokenPayloadDto decodes
{
  "success": true,
  "data": {
    "accessToken":  "string",        // new JWT
    "refreshToken": "string",        // rotated refresh token — persisted back to secure storage
    "tokenType":    "Bearer",
    "accessTokenExpiresInSeconds": 1800,  // app computes expiry = issuedAt + seconds
    "user": {                        // AuthUser — identity ONLY, no app-role
      "id":          "guid",
      "email":       "string",
      "displayName": "string"
    }
  },
  "error": null
}
```

On failure (expired/revoked/invalid refresh token) the envelope is `success: false` with
an error code below; the app clears the stored session and routes to the signed-out entry.

## E2 — `GET /app/users/me`  (identity → derive privilege)
| | |
|---|---|
| Route | `GET /api/v1/app/users/me` |
| Access | Authenticated with the **access token** (own `sub`). Available to any signed-in account, including not-yet-approved ones. No new permission code. |
| App privilege | Resolved **from** this response (`appRole` → Visitor/Moderator/Staff; an absent/unknown value falls back to **Guest** app-side) |
| Status | **Exists (shipped, D-249).** `CurrentUserEndpoint` (FastEndpoints). |
| Returns | `ApiResult<CurrentUserResponse>` |
| When called | After every successful restore — on the fast path it is **best-effort** (a failure is swallowed and the cached identity kept, Logic L-4). |

```jsonc
// ApiResult<CurrentUserResponse>  (success) — the shape the app's CurrentUserDto decodes
{
  "success": true,
  "data": {
    "id":                 "guid",
    "email":              "string",
    "displayName":        "string",
    "appRole":            "Visitor",     // wire values: Visitor/Moderator/Staff — drives route-out (L-5)
    "preferredLanguage":  "ar",
    "registrationStatus": "Pending",     // Pending/Approved/Rejected — gates effective access
    "avatarUrl":          "string|null",
    "profileComplete":    false          // D-374 — server-computed; false routes the splash to the profile form (L-5)
  },
  "error": null
}
```

> **Why this read, and not the token payload.** The `POST /app/auth/refresh` (E1)
> response embeds only `AuthUser` (`id` + `email` + `displayName`) — it carries
> **no** app-role, registration status or profile-complete flag. So after the
> silent restore the auth controller calls `GET /app/users/me` (the full
> `CurrentUserResponse`) to derive the **authoritative** privilege before
> route-out; without it an approved Visitor/Moderator/Staff would default to
> Guest/Pending. The `profileComplete` flag from this read also drives the
> splash's add-profile-first gate (D-374 — Logic L-5). (Earlier drafts named
> `GET /app/account/profile` here; the app standardised on `/app/users/me`, the
> privilege-bearing read built for the mobile app in D-249.)

## Error codes
Standard envelope errors apply (see SIMF-API-001 error model). The splash treats them as:
| Condition | HTTP | Envelope | Splash handling (Page_001_Logic) |
|---|---|---|---|
| Refresh token expired / revoked / invalid | 401 | `success:false` | L-4: clear session → signed-out entry |
| Server unreachable / timeout on refresh | — | network error | L-4/L-6: offline-degraded resume on the cached identity; with no cached identity → signed-out entry |
| `GET /app/users/me` fails (any wire error) | 401/5xx | `success:false` | L-4: swallowed — the restored/cached session is kept; the next protected call surfaces it |
| Server error on refresh | 500 | `success:false` | L-4: treated like an invalid refresh → clear session → signed-out entry; never strand on splash |

## No new endpoint
This page introduces **no** new or `(TO BUILD)` SIMF endpoint. It composes the launch flow
from the two shipped reads above plus the **store-native** update check.
