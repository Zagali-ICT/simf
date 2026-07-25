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
| **Last reviewed** | 2026-07-25 |

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
- **Hero video upload (D-768):** below the link field, an admin can **upload** a video file (mp4/m4v/webm, up to 200 MB) that SIMF serves from its own API (`GET …/app/organization/hero-video.mp4`, range-streamed). Uploading sets the link field to that served URL, so the **Android** home hero plays a moving video (a YouTube hero renders only on iOS — the Android WebView cannot clip into the band, D-761). **Remove uploaded video** reverts the hero to the banner image; a separately-pasted external/YouTube link is left intact.
- **Social:** Facebook, X, Instagram, LinkedIn, YouTube, TikTok, Snapchat (each an absolute http(s) URL).
- **About items** (repeating): title + text (bilingual). **Details** (repeating): name (bilingual) + value **(bilingual — `Value (EN)` + optional `Value (AR)`; D-762)**. A blank `Value (AR)` is stored as null and the app falls back to `Value (EN)` (for a language-neutral value like a year or a URL).

The long field set is grouped into numbered `SimfFormSection` cards with a responsive two-column grid (bilingual pairs sit side by side). The About-items and Details lists render as `simf-repeater` cards — each row has an index, **Up / Down** reorder (persisted as `DisplayOrder`), and **Remove**; an empty list shows a placeholder. Bio and About-item text use multi-line `SimfTextarea` fields (D-762).

## 3. Data flow

`OnInitializedAsync` → `simfAccount.getJson /account/api/admin/organization-profile`
→ prefill. **Save** → `simfAccount.putJson` with `AdminUpdateOrganizationProfileRequest`
(the whole document) → the admin service updates the scalars and reconciles each child
list by id (update existing / insert new / soft-delete removed), touches `UpdatedAt`,
invalidates the public read cache, and audits (`OrganizationProfile.Updated`). The page
re-loads from the PUT response and shows a success toast.

**Hero video (D-768):** the upload posts the file to `simfAccount.uploadFile
/account/api/admin/organization-profile/hero-video`, which streams it to the API
(`POST /admin/organization-profile/hero-video`); the API stores it in the `StoredFile`
store (service `OrganizationHeroVideo`, streamed + seekable), points `BackgroundVideoUrl`
at the served route, and returns the updated profile (the page re-loads from it). **Remove**
calls `simfAccount.deleteJson` on the same route. The public serve `GET
/app/organization/hero-video.mp4` is anonymous and Range-streamed (HTTP 206).

## 4. Validation

Server-side (`OrganizationProfileAdminService`): name/title required; social / website /
live-stream must be absolute http(s) URLs (else `400 ORGANIZATION_PROFILE_INVALID`);
status must be a valid `ForumStatus`; latitude ∈ [−90, 90], longitude ∈ [−180, 180];
lengths clamped to the EF column sizes. Dates accepted as `YYYY-MM-DD`. Hero video
(D-768): the file extension must be `.mp4`/`.m4v`/`.webm` and the size ≤ 200 MB, else
`400 ORGANIZATION_PROFILE_INVALID`.

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
The hero video serve (D-768) is deliberately **anonymous** (public branding shown to
guests) but exposes no GUID — a fixed singleton `.mp4` route serves only the one active
hero video, content-type is pinned and `nosniff` is set; upload/remove stay gated by
`OrganizationProfile.Manage`.

## 8. Related

- Public read: `docs/pages/web` / app About screen (`features/about/about_screen.dart`).
- E2E: [`../../tests/e2e/cp-organization-profile.md`](../../tests/e2e/cp-organization-profile.md).
- Decision: `DECISIONS_LOG` D-495, D-768.

## Changelog

- **2026-07-25 (D-768):** an admin can now **upload** a hero background video that SIMF
  serves from its own API (range-streamed `.mp4`), so a moving hero plays on Android
  (where a YouTube hero cannot). New `FileService.OrganizationHeroVideo` + public serve
  endpoint + a CP upload/remove control; no schema migration (additive enum value).
- **2026-07-24 (D-762):** the Details value is now bilingual (`Value (EN)` + optional
  `Value (AR)`, nullable, additive column `OrganizationDetails.ValueArabic`); the app
  About screen shows the Arabic value in Arabic and falls back to `Value (EN)` when the
  Arabic value is blank. The editor was rebuilt onto numbered `SimfFormSection` cards +
  the `simf-repeater` list pattern (index, Up/Down reorder, Remove, empty-state) and
  multi-line textareas for the long bilingual bodies.
- **2026-06-24 (D-495):** new page.
