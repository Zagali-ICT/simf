# Speakers — Logic (`/admin/speakers` · المتحدّثون)

The rules behind the page: validation + normalisation, uniqueness, referential
checks, ordering, audit, and the field mapping to the app/website. Traced to
`AdminSpeakerService`, `SpeakersAddEdit.razor`, `SpeakersExcelEndpoints.cs` and
`PublicSpeakerService` contracts.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

## L-1 — Validation + normalisation (`AdminSpeakerService.ValidateAndNormalise`)

Applied on **both** create and update, server-side (the client mirrors them):

| Field | Rule | On failure |
|-------|------|------------|
| Code | trimmed + **upper-cased** (invariant); length 2–16 | `SPEAKER_INVALID` 400 — "Speaker code must be between 2 and 16 characters." / "يجب أن يتراوح طول رمز المتحدّث بين 2 و 16 حرفاً." |
| Name (EN) | trimmed; length 1–128 | `SPEAKER_INVALID` 400 — EN/AR bilingual |
| Name (AR) | trimmed; length 1–128 | `SPEAKER_INVALID` 400 — EN/AR bilingual |
| DisplayOrder | must be ≥ 0 | `SPEAKER_INVALID` 400 — "Display order must be zero or a positive integer." |
| Facebook / LinkedIn / X URL | each ≤ 256 chars (`ValidateSocialUrls`) | `SPEAKER_INVALID` 400 |
| CountryId | when set, must reference an **existing active** Country (`EnsureCountryIsValidAsync`) | `SPEAKER_INVALID` 400 — "Country id '{id}' does not exist or is inactive." |
| ContactId | when set, must reference an **existing active** Contact (`EnsureContactIsValidAsync`) | `SPEAKER_INVALID` 400 — "Contact id '{id}' does not exist or is inactive." |

- **Optional text fields** are normalised through `NullIfBlank` — a blank/whitespace
  Rank, bio, qualifications, training, awards or social URL is stored as `NULL`.
- **Client mirror** (`SpeakersAddEdit.HandleSubmitAsync`): Code 2–16, Name ≤ 128,
  NameArabic ≤ 128, Display order parses to ≥ 0, Country id parses to > 0. A
  guard failure shows a `SimfAlert` (`Admin.Speakers.Field.CodeInvalid` /
  `NameInvalid` / `NameArabicInvalid` / `DisplayOrderInvalid` / `CountryInvalid`)
  and fires **no** request.

## L-2 — Code uniqueness

Codes are unique case-insensitively (they are stored upper-cased). On **create**
the clash check always runs; on **update** it runs **only when the code changes**
(`!string.Equals(old, new, OrdinalIgnoreCase)`). A clash →
`SPEAKER_CODE_DUPLICATE` 409, bilingual, surfacing the code: "A speaker with code
'{code}' already exists." / "يوجد متحدّث بالرمز '{code}' بالفعل."

## L-3 — Cross-context links (D-157 separation)

- **`UserProfileId`** (`Guid?`) is a logical FK to `SimfIdentityDbContext` — a
  **bare Guid**, NOT a DB constraint and NOT validated here. The link can be
  authored before the user account exists (import/migration), and a stale FK
  degrades gracefully to "no linked account" on the public page. It is
  **never** surfaced on the public projection.
- **`CountryId`** and **`ContactId`** are same-context (`SimfAppDbContext`) FKs,
  validated against the live `Country` / `Contact` tables (active-only) to return
  a clean 400 rather than a DB FK-violation 500.

## L-4 — Listing, filtering, ordering (`ListAllAsync`)

- `Skip = max(0, query.Skip)`; `Top = clamp(query.Top>0 ? query.Top : 25, 1, 200)`.
- **Search** matches the trimmed term (`EF.Functions.Like %term%`) against Code,
  Name OR NameArabic.
- **`isActive` filter** — when the query carries `Filters["isActive"]` parseable
  as a bool, rows are restricted to that active state.
- **Sort:** `code` / `name` / `displayorder` (asc/desc); default
  `OrderBy(DisplayOrder).ThenBy(Name)`.
- The page resolves country EN/AR names with a **single second query** over the
  distinct `CountryId`s (no cross-DB join), so the grid renders the country
  column without a per-row fetch.

## L-5 — Soft-delete (idempotent)

`DeactivateAsync` sets `IsActive = false` + stamps `UpdatedAt`. If the speaker is
**already inactive** it **early-returns** (writes no change, no second audit
row) — so re-deactivation is idempotent and still returns 200. There is no
"in-use" guard today (`SPEAKER_IN_USE` is reserved in `ErrorCodes`). Reactivation
is via the Edit form's **Active** checkbox.

