# Page 011 — API (حالة التسجيل · Registration status)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. The
status-mapping and state rules are in [Page_011_Logic.md](Page_011_Logic.md).

> **Status:** **BUILT (D-249).** The screen is driven by a **single read**,
> `GET /app/users/me`, which the Flutter app already calls. Implemented as
> `CurrentUserEndpoint` over `IAccountService.GetCurrentUserAsync` — a read over
> existing Identity data (no new table, no schema change, no migration). Covered by
> `tests/SIMF.Api.Tests/CurrentUserEndpointTests.cs`.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split) — so
> the full route below is `GET /api/v1/app/users/me`.

## E1 — `GET /app/users/me`  **(BUILT — D-249)**
| | |
|---|---|
| Full route | `GET /api/v1/app/users/me` |
| Access | Any **signed-in** account (valid bearer token); own `sub`. **Not** `AllowAnonymous`. Does **not** require an approved account — a pending user must be able to read their own status. No permission code (app self-read, not an admin action). |
| App privilege | Signed-in, including **pending** (a not-yet-approved account) |
| Returns | `ApiResult<CurrentUserResponse>` |

```jsonc
// CurrentUserResponse
{
  "id":                 "guid",     // user id (sub)
  "email":              "string",   // account email
  "displayName":        "string",   // resolved display name
  "appRole":            "string",   // "Visitor" | "Staff" | "Moderator" (name form)
  "preferredLanguage":  "string",   // "ar" | "en"  (server default "ar" today)
  "registrationStatus": "string",   // "Approved" | "Pending" | "Rejected"  ← drives this screen
  "avatarUrl":          "string?",  // avatar image URL, null if none
  "profileComplete":    "bool"      // D-374 — server-computed (names + ≥1 interest +
                                    // male→ID-photo, C7); drives the post-sign-in
                                    // add-profile-first route (Page_007)
}
```

### `registrationStatus` mapping (server-side)
The server maps the internal `AccountState` to the public tri-state — the raw
`AccountState` is **not** exposed. See [Page_011_Logic.md](Page_011_Logic.md) L-1.

| `registrationStatus` returned | Backing `AccountState` |
|------------------------------|------------------------|
| `Approved` | `Approved` |
| `Pending` | `Registered`, `EmailVerified`, `PendingApproval` |
| `Rejected` | `Rejected`, `Disabled` |

### `appRole` mapping (server-side)
The resolved mobile app-role (`IUserProfileService.ResolveMobileAppRoleAsync`) is
emitted as its **name** string so the integer drift between the backend
`MobileAppRole` and the app's `AppRole` enum never matters. `MobileAppRole.None`
(partner profile types / admins) is emitted as **`Visitor`** — the effective app
floor — so an authenticated user is never read back as `Guest`.

### Success envelope (example — pending)
```jsonc
{
  "success": true,
  "data": {
    "id": "8f2c…",
    "email": "user@example.com",
    "displayName": "زائر المنتدى",
    "appRole": "Visitor",
    "preferredLanguage": "ar",
    "registrationStatus": "Pending",
    "avatarUrl": null,
    "profileComplete": false
  },
  "error": null
}
```

### Error responses
| HTTP | When | `ApiResult.error` | Client handling (Page_011_Logic L-6) |
|------|------|-------------------|--------------------------------------|
| 401 | Missing / expired token | auth error | Route to sign-in — pending session invalid |
| 404 | Account row not found for the token's `sub` | `auth.account_not_found` | Treat as signed-out → sign-in |
| 500 | Server fault resolving status | server error | Error state + retry |
| (n/a) | `registrationStatus` missing / unknown value | — | Treat as Error (no silent `Pending`) |

> **Error codes:** the shared codes are defined in `SIMF.Common/ErrorCodes.cs` +
> SIMF-API-001 §error-model (`AuthAccountNotFound` backs the 404). No new codes were
> invented for this read.

## Approval reference number + date (D11)
**Not part of any API.** Per **D11** the approval reference number + date on the
screen are **decoration only** — `GET /app/users/me` does **not** return them and the
client must not depend on them. See [Page_011_Logic.md](Page_011_Logic.md) L-5.

## Build dependencies
- **`GET /app/users/me` — BUILT (D-249).** The only backing call. It is a read over
  existing Identity data; the Flutter client call was already wired and now resolves.
- No other endpoint serves this page. There is no write / no mutation on this screen —
  it is read-only.
