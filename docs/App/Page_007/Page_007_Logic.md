# Page 007 — Logic (إنشاء حساب · Sign up — profile data)

Client + server logic, validation, state transitions, and edge handling. The
contract lives in [Page_007_API.md](Page_007_API.md); the user flow in
[Page_007_Function.md](Page_007_Function.md).

> **Reworked (D-332).** Data screen only. The **interests** rule and the **save** moved
> to [Page 007‑01](../Page_007-01/README.md). This screen ends with **Next**.

## L-1 — Auth gate
AUTH-only. Every call requires a signed-in bearer token; **no role, no permission
code, not approval-gated, not `AllowAnonymous`** (D7). The lookups sit in the `auth`
rate-limit bucket and resolve the actor from the `sub` claim — the request body never
carries a user id / email.

## L-2 — Lookup sources (read-on-open)
| Picker | Endpoint | Rows |
|--------|----------|------|
| ProfileType (التصنيف) | `GET /app/account/profile-types?isVisitor={bool}` | active, non-Admin profile types, **filtered by نوع التسجيل** (D-190) |
| Nationality | `GET /app/account/user-profile/countries` | active countries (code + AR/EN name) |
| الجهة / Organisation | `GET /app/organisations?search=&top=20` | active organisations (typeahead; D-220) |

Pre-fill: `GET /app/account/user-profile` returns any existing values (including
`ProfileTypeId`, `OrganisationId`) so a re-entry shows the saved state. **Interests
are loaded on [Page 007‑01](../Page_007-01/README.md), not here.**

## L-3 — نوع التسجيل (Visitor / Other) filter
The first field is a **client-only** 2-way chip — **زائر (Visitor)** / **أخرى (Other)**
— that maps to `ProfileType.IsForVisitor` (`true` / `false`). It is **not** persisted:
there is no "registration type" field in the API (the `VisitorType` discriminator was
dropped in P8 — the only stored value is `ProfileTypeId`). Its sole effect is to
**filter** the ProfileType picker (`?isVisitor=`). Changing it re-queries / re-filters
the ProfileType list and clears any now-invalid ProfileType selection.

## L-4 — Validation (client mirrors server; data fields only)
Server rules (`UpsertUserProfileRequestValidator`) the client mirrors **for the fields
captured here**:
- **Names:** Arabic + English name required (≤ 256). **Nationality** required (2-letter ISO code from the lookup).
- **Date of birth:** **required**, registrant must be **≥ 18** (D-197 — leap-safe `today − 18y`). Place of birth optional (≤ 128); job title optional (≤ 128).
- **Organisation:** optional; if present must be a valid, active organisation id.
- **ProfileType:** optional self-pick; rejects unknown / inactive / Admin-scope rows. Admin pre-pick wins over the user self-pick (`UserProfileService.UpsertMineAsync`).
- **Identity-doc shape:** keyed off the is-Saudi flag — Saudi → national id required, `^1\d{9}$` + Luhn; non-Saudi → an Iqama (`^2\d{9}$` + Luhn) **or** a passport (`^[A-Za-z0-9]{6,9}$`) is required. Mobiles optional, permissive `+CC` shape.

The client blocks **Next** until these required data fields are valid. **The 1–10
interests rule and the upsert call are enforced on [Page 007‑01](../Page_007-01/README.md).**

## L-5 — State transitions
```
profile-incomplete ──[pick type + ProfileType + fill required fields]──▶ data-ready
data-ready ──[Next]──▶ Page 007‑01 (interests) — form state carried in memory
```
**No persistence on this screen.** The single `POST /app/account/user-profile`
(carrying these fields **and** the picked `interestIds`) fires on Page 007‑01; a
successful save there marks the profile complete → wait-for-approval.

## L-6 — Error / empty / RTL handling
- **Empty lookup:** show the picker's empty state, never a blocking error.
- **Validation error:** inline per-field (client-side only here — no server call until Page 007‑01); keep the user's input intact.
- **Back:** discards the in-memory form (nothing was written).
- **RTL:** Arabic is the primary locale; the type chips, pickers and toggles mirror; AR/EN labels come from each lookup row.

## L-7 — Dependencies
- Country / organisation / profile-type lookup data must be seeded for the pickers to populate (organisation via the D-220 module; profile types via D-190).
- Account must be signed-in and profile-incomplete for the screen to apply.
- **[Page 007‑01](../Page_007-01/README.md)** is the required next step and owns the interests rule + the save.
