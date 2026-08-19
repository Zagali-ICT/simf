# Session Q&A moderation desk - `/sessions/{id}/moderate`

| | |
|--|--|
| **Route** | `/sessions/{SessionId:guid}/moderate` (`@page` directive, `SessionModerationDesk.razor` line 4) |
| **Layout** | `CpShellLayout` (`@layout CpShellLayout`) |
| **Surface** | Control Panel |
| **Audience** | A signed-in admin holding `Questions.Moderate`, who is additionally either an Administrator or a per-session moderator of this session |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.Questions.Moderate)]`. API: `Policies(nameof(AuthorizationPolicies.RequireApprovedAccount))` plus the inline per-session check in `SessionModeratorAuth.ResolveAuthorizedUserAsync`. The two gates are different codes - see section 2. |
| **Pattern** | Not a CRUD list page. A single-purpose live console rendering a raw `<table class="simf-table">`; no `SimfDataGrid`, no toolbar, no pager, no modal. |
| **Status** | Real |
| **Implements use case(s)** | UC-36 "Manage the questions of an assigned session" (`SIMF-UCS-001` §4.5), requirement FR-705 |
| **Backend endpoints** | `GET /account/api/sessions/{sessionId}/questions/moderate`, `PUT /account/api/sessions/{sessionId}/questions/{questionId}/hide`, `PUT /account/api/sessions/{sessionId}/questions/{questionId}/push` |
| **Source file** | [`SessionModerationDesk.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionModerationDesk.razor) + [`SessionModerationDesk.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionModerationDesk.razor.cs) |
| **Tests** | [`docs/tests/e2e/cp-session-moderate.md`](../../tests/e2e/cp-session-moderate.md) (E2E-MOD-001..013, E2E-MOD-ELS-001/002); bUnit `tests/SIMF.ControlPanel.Tests/SessionModerationDeskTests.cs` + `SessionsListModerationTests.cs`; API `tests/SIMF.Api.Tests/SessionQuestionsTests.cs`, `ModeratorDeskStateTests.cs`, `SessionQuestionCommitteeTests.cs`, `AppWireContractPinTests.cs` |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

This is the desk a moderator works while a session is running on stage. Audience
questions arrive through the app; those that clear the Scientific Committee (PRE
questions) and those submitted while the session is live (which auto-approve) both
land in one ordered queue, and the moderator decides which of them reaches the
speaker. The page gives that person exactly three verbs and nothing else: read the
queue, hide a question so it stays off the stage, and push a question to the
speaker. It is scoped to one session by the route id, because a moderator's grant
is per session rather than global - a person may run the Q&A for one panel and
have no standing on the next. It is deliberately not a CRUD page: nothing is
created, edited or deleted here, only moved between states on a question someone
else wrote.

## 2. Audience + permissions

- **Who can reach it:** any signed-in Control Panel user whose `perm` claims
  contain `Questions.Moderate` or the Administrator wildcard `*`. The page carries
  `@attribute [RequirePermission(PermissionCatalog.Questions.Moderate)]`;
  `RequirePermissionAttribute` is an `AuthorizeAttribute` whose `Policy` is
  `PermissionCatalog.PolicyFor(code)`, satisfied by `PermissionAuthorizationHandler`
  against the `perm` claim
  (`src/ControlPanel/SIMF.ControlPanel/Authorization/PermissionAuthorization.cs`).
- **Who can actually load and change anything:** a strictly smaller set. Reaching
  the page is not the same as being allowed to use it. Every one of the three API
  calls resolves the caller through
  `SessionModeratorAuth.ResolveAuthorizedUserAsync`
  (`src/Backend/SIMF.Api/Endpoints/Sessions/SessionQuestionEndpoints.cs`), which
  authorises only when the caller holds the `Administrator` role **or** has a row in
  `SessionModerators` for this exact `SessionId`. Anyone else gets
  `Send.ForbiddenAsync` - HTTP 403.
- **Authorisation gates, both layers, quoted:**

  | Layer | Gate as written in source |
  |-------|---------------------------|
  | CP page | `@attribute [RequirePermission(PermissionCatalog.Questions.Moderate)]` - code value `"Questions.Moderate"` |
  | CP action buttons | `<AuthorizedAction Permission="@PermissionCatalog.Questions.Moderate">` wrapping the Hide / Show / Push cell |
  | Entry point (Sessions grid row action) | `<AuthorizedAction Permission="@PermissionCatalog.Questions.Moderate">` in `SessionsList.razor` |
  | API `GET .../questions/moderate` | `Policies(nameof(AuthorizationPolicies.RequireApprovedAccount))` + `SessionModeratorAuth.ResolveAuthorizedUserAsync` |
  | API `PUT .../hide` | `Policies(nameof(AuthorizationPolicies.RequireApprovedAccount))` + `Options(rb => rb.RequireRateLimiting("auth"))` + `SessionModeratorAuth.ResolveAuthorizedUserAsync` |
  | API `PUT .../push` | `Policies(nameof(AuthorizationPolicies.RequireApprovedAccount))` + `Options(rb => rb.RequireRateLimiting("auth"))` + `SessionModeratorAuth.ResolveAuthorizedUserAsync` |

  **These three endpoints carry no `PermissionCatalog.PolicyFor(...)` policy.** That
  is unusual for the admin surface and is worth knowing before changing anything
  here: the API's authority for this desk is the per-session grant table, not a
  permission code. A comment in `SessionsList.razor.cs` states "The desk page + the
  API both enforce Questions.Moderate"; the API source does not bear that out, and
  the page's own header comment is the accurate one - "the API also enforces the
  per-session moderator / Administrator check; this keeps the CP convention (only
  Questions.Moderate holders reach the desk + see the actions)".
- **`Questions.Moderate` in the catalogue:**
  `new(Questions.Moderate, "Questions", "Moderate", "Approve / hide questions", ScientificCommittee)`
  (`PermissionCatalog.All`). Baseline role is `ScientificCommittee`, and the code is
  also in `ModeratorAppPermissions`, the set an app user receives from the
  `Moderator` mobile app role.
- **A second, similarly named code exists and this page does not use it.**
  `PermissionCatalog.SessionModeration.Moderate` (`"SessionModeration.Moderate"`,
  described as "Moderate a live session", same `ScientificCommittee` baseline) is
  defined, seeded and included in `ModeratorAppPermissions`, but a repository-wide
  grep for it finds no `.razor` page and no API endpoint that gates on it. Do not
  reach for it when adding a control here - the page, its buttons and its entry
  point all use `Questions.Moderate`.
- **What an unauthenticated user sees:** `Routes.razor` renders
  `<AuthorizeRouteView>`'s `NotAuthorized` fragment for both the unauthenticated and
  the authenticated-but-forbidden case and branches on the authentication state, so
  an anonymous visitor gets `<RedirectToLogin />`.
- **What a signed-in user without `Questions.Moderate` sees:**
  `<RedirectToNotPermitted />`, which does `Nav.NavigateTo("/not-permitted")`. The
  cookie handler's `AccessDeniedPath` is `/not-permitted` as well
  (`Program.cs` line 77).
- **What a `Questions.Moderate` holder without the per-session grant sees:** the
  page renders normally, then the `GET` returns 403, `env.Success` is false, and the
  page shows a red `SimfAlert` with no table. This is the only denial on this page
  that is surfaced as a message rather than a redirect, and it is the common one -
  the permission is a role-wide grant while the moderator assignment is per session.

## 3. Screenshots

No screenshots exist for this page. The table below records the file names the
catalogue expects so a capture pass has somewhere to put them.

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/cp-session-moderate-default.png` | Not captured |
| Golden path, before | `docs/screenshots/cp-session-moderate-golden-before.png` | Not captured |
| Golden path, after | `docs/screenshots/cp-session-moderate-golden-after.png` | Not captured |
| Empty state | `docs/screenshots/cp-session-moderate-empty.png` | Not captured |
| Add modal | N/A - the page has no modal | Not captured |
| Edit modal | N/A - the page has no modal | Not captured |
| Details modal | N/A - the page has no modal | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-session-moderate-rtl.png` | Not captured |
| Error state (403 / 500 toast) | `docs/screenshots/cp-session-moderate-error.png` | Not captured |

## 4. UI affordances

### 4.1 Banner / page header

`<SimfBanner Title="@L["Admin.SessionModeration.Title"]" />` - title only, no
`Subtitle` and no `Actions` fragment, both of which `SimfBanner` supports. The title
resolves to "Session Q&A moderation" / "إدارة أسئلة الجلسات". `SimfBanner` renders
the title as `<h1 class="simf-banner__title">`, which is what `Routes.razor`'s
`<FocusOnNavigate Selector="h1" />` moves focus to on arrival.

`<PageTitle>` is `@L["Admin.SessionModeration.Title"] · SIMF`.

The body is `<div class="simf-page-wide"><div class="simf-surface">`, with the
error alert, the Refresh action row and then one of three mutually exclusive
blocks: the loading paragraph, the empty state, or the table.

**The banner does not name the session.** The route carries only `SessionId`, the
component takes only `[Parameter] public Guid SessionId`, and nothing on the page
fetches the session title. A moderator working two panels sees the identical
heading on both and must rely on the URL or on having arrived from the right row of
the Sessions grid.

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. There is no toolbar, no Multiselect, no Add / Edit /
Details / Delete, no Copy / Paste / Duplicate, and no Excel Import / Export. The
page's only non-row control is a single button:

| Button | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| Refresh (`Admin.SessionModeration.Refresh`) | `LoadAsync` | `GET /account/api/sessions/{SessionId}/questions/moderate` | `<SimfButton Type="button" OnClick="LoadAsync" Disabled="_loading">`. Sits in a `simf-form__actions` row above the table. It is the page's only manual reload; the queue does not poll or push. |

### 4.3 Grid columns (CRUD pages only)

N/A - not a `SimfDataGrid`. The queue is a hand-written
`<table class="simf-table">` with a fixed five-column `<thead>` and a `@foreach`
body over `_rows`. It has no sorting, no filtering, no column chooser and no
selection. For completeness, the columns as written:

| Header key (EN / AR) | Bound expression | Sortable | Filterable | Notes |
|----------------------|------------------|----------|------------|-------|
| `Col.Order` - "#" / "#" | `@row.Order` | no | no | The persisted `Order` column. The server sorts by `Order` then `CreatedAt`; the page does not re-sort. |
| `Col.Submitter` - "Submitter" / "المرسل" | `@row.SubmittedByDisplayName` then `<small>(@(row.SubmittedByEmail ?? "—"))</small>` | no | no | The email is always `null` by design, so the small text is permanently the `"—"` placeholder. See section 7. |
| `Col.Question` - "Question" / "السؤال" | `@row.QuestionText` | no | no | Plain Razor interpolation, so it is HTML-encoded. No truncation and no expander, so a long question stretches its row. |
| `Col.State` - "State" / "الحالة" | derived, see below | no | no | Four-branch cascade, not a `SimfPill`. |
| `Col.Actions` - "Actions" / "الإجراءات" | buttons, see below | no | no | The whole cell is inside one `<AuthorizedAction Permission="@PermissionCatalog.Questions.Moderate">`. |

**State cell branch order**, which is load-bearing and pinned by tests:

1. `row.IsHidden` -> `State.Hidden` ("Hidden" / "مخفي")
2. `row.Status == QuestionStatus.Answered` -> `State.Answered` ("Answered" / "تمت الإجابة")
3. `row.IsPushed` -> `Pushed` ("Pushed" / "تم العرض")
4. otherwise -> `State.Queued` ("Queued" / "في الانتظار")

The Answered branch sits above the Pushed branch on purpose. The page comment gives
the reason: "Answered is the terminal desk state, so it wins over the pushed marker
(an answered question was pushed first). Without this branch the row rendered as
'Queued'." `SessionModerationDeskTests` pins all three of
`An_answered_question_is_labelled_answered_not_queued`,
`An_answered_question_that_was_pushed_still_reads_answered` and
`A_plain_approved_question_is_still_queued`.

**Row action buttons**, both inside the single `AuthorizedAction` wrapper:

| Button | Rendered when | Wired callback | Calls |
|--------|---------------|----------------|-------|
| Hide (`Admin.SessionModeration.Hide`) | `!row.IsHidden` | `SetHiddenAsync(row, true)` | `PUT .../{questionId}/hide` with `{ "isHidden": true }` |
| Show (`Admin.SessionModeration.Unhide`) | `row.IsHidden` | `SetHiddenAsync(row, false)` | `PUT .../{questionId}/hide` with `{ "isHidden": false }` |
| Push to speaker (`Admin.SessionModeration.Push`) | `!row.IsPushed && !row.IsHidden` | `PushAsync(row)` | `PUT .../{questionId}/push` with `{}` |

None of the three is disabled while its request is in flight. Re-entrancy is
blocked in the handler instead, by the `_busy` field, so a second click is
swallowed silently with no visual acknowledgement.

### 4.4 Pager

N/A - the page has no pager. `LoadAsync` fetches the whole queue in one call, the
API returns a bare `IReadOnlyList<SessionQuestionModeratorRow>` rather than a
`GridPage<T>`, and `SessionModerationService.ListAsync` applies no `Skip`/`Take`.
A session with a very long queue renders every approved and answered row in one
table. There is no "Showing X-Y of Z" caption and no page-size selector.

### 4.5 Form fields

N/A - the page hosts no form and no modal. The only request bodies it sends are
machine-set:

| Body | Shape | Set by |
|------|-------|--------|
| Hide / Show | `SetQuestionHiddenRequest { bool IsHidden }` | The button that was clicked, never typed by the user |
| Push | `new { }` (an empty object literal) | Fixed |

There is no free-text input anywhere on this page, which is why section 6 has no
field-level validation table.

## 5. Data flow

```
Refresh / OnInitializedAsync
  -> SessionModerationDesk.LoadAsync
  -> JS.InvokeAsync("simfAccount.getJson", "/account/api/sessions/{id}/questions/moderate")
  -> BFF  AccountEndpoints.Moderation.MapModeration -> group.MapGet(...)
  -> SimfAdminClient.ListModeratorQueueAsync (bearer = the cookie's access_token)
  -> API  GET /api/v1/app/sessions/{sessionId}/questions/moderate
  -> ListModeratorQueueEndpoint -> SessionModeratorAuth.ResolveAuthorizedUserAsync
  -> SessionModerationService.ListAsync -> SimfAppDbContext.SessionQuestions
                                        + SimfIdentityDbContext.Users (display name only)
  -> ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>
  -> _rows re-render, or _toast = error

Hide / Show click
  -> SetHiddenAsync(row, isHidden) [guarded by _busy]
  -> JS.InvokeAsync("simfAccount.putJson", ".../questions/{qid}/hide", SetQuestionHiddenRequest)
  -> BFF MapPut -> SimfAdminClient.HideQuestionAsync
  -> API PUT /api/v1/app/sessions/{sessionId}/questions/{questionId}/hide
  -> SessionModerationService.SetHiddenAsync -> SaveChanges + IAuditLog
  -> on Success: LoadAsync() re-fetches the whole queue; the returned row is discarded

Push click
  -> PushAsync(row) [guarded by _busy]
  -> JS.InvokeAsync("simfAccount.putJson", ".../questions/{qid}/push", new { })
  -> BFF MapPut -> SimfAdminClient.PushQuestionAsync
  -> API PUT /api/v1/app/sessions/{sessionId}/questions/{questionId}/push
  -> SessionModerationService.PushAsync -> SaveChanges + IAuditLog
  -> on Success: LoadAsync()
```

**The three layers, named.** The Control Panel is a BFF, so every call crosses three
hops and each hop has to be mapped explicitly:

1. **Page -> BFF.** `window.simfAccount.getJson` / `putJson` in
   `src/ControlPanel/SIMF.ControlPanel/wwwroot/js/simf-account.js` - a `fetch` with
   `credentials: 'same-origin'` (and `Content-Type: application/json` on the PUTs),
   whose response passes through `simfReadEnvelope`. The CP auth cookie travels; no
   bearer token is exposed to the browser.
2. **BFF -> API.** `MapModeration` in
   `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.Moderation.cs`.
   Each route reads `await http.GetTokenAsync("access_token")`, returns
   `Results.Unauthorized()` when it is null, and otherwise `Forward(...)` the
   `SimfAdminClient` result unchanged.
3. **API.** `SimfAdminClient` sends these three through `SendSessionsAsync`, whose
   base path is `"api/v1/app/sessions/"` - not the `/api/v1/admin/` base the rest of
   the client uses. The FastEndpoints classes declare relative routes
   (`Get("/app/sessions/{sessionId:guid}/questions/moderate")`), and
   `Program.cs` line 703 sets `config.Endpoints.RoutePrefix = "api/v1"`, so the
   effective path is `/api/v1/app/sessions/...`. This is the same surface the mobile
   moderator screen calls.

| When | CP page call | BFF mapping | API route (effective) | Request body | Response shape |
|------|--------------|-------------|-----------------------|--------------|----------------|
| `OnInitializedAsync`, and every Refresh, and after every successful Hide/Show/Push | `GET /account/api/sessions/{SessionId}/questions/moderate` | `group.MapGet("/sessions/{sessionId:guid}/questions/moderate", ...)` -> `ListModeratorQueueAsync` | `GET /api/v1/app/sessions/{sessionId}/questions/moderate` | none | `ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>` |
| Hide / Show click | `PUT /account/api/sessions/{SessionId}/questions/{row.Id}/hide` | `group.MapPut(".../{questionId:guid}/hide", ...)` -> `HideQuestionAsync` | `PUT /api/v1/app/sessions/{sessionId}/questions/{questionId}/hide` | `SetQuestionHiddenRequest` -> `{ "isHidden": bool }` | `ApiResult<SessionQuestionModeratorRow>` |
| Push click | `PUT /account/api/sessions/{SessionId}/questions/{row.Id}/push` | `group.MapPut(".../{questionId:guid}/push", ...)` -> `PushQuestionAsync` | `PUT /api/v1/app/sessions/{sessionId}/questions/{questionId}/push` | `{}` | `ApiResult<SessionQuestionModeratorRow>` |

**Mapped but not called from this page.** `AccountEndpoints.Moderation.cs` also maps
`PUT /account/api/sessions/{sessionId}/questions/reorder`
(`ReorderQuestionsRequest`, full-list contract). This page renders no reorder
control, so the route is unreachable from the Control Panel. The API additionally
exposes `PUT /api/v1/app/sessions/{sessionId}/questions/{questionId}/answered`
(`SetQuestionAnsweredEndpoint`), and there is **no** `/answered` mapping in the BFF
at all - the CP can display the Answered state but cannot set or clear it. Both
verbs are driven from the mobile moderator screen.

**Response fields** on `SessionQuestionModeratorRow`
(`src/Shared/SIMF.Contracts/Sessions/SessionQuestions.cs`): `Id`, `SessionId`,
`SubmittedByUserId`, `SubmittedByDisplayName`, `SubmittedByEmail`, `QuestionText`,
`Recipient`, `Order`, `IsHidden`, `IsPushed`, `PushedAt`, `CreatedAt`, `Phase`,
`Status`. The page reads eight of the fourteen: the markup uses `Order`,
`SubmittedByDisplayName`, `SubmittedByEmail`, `QuestionText`, `IsHidden`,
`IsPushed` and `Status`, and the code-behind uses `Id` to build the hide and push
URLs. The wire shape is pinned by
`tests/SIMF.Api.Tests/AppWireContractPinTests.cs`.

**Cross-database rule.** `SessionModerationService.ListAsync` reads the questions
from `SimfAppDbContext` and then resolves submitter display names with a separate
`SimfIdentityDbContext` query keyed on the bare `SubmittedByUserId` GUID. That is
the D-157 pattern - a logical FK resolved on read, never a cross-database join.

## 6. Validation + error handling

- **Client-side guards.** There are three, all in the code-behind, and none of them
  validates user input because the page accepts none:
  - `_loading` disables the Refresh button while a load is in flight
    (`Disabled="_loading"`), and is reset in a `finally`.
  - `_busy` is checked at the top of `SetHiddenAsync` and `PushAsync`
    (`if (_busy) return;`) and reset in a `finally`. It blocks a second mutation
    while one is in flight but does not disable any button, so the block is
    invisible to the user.
  - The success test is `env is { Success: true, Data: not null }` on load and
    `env is { Success: true }` on the mutations, so a malformed envelope falls into
    the error branch rather than throwing.
- **Server-side validation.** There is no FluentValidation validator for these
  routes - a search for `AbstractValidator<...Question...>` across
  `src/Backend` returns nothing. All rules live in
  `SessionModerationService` and throw `ApiException`:

  | Rule | Thrown by | Code | HTTP |
  |------|-----------|------|------|
  | The question id is not on this session | `LoadQuestionAsync` | `ErrorCodes.SessionQuestionNotFound` = `"SESSION_QUESTION_NOT_FOUND"` | 404 |
  | Push a question whose `Status != Approved` | `PushAsync` | `ErrorCodes.SessionQuestionInvalid` = `"SESSION_QUESTION_INVALID"` | 400 |
  | Mark answered a question whose `Status != Approved` | `SetAnsweredAsync` | `SESSION_QUESTION_INVALID` | 400 |
  | A `?status=` value outside the desk's three tabs | `ListAsync` | `SESSION_QUESTION_INVALID` | 400 |
  | A reorder list with duplicates, or not equal to the desk set | `ReorderAsync` | `SESSION_QUESTION_INVALID` | 400 |

  Only the first two are reachable from this page. The 404 message pair is "The
  question was not found on this session." / "لم يتم العثور على السؤال على هذه الجلسة."
  The push-not-approved pair is "Only an approved question can be pushed to the
  speaker." / "لا يمكن دفع سؤال غير معتمد إلى المتحدث."
- **Error envelope.** The standard `ApiResult<T>`; the page reads
  `env?.Error?.MessageForCurrentCulture()`, so the bilingual message is picked by the
  current UI culture rather than by the page.
- **Toast strategy.** Error only. `_toast` is set in the failure branch of all three
  handlers, with the same fallback when the server sent no message:
  `?? L["Admin.SessionModeration.Loading"]`, which renders "Loading queue…" /
  "جارٍ تحميل القائمة…" as the text of a red alert. That is the loading string being
  reused as an error string; it reads as a status, not a failure. There is no success
  toast and no info toast - a successful Hide or Push is acknowledged only by the row
  changing after the reload.
- **`_toast` is never cleared.** No handler sets it back to `null`, so once an error
  has been shown the red `SimfAlert` stays on the page for the rest of the
  component's life, including after subsequent successful actions. A moderator who
  hits one 404 sees the alert above every later success.

## 7. Edge cases + known limitations

- **The Show / Unhide button is unreachable on this page, and a hidden question
  disappears.** `LoadAsync` requests the queue with no query string, so `status` is
  null at the endpoint. `SessionModerationService.ListAsync` then applies its default
  bucket, `q.Status == QuestionStatus.Approved || q.Status == QuestionStatus.Answered`
  - hidden rows are excluded. Since `IsHidden` on the DTO is projected as
  `question.Status == QuestionStatus.Hidden`, no row the CP ever receives can have
  `IsHidden == true`. Consequences: the `State.Hidden` label and the `Unhide`
  ("Show") button are dead branches in the CP markup, and hiding a question makes it
  vanish from the table on the follow-up reload rather than switching to a "Hidden"
  state. Recovery is not available from this page; it is available from the mobile
  moderator desk's Hidden tab (the API supports `?status=Hidden`, which returns rows
  whose `StatusBeforeHidden` was `Approved` or `Answered`) or, for a
  Committee-rejected question, from the Committee queue.
- **Hiding a pushed question un-pushes it.** `SetHiddenAsync` clears both `IsPushed`
  and `PushedAt` when hiding, so a question already on the speaker's queue drops off
  it. Un-hiding does not re-push; the service comment states "a fresh push is an
  explicit action". Pinned by
  `SessionQuestionsTests.Hiding_a_pushed_question_clears_the_pushed_marker`.
- **Un-hiding restores the prior status, not `Approved`.** `SetHiddenAsync` writes
  `question.StatusBeforeHidden` on the way in and reads it back on the way out, so an
  answered question keeps its answered mark and a Committee-rejected question returns
  to `Pending` (back to the Committee, not onto the desk). Rows hidden before that
  column existed have no memory and fall back to `Approved`.
- **Hide and Push are both idempotent.** `SetHiddenAsync` returns early when
  `currentlyHidden == isHidden`, and `PushAsync` returns early when
  `question.IsPushed` is already true. Neither writes a second audit row. Pinned by
  `Hide_then_unhide_round_trips_state_and_is_idempotent` and
  `Push_marks_question_pushed_with_timestamp_and_is_idempotent`.
- **Only an `Approved` question can be pushed.** An `Answered` row still renders a
  Push button in the CP when `IsPushed` is false, because the markup tests
  `!row.IsPushed && !row.IsHidden` and does not test `Status`. Clicking it produces a
  400 `SESSION_QUESTION_INVALID` toast rather than a push. This is a UI-versus-API
  mismatch on a state the CP cannot itself create, since the CP has no way to mark a
  question answered.
- **The submitter email column is permanently a dash.** `SessionQuestionModeratorRow`
  documents `SubmittedByEmail` as "Always null on the moderator queue (PII
  redaction); kept for wire compatibility", and both `ListAsync` and `ToRowAsync`
  pass `null` explicitly. The page renders `(@(row.SubmittedByEmail ?? "—"))`, so
  every row shows `(—)`. The field is kept on the wire because removing it would
  break the shipped mobile contract. Pinned by
  `SessionQuestionsTests.Moderator_queue_redacts_the_submitter_email`.
- **A missing Identity user degrades to an empty name.** `ListAsync` does
  `users.TryGetValue(...)` and passes `user?.DisplayName ?? string.Empty`, so a
  question whose submitter row is gone renders a blank Submitter cell beside the
  `(—)`, not an error.
- **Two kinds of question share the queue.** Per
  `docs/tests/e2e/cp-session-moderate.md`, PRE questions reach `Approved` through the
  Scientific Committee while LIVE questions auto-approve straight onto the desk with
  no AI filter and no Committee step. The desk treats them identically and the table
  shows no marker distinguishing them, even though the DTO carries `Phase`. Verified
  at the API layer by
  `SessionQuestionsTests.Live_question_skips_AI_and_lands_directly_on_the_moderator_desk`.
- **No live update.** The queue is fetched on init and after each action, and
  otherwise only on Refresh. There is no polling, SignalR or server push, so a
  question approved while the moderator is looking at the page does not appear until
  they click Refresh.
- **No reorder control**, despite FR-705 naming ordering as part of the moderator's
  job and despite `PUT .../questions/reorder` being mapped all the way through the
  BFF. The `#` column shows `Order` but cannot change it. Reorder is a mobile-only
  affordance today.
- **The whole queue renders at once**; see section 4.4.
- **The page does not use `SimfDataGrid`.** It is a raw
  `<table class="simf-table">`, which deviates from the CP list-page standard.
  `docs/dev/SIMF_TABLE_PATTERN.md` records no exception for this page, and no other
  doc found this session sanctions the deviation, so it is undocumented rather than
  agreed. It is also not unique: ten other CP pages under `Components/Pages/Admin`
  render a raw `simf-table`. The deviation costs this page sorting, filtering,
  paging, the accessible grid caption and the standard row-action slot.
- **Rate limiting is asymmetric.** Both PUTs carry
  `Options(rb => rb.RequireRateLimiting("auth"))`; the GET does not, so repeated
  Refresh clicks are not rate-limited at the endpoint.
- **Audit trail.** Every successful mutation writes through `IAuditLog`:
  `AuditEvents.SessionQuestionHidden` / `SessionQuestionUnhidden` /
  `SessionQuestionPushed`, with `Detail = "sessionId=...; questionId=..."` and the
  actor id resolved by `SessionModeratorAuth`. Hide and unhide also emit an
  `ILogger` information line; push does not.

## 8. i18n + RTL

- Every visible string on this page resolves through
  `IStringLocalizer<Strings> L`. There are 16 keys, all under the
  `Admin.SessionModeration.` prefix, and all 16 are present in both
  `src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx` and
  `Strings.ar.resx` (verified line by line):

  | Key | EN | AR |
  |-----|----|----|
  | `Admin.SessionModeration.Title` | Session Q&A moderation | إدارة أسئلة الجلسات |
  | `Admin.SessionModeration.Loading` | Loading queue… | جارٍ تحميل القائمة… |
  | `Admin.SessionModeration.None` | No questions yet. | لا توجد أسئلة بعد. |
  | `Admin.SessionModeration.Refresh` | Refresh | تحديث |
  | `Admin.SessionModeration.Hide` | Hide | إخفاء |
  | `Admin.SessionModeration.Unhide` | Show | إظهار |
  | `Admin.SessionModeration.Push` | Push to speaker | عرض للمتحدّث |
  | `Admin.SessionModeration.Pushed` | Pushed | تم العرض |
  | `Admin.SessionModeration.Col.Order` | # | # |
  | `Admin.SessionModeration.Col.Submitter` | Submitter | المرسل |
  | `Admin.SessionModeration.Col.Question` | Question | السؤال |
  | `Admin.SessionModeration.Col.State` | State | الحالة |
  | `Admin.SessionModeration.Col.Actions` | Actions | الإجراءات |
  | `Admin.SessionModeration.State.Hidden` | Hidden | مخفي |
  | `Admin.SessionModeration.State.Queued` | Queued | في الانتظار |
  | `Admin.SessionModeration.State.Answered` | Answered | تمت الإجابة |

  Counted across the `.razor` and its code-behind, two keys are used more than
  once: `Title` twice (the `<PageTitle>` and the `SimfBanner`) and `Loading` four
  times (the loading paragraph plus the error fallback in each of the three
  handlers). `Pushed` is used once. It is the only state label not under a
  `State.` prefix, which is a naming inconsistency rather than a second use.
- **Direction** is set document-wide, not by this page:
  `src/ControlPanel/SIMF.ControlPanel/Components/App.razor` line 4 renders
  `dir="@(CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? "rtl" : "ltr")"`.
  The page contributes no direction-specific markup and no inline styles, so its
  mirroring is entirely whatever `simf-table`, `simf-page-wide`, `simf-surface` and
  `simf-form__actions` do in RTL. **Unverified** - the RTL render of this specific
  page has not been captured or inspected in a browser; E2E-MOD-012 covers it and is
  still `_to author_`.
- **Untranslated content.** `QuestionText` and `SubmittedByDisplayName` are user
  data and are shown as submitted, in whatever language the attendee typed.
- **The `"—"` email placeholder is a literal in the markup**, not a resx key.

## 9. Accessibility

- **Keyboard.** All controls are real `<button>` elements: `SimfButton` renders
  `<button type="@Type" ... disabled="@(Disabled || Loading)" @onclick="OnClick">`,
  and this page passes `Type="button"` everywhere, so tab order is DOM order -
  Refresh, then each row's Hide / Push pair top to bottom. There are no modals, so
  there is no focus trap and nothing to close with ESC.
- **Focus on navigation.** `Routes.razor` includes
  `<FocusOnNavigate RouteData="routeData" Selector="h1" />`, and `SimfBanner`
  renders the only `<h1>` on the page, so arriving from the Sessions grid puts focus
  on the "Session Q&A moderation" heading.
- **Announcements.** `SimfAlert` with `Variant="error"` renders
  `<div class="simf-alert simf-alert--error" role="alert">`, which is announced
  assertively. Since the page raises only error toasts, successful actions are not
  announced at all - a screen-reader user gets no confirmation that a Push
  succeeded, only the table re-rendering.
- **Known gaps in the table markup.** The `<table class="simf-table">` has no
  `<caption>`, no `scope="col"` on its `<th>` elements and no `aria-label`. This is
  one of the things a `SimfDataGrid` would have supplied. The state cell conveys
  status as plain text (not colour alone), which is correct.
- **Busy state is not exposed.** `SimfButton` sets `aria-busy` only from its
  `Loading` parameter, which this page never passes. Refresh goes `disabled` while
  `_loading`; the row buttons show no busy state at all while `_busy` is blocking
  them.
- **Colour contrast.** Inherited from the shared component library and
  `theme.tokens.css`; nothing on this page sets a colour. Not independently measured
  for this page.
- **Focus indicators.** Inherited `--focus-ring` token; no page-level override.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| UC-36 | Manage the questions of an assigned session | `SIMF-UCS-001` §4.5, primary actor Moderator, requirement FR-705. This page is the Control Panel realisation of it. |
| UC-14 (§5, "Ask a question") | The attendee side that feeds this queue | Its main flow step 5 reads "The moderator handles the question (UC-36)". |

`SIMF-UCS-001` open item **OI-2** is still recorded against UC-36: "Confirm the
moderator's exact in-session controls for UC-36 against the app design." FR-705 as
written in `SIMF-SRS-001` is "The system shall let a moderator, for the sessions
assigned to them, view, order, hide and put questions to the speaker" - this page
delivers view, hide and put; **order** is not implemented here (section 7).

## 11. Related E2E test scenarios

| Scenario | File | Coverage |
|----------|------|----------|
| E2E-MOD-001 golden path, Hide -> Show -> Push | [`cp-session-moderate.md`](../../tests/e2e/cp-session-moderate.md) | The whole round trip. Its Show leg does not match this page today - see the drift note below. |
| E2E-MOD-002 Refresh reloads in place | same | The one toolbar control. |
| E2E-MOD-003 hide a queued question | same | `PUT .../hide` with `isHidden: true`. |
| E2E-MOD-004 show a hidden question | same | `PUT .../hide` with `isHidden: false`. Not reachable from this page - see drift. |
| E2E-MOD-005 push to speaker | same | `PUT .../push`, state becomes Pushed, Push button withdrawn. |
| E2E-MOD-006 empty state | same | `SimfEmptyState` titled "No questions yet." / "لا توجد أسئلة بعد." |
| E2E-MOD-007 per-session moderator without the Administrator role | same | The `SessionModerators` grant path. |
| E2E-MOD-008 neither Administrator nor moderator -> 403 -> toast | same | The API denial. Precondition has drifted - see below. |
| E2E-MOD-009 idempotent push | same | Early return, no duplicate audit row. |
| E2E-MOD-010 unknown question id -> 404 | same | `SESSION_QUESTION_NOT_FOUND`. |
| E2E-MOD-011 server 500 on load -> bilingual fallback toast | same | The `?? L["Admin.SessionModeration.Loading"]` fallback. |
| E2E-MOD-012 RTL / Arabic render | same | The only coverage of this page's RTL. |
| E2E-MOD-013 a live question lands on the desk directly | same | Backed by `SessionQuestionsTests.Live_question_skips_AI_and_lands_directly_on_the_moderator_desk`. |
| E2E-MOD-ELS-001 / 002 element inventory and health | same | Element-sweep pair. |

**None of these has been executed against a browser.** Every scenario above is
written out in full Gherkin in the catalogue, but the catalogue's own Status
column marks all of them `_to author_` except `E2E-MOD-013`, which reads
"authored ✓" and cites the API test rather than a browser run. Read the Coverage
column here as what a scenario is meant to prove, not as coverage that has run.

**Drift between this page and its catalogue, recorded not fixed.** Three items in
`cp-session-moderate.md` no longer describe the code:

1. Its "Authorization model" preamble states the page "carries only
   `@attribute [Authorize]` - it is **NOT** gated by a `PermissionCatalog` code, so
   there is no `RequiredPermission` and no `/not-permitted` redirect on the page
   itself". The page now carries
   `@attribute [RequirePermission(PermissionCatalog.Questions.Moderate)]`, and its own
   comment records the change: "Per-page permission gate (was `[Authorize]` only)".
   A signed-in admin without `Questions.Moderate` **is** redirected to
   `/not-permitted`.
2. E2E-MOD-008's precondition ("a signed-in user with no Administrator role and no
   `SessionModerators` row") no longer produces the 403 path on its own - such a
   user must also hold `Questions.Moderate` to get past the page gate and reach the
   API call. Without it they are redirected before any request fires.
3. E2E-MOD-003 and E2E-MOD-004 assert a row that stays visible and reads "Hidden",
   then a "Show" button that restores it. On this page the hidden row leaves the
   list entirely (section 7), so the Show leg cannot be executed here.

The catalogue also states the desk lists "the `QuestionStatus.Approved` set"; the
service's default bucket is `Approved` **or** `Answered`, which is why this page has
an Answered state label at all.

**Non-browser coverage.** bUnit `SessionModerationDeskTests` pins the state cell,
`SessionsListModerationTests` pins the entry-point row action
(`Moderate_action_is_shown_to_a_moderator`,
`Moderate_action_is_hidden_without_the_permission`,
`Moderate_action_navigates_to_the_desk`). At the API layer,
`SessionQuestionsTests` covers the queue projection, the email redaction, the 403
and grant paths and hide/push idempotency; `ModeratorDeskStateTests` covers the
answered mark, the Hidden tab and rejected-question recovery.

## 12. Related docs

- Admin Manual: **N/A** - the manual has no chapter for
  `/sessions/{id}/moderate`. The adjacent chapter is §6.1 "Session moderators -
  `/admin/session-moderators`", which is where the per-session grant this page
  depends on is created.
- Entry point: [`docs/pages/cp/admin-sessions.md`](admin-sessions.md) if present, and
  the Sessions grid source
  [`SessionsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionsList.razor)
  - the "Moderate" (gavel) `SimfToolbarButton` in its `<RowActions>` slot is the only
  navigation into this route. `CpNavigation.cs` deliberately omits it: "The
  moderator's own live desk is `/sessions/{id}/moderate`, reached from the Sessions
  grid rather than from the nav."
- Pattern doc: [`SIMF_TABLE_PATTERN.md`](../../dev/SIMF_TABLE_PATTERN.md) - listed
  for contrast; this page does not follow it.
- Permissions: [`SIMF-Auth-Permissions-Dev-Guide.md`](../../manuals/SIMF-Auth-Permissions-Dev-Guide.md)
  and [`SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md).
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) for the
  `ApiResult<T>` envelope and error model. **Unverified** - a search of that file for
  "questions/moderate" and "moderation" found no section for these three endpoints,
  so the envelope is the only part it covers.
- Component catalogue: [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md).
  The components this page uses are `SimfBanner`, `SimfAlert`, `SimfButton`,
  `SimfEmptyState` and `AuthorizedAction` (a thin wrapper over `SimfActionGate`).
  Only three of those are in the catalogue: `SimfButton`, `SimfAlert` and
  `SimfEmptyState`. `SimfBanner` and `AuthorizedAction` do not appear in it at all,
  so read the source for those two.
- Requirements: FR-705 in
  [`SIMF-SRS-001`](../../SIMF-SRS-001-Software-Requirements-Specification.md);
  FR-705 -> UC-36 traceability in
  [`SIMF-FDS-007-Engagement.md`](../../SIMF-FDS-007-Engagement.md) line 65.
- Mobile counterpart: the same API surface backs the app's `sessionModerate`
  screen; see [`docs/pages/mobile/session-moderate/`](../mobile/session-moderate/README.md)
  and [`e2e/mobile-session-moderate.md`](../../tests/e2e/mobile-session-moderate.md)
  as indexed in `PAGE-INDEX.md`.
- Source: [`SessionModerationDesk.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionModerationDesk.razor),
  [`SessionModerationDesk.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SessionModerationDesk.razor.cs),
  [`AccountEndpoints.Moderation.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.Moderation.cs),
  [`SessionQuestionEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Sessions/SessionQuestionEndpoints.cs),
  [`SessionModerationService.cs`](../../../src/Backend/SIMF.Infrastructure/SessionQuestions/SessionModerationService.cs).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| Unknown | Unverified | The decision that originally shipped this page was not traced this session. The E2E catalogue attributes the desk's Approved-set behaviour to D-212 and its entry point to D-646, but neither id was read in a source file, so neither is asserted here as this page's shipping decision. |
| Unknown | Security finding M6 | `@attribute [Authorize]` replaced by `@attribute [RequirePermission(PermissionCatalog.Questions.Moderate)]`, and the row actions wrapped in `<AuthorizedAction>`. `docs/security/SIMF-Security-Assessment-2026-06-20.md` raises it at line 189 as M6, "CP `SessionModerationDesk` gated only `[Authorize]`, not `[RequirePermission]`; action buttons unwrapped", and records the remediation at line 316 as "committed `0cd796b6`". D-207 records the original state, that the page "stays `[Authorize]` - its access is data-scoped per-session at the API". The assessment carries no date for the fix itself, so the date stays Unknown. |
| Unknown | DEF-MOD-001 (r2) | The State column gained its `QuestionStatus.Answered` branch above the `IsPushed` branch. Source: the header comment of `tests/SIMF.ControlPanel.Tests/SessionModerationDeskTests.cs` - "a not-yet-pushed answered question rendered as 'Queued'... These tests pin the cell." The defect id is quoted from that file; no date was recorded in it. |
| 2026-08-19 | - | This reference doc authored. |

---

_Last reviewed:_ `2026-08-19` by Claude, from source only - the page was **not**
opened in a browser and no screenshot, console or DOM check was performed, so
sections 3 and the RTL half of section 8 are unverified by observation. If the page
has changed and this doc has not been re-reviewed in 60 days, it is **out of date**.
Re-walk the page in a browser and update every section that drifted.
