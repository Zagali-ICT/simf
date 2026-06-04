# Page 010 — API (تم التسجيل بنجاح · Registration success)

Backend contract for this page. Inherits the `ApiResult<T>` envelope, headers,
error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Behaviour rules
are in [Page_010_Logic.md](Page_010_Logic.md).

> **Status:** transitional confirmation screen — it owns **no write API**. The
> account was already created by the Page 009 submit. The only call this screen
> may make is an **optional** read of the user's own account status to auto-
> advance once approved.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247).

## Does this screen have its own API?
**No write API.** The base confirmation renders entirely client-side from the
result of the Page 009 submit. The single optional call below is a **status
poll** — `GET /app/users/me`, **built** (D-249).

## E1 — `GET /app/users/me`  *(BUILT — `CurrentUserEndpoint`, D-249)*
Optional status poll. Lets the screen detect when the pending account becomes
**Approved** and route the user forward (Logic L-3).

| | |
|---|---|
| Route | `GET /api/v1/app/users/me` |
| Owner page | Page 011 (this is that page's endpoint, reused here) |
| Access | The caller's **own** account (`sub`); valid session/token. A pending account is allowed to read its own status. |
| App privilege | Signed-in, pending approval (and above) |
| Returns | `ApiResult<CurrentUserResponse>` |
| Status | **BUILT** — shipped under D-249 (`CurrentUserEndpoint`, `GET /api/v1/app/users/me`). |

```jsonc
// CurrentUserResponse  (wire = camelCase of the C# record members)
{
  "id":                "guid",
  "email":             "string",
  "displayName":       "string",
  "appRole":           "string",
  "preferredLanguage": "string",   // IETF short tag: "ar" | "en"
  "registrationStatus":"string",   // "Pending" | "Approved" | "Rejected"  ← the field this screen watches
  "avatarUrl":         "string?"   // optional
}
```

**How this screen uses it:** poll (bounded — Logic L-3) and inspect
`registrationStatus`. While **Pending**, keep showing the confirmation. On
**Approved**, stop polling and route the user into the signed-in home.

### Error codes
| Condition | HTTP | `ApiResult` error |
|---|---|---|
| No / invalid session | 401 | unauthenticated |
| Reading another user's status | 403 | forbidden |

## Reused / related endpoints
| Endpoint | Relation |
|---|---|
| Page 009 submit (profile completion) | Creates the pending account; its **success** is what brings the user here. Not called from this screen. |
| Page 011 (registrationStatus) | The primary forward navigation target. Pure client navigation — no call made from this screen. |
| Home screen | The ghost/secondary navigation target. Pure client navigation — no call made from this screen. |

## Build dependencies
- **E1 `GET /app/users/me` is BUILT** (Page 011's endpoint, `CurrentUserEndpoint`,
  D-249), so the optional auto-advance poll is available. The base confirmation
  has **no** backend dependency.
