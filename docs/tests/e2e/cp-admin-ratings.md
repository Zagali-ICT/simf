# E2E test catalogue — Ratings (read-only) (`/admin/ratings`)

| | |
|--|--|
| **Page** | [`cp/admin-ratings.md`](../../pages/cp/admin-ratings.md) |
| **Route** | `/admin/ratings` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page shape (read this first).** `/admin/ratings` is a **read-only** admin view
> of attendee forum ratings (D-199, Mockup screen 40 "Rate the Forum"). There is
> **no Add / Edit / Delete / Deactivate** on this page — ratings are owned by the
> attendees who submit them via `POST /api/v1/feedback/rate`. The CP page therefore
> exposes exactly these functions, all of which the catalogue below must drive and
> assert:
>
> 1. On load, a single `POST /account/api/admin/feedback/ratings` (BFF) → API
>    `POST /api/v1/admin/feedback/ratings` with body `GridQuery { Top = 50 }`.
> 2. Two headline **stat cards** — "Average rating" (`AverageStars`, formatted `0.0`,
>    invariant) and "Total ratings" (`RatingCount`).
> 3. A **`simf-table`** with four columns: Stars (`{n} / 5`), Comment (`—` when blank),
>    Active (`✓` / `—`), Submitted at (`yyyy-MM-dd HH:mm:ss 'UTC'`).
> 4. A **pager summary** line: "Showing {from}–{to} of {total}".
> 5. The **`SimfEmptyState`** ("No ratings yet.") when the active set is empty.
> 6. A **`SimfAlert` error** banner + load-failed copy when the list call fails.
>
> The "golden CRUD round-trip" therefore becomes a **golden read round-trip**: an
> attendee submits a rating through the public API, then the admin page renders it
> in the grid with the recomputed average. Inactive (soft-deleted) ratings are
> excluded by the service (`.Where(rating => rating.IsActive)`), so the grid only
> ever shows `Active = ✓`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-RAT-001 | Golden read round-trip — attendee rates via API → admin grid + average update | happy | P0 | _to author_ |
| E2E-RAT-002 | Stat cards render Average (`0.0`) + Total over the active set | happy | P1 | _to author_ |
| E2E-RAT-003 | Table columns render exactly (Stars `n/5`, blank Comment → `—`, Active `✓`, UTC timestamp) | happy | P1 | _to author_ |
| E2E-RAT-004 | Pager summary line "Showing {from}–{to} of {total}" | happy | P2 | _to author_ |
| E2E-RAT-005 | Read-only guarantee — no Add/Edit/Delete/Deactivate controls anywhere | happy | P1 | _to author_ |
| E2E-RAT-006 | Empty state renders `SimfEmptyState` ("No ratings yet.") | happy | P1 | _to author_ |
| E2E-RAT-007 | Auth gate — signed-in admin lacking `Ratings.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-RAT-008 | Inactive ratings excluded — soft-deleted row never appears, count/average ignore it | error | P1 | _to author_ |
| E2E-RAT-009 | Server 500 on `/ratings` → `SimfAlert` error + bilingual load-failed copy, no rows | resilience | P2 | _to author_ |
| E2E-RAT-010 | RTL / Arabic render — page, stat cards, table headers mirror | i18n | P1 | _to author_ |

## Scenarios

### E2E-RAT-001 — Golden read round-trip

```gherkin
Feature: Ratings read-only admin view — golden round-trip
  As an Administrator with the Ratings.View permission
  I want to see the attendee forum ratings and the headline average
  So that I can report how the forum was received

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Ratings.View permission signs in via /login + /login/totp
    using superadmin@zagali-ict.com and a TOTP from the Get-Totp helper

