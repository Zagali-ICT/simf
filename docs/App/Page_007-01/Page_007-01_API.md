# Page 007‑01 — API (اهتماماتي · Sign up — interests)

*Last updated: 2026-06-13 — conformance pass against the as-built screen + backend (D-365).*

Authoritative backend contract for the interests screen + the single profile save.
Inherits the `ApiResult<T>` envelope, headers, error model and auth from SIMF-API-001 +
SIMF-MOB-API-001 §3–§4. Business rules are in [Page_007-01_Logic.md](Page_007-01_Logic.md).

> **No new endpoint** (D-332): the interests lookup + the upsert are the shipped,
> already-built contract, sequenced onto this screen (split out of Page 007). The
> D-365 redesign changed **visuals only** — the wire contract is byte-identical.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split, D-247).
>
> **Auth shape:** `Tags("Account")` + the `auth` rate-limit bucket, **no `Policies(...)`**
> — signed-in, not admin-gated, not approval-gated, not `AllowAnonymous`. Actor from the
> `sub` claim (D7).

## E1 — `GET /app/account/interests`  (interests lookup)
| | |
|---|---|
| Full route | `GET /api/v1/app/account/interests` |
| Access | Signed-in; no role / no permission |
| Returns | `ApiResult<InterestListResponse>` — active rows, ordered `DisplayOrder` then `Name` (D-050) |

```jsonc
{ "interests": [ { "id": "guid", "name": "Naval Defence",
                  "nameArabic": "الدفاع البحري", "displayOrder": 1 } ] }
```

## E2 — `POST /app/account/user-profile`  (the single Save — data + interests)
| | |
|---|---|
| Full route | `POST /api/v1/app/account/user-profile` |
| Access | Signed-in (own `sub`); no role / no permission |
| Returns | `ApiResult<UserProfileResponse>` (the upserted profile — incl. `referenceNumber`, D-373) |

```jsonc
// UpsertUserProfileRequest — the Page-007 fields + the interests picked here.
// NO user id / email (actor from token, D7).
{
  "profileTypeId":   "guid?",   // from Page 007 (optional; admin pre-pick wins, D-190)
  "interestIds":     ["guid"],  // REQUIRED 1–10 active ids, distinct (D-050)
  "arabicName":      "string",  // ── from Page 007 ──
  "englishName":     "string",
  "jobTitle":        "string?",
  "nationalityCode": "string",  // ISO 3166-1 alpha-2 (2 chars)
  "dateOfBirth":     "2000-01-31", // required, ≥ 18 (D-197)
  "placeOfBirth":    "string",
  "isSaudi":         false,
  "nationalId":      "string?",
  "iqamaNumber":     "string?",
  "passportNumber":  "string?",
  "saudiMobile":     "string?",     // C4 (D-371): 05XXXXXXXX or +9665XXXXXXXX
  "internationalMobile": "string?", // C4 (D-371): E.164
  "plateNumber":     "string?",     // C6 (D-371): optional Saudi plate (3 letters + 1–4 digits)
  "organisationId":  "guid",        // REQUIRED (B3 — D-221 owner rule; service checks existence/active)
  "gender":          0              // Gender enum: 0 Unspecified · 1 Male · 2 Female
}
```
Idempotent: first call creates the row, later calls update it. A successful save marks
the profile complete → the account moves to **wait-for-approval** (`Page_010` →
`Page_011`). **The interests persist here — there is no separate interests write.**
The response's **`referenceNumber`** (`SIMF-2026-00000001`, issued once at profile
creation — D-373) is carried by the app to Page 010 as the route extra, so the
success screen renders it without another fetch.

## E3 — ID-document image (optional, on save)
If an ID/Iqama/Passport image was picked on Page 007, it is uploaded **after** the
profile row exists via the shipped multipart upload
(`POST /api/v1/app/account/user-profile/id-image`; the MIME — `image/jpeg` /
`image/png` / `image/webp` — is derived from the filename so the server's MIME +
magic-byte gate accepts it). Optional — skipped when no image was carried. **An
upload failure is non-blocking**: the profile save already succeeded, so the app
shows the bilingual warning toast (`idImageUploadFailed`) and still proceeds to
Page 010.

## Error codes (envelope `ApiResult<T>.Error`)
| Code | When | Bilingual surface (as built) |
|------|------|------------------------------|
| `Auth.Unauthorized` (401) | no / invalid bearer token | route 701 is auth-gated — the router redirects to sign-in |
| `Validation.Failed` (400) | interests not 1–10 / duplicate / unknown / inactive id; or any Page-007 field invalid (missing/unknown/inactive organisation; profile-type; identity-doc shape/Luhn; DOB missing or < 18; mobile/plate shape) | the server's bilingual `error.message` renders **inline in red under the counter**; the selection is preserved and the **Back** chevron returns to Page 007 with the draft intact |
| `RateLimit.Exceeded` (429) | `auth` bucket exceeded | same inline `ApiFailure.message` surface (submit) / retry button (lookup) |

> No `(TO BUILD)` API: the interests lookup + the upsert + the id-image upload are all
> shipped. The only data dependency is **interests seed data** (D-050).
