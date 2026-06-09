# SIMF — Uniform CRUD Standard Program (D-356)

Last updated: 2026-06-09
Owner directive (2026-06-09): *"Make one big plan, execute in your recommended
order, no more ask, one-time execution. Iterate through ALL pages and implement
Page⇄Popup switching; do Export AND Import on all pages (you may do it as a
dynamic per-CRUD grid data component); then review, verify and run E2E."*

This is the binding execution plan for the program. It is the continuity anchor:
if context is lost, resume from the **Status board** below. It does not override
the global rules (`~/.claude/CLAUDE.md`) or the controlled docs under `docs/`.

---

## 1. Goal — the "uniform CRUD page" standard

Every admin **list** page converges on the same shape:

1. **`SimfDataGrid`** (already mandated) with the full action surface where
   meaningful: Add / Edit / Details / Delete + bulk-delete + Copy/Paste/Duplicate.
2. **`CrudPresentationToggle`** — per-user Page⇄Popup switch (D-353), persisted in
   localStorage via `CpPreferences`.
3. **Two reusable forms per entity**, hosted by **`CrudShell`** (dialog or
   full-page): `{Entity}AddEdit` (`IsEdit`) and `{Entity}ViewDelete` (`IsDelete`).
4. **Excel Export + Import** through one **dynamic/generic grid Excel component**
   (no per-entity bespoke service unless the entity already has one).
5. Per-resource **permissions** (`{Resource}.Export` / `.Import`), **tests**,
   **docs** (PAGE-INDEX + per-page) and an **E2E catalogue** — same changeset
   (DoD D-246).

---

## 2. Generic Excel engine design (the "dynamic per-CRUD grid data component")

Reuses the **existing, security-hardened** ClosedXML path and the already-generic
JS/BFF layer — does **not** invent a new mechanism.

**Confirmed existing facts (grounded 2026-06-09):**
- API export endpoint: `POST /admin/{resource}/export` body `{ Ids, Query }` →
  XLSX bytes + `Content-Disposition`; gated `PolicyFor({Resource}.Export)` +
  `RequireApprovedAccount`; rate-limited `auth`.
- API import endpoint: `POST /admin/{resource}/import` (multipart field `file`)
  → `ApiResult<...ImportResponse>{ Created, Skipped, Errors[] }`; gated
  `{Resource}.Import`; `AllowFileUploads()`; 5 MB cap + ZIP-magic (`50 4B 03 04`).
- ClosedXML services live in `SIMF.Infrastructure/Excel`; export sanitises every
  string cell against formula injection (CWE-1236: leading `= + - @` / TAB / CR
  get an apostrophe), strict sheet name, header check, row cap (`MaxImportRows`).
- CP BFF (`AccountEndpoints.cs`) proxies each `/account/api/admin/{resource}/export`
  + `/import` to the API via the typed `SimfAdminClient`.
- JS (`simf-account.js`) is already generic by URL: `downloadXlsx(url, body)`,
  `uploadFile(url, inputId)`, `triggerFileInput(inputId)`.
- `SimfDataGrid` already exposes `OnExport`, `OnImport`, `OnDeleteSelected`,
  `OnDuplicateOne`, `OnCopy*`, `OnPaste` + their labels.

**New, generic pieces (additive — no schema change, D-110 safe):**
- `SIMF.Application.Excel.IGridExcelExporter` + `IGridExcelImporter` abstractions,
  driven by a **column descriptor** (`GridExcelColumn`: header, value selector for
  export, optional setter/parser for import, required, order). The descriptor list
  is the single source for a resource's export columns AND its import binding.
- `SIMF.Infrastructure.Excel.ClosedXmlGridExcelExporter` / `...Importer` — one
  hardened implementation each (carries the formula sanitisation, sheet name,
  header check, size/zip/row caps from the proven services).
- `AdminGridImportResult { Created, Updated, Skipped, IReadOnlyList<RowError> Errors }`
  contract (`RowError { int Row, string? Key, string Reason }`).
- A generic endpoint helper that maps a resource's export/import in a few lines
  given: the summary fetch, the column descriptors, and an upsert delegate.
- CP: a reusable **`<CrudExcelBar>`**-style integration (hidden `.xlsx` input +
  import-result modal + `OnExport`/`OnImport` handlers) so each page wires Excel
  in ~3 lines pointing at its resource slug.

**Import scope rule (non-arbitrary):** the generic component offers **Import**
only when the resource provides an **upsert binding** (a create/edit path). Pure
read-only / queue pages (no create) get **Export only** — bulk-importing a
moderation verdict, a rating, or a booking-approval from Excel is not a real
operation. Export is offered on **every** page. Each export-only page is listed
with its reason in the Status board.

Entities that already have bespoke, hardened Excel services (Users/Admins,
Visitors, Others, Attendees, Organisations, OperationLog) **keep them** — the
generic engine is for the rest. The CP grid wiring is uniform regardless.

---

## 3. Recommended execution order (waves)

- **Phase 0 — Foundation** (build the generic engine + standard; do it by hand,
  must be exactly right): 0.1 exporter · 0.2 importer · 0.3 endpoint family ·
  0.4 BFF proxy · 0.5 CP grid Excel component · 0.6 permission codes · 0.7 dev
  guide · 0.8 build+tests+commit.