Scenario: An attendee rating shows up in the admin grid with a recomputed average
  Given an approved attendee submits POST /api/v1/feedback/rate with body { "Stars": 5, "Comment": "Excellent forum" }
  And the API returns HTTP 200 with ApiResult.Data.Stars = 5 and UpdatedAt = null
  When the administrator navigates to /admin/ratings
  Then the page issues exactly one POST /account/api/admin/feedback/ratings with body { "Top": 50 }
  And the BFF forwards it to API POST /api/v1/admin/feedback/ratings and returns HTTP 200
  And the SimfBanner title reads "Ratings"
  And the "Average rating" stat card reflects the average of the active set (e.g. "5.0" when this is the only rating)
  And the "Total ratings" stat card equals the active rating count (e.g. "1")
  And a table row exists with Stars="5 / 5", Comment="Excellent forum", Active="✓", and a Submitted-at value in "yyyy-MM-dd HH:mm:ss UTC" form
  And the pager summary reads "Showing 1–1 of 1"

  When the same attendee re-submits POST /api/v1/feedback/rate with body { "Stars": 3, "Comment": "Good on day two" }
  And the API returns HTTP 200 (upsert — same rating Id, UpdatedAt now stamped)
  And the administrator reloads /admin/ratings
  Then the "Total ratings" stat card is unchanged (still one row per attendee)
  And the row for that attendee now shows Stars="3 / 5" and Comment="Good on day two"
  And the "Average rating" stat card has recomputed accordingly
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-ratings-golden-before.png`
- Screenshot after: `docs/screenshots/cp-admin-ratings-golden-after.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/feedback/ratings` call returns 200; the round-trip
  attendee write `/api/v1/feedback/rate` returns 200
- Audit rows: the attendee write produces an `OperationLog`/audit entry with
  `EventType = AuditEvents.RatingSubmitted` on first submit and
  `AuditEvents.RatingRevised` on the upsert (actor = the attendee, **not** the admin —
  the admin view performs no writes and emits no audit row)

### E2E-RAT-002 — Stat cards

```gherkin
Scenario: Average and Total stat cards compute over the active set
  Given two approved attendees submit ratings of 2 stars and 4 stars via POST /api/v1/feedback/rate
  When the administrator opens /admin/ratings
  Then the "Average rating" stat card reads "3.0" (formatted "0.0", invariant culture — a dot decimal even in Arabic)
  And the "Total ratings" stat card reads "2"
  And both cards render as SimfStatCard components laid out in the .simf-form__actions flex row
```

### E2E-RAT-003 — Table columns

```gherkin
Scenario: Table renders the four columns with the documented formatting
  Given the active set contains one rating of 4 stars with no comment
  When the administrator opens /admin/ratings
  Then the table header row reads: "Stars", "Comment", "Active", "Submitted at"
  And the single body row shows:
    | Stars | Comment | Active | Submitted at                |
    | 4 / 5 | —       | ✓      | <UTC timestamp yyyy-MM-dd HH:mm:ss UTC> |
  And the Comment cell renders the em-dash placeholder "—" because the comment is blank
  And the Active cell renders "✓" because the page only ever lists active ratings
```

### E2E-RAT-004 — Pager summary

```gherkin
Scenario: Pager summary line reflects skip/window/total
  Given the active set contains 3 ratings and the query Top = 50
  When the administrator opens /admin/ratings
  Then the summary line beneath the table reads "Showing 1–3 of 3"
  And it is rendered from L["Admin.Ratings.Summary"] = "Showing {0}–{1} of {2}"
    with {0}=Skip+1, {1}=Skip+Items.Count, {2}=Total
```

### E2E-RAT-005 — Read-only guarantee

```gherkin
Scenario: The page exposes no write controls
  Given the administrator is on /admin/ratings with at least one rating visible
  When the page has finished loading
  Then there is no "Add rating" / "New" / toolbar create button
  And no row exposes Edit, Delete, Deactivate, or Details actions
  And the only interactive surfaces are the language toggle and the nav rail
  And no /account/api/admin/feedback/ratings PUT/POST(create)/DELETE request can be issued from this page
```

### E2E-RAT-006 — Empty state

```gherkin
Scenario: Empty active set renders SimfEmptyState
  Given the database has no active Rating rows
  When the administrator opens /admin/ratings
  Then the page issues POST /account/api/admin/feedback/ratings and gets HTTP 200 with Ratings.Items empty
  And the grid area renders the SimfEmptyState component
  And the empty state title reads "No ratings yet." / "لا توجد تقييمات بعد."
  And the "Average rating" stat card reads "0.0" (AverageStars is 0 when there are no active ratings)
  And the "Total ratings" stat card reads "0"
  And no error SimfAlert appears
```

### E2E-RAT-007 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Ratings.View is denied
  Given a signed-in Control Panel user whose role does NOT include the "Ratings.View" permission
    (the page is gated by @attribute [RequirePermission(PermissionCatalog.Ratings.View)] and the
     CpNavigation item "Module.Ratings" carries RequiredPermission = Ratings.View)
  When they navigate to /admin/ratings
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/feedback/ratings request fires
  And the "Module.Ratings" item is hidden from the nav rail for that user

Scenario: API enforces the same permission at the lower layer
  Given a signed-in approved Visitor (no Administrator role / no Ratings.View)
  When a POST /api/v1/admin/feedback/ratings is sent with their token
  Then the API returns HTTP 403 Forbidden
  And no rating data is returned
```

