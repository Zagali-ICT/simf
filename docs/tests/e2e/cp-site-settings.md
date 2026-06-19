# E2E test catalogue — `Site Settings` (CP `/admin/site-settings`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). D-464 — the labelled CP page
> that edits the public branding settings (the registration welcome message +
> the social-media links). Backed by `GET`/`PUT /admin/site-settings`; the app +
> website consume the values via the public `GET /app/site-settings` (D-461/D-462).

| | |
|--|--|
| **Page** | CP `/admin/site-settings` (`SiteSettingsPage.razor`) |
| **Route** | `/admin/site-settings` (nav item `Module.SiteSettings`) |
| **APIs** | `GET /api/v1/admin/site-settings` (prefill, `Configuration.View`); `PUT /api/v1/admin/site-settings` — `AdminUpdateSiteSettingsRequest` (`Configuration.Edit`). Public read: `GET /api/v1/app/site-settings`. |
| **Surface** | Control Panel (Blazor) — Administrator |
| **Auth setup** | A signed-in admin with `Configuration.Edit`. Use `Get-Totp` for 2FA — never a literal secret. |
| **Last reviewed** | 2026-06-19 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CPSET-001 | Page loads, prefilled with the current effective values (defaults when unset) | happy | P0 | authored ✓ (`SiteSettingsAdminTests` GET) |
| E2E-CPSET-002 | Edit the registration message (AR + EN) + a social URL → Save → success toast; the values persist and `GET /app/site-settings` returns them | happy | P0 | authored ✓ (`SiteSettingsAdminTests` PUT→GET + public) |
| E2E-CPSET-003 | Clearing a social field (blank) → Save → that link becomes inert (public read returns null) | edge | P1 | authored ✓ (`SiteSettingsAdminTests` blank-clears) |
| E2E-CPSET-004 | Auth gate — a non-admin (or an admin without `Configuration.View`/`Edit`) is forbidden / the nav item is hidden | auth | P0 | authored ✓ (`SiteSettingsAdminTests` admin-gate + `CpNavigationPermissionTests`) |
| E2E-CPSET-005 | A save wire failure (5xx / network) → error toast, the form keeps the entered values | resilience | P1 | spec |
| E2E-CPSET-006 | RTL render (Arabic) — labels/sections/button mirror; URL fields stay LTR | i18n | P1 | spec |

## Scenarios

### E2E-CPSET-002 — Edit + save flows through to the public endpoint

```gherkin
Feature: CP Site Settings
Scenario: An admin sets the welcome message and a social link
  Given an admin with Configuration.Edit opens /admin/site-settings
  And the form is prefilled from GET /admin/site-settings
  When they set the Arabic welcome message to "تهانينا، مرحباً بكم في الملتقى السعودي الرابع"
  And set Instagram to "https://instagram.com/simf"
  And tap Save
  Then PUT /admin/site-settings upserts the keys and shows the success toast
  And GET /api/v1/app/site-settings returns the new message + Instagram URL
  And the app registration-success screen + home social row reflect them (D-462)
```

### E2E-CPSET-003 — Blank clears a link

```gherkin
Scenario: Removing a social link
  Given Instagram is set
  When the admin clears the Instagram field and taps Save
  Then the public read returns Instagram = null and the app keeps that button inert (D-369)
```

### E2E-CPSET-004 — Auth gate

```gherkin
Scenario: Only admins with the Configuration permission reach the page
  Given a signed-in non-admin
  When they request /api/v1/admin/site-settings
  Then the API returns 403 Forbidden
  And the CP nav hides the Site Settings item for accounts without Configuration.View
```

---

_Last reviewed:_ `2026-06-19` by `SIMF Team` — D-464 (new page).
