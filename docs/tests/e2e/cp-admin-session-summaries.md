# E2E test catalogue — Session summaries / محضر desk (`/admin/session-summaries`)

| | |
|--|--|
| **Page** | [`cp/admin-session-summaries.md`](../../pages/cp/admin-session-summaries.md) |
| **Route** | `/admin/session-summaries` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-10 (D-356 Phase 5 — Excel + toggle) |

> **What this page does (grounded in `SessionSummariesList.razor`).** This is the
> Scientific-Committee AI session-summary / محضر desk (P4.1 / D-238, Mockup screen
> 34). It is **not** a CRUD-add page — it lists **every active session** (newest
> session first) with the summary's state, and the Committee acts on each row:
> - **AI draft** (`Generate`) — POSTs `…/{sessionId}/generate`; routes through the
>   central AI seam (shipped provider = the deterministic **Echo** stub) and writes
>   the draft into the **Arabic full-text column only**, leaving English + curated
>   sections for the Committee. Opens the editor pre-filled. Gated by
>   `SessionSummaries.Edit`.
> - **Edit** (only shown when `HasSummary`) — GETs `…/{sessionId}`, opens the editor
>   with 8 bilingual textareas, **Save** PUTs `…/{sessionId}`. Gated by
>   `SessionSummaries.Edit`.
> - **Publish / Unpublish** (only shown when `HasSummary`) — PUTs `…/{sessionId}/publish`
>   or `…/{sessionId}/unpublish`; this is the gate the public app read honours.
>   Gated by `SessionSummaries.Publish`.
>
> **RequiredPermission for the page (`@attribute [RequirePermission]`):**
> `PermissionCatalog.SessionSummaries.View` (`"SessionSummaries.View"`). Nav item
> `Module.SessionSummaries` carries the same `RequiredPermission`.
>
> **Grid affordances (D-256 — `SimfDataGrid`).** The desk renders through
> `SimfDataGrid` over the in-memory rows (one read loads every active session, then
> filter / sort / page run **client-side** in `BuildPage()`). The page size is
> `Top = 20`. Only the **Session** column (`Key="session"`) is `Filterable` and
> `Sortable`; **Status** and **Source** are display-only. The three row actions are
> **quiet icon buttons** in `<RowActions>` (tooltip on hover), not filled text
> buttons: Generate = sparkle icon, Edit = pencil icon (only when `HasSummary`),
> Publish/Unpublish = power icon (only when `HasSummary`). There is **no bulk
> action** — no select-all / multiselect toolbar on this desk.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SUM-001 | Golden round-trip — AI draft → edit + Save → Publish → Unpublish | happy | P0 | _to author_ |
| E2E-SUM-002 | List renders one row per active session with Status + Source labels | happy | P1 | _to author_ |
| E2E-SUM-003 | AI draft (Generate) on a session with no summary → Arabic full-text filled, editor opens | happy | P0 | _to author_ |
| E2E-SUM-004 | Edit existing summary — open editor pre-filled, change a section, Save | happy | P1 | _to author_ |
| E2E-SUM-005 | Publish a draft → Status becomes "Published" | happy | P1 | _to author_ |
| E2E-SUM-006 | Unpublish a published summary → Status returns to "Draft" | happy | P1 | _to author_ |
| E2E-SUM-007 | Editor Cancel discards edits without saving | happy | P2 | _to author_ |
| E2E-SUM-008 | Empty state — no active sessions renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-SUM-009 | Auth gate — signed-in admin lacking `SessionSummaries.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SUM-010 | Action gate — admin with View but not Edit/Publish sees no action buttons | auth | P1 | _to author_ |
| E2E-SUM-011 | Validation — section over its max length → 400 `SESSION_SUMMARY_INVALID` | error | P1 | _to author_ |
| E2E-SUM-012 | Conflict — Publish a session that has no summary → 404 `SESSION_SUMMARY_NOT_FOUND` | error | P1 | _to author_ |
| E2E-SUM-013 | Missing session — generate against a deleted/unknown session → 404 `SESSION_NOT_FOUND` | error | P2 | _to author_ |
| E2E-SUM-014 | Server 500 on list → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-SUM-015 | RTL / Arabic render — page + editor modal mirror | i18n | P1 | _to author_ |
| E2E-SUM-016 | Per-column filter on Session narrows the grid (client-side, Skip→0) | happy | P1 | _to author_ |
| E2E-SUM-017 | Column sort on Session toggles ascending / descending | happy | P2 | _to author_ |
| E2E-SUM-018 | Excel export — toolbar Export downloads an .xlsx of the active-session set (D-356) | happy | P1 | _to author_ |

## Scenarios

### E2E-SUM-001 — Golden round-trip

```gherkin
Feature: Session-summary committee desk round-trip
  As a Scientific-Committee administrator
  I want to AI-draft a session محضر, edit it, publish it, then take it offline
  So that visitors read an accurate, reviewed minute in the app

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator (superadmin@zagali-ict.com) has signed in via /login + /login/totp
  And the administrator holds SessionSummaries.View, .Edit and .Publish (Administrator = "*")
  And at least one active session exists (e.g. "Naval Propulsion Futures", code "S-101")
  And they have landed on /admin/session-summaries