### E2E-RAT-008 — Inactive ratings excluded

```gherkin
Scenario: Soft-deleted (inactive) ratings never surface and do not skew the headline
  Given attendee A has an active rating of 5 stars
  And attendee B's rating row exists but has IsActive = false (soft-deleted)
  When the administrator opens /admin/ratings
  Then only attendee A's row appears in the table (Active = "✓")
  And the "Total ratings" stat card reads "1" (the inactive row is not counted)
  And the "Average rating" stat card reads "5.0" (computed over the active set only)
  And the service applied .Where(rating => rating.IsActive) so attendee B is excluded entirely
```

### E2E-RAT-009 — Server 500 on list

```gherkin
Scenario: API 500 on the ratings list shows the error banner and load-failed copy
  Given the API is configured to return 500 on /api/v1/admin/feedback/ratings (e.g. DB down)
  When the administrator opens /admin/ratings
  Then the page first shows the loading text "Loading ratings…" / "جارٍ تحميل التقييمات…"
  And then a SimfAlert with Variant="error" renders at the top of the surface
  And it reads the env Error.MessageForCurrentCulture(), or the fallback
    "Could not load ratings. Please try again." / "تعذّر تحميل التقييمات. يرجى المحاولة مرة أخرى."
  And no table and no stat cards render (the page stays on the failed-load state)
```

### E2E-RAT-010 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page, stat cards and table
  Given the administrator is on /admin/ratings in English
  When they click the "العربية" link in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "التقييمات"
  And the stat-card titles read "متوسط التقييم" and "إجمالي التقييمات"
  And the table headers read "النجوم", "التعليق", "مُفعّل", "تاريخ الإرسال"
  And the nav rail and stat-card row mirror (reverse order)
  And the average value still renders with a Latin/invariant decimal point (e.g. "5.0"),
    because it is formatted with CultureInfo.InvariantCulture regardless of UI culture
```

---

## Implementation notes

- **API integration tests at a lower layer.** `tests/SIMF.Api.Tests/FeedbackRatingsTests.cs`
  already covers this surface without a browser:
  - `Admin_list_returns_ratings_with_average` — two visitors rate 2 and 4, the admin
    list returns `RatingCount >= 2`, `AverageStars > 0`, non-empty `Ratings.Items`
    (backs **E2E-RAT-001 / -002**).
  - `Admin_list_is_forbidden_for_non_admin` — a visitor token gets **403** on
    `/api/v1/admin/feedback/ratings` (backs the API half of **E2E-RAT-007**).
  - `Visitor_can_submit_a_rating`, `Rating_twice_upserts_the_single_row_for_the_user`,
    `Out_of_range_stars_is_rejected_with_400`, `Unauthenticated_rate_is_401` — exercise
    the attendee write that seeds the data for the read round-trip (the upsert backs the
    re-submit half of **E2E-RAT-001**).
  When E2E covers these scenarios, keep both layers during the Playwright transition.
- **Backing surfaces grounded in source.** Page:
  `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RatingsList.razor` (route
  `/admin/ratings`, `[RequirePermission(PermissionCatalog.Ratings.View)]`). BFF
  passthrough: `AccountEndpoints.cs` `POST /admin/feedback/ratings` →
  `SimfAdminClient.ListRatingsAsync` → API `POST /api/v1/admin/feedback/ratings`
  (`FeedbackEndpoints.cs` `ListAdminRatingsEndpoint`). Service:
  `RatingService.ListAllAsync` (active-only filter, average guarded to `0` on empty,
  sort by `stars`/`createdat`, optional `Search` LIKE on `Comment`). Contracts:
  `SIMF.Contracts.Feedback.AdminRatingsPage` / `AdminRatingSummary`. Permission:
  `PermissionCatalog.Ratings.View` (`"Ratings.View"`, `AdminOnly` baseline).
- **No write path on this page.** Any future Edit/Deactivate would need its own
  permission code, API action and matching E2E rows per the per-page/per-action rule —
  it is intentionally absent today.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario into a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) + step
  definitions. The Gherkin shape is already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
