# Page 001 — API (البداية · Splash)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Boot rules are
in [Page_001_Logic.md](Page_001_Logic.md). Last updated 2026-07-10 (D-736 — the launch
update check moved from the never-wired store-native seam to the SIMF version-policy
endpoint E3 below; the owner reversed the old "no SIMF version API" contract).

> **Status:** the splash reuses two **already-shipped** App endpoints for silent
> session resume + identity, plus the **D-736 version-policy read** (E3). No schema
> change, no enum change, no migration (the policy lives in the pre-existing
> `SystemSettings` key/value table).
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split shipped,
> D-247) — so the routes below are `POST /api/v1/app/auth/refresh`,
> `GET /api/v1/app/users/me` and `GET /api/v1/app/version-policy`.

## E3 — `GET /app/version-policy`  (launch update check, D-736)
| | |
|---|---|
| Route | `GET /api/v1/app/version-policy` |
| Access | `AllowAnonymous` (runs before sign-in on every launch); **no** dedicated rate-limit bucket (D-731 — the global per-IP limiter applies) |
| App privilege | None |
| Status | **Exists (D-736).** `GetAppVersionPolicyEndpoint` (`src/Backend/SIMF.Api/Endpoints/Public/AppVersionPolicyEndpoint.cs`). |
| Returns | `ApiResult<AppVersionPolicyResponse>` |
| Source | The six whitelisted `AppUpdateSettingKeys` rows (`appUpdate.{android\|ios}.{minVersion\|latestVersion\|storeUrl}`) in `SystemSettings`, admin-edited on the CP configuration page (`/admin/configuration`); seeded empty by `DefaultContentSeeder`. |

```jsonc
// Response data (AppVersionPolicyResponse) — every field null when unconfigured
{
  "android": {
    "minVersion": "1.0.0",    // installed < min  → forced update (semver)
    "latestVersion": "1.1.0", // installed < latest → dismissible prompt (semver)
    "storeUrl": "https://play.google.com/store/apps/details?id=…" // http(s)-only (D-467)
  },
  "ios": { "minVersion": null, "latestVersion": null, "storeUrl": null }
}
```

Client rules (`ServerAppUpdateChecker`, `lib/core/startup/server_app_update_checker.dart`):
compare semver via `pub_semver` against the installed `package_info_plus` version;
**fail-open** — any fetch/parse error → up-to-date (the 5 s splash cap still applies);
**anti-brick** — `forced`/`optional` require a usable http(s) `storeUrl` (no dead-end
update screens), and a hard block only ever follows a **live** successful fetch, never a
cached policy. A dismissed optional prompt snoozes that version for 3 days (Logic L-2);
the About-the-app manual check reuses this endpoint and ignores the snooze.

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
| `GET /app/version-policy` fails (any error/timeout) | — | any | L-2/L-6: **fail-open** — treated as up-to-date; boot continues normally (D-736) |

## Endpoint summary
This page composes the launch flow from the two shipped session reads above plus the
**D-736 version-policy read** (E3) — one small anonymous GET, no schema change.
