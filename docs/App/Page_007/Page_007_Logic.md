# Page 007 — Logic (إنشاء حساب · Sign up — profile data)

Client + server logic, validation, state transitions, and edge handling. The
contract lives in [Page_007_API.md](Page_007_API.md); the user flow in
[Page_007_Function.md](Page_007_Function.md).

> Last updated: 2026-06-13 — as-built conformance pass (D-368 KSA rebuild;
> D-371/D-373/D-374/D-375 amendments).

> **Reworked (D-332), rebuilt to the KSA frame (D-368).** Data screen only. The
> **interests** rule and the **save** moved to
> [Page 007‑01](../Page_007-01/README.md). This screen ends with **Next**.

> **Owner constraint set (2026-06-12 — D-371, all BUILT).** The owner fixed seven
> binding rules for this form ahead of the Wave-1 production-readiness gate.
> The authoritative wording:
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
> Additive `UserProfile.PlateNumber` column (owner-authorised freeze lift).
> **C7** profile image: **mandatory for gender = male**, captured by **camera
> only** (no gallery), and must pass a **human-face detection** check —
> on-device (ML Kit) for instant feedback **and** server-side on the
> `id-image` endpoint as the authority. For women the image stays optional;
> when added, the same camera-only + face-check rules apply (recorded
> assumption, D-371).

> **D-373 amendments (owner, 2026-06-12 — all BUILT):** form **defaults** =
> Visitor + **Male** + nationality **Saudi Arabia**; the country picker is
> **searchable** (type-to-filter bottom sheet); the **"سعودي الجنسية" switch is
> removed** — `isSaudi` derives from the nationality pick (SA → national-ID,
> else the Iqama/Passport choice; wire contract unchanged); the **selected**
> segment of both segmented switches renders **white-background/ink-text**; the
> **plate field is the last input before the attach box**; the registration
> **reference number** (`SIMF-<year>-<8-digit sequence>`, DB-generated, unique,
> NOT the QR id) is created at profile-row creation and surfaced via the
> profile API / success screen / CP search.

## L-1 — Auth gate
AUTH-only. Every call requires a signed-in bearer token; **no role, no permission
code, not approval-gated, not `AllowAnonymous`** (D7). The lookups sit in the `auth`
rate-limit bucket and resolve the actor from the `sub` claim — the request body never
carries a user id / email.

## L-2 — Lookup sources (read-on-open)
| Picker | Endpoint | Rows |
|--------|----------|------|
| ProfileType (التصنيف) | `GET /app/account/profile-types?isVisitor={bool}` | active, non-Admin profile types, **filtered by نوع التسجيل** (D-190); the initial load uses `?isVisitor=true` (Visitor default) |
| Nationality | `GET /app/account/user-profile/countries` | active countries (code + AR/EN name); rendered as the searchable sheet (D-373) |
| الجهة / Organisation | `GET /app/organisations?search=&top=20` | active organisations (typeahead, 350 ms debounce; D-220) |

All four reads (the three lookups + the pre-fill) run **concurrently** behind one
full-screen loading state; a failure on any shows the screen-level error + retry.
**D-375:** the per-interaction re-fetches (ProfileType on tab switch, organisation
search) each surface their own fetch state — loading spinner while in flight, a
visible inline **retry** on failure — never a silently missing/empty control.

Pre-fill: `GET /app/account/user-profile` returns any existing values (including
`ProfileTypeId`, `OrganisationId`, `PlateNumber`, `HasIdImage`); picker values are
guarded against their lookup so a stale id never selects a missing row. D-373
defaults on an empty profile: gender → **Male**, nationality → **SA**. **Interests
are loaded on [Page 007‑01](../Page_007-01/README.md), not here** — any existing
interest ids ride the draft for pre-selection.

## L-3 — نوع التسجيل (Visitor / Other) filter + C5 lock
The first field is a **client-only** 2-way segmented tab — **زائر (Visitor)** /
**أخرى (Other)** — that maps to `ProfileType.IsForVisitor` (`true` / `false`). It is
**not** persisted: there is no "registration type" field in the API (the
`VisitorType` discriminator was dropped in P8 — the only stored value is
`ProfileTypeId`). Switching it re-queries the ProfileType lookup (`?isVisitor=`)
and clears any now-invalid ProfileType selection. **C5 (D-371):** under **Visitor**
no picker is shown — the id auto-locks to the seeded **"Normal"** row (falls back
to the only row when the lookup has exactly one; an empty lookup leaves null —
admin assigns). Under **Other** the picker is shown and a pick is **required**
(the empty-lookup case is excluded from the requirement per L-6). An
admin-assigned tier still wins server-side (D-190 precedence).

