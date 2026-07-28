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
>
> **Build #13 (2026-07-22):** a "Meet People Like You" section was added - one
> `SimfCheckbox` bound to `PartnerDirectoryEnabled` (default true). It rides the
> same `GET`/`PUT /admin/site-settings` path (no new permission - `Configuration.*`)
> and the same public `GET /app/site-settings` payload
> (`SiteSettingsResponse.partnerDirectoryEnabled`). When off, the app partner
> directory `GET /app/networking/partner-directory` returns empty and the app hides
> the Home "Meet People" tile. The app-side behaviour is catalogued in
> [`mobile-meet-people.md`](mobile-meet-people.md).

| | |
|--|--|
| **Page** | CP `/admin/site-settings` (`SiteSettingsPage.razor`) |
| **Route** | `/admin/site-settings` (nav item `Module.SiteSettings`) |
| **APIs** | `GET /api/v1/admin/site-settings` (prefill, `Configuration.View`); `PUT /api/v1/admin/site-settings` - `AdminUpdateSiteSettingsRequest`, page sends the registration message + the Build #13 `PartnerDirectoryEnabled` toggle (`Configuration.Edit`). Public read: `GET /api/v1/app/site-settings`; the toggle also gates `GET /api/v1/app/networking/partner-directory`. |
| **Surface** | Control Panel (Blazor) — Administrator |
| **Auth setup** | A signed-in admin with `Configuration.Edit`. Use `Get-Totp` for 2FA — never a literal secret. |
| **Last reviewed** | 2026-07-22 (Build #13 - partner-directory toggle) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CPSET-001 | Page loads, prefilled with the current registration message (defaults when unset); only the two message fields + Save render — **no social section** | happy | P0 | authored ✓ (`SiteSettingsAdminTests` GET) |
| E2E-CPSET-002 | Edit the registration message (AR + EN) → Save → success toast; the value persists and `GET /app/site-settings` returns it | happy | P0 | authored ✓ (`SiteSettingsAdminTests` PUT→GET + public) |
| E2E-CPSET-003 | Saving the message leaves the stored social links **untouched** (the page sends no social → null = unchanged) | edge | P1 | authored ✓ (`SiteSettingsAdminTests` — social set on Org Profile survives a message-only save) |
| E2E-CPSET-004 | Auth gate — a non-admin (or an admin without `Configuration.View`/`Edit`) is forbidden / the nav item is hidden | auth | P0 | authored ✓ (`SiteSettingsAdminTests` admin-gate + `CpNavigationPermissionTests`) |
| E2E-CPSET-005 | A save wire failure (5xx / network) → error toast, the form keeps the entered values | resilience | P1 | spec |
| E2E-CPSET-006 | RTL render (Arabic) — labels + button mirror correctly | i18n | P1 | spec |
| E2E-CPSET-007 | Build #13 - the page loads with the "Meet People Like You" checkbox prefilled from the current `PartnerDirectoryEnabled` state | happy | P0 | authored ✓ (`SiteSettingsAdminTests` GET) |
| E2E-CPSET-008 | Build #13 - un-tick the toggle → Save → success toast; `GET /app/site-settings` returns `partnerDirectoryEnabled=false` and `GET /app/networking/partner-directory` returns an empty list | happy | P0 | authored ✓ (`SiteSettingsAdminTests` PUT→GET + `PartnerDirectoryServiceTests` off→empty) |
| E2E-CPSET-009 | Build #13 - re-tick the toggle → Save → `partnerDirectoryEnabled=true`; the partner directory is populated again | happy | P1 | authored ✓ (`PartnerDirectoryServiceTests` on→entries) |
| E2E-CPSET-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-CPSET-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

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

### E2E-CPSET-007 - Meet-People toggle prefilled from current state (Build #13)

```gherkin
Scenario: The partner-directory checkbox reflects the stored flag
  Given the OrganizationProfile has PartnerDirectoryEnabled = true
  When an admin with Configuration.Edit opens /admin/site-settings
  Then the "Show the 'Meet People Like You' directory in the app" checkbox is ticked
  # GET /admin/site-settings returns SiteSettingsResponse.partnerDirectoryEnabled = true
```

### E2E-CPSET-008 - Turn the directory off (Build #13)

```gherkin
Scenario: An admin disables the Meet People Like You directory
  Given an admin with Configuration.Edit opens /admin/site-settings
  And the "Meet People Like You" checkbox is ticked
  When they un-tick it and tap Save
  Then PUT /admin/site-settings persists PartnerDirectoryEnabled = false and shows the success toast "تم حفظ إعدادات الموقع."
  And GET /api/v1/app/site-settings returns partnerDirectoryEnabled = false
  And GET /api/v1/app/networking/partner-directory returns an empty entries list
  And the app hides the Home "Meet People" tile (driven off the same public flag)
```

### E2E-CPSET-009 - Turn the directory back on (Build #13)

```gherkin
Scenario: An admin re-enables the directory
  Given PartnerDirectoryEnabled is currently false
  When an admin re-ticks the "Meet People Like You" checkbox on /admin/site-settings and taps Save
  Then GET /api/v1/app/site-settings returns partnerDirectoryEnabled = true
  And GET /api/v1/app/networking/partner-directory returns the deduped union of sponsors, speakers, booth companies and opted-in members again
```

---

_Last reviewed:_ `2026-07-22` by `SIMF Team` - Build #13 (Meet People Like You partner-directory toggle; E2E-CPSET-007..009). Prior: D-650 (social links removed; message-only). Original: D-464.