## L-6 — Audit (`AuditEvents`)

| Action | Event | Detail string |
|--------|-------|---------------|
| Create | `Speaker.Created` | `id=…; code=…; name=…` |
| Update | `Speaker.Updated` | `id=…; code=…; active=…` |
| Deactivate | `Speaker.Deactivated` | `id=…; code=…` |

Each `AuditOutcome.Success` with the actor user id in `ActorUserId`.

## L-7 — Excel mapping (D-356)

- **Export columns** (sheet "Speakers"):
  `Code | Name | NameArabic | Rank | Country | DisplayOrder | IsActive` — the
  Country cell is `CountryNameEn` (read-only display). Lists through the same
  `ListAllAsync`, so the export honours the current filter; capped at 5000 rows.
- **Import** is **insert-only**, bound columns `Code | Name | NameArabic | Rank
  | DisplayOrder` (required: Code/Name/NameArabic). Each row → an
  `AdminCreateSpeakerRequest` (Code upper-cased) through the same service, so it
  reuses L-1/L-2 (bounds + duplicate-code) — a duplicate Code is a **per-row**
  error, never a batch abort. The numeric Country FK, rich-text, social URLs and
  consent flags are intentionally **omitted** (can't be expressed safely flat);
  set them via Edit afterwards.

## L-8 — Field mapping to the app / website (downstream contract)

The speaker fields this page owns map to the public reads as follows:

| CP field (`AdminSpeakerDetail`) | App/website surface |
|---------------------------------|---------------------|
| Name / NameArabic | Speakers list card + profile + session speaker card |
| Rank | the rank line on the card / the `title` on the session speaker card |
| CountryId (+ EN/AR name) | the **flag** (rendered from `countryId`) + nationality label |
| PhotoRelativePath / `HasPhotoAsset` (D-357) | the **avatar**; when `HasPhotoAsset` the client/website prefer `/content|/app/assets/SpeakerPhoto/{id}/image` over the legacy path |
| Bio / Qualifications / TrainingExperience / Awards (EN/AR) | the four bilingual rich-text tabs on the **Speaker profile** (Mockup page 20) |
| AllowsMeetingRequests | drives the client's **"Request meeting"** affordance |
| AllowsDataSharing | the social URLs are returned **only** when this is true |
| FacebookUrl / LinkedInUrl / XUrl | the opted-in social links on the profile |
| DisplayOrder | the stable public ordering (`GET /app/speakers` orders by it) |
| IsActive | only active speakers are returned by the public reads |
| UserProfileId | **not** surfaced publicly (privacy) |

### Session ↔ speaker role (D-225)

A speaker also appears inside the programme. Each session carries an ordered
`PublicSessionSpeaker[]` whose **`role`** is `SessionSpeakerRole` (**0 = Speaker,
1 = Host**, D-225) — the app's "host" marker. The wire is an **int** (no
`JsonStringEnumConverter` in `SIMF.Api`, D-299); the Flutter client decodes
tolerantly (int **or** name; unknown → safe default). Those speaker cards carry
the **same** D-271 country (id + EN/AR names) + photo fields on both the session
list and the session detail. See
[`docs/App/Page_016/Page_016_API.md`](../../App/Page_016/Page_016_API.md).

## L-9 — Edge cases / known limitations

- **Code casing** — "spk-001" and "SPK-001" are the same code (stored upper).
- **Country picker resilience** — if `/admin/countries/list` fails on first
  render the picker stays empty; the admin can still submit with no country.
- **Import scope** — narrow by design (Code / Name / NameArabic / Rank /
  DisplayOrder); the rest is set via Edit.
- **EN resx gap (reported only):** `Admin.Speakers.Delete.Title` /
  `Admin.Speakers.Delete.Message` are missing from the English resx (present in
  `Strings.ar.resx`); the EN `SimfConfirm` falls back to the keys until added.

## Cross-links

- Design: [admin-speakers_Design.md](admin-speakers_Design.md)
- API: [admin-speakers_API.md](admin-speakers_API.md)
- Function: [admin-speakers_Function.md](admin-speakers_Function.md)
- Page index: [`docs/pages/cp/admin-speakers.md`](../../pages/cp/admin-speakers.md)
- E2E: [`docs/tests/e2e/cp-admin-speakers.md`](../../tests/e2e/cp-admin-speakers.md)
- App consumer: [`docs/App/Page_016/Page_016_Logic.md`](../../App/Page_016/Page_016_Logic.md)