## L-4 — Validation (client mirrors server; data fields only)
Server rules (`UpsertUserProfileRequestValidator`) the client mirrors **for the
fields captured here** (client shapes live in `phone_validation.dart` /
`plate_validation.dart` + the screen's validators):
- **Names:** Arabic + English name required (≤ 256). **Nationality** required server-side (2-letter ISO code); on the client it defaults to **SA** and an unset pick shows the inline `nationalityRequired` error after a submit attempt.
- **Date of birth:** **required**, registrant must be **≥ 18** (D-197 — leap-safe; the client picker simply caps the range at *today − 18y*). Place of birth optional (≤ 128); job title optional (≤ 128).
- **Organisation (B3, D-221):** **required** — the client blocks Next until one is picked (`organisationRequired` inline); the server requires a valid, active organisation id.
- **ProfileType:** Visitor → forced to "Normal" (C5); Other → required pick from the `?isVisitor=false` list. Server rejects unknown / inactive / Admin-scope rows; admin pre-pick wins over the user self-pick (`UserProfileService.UpsertMineAsync`).
- **Identity-doc shape:** keyed off the **derived** `isSaudi` (= nationality `SA`, D-373) — Saudi → national id required, `^1\d{9}$` + Luhn; non-Saudi → an Iqama (`^2\d{9}$` + Luhn) **or** a passport (`^[A-Za-z0-9]{6,9}$`) is required (the tab picks which; the number field is required either way on the client).
- **Mobiles (C4, D-371):** optional; only **one** field renders — `saudiMobile` when Saudi (`^05\d{8}$` or `^\+9665\d{8}$`), else `internationalMobile` (E.164 `^\+[1-9]\d{7,14}$`); spaces/dashes stripped before the match, client and server identically. The hidden counterpart is sent as null.
- **Plate number (C6, D-371):** optional; when present — exactly **3 letters + 1–4 digits** (either order), ≤ 7 chars (Arabic letters or Latin; separators stripped before validation). Stored normalized in `UserProfile.PlateNumber`.
- **Profile image (C7, D-371):** required when `gender = male` — a camera capture must be attached, or the server must already store one (`hasIdImage` pre-fill); capture is **camera-only**; the image must pass the human-face check (on-device ML Kit + the server-side detector on the `id-image` endpoint — the server is the authority). A no-face capture is rejected with the `noFaceDetectedError` snackbar.

The client blocks **Next** until the form fields validate **and** DOB is set
**and** an organisation is picked **and** the male-photo rule is met. **The 1–10
interests rule and the upsert call are enforced on
[Page 007‑01](../Page_007-01/README.md).**

## L-5 — State transitions
```
profile-incomplete ──[pick type (+ ProfileType under Other) + fill required fields]──▶ data-ready
data-ready ──[Next]──▶ Page 007‑01 (interests) — SignUpProfileDraft carried in memory
```
**No persistence on this screen.** The draft = the built `UpsertUserProfileRequest`
(with `interestIds` = any pre-existing picks, replaced via `copyWith` on Page
007‑01) **+** the captured image bytes/filename. The single
`POST /app/account/user-profile` fires on Page 007‑01 (then the image upload); a
successful save there routes to the success screen with the issued
`referenceNumber` (D-373) → wait-for-approval.

## L-6 — Error / empty / RTL handling
- **Initial load failure:** screen-level `profileLoadError` + retry (re-runs all four reads).
- **Per-picker fetch (D-375):** loading spinner while in flight; failure → inline `lookupLoadError` + **retry**; a completed-but-empty organisation search shows `organisationEmpty`. An empty ProfileType lookup under Other shows the same inline retry row — never a blocking error.
- **Validation error:** inline per-field (client-side only here — no server call until Page 007‑01); keep the user's input intact. The DOB / nationality / organisation / male-photo errors render after a blocked Next (`_triedSubmit`).
- **Switching nationality SA↔non-SA:** clears the national-id and Iqama/passport inputs so the derived document section stays consistent (D-373). Switching the Iqama/passport tab clears the number.
- **Back:** discards the in-memory form (nothing was written).
- **RTL:** Arabic is the primary locale; the tabs, pickers and fields mirror; the top back/globe row is forced LTR (frame parity); English name / mobile / plate inputs are LTR; AR/EN labels come from each lookup row.

## L-7 — Dependencies
- Country / organisation / profile-type lookup data must be seeded for the pickers to populate (organisation via the D-220 module; profile types via D-190 — the C5 lock expects the seeded "Normal" row).
- Account must be signed-in; the screen is reached after OTP and whenever the server-computed `profileComplete` flag on `GET /app/users/me` is false (D-374 — the post-sign-in / cold-start gate).
- **BUG-018:** the completeness rule (and the male face-photo hard reject on save) is a **visitor** rule. An account whose `ProfileType.IsForVisitor` is false — an operational partner type such as a gate operator or a moderator — is exempt from the interest / ID-document / face-photo evidence and reads `profileComplete = true` once both names are present, so it is never diverted to this screen. Names stay required for everyone.
- **[Page 007‑01](../Page_007-01/README.md)** is the required next step and owns the interests rule + the save + the image upload.
