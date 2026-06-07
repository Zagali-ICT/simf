# Page 007‑01 — API (اهتماماتي · Sign up — interests)

Authoritative backend contract for the interests screen + the single profile save.
Inherits the `ApiResult<T>` envelope, headers, error model and auth from SIMF-API-001 +
SIMF-MOB-API-001 §3–§4. Business rules are in [Page_007-01_Logic.md](Page_007-01_Logic.md).

> **New (D-332)** — but **no new endpoint**: the interests lookup + the upsert are the
> shipped, already-built contract, now sequenced onto this screen (split out of Page 007).
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
| Returns | `ApiResult<UserProfileResponse>` (the upserted profile) |

```jsonc
// UpsertUserProfileRequest — the Page-007 fields + the interests picked here.
// NO user id / email (actor from token, D7).
{
  "profileTypeId":   "guid?",   // from Page 007 (optional; admin pre-pick wins, D-190)
  "interestIds":     ["guid"],  // REQUIRED 1–10 active ids, distinct (D-050)
  "arabicName":      "string",  // ── from Page 007 ──
  "englishName":     "string",
  "jobTitle":        "string?",
  "nationalityCode": "string",
  "dateOfBirth":     "2000-01-31", // required ≥ 18 (D-197)
  "placeOfBirth":    "string",
  "isSaudi":         false,
  "nationalId":      "string?",
  "iqamaNumber":     "string?",
  "passportNumber":  "string?",
  "saudiMobile":     "string?",
  "internationalMobile": "string?",
  "organisationId":  "guid?",
  "gender":          0
}
```
Idempotent: first call creates the row, later calls update it. A successful save marks
the profile complete → the account moves to **wait-for-approval** (`Page_010` →
`Page_011`). **The interests persist here — there is no separate interests write.**

## E3 — ID-document image (optional, on save)
If an ID/Iqama/Passport image was picked on Page 007, it is uploaded **after** the
profile row exists via the shipped multipart upload
(`POST /api/v1/app/account/user-profile/id-image`, content-type set so the server's
MIME + magic-byte gate accepts it). Optional — skipped when no image was chosen.

## Error codes (envelope `ApiResult<T>.Error`)
| Code | When | Bilingual surface |
|------|------|-------------------|
| `Auth.Unauthorized` (401) | no / invalid bearer token | redirect to sign-in |
| `Validation.Failed` (400) | interests not 1–10 / duplicate / unknown / inactive id; or any Page-007 field invalid (unknown/inactive organisation or profile-type; identity-doc shape; DOB < 18) | field-level AR/EN message; if the bad field is on Page 007, let the user go Back to fix it |
| `RateLimit.Exceeded` (429) | `auth` bucket exceeded | retry-after toast |

> No `(TO BUILD)` API: the interests lookup + the upsert + the id-image upload are all
> shipped. The only build dependency is **interests seed data** (D-050).