Scenario: AI-draft → edit → publish → unpublish one session summary
  Given the row for "Naval Propulsion Futures" shows Status="No summary" and Source="—"
  And GET /account/api/admin/session-summaries returned 200 with that row

  When the administrator clicks the row's AI-draft (sparkle) action
  Then POST /account/api/admin/session-summaries/{sessionId}/generate returns 200
  And a green toast reads "AI draft generated." / "تم توليد مسودة بالذكاء الاصطناعي."
  And the editor modal opens titled with the session title
  And a blue SimfAlert reads "This draft was generated by AI — review and edit it before publishing."
  And the "Full text (Arabic)" textarea is populated by the Echo provider
  And the English columns + curated sections remain empty
  And the row now shows Status="Draft" and Source="AI-drafted"

  When the administrator fills "Key points (English) — one per line" with "Hybrid drives cut fuel 18%"
  And fills "Recommendations (English)" with "Pilot on two frigates by Q3"
  And clicks "Save"
  Then PUT /account/api/admin/session-summaries/{sessionId} returns 200
  And the modal closes
  And a green toast reads "Summary saved." / "تم حفظ الملخص."

  When the administrator clicks the row's Publish (power) action
  Then PUT /account/api/admin/session-summaries/{sessionId}/publish returns 200
  And a green toast reads "Summary published." / "تم نشر الملخص."
  And the row Status changes to "Published"
  And the power icon now carries the "Unpublish" tooltip instead of "Publish"

  When the administrator clicks the row's Unpublish (power) action
  Then PUT /account/api/admin/session-summaries/{sessionId}/unpublish returns 200
  And a green toast reads "Summary unpublished." / "تم إلغاء نشر الملخص."
  And the row Status returns to "Draft"
  And the power icon carries the "Publish" tooltip again
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-session-summaries-golden-before.png` (row at "No summary")
- Screenshot after each step: `docs/screenshots/cp-admin-session-summaries-golden-{aidraft,editor,saved,published,unpublished}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/session-summaries/...` call returns 200
- Audit rows: `OperationLog` / audit entries `SessionSummary.Generated`,
  `SessionSummary.Saved`, `SessionSummary.Published`, `SessionSummary.Unpublished`
  — each with the actor's id and `sessionId={sessionId}`.

### E2E-SUM-002 — List renders one row per active session

```gherkin
Scenario: The desk lists every active session with its summary state
  Given there are 3 active sessions, one published, one AI-draft, one with no summary
  When the administrator opens /admin/session-summaries
  Then GET /account/api/admin/session-summaries returns 200 with 3 rows
  And the rows are ordered newest session first (by StartUtc descending)
  And the grid shows columns "Session", "Status", "Source", and a row-actions column
  And the published row shows Status="Published"
  And the AI-draft row shows Status="Draft" and Source="AI-drafted"
  And the no-summary row shows Status="No summary" and Source="—"
  And the no-summary row offers only the AI-draft (sparkle) action (no Edit / Publish icon until a summary exists)
```

### E2E-SUM-003 — AI draft (Generate) creates an Arabic draft

```gherkin
Scenario: AI draft on a session with no summary fills Arabic full-text only
  Given the row for session "S-202" shows Status="No summary"
  When the administrator clicks the row's AI-draft (sparkle) action
  Then POST /account/api/admin/session-summaries/{sessionId}/generate returns 200
  And the editor opens with the AI banner SimfAlert visible
  And the "Full text (Arabic)" textarea contains the Echo provider output
  And the "Full text (English)" textarea is empty
  And the curated section fields (Key points / Recommendations / Speakers) are empty
  And the row Source becomes "AI-drafted"
  And a SessionSummary.Generated audit row is written with model + invocation id
