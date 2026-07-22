# CP — Organization Profile (`/admin/organization-profile`)

| | |
|--|--|
| **Route** | `/admin/organization-profile` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel (Blazor Server) |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.OrganizationProfile.View)]`; Save gated by `OrganizationProfile.Manage` (`<AuthorizedAction>`) |
| **Pattern** | Single-record editor — full-document `GET` + `PUT` |
| **Status** | ✅ Real (D-495) |
| **Backend** | `GET`/`PUT /api/v1/admin/organization-profile` (via BFF `/account/api/admin/organization-profile`); public read `GET /api/v1/app/organization-profile` |
| **Source** | `Components/Pages/Admin/OrganizationProfilePage.razor` |
| **Tests** | `tests/SIMF.Api.Tests/OrganizationProfileTests.cs`, `CpNavigationPermissionTests` |
| **Implements** | D-495 — edition-generic forum config |
| **Last reviewed** | 2026-06-24 |

## 1. Purpose

Edit the single, edition-generic "project / about" record for the forum — the data
the app + website read so the same build re-skins to any edition by editing data,
not code. One singleton row (`OrganizationProfile`) plus two child lists
(`OrganizationAboutItems`, `OrganizationDetails`); social links + the single website
are columns. Logo is `Asset`-backed (category `OrganizationLogo`).

## 2. Fields

- **Identity:** name, title, slogan, bio (each bilingual EN/AR).
- **Edition:** current year, status (`Soon` / `Open` / `Archived`), event start/end dates, version, system version.
- **Location:** location text (bilingual), latitude, longitude.
- **Contact:** phone, email, website.
- **Live stream:** main home-page YouTube link.
- **Hero background video (D-756):** a YouTube link (or a direct MP4/HLS link) played muted + looping behind the home hero on both the app and the website; blank falls back to the bundled hero media. Absolute http(s), max 1024.
- **Social:** Facebook, X, Instagram, LinkedIn, YouTube, TikTok, Snapchat (each an absolute http(s) URL).
- **About items** (repeating): title + text (bilingual). **Details** (repeating): name (bilingual) + value.

## 3. Data flow

`OnInitializedAsync` → `simfAccount.getJson /account/api/admin/organization-profile`
→ prefill. **Save** → `simfAccount.putJson` with `AdminUpdateOrganizationProfileRequest`
(the whole document) → the admin service updates the scalars and reconciles each child
list by id (update existing / insert new / soft-delete removed), touches `UpdatedAt`,
invalidates the public read cache, and audits (`OrganizationProfile.Updated`). The page
re-loads from the PUT response and shows a success toast.

## 4. Validation

Server-side (`OrganizationProfileAdminService`): name/title required; social / website /
live-stream must be absolute http(s) URLs (else `400 ORGANIZATION_PROFILE_INVALID`);
status must be a valid `ForumStatus`; latitude ∈ [−90, 90], longitude ∈ [−180, 180];
lengths clamped to the EF column sizes. Dates accepted as `YYYY-MM-DD`.

## 5. Edge cases

- Empty about/detail lists are allowed. Removing all rows + Save soft-deletes them.
- A blank social/website URL clears the field (public read returns null → inert link).
- The singleton is seeded (2026 edition), so the page is never empty on first load.

## 6. i18n / RTL

Arabic mirrors the layout; URL + GPS fields stay LTR. Bilingual fields are paired EN/AR.

## 7. Security

Read of the *public* projection is anonymous (branding loads pre-login) but carries no
secret; all writes require `OrganizationProfile.Manage` + an approved account and are
audited. URLs are http(s)-sanitised on read (D-467); text renders as text (no HTML).

## 8. Related

- Public read: `docs/pages/web` / app About screen (`features/about/about_screen.dart`).
- E2E: [`../../tests/e2e/cp-organization-profile.md`](../../tests/e2e/cp-organization-profile.md).
- Decision: `DECISIONS_LOG` D-495.

## Changelog

- **2026-06-24 (D-495):** new page.
