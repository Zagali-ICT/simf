# Page 007 — API (إنشاء حساب · Sign up — profile data)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Business
rules are in [Page_007_Logic.md](Page_007_Logic.md).

> **Reworked (D-332).** This screen reads the **pre-fill + three lookups** below. The
> **interests lookup** and the **`POST` upsert (the Save)** moved to
> **[Page 007‑01](../Page_007-01/Page_007-01_API.md)** — because the API requires
> `interestIds` (1–10) on the single upsert, the save fires only after interests are
> picked. **No new or changed endpoint** — the same shipped contract, re-sequenced.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split, D-247).
>
> **Auth shape (all):** `Tags("Account")` + the `auth` rate-limit bucket, **no
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
// UserProfileResponse — empty/null on a first-time profile (full shape; the
// interestIds/qrId fields are consumed on Page 007‑01 / after approval)
{
  "profileTypeId":   "guid?",   // assigned subtype, when present (D-190)
  "arabicName":      "string",
  "englishName":     "string",
  "jobTitle":        "string?", // optional (D-163)
  "nationalityCode": "string",
  "dateOfBirth":     "2000-01-31", // DateOnly?, nullable — required on save (D-197)
  "placeOfBirth":    "string",
  "isSaudi":         false,
  "nationalId":      "string?",
  "iqamaNumber":     "string?",
  "passportNumber":  "string?",
  "saudiMobile":     "string?",
  "internationalMobile": "string?",
  "organisationId":  "guid?",   // الجهة (D-221)
  "gender":          0,         // Gender enum; Unspecified until picked (D-221)
  "hasIdImage":      false,
  "interestIds":     ["guid"],  // consumed on Page 007‑01
  "qrId":            "string?"  // 12-char Crockford id; null until Approved
}
```

## E3 — `GET /app/account/user-profile/countries`  (nationality lookup)
| | |
|---|---|
| Full route | `GET /api/v1/app/account/user-profile/countries` |
| Returns | `ApiResult<CountryListResponse>` |

```jsonc
{ "countries": [ { "code": "SA", "name": "Saudi Arabia", "nameArabic": "السعودية" } ] }
```

## E4 — `GET /app/account/profile-types?isVisitor={bool}`  (ProfileType lookup, filtered)
| | |
|---|---|
| Full route | `GET /api/v1/app/account/profile-types?isVisitor=true|false` (omit for all) |
| Returns | `ApiResult<ProfileTypePickerListResponse>` — active, non-Admin rows (D-190) |
| **Filter** | `isVisitor` mirrors the **نوع التسجيل** chip — `true` → audience ProfileTypes, `false` → partner/Other ProfileTypes |

```jsonc
{ "items": [ { "id": "guid", "name": "Normal", "nameArabic": "عادي",
              "pageColor": "#0B5", "isVisitor": true } ] }
```

## E6 — `GET /app/organisations`  (الجهة lookup / typeahead)
| | |
|---|---|
| Full route | `GET /api/v1/app/organisations?search={text}&top={n}` |
| Query | `search` (free-text over AR/EN name; null → top rows) · `top` (default 20) |
| Returns | `ApiResult<IReadOnlyList<OrganisationPickerItem>>` |

```jsonc
[ { "id": "guid", "nameAr": "القوات البحرية الملكية السعودية",
    "nameEn": "Royal Saudi Naval Forces", "city": "Riyadh" } ]
```

## The Save — on Page 007‑01
`POST /app/account/user-profile` (the upsert carrying these fields **+** the
`interestIds`) and the `GET /app/account/interests` lookup are documented on
**[Page 007‑01 API](../Page_007-01/Page_007-01_API.md)**. The ID-document image upload
(`POST` multipart, after the profile row exists) also runs on save.

## Error codes (envelope `ApiResult<T>.Error`)
| Code | When | Bilingual surface |
|------|------|-------------------|
| `Auth.Unauthorized` (401) | no / invalid bearer token | redirect to sign-in |
| `RateLimit.Exceeded` (429) | `auth` bucket exceeded | retry-after toast |

> No screen-specific `(TO BUILD)` API: the pre-fill + three lookups are all shipped.
> The only build dependency is **seed data** (countries, organisations, profile types).