```

### E2E-SUM-004 — Edit an existing summary

```gherkin
Scenario: Edit opens the editor pre-filled and Save persists the change
  Given session "S-101" already has a summary (HasSummary = true)
  When the administrator clicks the row's Edit (pencil) action
  Then GET /account/api/admin/session-summaries/{sessionId} returns 200
  And the editor modal opens with all 8 textareas pre-filled from the stored summary
  When they change "Recommendations (English)" to "Adopt the standard fleet-wide"
  And click "Save"
  Then PUT /account/api/admin/session-summaries/{sessionId} returns 200
  And the modal closes
  And a green toast reads "Summary saved." / "تم حفظ الملخص."
  And re-opening Edit shows the changed value persisted
```

### E2E-SUM-005 — Publish a draft

```gherkin
Scenario: Publishing flips Status to Published
  Given session "S-101" has a draft summary (Status="Draft")
  When the administrator clicks the row's Publish (power) action
  Then PUT /account/api/admin/session-summaries/{sessionId}/publish returns 200
  And a green toast reads "Summary published." / "تم نشر الملخص."
  And the row Status reads "Published"
  And the power icon now carries the "Unpublish" tooltip
  And the public read (GET /programme/sessions/{id}/summary) now returns the summary
```

### E2E-SUM-006 — Unpublish a published summary

```gherkin
Scenario: Unpublishing takes a summary offline
  Given session "S-101" has a published summary (Status="Published")
  When the administrator clicks the row's Unpublish (power) action
  Then PUT /account/api/admin/session-summaries/{sessionId}/unpublish returns 200
  And a green toast reads "Summary unpublished." / "تم إلغاء نشر الملخص."
  And the row Status returns to "Draft"
  And the power icon carries the "Publish" tooltip again
  And the public read no longer returns the summary
```

### E2E-SUM-007 — Editor Cancel discards edits

```gherkin
Scenario: Cancel closes the editor without saving
  Given the editor modal is open for session "S-101" with stored content
  When the administrator changes "Key points (English)" to "scratch text"
  And clicks "Cancel"
  Then the modal closes
  And no PUT /account/api/admin/session-summaries/{sessionId} request fires
  And re-opening Edit shows the original "Key points (English)" value (scratch text discarded)
```

### E2E-SUM-008 — Empty state

```gherkin
Scenario: No active sessions renders SimfEmptyState
  Given the database has no active Session rows
  When the administrator opens /admin/session-summaries
  Then GET /account/api/admin/session-summaries returns 200 with an empty list
  And the surface renders the SimfEmptyState component
  And it shows the bilingual title "No sessions to summarise yet." / "لا توجد جلسات لتلخيصها بعد."
  And no table renders
  And no error toast appears
```

### E2E-SUM-009 — Auth gate (page-level)

```gherkin
Scenario: Signed-in admin lacking SessionSummaries.View is denied
  Given a signed-in administrator whose roles do NOT grant SessionSummaries.View
  And whose JWT permission claims do not include "SessionSummaries.View" or "*"
  When they navigate to /admin/session-summaries
  Then the [RequirePermission(PermissionCatalog.SessionSummaries.View)] attribute denies them
  And they land on /not-permitted with HTTP 200
  And no GET /account/api/admin/session-summaries request fires
  And the Module.SessionSummaries nav item is hidden for this user
```

### E2E-SUM-010 — Action gate (button-level)

```gherkin
Scenario: View-only admin sees no Generate / Edit / Publish buttons
  Given a signed-in administrator who holds SessionSummaries.View but NOT .Edit and NOT .Publish
  When they open /admin/session-summaries
  Then the page loads and the rows render (View is satisfied)
  And the <AuthorizedAction Permission="SessionSummaries.Edit"> block hides the AI-draft (sparkle) and Edit (pencil) icons
  And the <AuthorizedAction Permission="SessionSummaries.Publish"> block hides the Publish / Unpublish (power) icon
  And the row-actions column shows no icons for this user
  And even if forged, POST /generate and PUT /publish return 403 at the API (policy SessionSummaries.Edit/.Publish)