- **Phase 1 — Checkpoint push** of the current branch (fast-forward; the
  concurrent worker's *uncommitted* files stay out).
- **Phase 2 — Retrofit the 17 already-toggled pages**: add Export (+Import where
  upsert exists) + bulk actions. Parallel worktree workflow.
- **Phase 3 — Simple/medium pages** to the full standard: Sponsors, Exhibitors,
  Booths, VenueMap, Invitations, Speakers.
- **Phase 4 — Complex/account pages**: Sessions, Users/Admins, Visitors, Others,
  Attendees, BusinessMeetings, MeetingTables, SessionModerators, QuestionQueue,
  SpeakerPresentations, SessionSummaries, CommentsModeration, Ratings, Bookings,
  Vips, SpeakerMeetingRequests. (Visitor-class already have Export/Import — add
  the toggle + CrudShell forms.)
- **Phase 5 — Docs + E2E** catalogue pass for every changed page.
- **Phase 6 — Final review + verify + E2E + push.**

Each page/wave is **not done** until: build 0/0 · unit+integration tests green ·
docs (PAGE-INDEX + per-page) · E2E catalogue file · live DOM smoke · review +
simplify. Commit per wave; surgical staging only (never `git add -A` — concurrent
worker on the shared tree). All resx edits UTF-8-safe (Edit tool or
`[IO.File]::ReadAllText(...,UTF8)`), keep EN/AR parity, zero mojibake.

---

## 4. Status board (39 admin list pages)

Legend: T=Page⇄Popup toggle · X=Export · I=Import · ✓ done · — todo · n/a not applicable.

| Page (route) | T | X | I | Notes |
|---|---|---|---|---|
| Interests | ✓ | — | — | retrofit X+I (Phase 2) |
| Countries | ✓ | — | — | retrofit X+I |
| Themes | ✓ | — | — | retrofit X+I |
| Halls | ✓ | — | — | retrofit X+I |
| Gates | ✓ | — | — | retrofit X+I |
| SessionCategories | ✓ | — | — | retrofit X+I |
| Roles | ✓ | — | — | retrofit X+I |
| Banners | ✓ | — | — | retrofit X+I |
| ContentBlocks | ✓ | — | — | retrofit X+I (upsert-by-key) |
| MediaPartners | ✓ | — | — | retrofit X+I |
| Archive | ✓ | — | — | retrofit X+I |
| Media | ✓ | — | — | retrofit X (+I metadata only; image is out-of-row) |
| Organisations | ✓ | — | ✓ | has bespoke Import; add X + toggle already done |
| Configuration | ✓ | — | — | retrofit X+I |
| Contacts | ✓ | — | — | retrofit X+I |
| News | ✓ | — | — | retrofit X+I |
| AiPrompts | ✓ | — | — | retrofit X+I |
| Visitors | — | ✓ | ✓ | add toggle + CrudShell forms |
| Users (/admin/admins) | — | ✓ | ✓ | add toggle + CrudShell forms |
| Others | — | ✓ | ✓ | add toggle + CrudShell forms |
| Attendees | — | ✓ | n/a | roster, no create → Export only; add toggle |
| Sessions | — | — | — | full standard |
| Speakers | — | — | — | full standard |
| SessionModerators | — | — | n/a | join/assign → Export only |
| CommentsModeration | — | — | n/a | moderation → Export only |
| Ratings | — | — | n/a | read-only feedback → Export only |
| Bookings | — | — | n/a | approval queue → Export only |
| QuestionQueue | — | — | n/a | moderation queue → Export only |
| SpeakerPresentations | — | — | — | files; X metadata; I if upsert |
| Vips | — | — | n/a | PR list → Export only |
| SessionSummaries | — | — | n/a | edit/publish, no create → Export only |
| SpeakerMeetingRequests | — | — | n/a | queue → Export only |
| Exhibitors | — | — | — | full standard |
| Sponsors | — | — | — | full standard |
| MeetingTables | — | — | n/a | generated → Export only |
| BusinessMeetings | — | — | n/a | scheduled → Export only |
| Booths | — | — | — | full standard |
| VenueMap | — | — | — | full standard (nodes) |
| Invitations | — | — | n/a | PR-managed → Export only |

(The n/a Import calls are provisional engineering reads; the generic engine
simply omits the Import button when a page declares no upsert binding.)

---

## 5. Risks / guardrails
- **Concurrent worker** edits the same working tree (CpNavigation.cs, Strings.resx,
  SimfIcon.razor, CpShellLayout.razor, SimfNavItem.razor, appsettings.json) on a
  sibling branch. Stage only files this program touches; never sweep theirs.
- **D-110 freeze** — everything here is additive (endpoints, permission codes,
  UI). No EF schema/enum changes.
- **resx** — UTF-8-safe edits only; EN/AR parity; zero mojibake (a prior PS
  encoding bug corrupted Arabic — fixed in fdb90fc).
- **Permission HARD RULE** — every new action gated on API + CP; seeder idempotent
  (no migration).
