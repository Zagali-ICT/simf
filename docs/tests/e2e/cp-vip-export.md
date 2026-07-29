# E2E test catalogue — `VIP welcome export` (`/admin/visitors/vip/export`)

> **Authority:** SIMF E2E test catalogue (D-133 / D-245). VVIP/VIP feature D-429 (V-3).

| | |
|--|--|
| **Page** | [`vip-export.md`](../../pages/cp/vip-export.md) |
| **Route** | `/admin/visitors/vip/export` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver _(or: Playwright when adopted)_ |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Last reviewed** | 2026-06-15 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-VIPX-001 | Golden path — roster lists VVIP/VIP with Mawj data + photo | happy | P0 | _to author_ |
| E2E-VIPX-002 | Non-VIP tiers excluded from the roster | happy | P0 | _to author_ |
| E2E-VIPX-003 | Auth gate (no ExportVip permission → /not-permitted) | auth | P0 | _to author_ |
| E2E-VIPX-004 | Empty state (no VVIP/VIP registered) | happy | P1 | _to author_ |
| E2E-VIPX-005 | CSV download contains headers + Mawj ID | happy | P0 | _to author_ |
| E2E-VIPX-006 | Excel (xlsx) download opens with the roster | happy | P1 | _to author_ |
| E2E-VIPX-007 | JSON API returns the roster | happy | P1 | _to author_ |
| E2E-VIPX-008 | RTL render (Arabic) | i18n | P1 | _to author_ |
| E2E-VIPX-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-VIPX-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-VIPX-001 — Golden path: roster lists VVIP/VIP

```gherkin
Feature: VIP welcome export roster
  As an Administrator with the VIP-export permission
  I want to see and download the VVIP/VIP welcome roster
  So that I can share the Mawj welcome data with the technical teams

Background:
  Given an Administrator with "Visitors.ExportVip" is signed in
  And a VVIP guest "Khalid" with Mawj ID "MAWJ-10293" and a photo exists
  And a VIP guest "Sara" with Mawj ID "MAWJ-44551" exists

Scenario: The roster shows both VIP guests
  Given I am on "/admin/visitors/vip/export"
  Then the grid lists "Khalid" (VVIP) and "Sara" (VIP)
  And each row shows the Mawj ID, honorific, tier and contact
  And Khalid's row shows a photo thumbnail with a download link
```

**Evidence captured:** screenshot; console 0 errors; network 0 failed; the vip-photo `<img>` loads (200).

### E2E-VIPX-002 — Non-VIP tiers excluded

```gherkin
Scenario: A Normal-tier visitor never appears in the VIP roster
  Given a "Normal" visitor "Ahmed" exists
  When I open "/admin/visitors/vip/export"
  Then "Ahmed" is NOT listed
```

### E2E-VIPX-003 — Auth gate

```gherkin
Scenario: An admin without ExportVip cannot open the page
  Given an Administrator without "Visitors.ExportVip" is signed in
  When I navigate to "/admin/visitors/vip/export"
  Then I am redirected to "/not-permitted"
```

### E2E-VIPX-004 — Empty state

```gherkin
Scenario: No VVIP/VIP visitors registered yet
  Given no VVIP or VIP visitor exists
  When I open "/admin/visitors/vip/export"
  Then the empty state "No VVIP or VIP visitors have been registered yet." shows
```

### E2E-VIPX-005 — CSV download

```gherkin
Scenario: Download the roster as CSV
  Given I am on "/admin/visitors/vip/export"
  When I click "Download CSV"
  Then a "vip-welcome-roster.csv" file downloads
  And it contains the "Mawj ID" header and the registered Mawj IDs
  And it opens in Excel with the Arabic columns intact (UTF-8 BOM)
```

### E2E-VIPX-006 — Excel download

```gherkin
Scenario: Download the roster as Excel
  Given I am on "/admin/visitors/vip/export"
  When I click "Download Excel"
  Then a "vip-welcome-roster.xlsx" file downloads
  And it opens with one header row + one row per VIP guest
```

### E2E-VIPX-007 — JSON API

```gherkin
Scenario: The Mawj integration reads the JSON roster
  Given a bearer token with "Visitors.ExportVip"
  When I GET "/api/v1/admin/visitors/vip/roster"
  Then the response is an ApiResult with the VVIP/VIP rows
  And each row carries UserId, MawjId, Honorific, PreferredLanguage, Tier, HasVipPhoto
```

### E2E-VIPX-008 — RTL render

```gherkin
Scenario: Arabic UI renders right-to-left with no overflow
  Given the UI culture is "ar"
  When I open "/admin/visitors/vip/export"
  Then the grid + tier pills render in Arabic
  And scrollWidth == clientWidth (no horizontal overflow)
```