```

### E2E-SUM-011 — Validation: section over max length

```gherkin
Scenario: A section longer than its limit is rejected with a bilingual error
  Given the editor is open for session "S-101"
  When the administrator pastes 8,001 characters into "Full text (English)" (limit 8000)
  And clicks "Save"
  Then PUT /account/api/admin/session-summaries/{sessionId} returns HTTP 400
  And ApiResult.Error.Code = "SESSION_SUMMARY_INVALID"
  And the editor stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture():
    "The full text must be 8000 characters or fewer." / "يجب ألا يتجاوز هذا الحقل 8000 حرفاً."
  # Note: the SimfTextarea MaxLength props (4000 / 1000 / 8000) cap input client-side;
  # this scenario exercises the server guard with a forged / pasted over-length body.
```

### E2E-SUM-012 — Conflict: Publish without a summary

```gherkin
Scenario: Publishing a session that has no summary yet returns 404
  Given session "S-303" has Status="No summary" (HasSummary = false)
  # The UI hides Publish until HasSummary, so this drives the API directly (forged call)
  When PUT /account/api/admin/session-summaries/{sessionId}/publish is issued
  Then the API returns HTTP 404
  And ApiResult.Error.Code = "SESSION_SUMMARY_NOT_FOUND"
  And the bilingual message reads
    "No summary exists for this session yet." / "لا يوجد ملخّص لهذه الجلسة بعد."
  And no audit SessionSummary.Published row is written
```

### E2E-SUM-013 — Missing / deleted session

```gherkin
Scenario: Generating against an unknown or soft-deleted session returns 404
  Given a sessionId that does not exist or is soft-deleted (IsActive = false)
  When POST /account/api/admin/session-summaries/{sessionId}/generate is issued
  Then the API returns HTTP 404
  And ApiResult.Error.Code = "SESSION_NOT_FOUND"
  And the bilingual message reads
    "The session was not found." / "لم يتم العثور على الجلسة."
```

### E2E-SUM-014 — Server 500 on list

```gherkin
Scenario: API 500 on list shows the fallback bilingual toast
  Given the API is configured to return 500 on GET /admin/session-summaries (e.g. DB down)
  When the administrator opens /admin/session-summaries
  Then the page shows the loading text "Loading summaries…" / "جارٍ تحميل الملخصات…"
  And then a red SimfAlert error appears reading
    "Could not load the summaries." / "تعذّر تحميل الملخصات."
  And no table rows render
```

### E2E-SUM-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the editor modal
  Given the administrator is on /admin/session-summaries in English
  When they switch the language to العربية in the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "ملخصات الجلسات (المحاضر)"
  And the column headers read "الجلسة", "الحالة", "المصدر"
  And the Status labels render as "لا يوجد ملخص" / "مسودة" / "منشور"
  And the Source labels render as "مُولّد بالذكاء الاصطناعي" / "مُدخل يدويًا"
  And the row-action icon tooltips read "توليد بالذكاء الاصطناعي", "تعديل", "نشر", "إلغاء النشر"

  When the administrator opens the editor (Edit on a row with a summary)
  Then the modal renders RTL
  And the AI banner (if present) reads "تم توليد هذه المسودة بالذكاء الاصطناعي — راجعها وعدّلها قبل النشر."
  And the field labels are Arabic ("أبرز النقاط (العربية) — نقطة في كل سطر", "التوصيات (العربية)", …)
  And the footer buttons read "حفظ" and "إلغاء" in reverse order
```

### E2E-SUM-016 — Per-column filter on Session narrows the grid

```gherkin
Scenario: Typing into the Session column filter narrows the grid client-side
  Given at least 3 active sessions are listed, including "Naval Propulsion Futures"
  And GET /account/api/admin/session-summaries returned 200 with every row loaded once
  When the administrator types "Naval" into the per-column filter for "Session"
  Then the grid query carries GridQuery.Filters["session"]="Naval"
  And Skip resets to 0 (the grid returns to the first page)
  And only rows whose title contains "Naval" (case-insensitive) remain visible
  And no new GET /account/api/admin/session-summaries request fires
  # The desk loads every session in one read; BuildPage() filters in memory,
  # so the per-column filter is purely client-side. Only the Session column is
  # Filterable — Status and Source have no filter input.
  When the administrator clears the "Session" filter
  Then GridQuery.Filters["session"] is empty
  And all active-session rows are visible again
```

### E2E-SUM-017 — Column sort on Session toggles ascending / descending

