# E2E test catalogue — `Site Settings` (CP `/admin/site-settings`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). D-464 — the labelled CP page
> that edits the public **registration welcome message** (bilingual). Backed by
> `GET`/`PUT /admin/site-settings`; the app + website consume the value via the
> public `GET /app/site-settings` (D-461/D-462).
>
> **D-650 (2026-07-07):** the **social-media links were removed from this page** —
> they are now edited only on the **Organization Profile** page (`/admin/organization-profile`),
> which is the single source of truth (both feeds already read/write the same
> `OrganizationProfile` columns — D-495). Social-link scenarios therefore live in
> the Organization Profile catalogue, not here. The API `PUT /admin/site-settings`
> still *accepts* social fields (the `SiteSettingsAdminTests` round-trip), but the
> page no longer sends them (unset → null → left unchanged).

| | |
|--|--|
| **Page** | CP `/admin/site-settings` (`SiteSettingsPage.razor`) |
| **Route** | `/admin/site-settings` (nav item `Module.SiteSettings`) |
| **APIs** | `GET /api/v1/admin/site-settings` (prefill, `Configuration.View`); `PUT /api/v1/admin/site-settings` — `AdminUpdateSiteSettingsRequest`, page sends only the registration message (`Configuration.Edit`). Public read: `GET /api/v1/app/site-settings`. |
| **Surface** | Control Panel (Blazor) — Administrator |
| **Auth setup** | A signed-in admin with `Configuration.Edit`. Use `Get-Totp` for 2FA — never a literal secret. |
| **Last reviewed** | 2026-07-07 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CPSET-001 | Page loads, prefilled with the current registration message (defaults when unset); only the two message fields + Save render — **no social section** | happy | P0 | authored ✓ (`SiteSettingsAdminTests` GET) |
| E2E-CPSET-002 | Edit the registration message (AR + EN) → Save → success toast; the value persists and `GET /app/site-settings` returns it | happy | P0 | authored ✓ (`SiteSettingsAdminTests` PUT→GET + public) |
| E2E-CPSET-003 | Saving the message leaves the stored social links **untouched** (the page sends no social → null = unchanged) | edge | P1 | authored ✓ (`SiteSettingsAdminTests` — social set on Org Profile survives a message-only save) |
| E2E-CPSET-004 | Auth gate — a non-admin (or an admin without `Configuration.View`/`Edit`) is forbidden / the nav item is hidden | auth | P0 | authored ✓ (`SiteSettingsAdminTests` admin-gate + `CpNavigationPermissionTests`) |
| E2E-CPSET-005 | A save wire failure (5xx / network) → error toast, the form keeps the entered values | resilience | P1 | spec |
| E2E-CPSET-006 | RTL render (Arabic) — labels + button mirror correctly | i18n | P1 | spec |

## Scenarios

### E2E-CPSET-001 — Page renders message-only (no social)

```gherkin
Feature: CP Site Settings
Scenario: The page shows only the registration welcome message
  Given an admin with Configuration.Edit opens /admin/site-settings
  And the form is prefilled from GET /admin/site-settings
  Then only the two "Welcome message (Arabic/English)" fields + Save are shown
  And there is NO "Social links" section (moved to Organization Profile, D-650)
```

### E2E-CPSET-002 — Edit + save flows through to the public endpoint

```gherkin
Scenario: An admin sets the welcome message
  Given an admin with Configuration.Edit opens /admin/site-settings
  When they set the Arabic welcome message to "تهانينا، مرحباً بكم في الملتقى السعودي الرابع"
  And tap Save
  Then PUT /admin/site-settings persists the message and shows the success toast
  And GET /api/v1/app/site-settings returns the new message
  And the app registration-success screen reflects it (D-462)
```

### E2E-CPSET-003 — A message-only save never disturbs social links

```gherkin
Scenario: Social links survive a Site Settings save
  Given social links are set on the Organization Profile page
  When an admin edits only the welcome message on /admin/site-settings and taps Save
  Then GET /app/site-settings and GET /app/organization-profile still return the social links
  # SaveSiteSettingsAsync uses null-means-unchanged; the page sends no social fields
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

_Last reviewed:_ `2026-07-07` by `SIMF Team` — D-650 (social links removed; page is now message-only). Original: D-464.
