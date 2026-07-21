# CP - Site Settings (`/admin/site-settings`)

| | |
|--|--|
| **Route** | `/admin/site-settings` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel (Blazor Server) |
| **Audience** | Administrator |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Configuration.Edit)]`; API `GET` gated by `Configuration.View`, `PUT` by `Configuration.Edit` + `RequireApprovedAccount`. **No new permission for Build #13** - the Meet-People toggle reuses `Configuration.*`. |
| **Pattern** | Single-record editor - full-document `GET` + partial (`null` = no change) `PUT` |
| **Status** | Real (D-464; Build #13 partner-directory toggle 2026-07-21) |
| **Backend** | `GET`/`PUT /api/v1/admin/site-settings` (via BFF `/account/api/admin/site-settings`); public read `GET /api/v1/app/site-settings`; the toggle also gates `GET /api/v1/app/networking/partner-directory` |
| **Source** | `Components/Pages/Admin/SiteSettingsPage.razor` (+ `.razor.cs`) |
| **Tests** | `tests/SIMF.Api.Tests/SiteSettingsAdminTests.cs`, `SiteSettingsPublicTests.cs`, `PartnerDirectoryServiceTests.cs`, `CpNavigationPermissionTests` |
| **Implements** | D-464 (registration message), D-650 (social links moved out), Build #13 (partner-directory switch) |
| **Last reviewed** | 2026-07-22 |

## 1. Purpose

Edit the small set of public, CP-managed site/app settings the mobile app and
website consume:

- the bilingual **registration welcome message** shown when a visitor finishes
  registering (D-464), and
- the **Meet People Like You** partner-directory switch (Build #13) that turns
  the app's partner directory on or off.

The **social-media links are not edited here** - they were moved to the
Organization Profile page (`/admin/organization-profile`), the single source of
truth (D-650/D-495). The `PUT /admin/site-settings` request still carries the
social fields for the round-trip, but this page never sends them (unset → null →
left unchanged).

## 2. Fields

| Field | Control | MaxLength | Notes |
|-------|---------|-----------|-------|
| Welcome message (Arabic) | `SimfTextField` | 2048 | `RegistrationMessageAr`; falls back to the in-code default when blank |
| Welcome message (English) | `SimfTextField` | 2048 | `RegistrationMessageEn`; falls back to the in-code default when blank |
| Show the "Meet People Like You" directory in the app | `SimfCheckbox` | bool | Build #13 - `PartnerDirectoryEnabled` (default true) |

The registration fields sit under the `Admin.SiteSettings.Section.Registration`
heading; the checkbox sits under `Admin.SiteSettings.Section.PartnerDirectory`
("Meet People Like You" / "قابل أشخاصاً مثلك") with the hint "Controls the partner
directory in the mobile app (sponsors, speakers, exhibition companies, and
opted-in members)."

## 3. Data flow

`OnInitializedAsync` → `simfAccount.getJson /account/api/admin/site-settings` →
prefill the two message fields and the checkbox from `SiteSettingsResponse`
(`RegistrationSuccessMessageAr/En`, `PartnerDirectoryEnabled`). A load failure
shows the `Admin.SiteSettings.LoadFailed` error toast.

**Save** → `simfAccount.putJson` with an `AdminUpdateSiteSettingsRequest` carrying
only `RegistrationMessageAr/En` + `PartnerDirectoryEnabled` (the social fields are
left null = unchanged). On success it shows the `Admin.SiteSettings.Saved` toast
("تم حفظ إعدادات الموقع."); on failure `Admin.SiteSettings.SaveFailed`.

The registration message, social links and the partner-directory flag all persist
on the singleton `OrganizationProfile` row (consolidated there under D-495), so:

- the public **`GET /api/v1/app/site-settings`** returns the same
  `SiteSettingsResponse` shape - now including `partnerDirectoryEnabled` (an
  append-only field defaulting to `true` / fail-open), and
- the Build #13 partner-directory read **`GET /api/v1/app/networking/partner-directory`**
  returns an **empty** list when `PartnerDirectoryEnabled` is off. The app also
  hides the Home "Meet People" tile off the same public `site-settings` flag.

## 4. Validation

Message fields are free text clamped to 2048 chars (EF column size); a blank value
clears the setting and the public read substitutes the in-code default. The toggle
is a plain boolean. `null` on any request field means "no change" (partial update),
which is how a message-only save leaves the social links and (if unsent) the flag
untouched.

## 5. Edge cases

- On first load the singleton is seeded, so the page is never empty; the toggle
  defaults to **on** (`true`).
- A save that omits the social fields never disturbs the social links stored on
  the Organization Profile (null = unchanged).
- Turning the toggle **off** immediately empties the app partner directory and
  hides the Home entry point; turning it back **on** restores both. No migration
  or redeploy is involved - it is a data change on one row
  (`OrganizationProfile.PartnerDirectoryEnabled`, additive migration
  `AddPartnerDirectoryEnabled`).

## 6. i18n / RTL

`Admin.SiteSettings.*` resx keys (Title, Loading, Save, Saving, Saved, LoadFailed,
SaveFailed, the two `Section.*` headings + hints, and the field labels). EN ↔ AR
parity is maintained and the page mirrors to RTL under Arabic. Exact Arabic
phrasings live in the resx files and are quoted in the E2E catalogue.

## 7. Security

Both `GET` and `PUT /admin/site-settings` require an approved admin holding the
`Configuration.View` / `Configuration.Edit` permission; the CP nav hides the item
for accounts without it (`CpNavigationPermissionTests`). The Build #13 toggle adds
no new permission and no new endpoint on this page - it rides the existing
`Configuration.*`-gated save. The public `GET /app/site-settings` read is anonymous
(branding loads pre-login) but carries no secret.

## 8. Related

- Registration message consumer: app registration-success screen (D-462).
- Social links (moved out): [`organization-profile.md`](organization-profile.md).
- Partner directory (the app screen the toggle controls):
  [`../mobile/meet-people/README.md`](../mobile/meet-people/README.md).
- E2E: [`../../tests/e2e/cp-site-settings.md`](../../tests/e2e/cp-site-settings.md).
- Decisions: `DECISIONS_LOG` D-464 (page), D-650 (social links moved), D-495
  (settings consolidated onto `OrganizationProfile`), Build #13 (partner-directory
  toggle).

## Changelog

- **2026-07-22 (Build #13):** added the "Meet People Like You" section - a
  `SimfCheckbox` bound to `PartnerDirectoryEnabled`, saved through the existing
  site-settings load/save path; new page reference doc authored.
- **2026-07-07 (D-650):** social links removed from this page (now edited only on
  Organization Profile). Page became message-only.
- **(D-464):** original - CP editor for the bilingual registration welcome message.