```gherkin
Scenario: Sorting the Session column toggles ascending then descending
  Given the grid lists every active session (default order newest-session-first)
  When the administrator clicks the "Session" column sort
  Then the grid query carries Sort="session" with SortDescending=false
  And the rows reorder by SessionTitle ascending (A→Z), in memory
  When the administrator clicks the "Session" column sort again
  Then SortDescending=true
  And the rows reorder by SessionTitle descending (Z→A)
  # Only the Session column is Sortable; Status and Source are display-only.
  And no new GET /account/api/admin/session-summaries request fires
```

### E2E-SUM-018 — Excel export (D-356)

```gherkin
Scenario: Export the active-session set to an XLSX workbook
  Given the administrator is on /admin/session-summaries with at least two active sessions
  And the administrator holds SessionSummaries.Export (Administrator = "*")
  And the grid toolbar shows the "Export" action
  When they click the toolbar "Export" action
  Then simfAccount.downloadXlsx POSTs to /account/api/admin/session-summaries/export
  And the request body is an AdminGridExportRequest with an empty Ids list and the current Query
  # The desk has no select-all / multiselect toolbar, so the selected list is always
  # empty and Query is sent — the export always covers the whole active-session set.
  And the endpoint runs under policy "SessionSummaries.Export" + RequireApprovedAccount (and the "auth" rate limiter)
  And the response is an attachment named simf-session-summaries-{yyyyMMddHHmmss}.xlsx
  And the workbook's "SessionSummaries" sheet has the header row
    SessionCode | SessionTitle | SessionTitleArabic | SessionStartUtc | Status | Source | PublishedAt | UpdatedAt
  And a no-summary row exports Status="None" and Source="—"
  And an AI-drafted draft row exports Status="Draft" and Source="AI"
  And a published, hand-written row exports Status="Published" and Source="Manual"
  And the whole-grid export is capped at 5000 rows (MaxExportRows)
  # Export only — this desk drafts / edits / publishes summaries through its own
  # bespoke endpoints, so there is NO import path (the grid wires OnExport but not
  # OnImport, and the API exposes /export with no /import).
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-session-summaries-export.png` (toolbar Export + saved file)
- Network: a single POST `/account/api/admin/session-summaries/export` returns 200 with `Content-Disposition: attachment; filename="simf-session-summaries-…xlsx"`
- Console errors: 0 expected
- Workbook check: the "SessionSummaries" sheet header row matches the eight columns above; one data row per active session

---

## Implementation notes

- **API integration tests** at
  [`tests/SIMF.Api.Tests/SessionSummaryCommitteeTests.cs`](../../../tests/SIMF.Api.Tests/SessionSummaryCommitteeTests.cs)
  (`SessionSummaryCommitteeTests : IClassFixture<SimfApiFactory>`) cover the same
  surface at a lower layer (no browser): list/get/generate/save/publish/unpublish,
  the permission policies, and the 404 / 400 error paths. When an E2E scenario
  here is automated, keep both during the transition.
- **AI seam is deterministic in tests.** Generate routes through `IAiService` with
  the seeded `session-summary` prompt; the shipped provider is the **Echo** stub,
  so the Arabic full-text draft is reproducible. A real provider plugs in by
  editing the prompt's provider in the CP — no code change — so the Generate
  scenarios assert *behaviour* (Arabic column filled, English preserved, model
  stamped, audit row) rather than exact draft text.
- **No CRUD-add here.** Unlike `/admin/interests`, this desk has no "Add" button
  and no per-row Deactivate — it is session-driven. Rows appear/disappear with the
  active Session set; the summary is a 1:1 child created lazily by AI draft or Save.
- **Permission gates (HARD RULE).** Page: `[RequirePermission(SessionSummaries.View)]`.
  API: list/get policy `SessionSummaries.View`, generate/save `SessionSummaries.Edit`,
  publish/unpublish `SessionSummaries.Publish` — all on
  `RequireApprovedAccount`. Generate/save/publish/unpublish also carry the `"auth"`
  rate-limiter. Action buttons are wrapped in `<AuthorizedAction>` for Edit / Publish.
- **Manual smoke as canonical run today.** Until Playwright is adopted, the
  canonical run is a Chrome DevTools MCP session signed in per the Auth setup,
  walking each scenario and capturing screenshots into
  `docs/screenshots/cp-admin-session-summaries-{scenario}.png`. The Gherkin shape
  is already runner-agnostic for the later Playwright port under
  `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-06-10 by Claude (D-356 Phase 5 — Excel + toggle; added E2E-SUM-018 Excel export, export-only). Earlier: 2026-06-03 (E2E catalogue rebuild) (D-256/D-257 grid affordances reconciled).
