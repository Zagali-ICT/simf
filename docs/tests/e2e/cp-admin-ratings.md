# E2E test catalogue — Ratings viewer (`/admin/ratings`)

| | |
|--|--|
| **Page** | [`cp/admin-ratings.md`](../../pages/cp/admin-ratings.md) |
| **Route** | `/admin/ratings` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-25 (D-496 — dynamic ratings; rebound to RatingResponses + KPI) |

> **Page shape (D-496).** Read-only viewer of submitted **rating responses**. A two
> stat-card headline (Average overall + Total responses, from `POST
> /account/api/admin/feedback/ratings`), a **per-type KPI** card row (from `GET
> /account/api/admin/feedback/ratings/kpi` — each card = a type's average overall
> with its response count), then a `SimfDataGrid` of responses (Type, Target,
> Overall, Answers, Comment, Active, Submitted-at). **Export-only** (XLSX via
> `/account/api/admin/ratings/export`) — responses are owned by the attendees who
> submit them, so there is no create/edit/delete. The rating *configuration* lives on
> [`/admin/rating-config`](cp-admin-rating-config.md). Gated by
> `PermissionCatalog.Ratings.View`; export by `Ratings.Export`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-RAT-001 | Headline stats — Average overall + Total responses render | happy | P0 | _to author_ |
| E2E-RAT-002 | Per-type KPI cards render (avg overall + response count per type) | happy | P0 | _to author_ |
| E2E-RAT-003 | Responses grid lists rows (Type/Target/Overall/Answers/Comment/Active/Submitted) | happy | P0 | _to author_ |
| E2E-RAT-004 | Global response shows Target "—"; per-session shows the session id | happy | P1 | _to author_ |
| E2E-RAT-005 | Comment per-column filter narrows the grid | happy | P1 | _to author_ |
| E2E-RAT-006 | Sort by Overall / Submitted-at | happy | P2 | _to author_ |
| E2E-RAT-007 | Empty state renders `SimfEmptyState` when there are no responses | happy | P1 | _to author_ |
| E2E-RAT-008 | Export — XLSX downloads (selected rows or filtered set) | happy | P1 | _to author_ |
| E2E-RAT-009 | Auth gate — admin lacking `Ratings.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-RAT-010 | Export gate — admin lacking `Ratings.Export` is forbidden | auth | P1 | _to author_ |
| E2E-RAT-011 | Server 500 on `/feedback/ratings` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-RAT-012 | RTL / Arabic render — page mirrors, headers translate | i18n | P1 | _to author_ |

## Scenarios

### E2E-RAT-001/002/003 — Headline, KPI cards, responses grid

```gherkin
Feature: Admin reviews submitted ratings
  As an Administrator
  I want to see the aggregate and per-type rating results
  So that I can report on attendee satisfaction

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with Ratings.View has signed in via /login + /login/totp
  And at least two attendees have submitted an "App" rating (overall 2 and 4)
  And the administrator has landed on /admin/ratings

Scenario: The page shows the headline stats, per-type KPI cards and the responses grid
  Then the page fires POST /account/api/admin/feedback/ratings and GET /account/api/admin/feedback/ratings/kpi
  And two stat cards render: "Average rating" = "3.0" and "Total ratings" = "2" (or more)
  And under "Ratings by type" a stat card for "App" shows its average overall with "2 responses"
  And the responses grid lists a row per submission with columns
    Type="App", Target="—", Overall="2 / 5" (or "4 / 5"), Answers="0", the comment, the green "Active" pill, and the Submitted-at UTC timestamp
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-ratings-overview.png`
- Console errors: 0 expected
- Network: `/feedback/ratings` and `/feedback/ratings/kpi` both 200

### E2E-RAT-004 — Target column

```gherkin
Scenario: Global vs per-session responses show the Target column correctly
  Given an "App" (global) response and a "Session" (per-session) response exist
  When the administrator views the grid
  Then the App row's Target column reads "—"
  And the Session row's Target column reads the rated session's id
```

### E2E-RAT-008 — Export

```gherkin
Scenario: Export the current responses to XLSX
  Given the responses grid has rows
  When the administrator clicks "Export"
  Then JS invokes simfAccount.downloadXlsx against /account/api/admin/ratings/export
  And an .xlsx file downloads (ZIP magic bytes 50 4B 03 04)
  And its columns are Type, Target, Overall, Comment, Answers, AverageAnswerStars, IsActive, CreatedAt
```

### E2E-RAT-009 / E2E-RAT-010 — Auth + export gates

```gherkin
Scenario: Signed-in admin lacking Ratings.View is denied the page
  Given a signed-in admin whose roles grant no Ratings.View permission
  When they navigate to /admin/ratings
  Then they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/feedback/ratings request fires

Scenario: Admin lacking Ratings.Export is forbidden from the export endpoint
  Given a signed-in admin granted Ratings.View but not Ratings.Export
  When an export is attempted against /admin/ratings/export
  Then the API returns HTTP 403
```

### E2E-RAT-012 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page
  Given the administrator is on /admin/ratings in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar"> and the banner reads "التقييمات"
  And the grid headers read "النوع", "العنصر", "التقييم العام", "الإجابات", "التعليق", "نشط"
  And the "Ratings by type" heading reads "التقييمات حسب النوع"
```

---

## Implementation notes

- **Read-only viewer.** No create/edit/delete — responses belong to attendees. The
  only write surface for ratings is the configuration page (`/admin/rating-config`).
- **KPI endpoint** `GET /admin/feedback/ratings/kpi` returns per-type response counts,
  average overall and per-question averages; the page surfaces the per-type average +
  count as stat cards (per-question averages are available for a future drill-down).
- **API integration tests** at `tests/SIMF.Api.Tests/FeedbackRatingsTests.cs` (admin
  list + average) and `RatingsExcelTests.cs` (export round-trip + `Ratings.Export`
  gate) cover this surface at a lower layer.
- **Permission gates** (HARD RULE): page `[RequirePermission(PermissionCatalog.Ratings.View)]`;
  list/KPI gated `Ratings.View`; export gated `Ratings.Export`.

---

_Last reviewed:_ 2026-06-25 by Claude (D-496 dynamic ratings).
