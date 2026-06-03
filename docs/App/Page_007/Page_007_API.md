# Page 007 — API (إنشاء حساب · زائر · Sign up — visitor)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Business
rules are in [Page_007_Logic.md](Page_007_Logic.md).

> **Status:** all endpoints below are **built** (verified in
> `src/Backend/SIMF.Api/Endpoints/Account` + `…/Admin/OrganisationEndpoints.cs`).
> No `(TO BUILD)` endpoint on this screen.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split, D-247).
> The `Configure()` route strings are `/app/account/…` and `/app/organisations`, so
> the full paths are `GET /api/v1/app/account/user-profile`, etc.
>
> **Auth shape (all six):** `Tags("Account")` + the `auth` rate-limit bucket, **no
> `Policies(...)`** — caller must be **signed in** but is **not** admin-gated and
> **not** approval-gated. None is `AllowAnonymous`. Actor resolved from the `sub`
> claim (D7); the body never carries a user id.

## E1 — `GET /app/account/user-profile`  (pre-fill)
| | |
|---|---|
| Full route | `GET /api/v1/app/account/user-profile` |
| Access | Signed-in (own `sub`); no role / no permission |
| Returns | `ApiResult<UserProfileResponse>` |

```jsonc
// UserProfileResponse — empty/null on a first-time profile
{
  "profileTypeId":   "guid?",   // assigned subtype, when present (D-190)
  "interestIds":     ["guid"],  // picked interests (1–10 once saved; empty until then)
  "arabicName":      "string",
  "englishName":     "string",
  "jobTitle":        "string?", // optional (D-163)
  "nationalityCode": "string",
  "dateOfBirth":     "2000-01-31", // DateOnly?, nullable
  "placeOfBirth":    "string",
  "isSaudi":         false,
  "nationalId":      "string?",
  "iqamaNumber":     "string?",
  "passportNumber":  "string?",
  "saudiMobile":     "string?",
  "internationalMobile": "string?",
  "organisationId":  "guid?",   // الجهة (D-221); COMPANY dropped (D6)
  "gender":          0,         // Gender enum; Unspecified until picked (D-221)
  "hasIdImage":      false,
  "qrId":            "string?"  // 12-char Crockford id; null until Approved
}
```

## E2 — `POST /app/account/user-profile`  (upsert — the Save)
| | |
|---|---|
| Full route | `POST /api/v1/app/account/user-profile` |
| Access | Signed-in (own `sub`); no role / no permission |
| Returns | `ApiResult<UserProfileResponse>` (the upserted profile, same shape as E1) |

```jsonc
// UpsertUserProfileRequest — body (NO user id / email; actor from token, D7)
{
  "profileTypeId":   "guid?",   // optional self-pick; admin pre-pick wins (D-190)
  "interestIds":     ["guid"],  // REQUIRED 1–10 active ids, distinct (D-050 / D12)
  "arabicName":      "string",
  "englishName":     "string",
  "jobTitle":        "string?",
  "nationalityCode": "string",
  "dateOfBirth":     "2000-01-31",
  "placeOfBirth":    "string",
  "isSaudi":         false,
  "nationalId":      "string?",
  "iqamaNumber":     "string?",
  "passportNumber":  "string?",
  "saudiMobile":     "string?",
  "internationalMobile": "string?",
  "organisationId":  "guid?",   // optional; rejected if unknown / inactive
  "gender":          0          // optional; Unspecified when not picked
}
```
Idempotent: first call creates the row, later calls update it. A successful save
marks the profile complete → the account moves to **wait-for-approval**. The
interests sub-step persists **here** — there is no separate interests write.

## E3 — `GET /app/account/user-profile/countries`  (nationality lookup)
| | |
|---|---|
| Full route | `GET /api/v1/app/account/user-profile/countries` |
| Access | Signed-in; no role / no permission |
| Returns | `ApiResult<CountryListResponse>` |

```jsonc
// CountryListResponse
{ "countries": [ { "code": "SA", "nameEn": "Saudi Arabia", "nameAr": "السعودية" } ] }
```

## E4 — `GET /app/account/profile-types`  (profile-type lookup)
| | |
|---|---|
| Full route | `GET /api/v1/app/account/profile-types` |
| Access | Signed-in; no role / no permission |
| Returns | `ApiResult<ProfileTypePickerListResponse>` — active, non-Admin rows (D-190) |

```jsonc
// ProfileTypePickerListResponse
{ "items": [ { "id": "guid", "name": "Visitor", "nameArabic": "زائر",
              "pageColor": "#0B5", "isVisitor": true } ] }
```

## E5 — `GET /app/account/interests`  (interests sub-step lookup)
| | |
|---|---|
| Full route | `GET /api/v1/app/account/interests` |
| Access | Signed-in; no role / no permission |
| Returns | `ApiResult<InterestListResponse>` — active rows, ordered `DisplayOrder` then `Name` (D-050) |

```jsonc
// InterestListResponse
{ "interests": [ { "id": "guid", "name": "Naval Defence",
                  "nameArabic": "الدفاع البحري", "displayOrder": 1 } ] }
```

## E6 — `GET /app/organisations`  (الجهة lookup / typeahead)
| | |
|---|---|
| Full route | `GET /api/v1/app/organisations?search={text}&top={n}` |
| Access | Signed-in; no role / no permission (auth-only, **not** the admin `/admin/organisations` CRUD) |
| Query | `search` (free-text over AR/EN name; null → top rows) · `top` (default 20) |
| Returns | `ApiResult<IReadOnlyList<OrganisationPickerItem>>` |

```jsonc
// OrganisationPickerItem[]
[ { "id": "guid", "nameAr": "القوات البحرية الملكية السعودية",
    "nameEn": "Royal Saudi Naval Forces", "city": "Riyadh" } ]
```
This is the lookup that replaces the dropped COMPANY field (D6 / D-220 / D-221).

## Error codes (envelope `ApiResult<T>.Error`)
| Code | When | Bilingual surface |
|------|------|-------------------|
| `Auth.Unauthorized` (401) | no / invalid bearer token | redirect to sign-in |
| `Validation.Failed` (400) | interests not 1–10, duplicate / unknown / inactive interest id; unknown / inactive organisation id; unknown / inactive / Admin-scope profile-type id; identity-doc shape mismatch | field-level AR/EN message from the validator |
| `RateLimit.Exceeded` (429) | `auth` bucket exceeded | retry-after toast |

> No screen-specific `(TO BUILD)` API: the upsert + four lookups + the organisation
> picker are all shipped. The only build dependency is **seed data** (countries,
> organisations, profile types, interests) — not a missing endpoint.
