# Page 007 — Logic (إنشاء حساب · Sign up — profile data)

Client + server logic, validation, state transitions, and edge handling. The
contract lives in [Page_007_API.md](Page_007_API.md); the user flow in
[Page_007_Function.md](Page_007_Function.md).

> **Reworked (D-332).** Data screen only. The **interests** rule and the **save** moved
> to [Page 007‑01](../Page_007-01/README.md). This screen ends with **Next**.

> **Owner constraint set (2026-06-12 — D-371).** The owner fixed seven binding
> rules for this form ahead of the Wave-1 production-readiness gate. C1–C3 were
> already as-built; C4–C7 are the build items. The authoritative wording:
> **C1** Saudi → national ID: starts with `1`, 10 digits, Luhn. **C2** non-Saudi
> → Iqama (`2` + 10 digits + Luhn) **or** passport (6–9 alphanumeric) — never
> the national-ID field. **C3** password ≥ 8 chars with a letter and a digit
> (server-authoritative). **C4** phones validate to the standard: Saudi mobile
> `05XXXXXXXX` or `+9665XXXXXXXX`; international mobile **E.164** (`+`, then
> 8–15 digits) — client and server enforce the same rule (supersedes the old
> permissive `+CC` shape in L-4). **C5** نوع التسجيل: **Visitor → the profile
> type is locked to the single "Normal" (عادي) type — no picker shown**; Other
> → the filtered picker is shown and a selection is **required** (extends L-3).
> **C6** **plate number**: optional; when provided it must match the Saudi
> standard — **exactly 3 letters (Arabic or Latin equivalents) + 1–4 digits,
> max 7 characters** excluding separators (owner wrote "17" — read as 7, the
> only total consistent with 3 letters + max 4 digits; flagged for correction).
> New additive `UserProfile.PlateNumber` column (owner-authorised freeze lift).
> **C7** profile image: **mandatory for gender = male**, captured by **camera
> only** (no gallery), and must pass a **human-face detection** check —
> on-device (ML Kit) for instant feedback **and** server-side on the
> `id-image` endpoint as the authority. For women the image stays optional;
> when added, the same camera-only + face-check rules apply (recorded
> assumption, D-371).

> **D-373 amendments (owner, 2026-06-12):** form **defaults** = Visitor +
> **Male** + nationality **Saudi Arabia**; the country picker is
> **searchable** (type-to-filter); the **"سعودي الجنسية" switch is removed** —
> `isSaudi` derives from the nationality pick (SA → national-ID, else the
> Iqama/Passport choice; wire contract unchanged); the **selected** segment of
> both segmented switches renders **white-background/ink-text**; the **plate
> field is the last input before the attach box**; the registration
> **reference number** (`SIMF-<year>-<8-digit sequence>`, DB-generated,
> unique, NOT the QR id) is created at profile-row creation and surfaced via
> the profile API / success screen / CP search.

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
- **Identity-doc shape:** keyed off the is-Saudi flag — Saudi → national id required, `^1\d{9}$` + Luhn; non-Saudi → an Iqama (`^2\d{9}$` + Luhn) **or** a passport (`^[A-Za-z0-9]{6,9}$`) is required.
- **Mobiles (C4, D-371):** optional; when present they must match the standard — `saudiMobile`: `^05\d{8}$` or `^\+9665\d{8}$`; `internationalMobile`: E.164 `^\+[1-9]\d{7,14}$`. Client and server enforce identically (supersedes the old permissive shape).
- **Plate number (C6, D-371):** optional; when present — exactly **3 letters + 1–4 digits, ≤ 7 chars** (Arabic letters or Latin equivalents; separators stripped before validation). Stored in the new additive `UserProfile.PlateNumber` column.
- **Profile image (C7, D-371):** required when `gender = male` before the Page 007‑01 save is allowed; capture is **camera-only**; the image must pass the human-face check (on-device ML Kit + the server-side detector on the `id-image` endpoint — the server is the authority).
- **ProfileType lock (C5, D-371):** Visitor → `profileTypeId` is forced to the seeded **"Normal"** type and the picker is hidden; Other → a pick from the `?isVisitor=false` list is **required** (no longer optional).

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
