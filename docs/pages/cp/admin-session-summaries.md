# Session summaries (محضر desk) — `/admin/session-summaries`

| | |
|--|--|
| **Route** | `/admin/session-summaries` |
| **Audience** | Administrator / Scientific Committee |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.SessionSummaries.View)]` (page). API: list/read on `SessionSummaries.View`, generate/save on `SessionSummaries.Edit`, publish/unpublish on `SessionSummaries.Publish`, export on `SessionSummaries.Export` — all on top of `RequireApprovedAccount`. Generate/save/publish/unpublish + export also carry the `"auth"` rate limiter. |
| **Pattern** | P4.1 / D-238 (Completion Programme §6.4.1, Mockup screen 34). **Not** a CRUD-add page — a session-driven editorial desk: one row per active session, summary created lazily by AI draft or Save. |
| **Status** | ✅ Real (D-238); Excel export added D-356 |
| **Backend endpoints** | BFF `/account/api/admin/session-summaries/*` → API: `GET /admin/session-summaries` (list), `GET /admin/session-summaries/{sessionId}` (detail), `POST /admin/session-summaries/{sessionId}/generate` (AI draft), `PUT /admin/session-summaries/{sessionId}` (save), `PUT /admin/session-summaries/{sessionId}/publish`, `PUT /admin/session-summaries/{sessionId}/unpublish`, `POST /admin/session-summaries/export` (D-356, export only) |
| **Source** | [`SessionSummariesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionSummariesList.razor), [`SessionSummaryEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionSummaryEndpoints.cs), [`SessionSummariesExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SessionSummariesExcelEndpoints.cs), [`AdminSessionSummaryService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminSessionSummaryService.cs), [`SessionSummary.cs`](../../../src/Backend/SIMF.Domain/Programme/SessionSummary.cs), [`SessionSummaries.cs`](../../../src/Shared/SIMF.Contracts/Admin/SessionSummaries.cs) |
| **Backed by** | `dbo.SessionSummaries` (1:1 with `Session`, migration `D237_AddSessionSummary`, 2026-06-02; Slice-D read-only columns `AiDraftFullTextArabic` + `AiDraftGeneratedAt` added by migration `AddSessionSummaryAiDraftSnapshot`, 2026-07-19, both additive/nullable). |
| **Tests** | [`docs/tests/e2e/cp-admin-session-summaries.md`](../../tests/e2e/cp-admin-session-summaries.md) |
| **Last reviewed** | 2026-07-19 |

## 1. Purpose

The Scientific-Committee AI session-summary / محضر desk per the Completion
Programme §6.4.1 (Mockup screen 34, "ملخص الجلسة بالذكاء الاصطناعي"). The
محضر is the Committee's own editorial minute for a session; the app reads it on
screen 34 **only once it is published**.

The desk lists **every active session** (newest session first, by `Start`
descending) with the state of its summary, and the Committee acts per row:

- **AI draft** (Generate) — routes through the central `IAiService` seam using
  the seeded `session-summary` prompt. The shipped provider is the
  deterministic **Echo** stub; a real provider plugs in by editing the prompt's
  provider in the CP (no code change). **A18 (2026-07-26) — a stub draft cannot
  be shipped.** The stub only echoes the prompt back, so its output now opens
  with the sentinel `[AI-STUB-DO-NOT-PUBLISH]` and a bilingual "not real AI
  output" banner, and **Approve and Publish both refuse** any summary whose text
  still carries that sentinel in any field (400 `SESSION_SUMMARY_INVALID`,
  bilingual). The Committee must replace the placeholder with the real minutes
  first. Configuring a real provider + key stays an owner/procurement decision. The draft is written into the **Arabic
  full-text column only**, leaving the English column + curated sections for the
  Committee. Re-generating replaces the Arabic AI draft but preserves the
  Committee's English text and curated sections; because the content changed it
  returns the summary to **Draft** and takes it **offline** — any prior approval and
  publish stamp are cleared (invariant `PublishedAt ⇒ ApprovedAt`), so it must be
  re-approved and re-published (owner 2026-07-19).
- **Edit / Save** — eight bilingual sections (key points, recommendations,
  speakers, full text — each EN + AR). Saving a session that has no summary yet
  creates a hand-written draft (`AiModel` stays null). **A19 (2026-07-26) — a
  save that changes nothing resets nothing.** `SaveAsync` compares the incoming
  values against the stored ones and only calls `ResetReviewState` when a
  persisted field actually differs (same for a re-generate that produces
  byte-identical text). Re-opening the editor and pressing Save used to clear
  the review + publish stamps and pull a live محضر out of the app with no
  warning. A **real** edit still resets (the approval was of the old text) — and
  the CP now opens a `SimfConfirm` first that names the consequence
  (`Admin.SessionSummaries.Confirm.UnpublishOnSave` for a published summary,
  `…Confirm.UnapproveOnSave` for an approved / in-review one).
- **Publish / Unpublish** — stamps / clears `PublishedAt`; this is the gate the
  public app read honours. **Owner 2026-07-19 — Publish is hard-gated on `ApprovedAt`:**
  a Draft / In-review summary cannot be published (API 400 `SESSION_SUMMARY_INVALID`);
  the Publish button is disabled until the team approves it. So the app can never show
  an unreviewed summary. Unpublish is always allowed. **Editing a published summary**
  (Save / re-Generate / Return-to-draft) invalidates its approval and therefore
  **unpublishes it** (clears `PublishedAt`) — the invariant is `PublishedAt ⇒ ApprovedAt`,
  so edited-but-unreviewed text can never stay live; it must be re-approved + re-published.

This is a session-driven desk: rows appear / disappear with the active `Session`
set; the summary is a 1:1 child created lazily. There is no "Add" button and no
per-row Deactivate.

## 4. UI

- `SimfBanner` titled `Admin.SessionSummaries.Title`, inside `simf-page-wide`
  → `simf-surface`. A `SimfAlert` (variant from the toast) shows transient
  success / error feedback above the grid.
- Grid via `SimfDataGrid` over the in-memory rows. One read loads every active
  session, then **filter / sort / page run client-side** in `BuildPage()`; page
  size `Top = 20`, rows keyed by `SessionId`.
- Columns:
  - **Session** (`Key="session"`) — `Sortable` + `Filterable`; renders
    `SessionTitle`.
  - **Status** (`Key="status"`) — display-only (no filter / sort). A `SimfPill`:
    `off` → "No summary" (`!HasSummary`), `on` → "Published" (`IsPublished`),
    `warn` → "Draft" otherwise.
  - **Source** (`Key="source"`) — display-only. `—` when no summary, else
    "AI-drafted" (`GeneratedByAi`) or "Manual".
- Row actions are **quiet icon buttons** (`SimfToolbarButton`, tooltip on
  hover), wrapped in `<AuthorizedAction>`:
  - **Generate** (sparkle, `SessionSummaries.Edit`) — always shown.
  - **Edit** (pencil, `SessionSummaries.Edit`) — only when `HasSummary`.
  - **Publish / Unpublish** (power, `SessionSummaries.Publish`) — only when
    `HasSummary`; the icon's tooltip toggles between Publish / Unpublish on
    `IsPublished`. When not yet published, the Publish button is **disabled until
    `IsApproved`** and carries the "Approve the summary before it can be published"
    tooltip (owner 2026-07-19 hard gate).
  - There is **no bulk action** — no select-all / multiselect toolbar on this
    desk.
- **Empty state** — `SimfEmptyState` titled `Admin.SessionSummaries.None` when
  no active sessions are listed.
- **Editor modal** (`SimfModal`, opened by Generate or Edit) titled with the
  session title. When the loaded summary carries an `AiModel`, a `SimfAlert`
  variant `info` shows the AI banner (`Admin.SessionSummaries.AiBanner`).
  **AI-transparency read-only sources (Slice D, 2026-07-19)** then render above the
  editable fields, each only when it carries content: the raw **subtitle** the AI
  drafted from (`Subtitle` / `SubtitleArabic`, from `Session.LiveCaptions*`) and the
  **original AI draft** (`AiDraftFullTextArabic`, the pristine output captured at
  generation, its label carrying the UTC capture time). All three are `Disabled`
  `SimfTextarea`s: read-only, never sent back on Save. Then eight editable
  `SimfTextarea` fields (see §4.5). Footer: **Save** (`SaveAsync`) + **Cancel**
  (`CloseEditor`, discards without a PUT).
- **Excel export (D-356):** the grid toolbar carries an **Export** action
  (`OnExport` wired, `ExportLabel="@L["Grid.Export"]"`). It posts an
  `AdminGridExportRequest { Ids, Query }` to
  `/account/api/admin/session-summaries/export` via
  `simfAccount.downloadXlsx`. Because the desk has no select-all, `Ids` is
  always empty and `Query` is sent, so the export always covers the whole
  active-session set. The file is `simf-session-summaries-{yyyyMMddHHmmss}.xlsx`,
  sheet "SessionSummaries". **Export only** — the grid does **not** wire
  `OnImport` and the API exposes no `/import` route; summaries are drafted /
  edited / published through this desk's own bespoke endpoints.

## 4.5 Form fields

The editor's eight sections bind to `SaveSessionSummaryRequest`; the client
`MaxLength` props mirror the server limits (`Clean(...)` in
`AdminSessionSummaryService`).

| Field (resx) | Rows | MaxLength | Server limit |
|--------------|------|-----------|--------------|
| Key points (English) | 3 | 4000 | `SectionMax = 4000` |
| Key points (Arabic) | 3 | 4000 | `SectionMax = 4000` |
| Recommendations (English) | 3 | 4000 | `SectionMax = 4000` |
| Recommendations (Arabic) | 3 | 4000 | `SectionMax = 4000` |
| Speakers (English) | 2 | 1000 | `SpeakersMax = 1000` |
| Speakers (Arabic) | 2 | 1000 | `SpeakersMax = 1000` |
| Full text (English) | 5 | 8000 | `FullTextMax = 8000` |
| Full text (Arabic) | 5 | 8000 | `FullTextMax = 8000` |

Each value is trimmed server-side; a section over its limit is rejected (see §6).
Key points are newline-delimited bullet lists (one bullet per non-empty line on
screen 34).

**Read-only source panels (Slice D)** are *not* form fields: they are display-only
`Disabled` textareas shown above the editable eight when present. `Subtitle` /
`SubtitleArabic` come from the session's `LiveCaptions*` (≤ 2048 chars each);
`AiDraftFullTextArabic` is the pristine AI draft (≤ 8000). They carry no input
`MaxLength` cap because they are never edited, and they are absent from
`SaveSessionSummaryRequest`.

## 5. Data flow + endpoints

- **List** — page `OnInitializedAsync` → `LoadAsync()` → `simfAccount.getJson`
  `/account/api/admin/session-summaries` → BFF passthrough
  `api.ListSessionSummariesAsync` → API `GET /admin/session-summaries`
  (`ListSessionSummariesEndpoint`) → `service.ListAsync`. The service projects one
  `AdminSessionSummaryRow` per active session with a correlated sub-select on the
  1:1 summary (`HasSummary` / `GeneratedByAi` / `IsPublished` derived from
  `AiModel` / `PublishedAt`). Returns the whole set in one read; the grid pages
  it client-side.
- **Detail** — Edit → `getJson` `/{sessionId}` → `GetSessionSummaryAdminEndpoint`
  → `service.GetAsync`. Returns `AdminSessionSummaryDetail` (all eight sections +
  `AiModel` + publish state + timestamps + the Slice-D read-only sources
  `Subtitle` / `SubtitleArabic` from the session's `LiveCaptions*` and
  `AiDraftFullTextArabic` / `AiDraftGeneratedAt`), or 404
  `SESSION_SUMMARY_NOT_FOUND`. `GetAsync` widens its session projection to carry the
  captions; these fields appear **only** on this admin detail, never on any
  public/app contract.
- **Generate** — sparkle → `postJson` `/{sessionId}/generate` →
  `GenerateSessionSummaryEndpoint` (actor `sub` from JWT) → `service.GenerateAsync`.
  Builds prompt inputs from the session title + active speakers + abstract +
  subtitle, invokes `IAiService`, writes the truncated output into `FullTextArabic`,
  stamps `AiModel`, and (Slice D) captures the same output into the pristine
  `AiDraftFullTextArabic` + stamps `AiDraftGeneratedAt`. A re-generate refreshes both
  to the latest output. Audits `SessionSummary.Generated` (with model + invocation
  id). Returns the detail and opens the editor pre-filled.
- **Save** — `putJson` `/{sessionId}` with `SaveSessionSummaryRequest` →
  `SaveSessionSummaryEndpoint` → `service.SaveAsync` (creates the row if absent;
  `AiModel` stays null for a hand-written draft). **Save never touches the pristine
  `AiDraftFullTextArabic` / `AiDraftGeneratedAt`** (Slice D), so the original AI draft
  survives every Committee edit — that divergence is exactly what the read-only panel
  surfaces. Audits `SessionSummary.Saved`.
- **Publish / Unpublish** — `putJson` `/{sessionId}/publish` or `/unpublish` →
  `Publish`/`UnpublishSessionSummaryEndpoint` → `SetPublishedAsync` (stamps /
  clears `PublishedAt` + `PublishedByUserId`). Audits `SessionSummary.Published`
  / `SessionSummary.Unpublished`.
- **Export (D-356)** — toolbar Export → `simfAccount.downloadXlsx`
  `/account/api/admin/session-summaries/export` → BFF `MapGridExport(group,
  "session-summaries")` → API `ExportSessionSummariesEndpoint` (extends
  `AdminGridExportEndpoint<AdminSessionSummaryRow>`) → `service.ListAsync`. The
  base resets `Skip = 0`, caps `Top = MaxExportRows (5000)`, applies the
  selected-id filter (none here), and streams the workbook. `IdOf` = `SessionId`.

The BFF `AccountEndpoints` forwards each route with the bearer token via
`SimfAdminClient`; all responses use the `ApiResult<T>` envelope.

## 6. Validation + error handling

- **Section length** — `Clean(value, max, field)` trims then rejects an
  over-length section with HTTP 400, `ApiResult.Error.Code =
  "SESSION_SUMMARY_INVALID"` (`ErrorCodes.SessionSummaryInvalid`), bilingual
  message ("The {field} must be {max} characters or fewer." / Arabic). The
  client `MaxLength` props cap normal input, so this guard fires on a forged /
  pasted over-length body.
- **Summary not found** — reading, publishing, or unpublishing a session with no
  summary returns HTTP 404, `Code = "SESSION_SUMMARY_NOT_FOUND"`
  (`ErrorCodes.SessionSummaryNotFound`), "No summary exists for this session
  yet." / "لا يوجد ملخّص لهذه الجلسة بعد."
- **Publish without approval** (owner 2026-07-19) — publishing a Draft / In-review
  (`ApprovedAt == null`) summary returns HTTP 400, `Code =
  "SESSION_SUMMARY_INVALID"`, "This summary must be reviewed and approved by the
  scientific team before it can be published." / "يجب أن يراجع الفريق العلمي هذا
  الملخّص ويوافق عليه قبل نشره." The CP Publish button is disabled until `IsApproved`,
  so this guard fires on a forged call. (The S-6 clock gate — publish before the
  session starts → 400 — still applies as well.)
- **Session not found** — generate/save/publish against an unknown or
  soft-deleted (`IsActive = false`) session returns HTTP 404, `Code =
  "SESSION_NOT_FOUND"` (`ErrorCodes.SessionNotFound`), "The session was not
  found." / "لم يتم العثور على الجلسة." (`LoadSessionForDraftAsync`).
- **Stub placeholder text** (A18, 2026-07-26) — approving or publishing a summary
  whose text still contains `[AI-STUB-DO-NOT-PUBLISH]` (any field, either
  language) returns HTTP 400, `Code = "SESSION_SUMMARY_INVALID"`, "This summary
  still contains placeholder text from the offline AI stub provider. Replace it
  with the real minutes before approving or publishing it." / "لا يزال هذا
  الملخّص يحتوي على نص مؤقّت من مزوّد الذكاء الاصطناعي التجريبي. استبدله بالمحضر
  الحقيقي قبل الموافقة عليه أو نشره."
- **Generate AI draft** is truncated to `FullTextMax (8000)` rather than rejected
  (`Truncate`), since the provider output is not user input.
- **Client feedback** — the page surfaces a transient toast (`success` / `error`
  `SimfAlert`); errors prefer `envelope.Error.MessageForCurrentCulture()`, falling
  back to a bilingual resx string (`Fallback` / `LoadFailed`).

## 7. Edge cases + known limitations

- **AI draft is Arabic-only.** The seeded `session-summary` prompt produces
  Arabic minutes, so the draft lands in `FullTextArabic`; the English column +
  curated sections stay empty for the Committee. Writing one language into both
  columns would surface the wrong language once a real Arabic provider replaces
  Echo.
- **Provenance, not state.** `AiModel` records the draft origin; editing never
  clears it. Re-generate preserves the Committee's English text and curated
  sections, but (like any content edit) returns the summary to Draft and takes it
  offline — `ResetReviewState` clears `PublishedAt`, keeping the invariant
  `PublishedAt ⇒ ApprovedAt` (owner 2026-07-19).
- **Pristine AI draft (Slice D).** `AiDraftFullTextArabic` stores the untouched AI
  output captured at generation; `SaveAsync` never overwrites it, so the editor can
  always show the original beside the (possibly edited) working copy. A re-generate
  refreshes it to the newest output. Rows created **before** this column (and any
  hand-written summary) have no snapshot, so the panel simply does not render.
- **Raw subtitle panel (Slice D).** The subtitle shown is the session's
  `LiveCaptions` / `LiveCaptionsArabic` (each ≤ 2048 chars) — the exact text the AI
  drafts from, authored on the Sessions editor. Read-only here and capped at 2048 by
  that column; a fuller transcript store is out of scope.
- **Publish is orthogonal to the session's broadcast `Status`** — the محضر is the
  Committee's own editorial document, published by its own action.
- **Export only.** No import path exists on this desk (export endpoint with no
  `/import`; grid wires `OnExport` only). The whole-grid export is capped at 5000
  rows (`MaxExportRows`); a non-`.xlsx` concern does not arise as there is no
  upload.
- **Soft delete** — `SessionSummary.IsActive`; all reads filter `IsActive`. The
  summary is cascade-deleted with its session (it is meaningless without it).

## 8. i18n + RTL

`Admin.SessionSummaries.*` keys in `Strings.resx` / `Strings.ar.resx` (EN ↔ AR
parity): `Title`, `Loading`, `None`, `LoadFailed`, `Fallback`, `Generated`,
`Saved`, `Published`, `Unpublished`, `AiBanner`, `Col.Session` / `Col.Status` /
`Col.Source`, `Status.None` / `Status.Draft` / `Status.Published`, `Source.Ai` /
`Source.Manual`, `Action.Generate` / `Action.Edit` / `Action.Publish` /
`Action.Unpublish`, `Editor.Save` / `Editor.Cancel`, the eight editable `Field.*`
labels, and the Slice-D read-only source labels `Field.Subtitle` /
`Field.SubtitleArabic` / `Field.AiDraft`. Grid chrome reuses the shared `Grid.*` keys (`Export`, `FilterColumn`,
`FilterPlaceholder`, `Actions`, paging). Under the Arabic toggle the page and the
editor modal mirror RTL; the Arabic strings describe the desk as
"ملخصات الجلسات (المحاضر)" with the AI-draft banner phrased as a review-before-
publish notice.

## 10. Use cases

- AI-draft a محضر, review/edit the bilingual sections, publish it, and take it
  offline again — the editorial round-trip the app's screen-34 read depends on.
- Hand-write a محضر from scratch for a session that was not AI-drafted.
- Export the active-session summary set to Excel for offline committee review
  (D-356).

## 11. E2E

See [`docs/tests/e2e/cp-admin-session-summaries.md`](../../tests/e2e/cp-admin-session-summaries.md):
E2E-SUM-001 golden round-trip (AI draft → edit → Submit for review → Approve →
publish → unpublish), 002 list renders one row per active session, 003 AI draft
fills Arabic full-text, 004 edit existing, 005 publish an approved summary, 006
unpublish, 007 editor cancel discards, 008 empty state, 009 page auth gate, 010
action gate, 011 section over max length (400), 012 publish without a summary
(404), 013 missing / deleted session (404), 014 server 500 on list, 015 RTL
render, 016 per-column filter (client-side), 017 column sort, 018 Excel export
(export only), 019-022 team review/approval workflow + المحاور read, **023 publish
requires approval (400)**, **024 edit-after-publish takes it offline (owner
2026-07-19)**, 025 publish clock-gate (S-6), **026 raw subtitle visible read-only in
the editor**, **027 pristine AI draft survives an edit (Slice D)**.

## 12. Related docs

- Authority spec: SIMF Completion Programme §6.4.1; Mockup screen 34.
- Permission catalogue: `docs/SIMF-Permission-Catalogue.md` —
  `SessionSummaries.View` / `.Edit` / `.Publish` / `.Export`.
- Decisions: D-237 (entity + migration), D-238 (committee desk + permissions),
  D-356 (Excel export-only).
- Sibling Programme modules: Sessions, Speakers, Themes (`admin-themes.md`).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-02 | D-237 / D-238 | Original — `SessionSummary` entity + migration `D237_AddSessionSummary` + the Scientific-Committee محضر desk (list / generate / edit / publish / unpublish) through the central AI seam (Echo provider). Gated by `SessionSummaries.View` / `.Edit` / `.Publish`. |
| 2026-06-11 | D-356 | Excel **export added** (toolbar Export → `/account/api/admin/session-summaries/export`, sheet "SessionSummaries", columns `SessionCode \| SessionTitle \| SessionTitleArabic \| SessionStart \| Status \| Source \| PublishedAt \| UpdatedAt`, capped at 5000 rows). New permission `SessionSummaries.Export`. **Export only** — no import path (source wires `OnExport`, not `OnImport`). E2E catalogue extended with E2E-SUM-018. |
| 2026-07-19 | owner (Q&A/summary/rating batch) | **Approval hard-gate before publish.** `SetPublishedAsync` now requires `ApprovedAt` (Draft/In-review → 400 `SESSION_SUMMARY_INVALID`); editing / re-generating / returning a **published** summary clears `PublishedAt` (invariant `PublishedAt ⇒ ApprovedAt`); the public app read **and** `HasPublishedSummary` now also require `ApprovedAt` (hides legacy published-but-unapproved rows). New resx `Admin.SessionSummaries.Action.PublishNeedsApproval` (en+ar); CP Publish button disabled until approved. E2E-SUM-023/024 added; the S-6 clock-gate case renumbered to E2E-SUM-025. |
| 2026-07-19 | Slice D — AI transparency | **Pristine AI-draft snapshot + raw subtitle in the editor.** Additive nullable columns `AiDraftFullTextArabic` + `AiDraftGeneratedAt` on `SessionSummaries` (migration `AddSessionSummaryAiDraftSnapshot`); `GenerateAsync` captures the untouched AI output into the snapshot (a re-generate refreshes it) and `SaveAsync` never overwrites it; `GetAsync`/`ToDetail` also surface the session's `LiveCaptions*` as `Subtitle`/`SubtitleArabic`. `AdminSessionSummaryDetail` gains the four read-only fields (append-only; **never** on `PublicSessionSummary`/`PublicSessionDetail`). CP editor renders three read-only `Disabled` `SimfTextarea` panels above the editable fields; new resx `Field.Subtitle` / `Field.SubtitleArabic` / `Field.AiDraft` (en+ar). E2E-SUM-026/027 added. |
| 2026-07-26 | A18 / A19 (QA round) | **The echo stub can no longer be published, and a typo fix no longer silently unpublishes.** `EchoAiProvider` prefixes every answer with `[AI-STUB-DO-NOT-PUBLISH]` + a bilingual "not real AI output" banner (`EchoAiProvider.StubMarker`); `ApproveAsync` and `SetPublishedAsync(publish: true)` call `EnsureNotStubContent`, rejecting 400 `SESSION_SUMMARY_INVALID` when any text field still carries it. `SaveAsync` / `GenerateAsync` only call `ResetReviewState` when the persisted content actually changed. New resx `Admin.SessionSummaries.Confirm.Title` / `.UnpublishOnSave` / `.UnapproveOnSave` / `.Save` (en+ar) behind a `SimfConfirm` in the editor footer. Configured provider, keys and appsettings untouched — a real provider is an owner/procurement decision. E2E-SUM-029..031. |

_Last reviewed:_ 2026-07-26 by Claude (A18 — the shipped Echo stub marks its output and Approve/Publish refuse marked text; A19 — a no-op save/regenerate no longer clears the review + publish stamps, and the CP warns before an unpublishing save). Earlier: 2026-07-19 by Claude (Slice D — pristine AI-draft snapshot + raw subtitle surfaced read-only in the CP editor; CP-internal only, no public-contract change). Earlier the same day: owner approval hard-gate (Publish requires ApprovedAt; edit-after-publish unpublishes; the public read + HasPublishedSummary require ApprovedAt). Earlier: 2026-06-11 by Claude (D-356 — reference doc authored, grounded in live source; Excel export-only).
