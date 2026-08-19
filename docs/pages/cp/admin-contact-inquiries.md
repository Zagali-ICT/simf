# Contact inquiries - `/admin/contact-inquiries`

| | |
|--|--|
| **Route** | `/admin/contact-inquiries` |
| **Layout** | `CpShellLayout` (`@layout CpShellLayout`) |
| **Surface** | Control Panel (Blazor) |
| **Audience** | Any signed-in admin whose role holds `ContactInquiries.View`. The nav item sits under the `Nav.PublicRelations` group in `CpNavigation.cs`, icon `inbox`. |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.ContactInquiries.View)]`. API: `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.ContactInquiries.View), nameof(AuthorizationPolicies.RequireApprovedAccount))`. |
| **Pattern** | Not canonical CRUD. The page header comment calls it a "Read-only SimfDataGrid (server-paged); the per-row quiet action marks an inquiry handled / reopens it (ContactInquiries.Manage). Mirrors DelegationMeetingsList." |
| **Status** | Real. `docs/pages/PAGE-INDEX.md` line 124 records it as "Real (D-464; BFF wired D-649)". |
| **Implements use case(s)** | N/A - searched `docs/SIMF-UCS-001-Use-Case-Specifications.md` for "contact"; the three hits are unrelated (a message with contact details, "contact another admin", and a sign-up Contact step). No use case covers this page. |
| **Backend endpoints** | `POST /account/api/admin/contact-inquiries/list`, `POST /account/api/admin/contact-inquiries/{id:guid}/handled`. Both forward to `/api/v1/admin/contact-inquiries/*`. The rows are fed by the anonymous `POST /api/v1/app/contact-inquiry`. |
| **Source file** | [`ContactInquiriesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactInquiriesList.razor) + [`ContactInquiriesList.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactInquiriesList.razor.cs) |
| **Tests** | E2E: [`docs/tests/e2e/cp-contact-inquiries.md`](../../tests/e2e/cp-contact-inquiries.md). Integration: `tests/SIMF.Api.Tests/ContactInquiryTests.cs` (3 facts). Generic gates: `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` (`Every_admin_endpoint_is_permission_and_approval_gated`), `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`. |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

This page is the Control Panel inbox for the "تواصل معنا / Contact us" messages
submitted from the mobile app. The submit endpoint is anonymous, so anyone -
signed in or not - can write in, and there is no other place in SIMF where those
messages surface. A public-relations operator walks in expecting to read the
backlog, work down it, and mark each message closed once they have replied. It is
a triage list rather than a mailbox: the page carries no reply box and no
internal notes, so the operator answers by email outside SIMF and uses the row
action only to record that the message has been dealt with. `Admin-Manual.md`
§8.11 states the same job in the operator's words: "you read the message here and
reply outside SIMF using the sender's email."

## 2. Audience + permissions

- **Who can reach it:** an authenticated Control Panel user whose `perm` claims
  contain `ContactInquiries.View` or the Administrator wildcard
  (`PermissionCatalog.Wildcard`). The nav item carries the same code
  (`CpNavigation.cs` line 153, `RequiredPermission: PermissionCatalog.ContactInquiries.View`),
  so an admin without it does not see the menu entry either.
- **Who can write on it:** the mark-handled / reopen row action is wrapped in
  `<AuthorizedAction Permission="@PermissionCatalog.ContactInquiries.Manage">`.
  `ContactInquiries.View` alone renders the grid with no action icons.
- **Authorisation gates, all three layers:**
  - Page: `@attribute [RequirePermission(PermissionCatalog.ContactInquiries.View)]`.
    `RequirePermissionAttribute` is an `AuthorizeAttribute` whose `Policy` is
    `PermissionCatalog.PolicyFor(code)`, satisfied by a `perm` claim equal to the
    code or to the wildcard (`PermissionAuthorization.cs`).
  - BFF: the whole `/account/api` route group is built as
    `routes.MapGroup("/account/api").RequireAuthorization()`, and each forwarder
    additionally returns `Results.Unauthorized()` when the auth cookie holds no
    `access_token`.
  - API: `ListContactInquiriesEndpoint` requires
    `PolicyFor(ContactInquiries.View)` + `RequireApprovedAccount`;
    `MarkContactInquiryHandledEndpoint` requires
    `PolicyFor(ContactInquiries.Manage)` + `RequireApprovedAccount`.
- **The CP gate is UX, not the boundary.** `AuthorizedAction.razor` says so in its
  own header: "The API still enforces the same permission, so this is a UX layer
  (don't show what you can't do), not the security boundary." Hiding the icon does
  not stop the call; the endpoint policy does.
- **What an unauthenticated user sees:** `Routes.razor` branches inside
  `AuthorizeRouteView`'s `<NotAuthorized>` fragment. An unauthenticated visitor
  gets `<RedirectToLogin />`. A signed-in admin who lacks the permission gets
  `<RedirectToNotPermitted />`, which sends them to `/not-permitted` - the comment
  records why: without the branch "every permission denial on the 86 gated CP
  pages force-reloaded a signed-in admin onto /login, which reads as 'your session
  expired' rather than 'you may not open this page'."
- **Catalogue entries:** `PermissionCatalog.All` registers both codes with
  `BaselineRoles` `AdminOnly` - `new(ContactInquiries.View, "ContactInquiries", "View", "View contact-us inquiries", AdminOnly)`
  and `new(ContactInquiries.Manage, "ContactInquiries", "Manage", "Mark contact-us inquiries handled", AdminOnly)`.

## 3. Screenshots

**No screenshots exist for this page.** `docs/screenshots/` holds no file whose
name matches "contact". The table below is the intended capture set, not a record
of captures taken.

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/admin-contact-inquiries-default.png` | Not captured |
| Empty state | `docs/screenshots/admin-contact-inquiries-empty.png` | Not captured |
| Add modal | N/A - the page hosts no modal | N/A |
| Edit modal | N/A - the page hosts no modal | N/A |
| Details modal | N/A - the page hosts no modal | N/A |
| RTL (Arabic) | `docs/screenshots/admin-contact-inquiries-rtl.png` | Not captured |
| Error state (load failure banner) | `docs/screenshots/admin-contact-inquiries-error.png` | Not captured |

## 4. UI affordances

### 4.1 Banner / page header

`<SimfBanner Title="@L["Admin.ContactInquiries.Title"]" />` - the only parameter
passed. No subtitle and no actions slot. The title resolves to "Contact inquiries"
(EN) / "رسائل تواصل معنا" (AR). The document title is
`<PageTitle>@L["Admin.ContactInquiries.Title"] · SIMF</PageTitle>`.

Directly under the banner, inside `div.simf-page-wide > div.simf-surface`, the page
renders `<SimfAlert Variant="@_toast.Variant">@_toast.Message</SimfAlert>` when
`_toast` is non-null. That single alert is the page's whole toast surface: it is
used for both the success message after a toggle and the error message after a
failed call.

### 4.2 Toolbar

**N/A - the page wires no toolbar actions.** `SimfDataGrid.HasToolbar` is true only
when `Multiselect` is set, when one of the `On*` callbacks is wired and permitted,
or when a `CustomToolbar` is supplied. This page passes `Multiselect="false"`,
wires only `OnQueryChanged`, and supplies no `CustomToolbar`, so no toolbar block
renders at all. There is no Add, Edit, Delete, Copy, Paste, Duplicate, Import or
Export on this page, and therefore no `AddPermission` / `EditPermission` /
`DeletePermission` / `ImportPermission` / `ExportPermission` to set.

The grid's v2 right-click context menu is also inert here:
`AnyContextHandlerWired` is the OR of `OnDetailsOne` / `OnEditOne` / `OnCopyOne` /
`OnDeleteOne` / `OnDuplicateOne`, none of which this page wires.

### 4.2b Row action (the page's only write)

| Control | Rendered when | Wired callback | Calls | Permission |
|---------|---------------|----------------|-------|------------|
| `SimfToolbarButton Icon="check"`, title `Admin.ContactInquiries.MarkHandled` ("Mark handled" / "وضع كمُعالجة") | `context.IsHandled` is false | `SetHandledAsync(context, true)` | `POST /account/api/admin/contact-inquiries/{id}/handled` with `{ handled: true }` | `ContactInquiries.Manage` |
| `SimfToolbarButton Icon="rotate"`, title `Admin.ContactInquiries.Reopen` ("Reopen" / "إعادة فتح") | `context.IsHandled` is true | `SetHandledAsync(context, false)` | same route, `{ handled: false }` | `ContactInquiries.Manage` |

Both live in the grid's `<RowActions>` slot inside one
`<AuthorizedAction Permission="@PermissionCatalog.ContactInquiries.Manage">`, so
exactly one icon shows per row and neither shows without Manage. Because
`RowActions` is non-null, `SimfDataGrid.HasRowEndCell` is true and the grid renders
the trailing actions column headed `@L["Grid.Actions"]` ("Actions" / "إجراءات").

### 4.3 Grid columns

`SimfDataGrid<AdminContactInquiryRow>` with `RowKey="@(r => r.Id.ToString())"`,
`RowLabel="@(r => r.Name)"`, `Caption="@L["Admin.ContactInquiries.Title"]"`.

| Column | Key | Source field | Cell template | Sortable | Filterable |
|--------|-----|--------------|---------------|----------|------------|
| Name (`Admin.ContactInquiries.Col.Name` / "الاسم") | `name` | `AdminContactInquiryRow.Name` | `@context.Name` | no | no |
| Email (`.Col.Email` / "البريد الإلكتروني") | `email` | `.Email` | `@context.Email` | no | no |
| Message (`.Col.Message` / "الرسالة") | `message` | `.Message` | `@Truncate(context.Message)` | no | no |
| Status (`.Col.Status` / "الحالة") | `status` | `.IsHandled` | `SimfPill Variant="on"` with `.Status.Handled` ("Handled" / "تمت المعالجة") when true, else `SimfPill Variant="warn"` with `.Status.Open` ("Open" / "مفتوحة") | no | no |
| Received (`.Col.Received` / "وردت") | `createdAt` | `.CreatedAt` | `@context.CreatedAt.FormatSaudi("dd-MM-yyyy hh:mm tt")` | **yes** | no |

Notes that matter when reading this table:

- **Truncation is exact:** `private static string Truncate(string text) => text.Length <= 80 ? text : text[..77] + "…";`
  A message of 80 characters or fewer is shown whole; a longer one is cut to 77
  characters plus a single U+2026 ellipsis, so the rendered cell is 78 characters.
  The E2E catalogue and `Admin-Manual.md` §8.11 both round this to "80" / "about
  80".
- **No filter row renders.** `SimfDataGridColumn.Filterable` defaults to false and
  no column on this page sets it, so `HasAnyFilterableColumn` is false and the grid
  omits the whole `tr.simf-grid__filter-row`. The `FilterColumnLabel` and
  `FilterPlaceholder` parameters the page passes are therefore never used.
- **The Status column's key is `status`, but the service's column is `isHandled`.**
  Nothing sends that key today (the column is neither sortable nor filterable), so
  the mismatch is inert - but a future change that makes Status sortable must
  rename the key to `isHandled` or the server will reject it as an undeclared
  column.

### 4.4 Pager

The `SimfDataGrid` pager renders unconditionally: `First page` / `Previous` /
numbered page buttons / `Next` / `Last page` (all `SimfToolbarButton`s, disabled at
the ends and while `Loading`), plus a page-size `<select>` and two text readouts.

- Page-size options are the grid default, `new[] { 10, 20, 50, 100 }` - the page
  does not override `PageSizeOptions`.
- The page seeds `_query = new() { Top = 20 }`.
- Summary: `FormatSummary` renders `Grid.Summary` = "Showing {0}–{1} of {2}" /
  "عرض {0}–{1} من {2}" as `(skip + 1, skip + taken, total)`.
- Page label: `FormatPage` renders `Grid.Page` = "Page {0} of {1}" /
  "صفحة {0} من {1}".
- `LoadingLabel` is `Admin.ContactInquiries.Loading` ("Loading inquiries…" /
  "جارٍ تحميل الرسائل…").
- Empty grid: `<EmptyTemplate><SimfEmptyState Title="@L["Admin.ContactInquiries.None"]" /></EmptyTemplate>`
  = "No inquiries yet." / "لا توجد رسائل بعد."

### 4.5 Form fields

**N/A - this page hosts no form and no modal.** The only inputs the page has are
the grid's own pager select and the two row-action buttons.

The form that creates these rows lives on the mobile app and posts to the
anonymous `POST /api/v1/app/contact-inquiry`. Its contract is
`SubmitContactInquiryRequest` (Name / Email / Message) and it is validated twice,
recorded here because the limits govern what can ever appear in this grid:

| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| Name | text | yes | 120 | `SubmitContactInquiryValidator`: `NotEmpty().MaximumLength(120)`; re-checked by `ContactInquiryService.RequireText` | bilingual `ApiException` message ("The name is required." / "الاسم مطلوب.") |
| Email | text | yes | 256 | `NotEmpty().EmailAddress().MaximumLength(256)`; length re-checked server-side | bilingual `ApiException` message |
| Message | text | yes | 4000 | `NotEmpty().MaximumLength(4000)`; length re-checked server-side | bilingual `ApiException` message |

The validator's own comment records why those numbers: "Lengths mirror EF
`HasMaxLength` on `ContactInquiry` (Name 120 / Email 256 / Message 4000)."

## 5. Data flow

The Control Panel is a BFF. The Blazor page never holds an access token; it calls
a `/account/api/*` route on its own origin, that route reads the token from the
auth cookie and forwards through `SimfAdminClient` to `/api/v1/*`.

```
OnInitializedAsync / OnQueryChangedAsync
  -> ContactInquiriesList.LoadAsync
  -> JS.InvokeAsync("simfAccount.postJson", "/account/api/admin/contact-inquiries/list", _query)
  -> simf-account.js postJson: fetch POST, credentials 'same-origin', JSON body
  -> AccountEndpoints.Geography.cs  group.MapPost("/admin/contact-inquiries/list")
       (group = routes.MapGroup("/account/api").RequireAuthorization())
       http.GetTokenAsync("access_token"); null -> Results.Unauthorized()
  -> SimfAdminClient.ListContactInquiriesAsync  (BasePath "api/v1/admin/" + "contact-inquiries/list")
  -> ListContactInquiriesEndpoint  POST /api/v1/admin/contact-inquiries/list
       Policies(PolicyFor(ContactInquiries.View), RequireApprovedAccount)
  -> IContactInquiryService.ListAsync -> ContactInquiryService.ListAsync
  -> SimfAppDbContext.ContactInquiries.ToGridPageAsync(query, Columns, i => i.Id, ToRow)
  -> ApiResult<GridPage<AdminContactInquiryRow>>
  -> Forward(): Results.Json(result.Body, statusCode: result.StatusCode)
  -> simfReadEnvelope -> _page = env.Data  (or _toast = error)
```

The write path is the same spine with a different terminal:

```
Mark handled / Reopen click
  -> SetHandledAsync(row, handled)   [guarded by the _busy flag]
  -> JS "simfAccount.postJson", "/account/api/admin/contact-inquiries/{id}/handled", new { handled }
  -> group.MapPost("/admin/contact-inquiries/{id:guid}/handled")  (body record SetContactInquiryHandledBody(bool Handled = true))
  -> SimfAdminClient.MarkContactInquiryHandledAsync(id, handled, token)
  -> MarkContactInquiryHandledEndpoint  POST /api/v1/admin/contact-inquiries/{id:guid}/handled
       Policies(PolicyFor(ContactInquiries.Manage), RequireApprovedAccount)
       Options(rb => rb.RequireRateLimiting("auth"))
  -> ContactInquiryService.MarkHandledAsync(actorId, id, handled)
  -> ApiResult<bool> -> success toast -> LoadAsync() reloads the grid
```

Every backend call this page makes:

| When | CP page call (BFF) | API endpoint | Request body | Response shape |
|------|--------------------|--------------|--------------|----------------|
| `OnInitializedAsync`, and every grid sort / page / page-size change via `OnQueryChangedAsync` | `POST /account/api/admin/contact-inquiries/list` | `POST /api/v1/admin/contact-inquiries/list` | `GridQuery` (page seeds `Top = 20`) | `ApiResult<GridPage<AdminContactInquiryRow>>` |
| Mark handled / Reopen row action | `POST /account/api/admin/contact-inquiries/{id}/handled` | `POST /api/v1/admin/contact-inquiries/{id:guid}/handled` | `{ handled: bool }` | `ApiResult<bool>` |
| After a successful toggle | the list call above, again (`await LoadAsync()`) | as above | as above | as above |

**Not called by this page, but it is where the rows come from:**
`POST /api/v1/app/contact-inquiry` (`SubmitContactInquiryEndpoint`) is
`AllowAnonymous()`, `Options(rb => rb.RequireRateLimiting("auth"))`, `Tags("Public")`.
It captures `User.ActorIdOrNull()` into `ContactInquiry.SubmittedByUserId`, so a
message sent by a signed-in user is attributable and a guest's is not.

**Row shape.** `AdminContactInquiryRow(Guid Id, string Name, string Email, string Message, bool IsHandled, Guid? SubmittedByUserId, DateTime CreatedAt, DateTime? HandledAt)`.
The grid renders five of the eight members; `SubmittedByUserId` and `HandledAt`
are sent over the wire but not displayed.

**Ordering and paging, server side.** `ContactInquiryService.Columns` declares
`name` / `email` / `message` as searchable, plus `isHandled` and `createdAt`, then
`.DefaultOrder("isHandled")` and `.DefaultOrder("createdAt", descending: true)`,
with `.PageSize(fallback: 25, max: 200)`. The source comment gives the intent:
"Open inquiries first — false sorts before true — then newest first." The fallback
of 25 applies only when the request's `Top` is unset (`Top <= 0`); this page always
sends a positive `Top`, so 25 is never the effective size here.

**Storage.** `ContactInquiry : BaseAuditEntity` in `SIMF.Domain/Support`, on
`SimfAppDbContext`. `SubmittedByUserId` and `HandledByUserId` are bare `Guid`s.
The reason is in the comment on `SubmittedByUserId`: "Null for a guest. A bare
Guid: the user lives in the Identity database." That comment names no decision
id; the rule it follows is the Data / Identity separation recorded as D-157 in
`docs/decisions/DECISIONS_LOG.md` line 840. `HandledByUserId` carries no comment
of its own. `HandledAt` is commented "Saudi local time".

## 6. Validation + error handling

- **Client-side guards:** there is exactly one. `SetHandledAsync` opens with
  `if (_busy) return;`, sets `_busy = true` before the `try`, and clears it in the
  `finally`, so a double-click cannot fire two toggles. There is no client-side validation, because the page
  submits no user-entered data.
- **Server-side validation:**
  - The write path validates nothing beyond routing: the body is a single `bool`.
  - `ContactInquiryService.MarkHandledAsync` throws
    `ApiException(ErrorCodes.NotFound, 404, "The contact inquiry was not found.", "لم يتم العثور على الرسالة.")`
    when the id does not resolve. `ErrorCodes.NotFound` is the string `"NOT_FOUND"`.
  - The read path validates the `GridQuery` through the shared grid composition -
    `GridQueryComposition` returns 400 when the request "names a column that is not
    declared, sends the same column twice, or carries a value that will not parse",
    and caps the search term at 128 characters and the filter keys at 20.
  - The public submit is validated by `SubmitContactInquiryValidator`
    (FluentValidation, in `ContactInquiryEndpoints.cs`) and again by
    `ContactInquiryService.RequireText`, which trims and throws
    `ApiException(ErrorCodes.ValidationFailed, 400, ...)`. The service's own
    summary calls this "Server-side length guards mirror the FastEndpoints
    validator (defense in depth)."
- **Error envelope:** the standard `ApiResult<T>.Error` with a `Code` from
  `ErrorCodes` plus bilingual `Message` / `MessageArabic`. `Forward` returns the
  upstream status verbatim (`Results.Json(result.Body, statusCode: result.StatusCode)`),
  so the browser sees the API's own 400 / 403 / 404 / 429.
- **Non-envelope responses are converted, not thrown.** `simfReadEnvelope` in
  `simf-account.js` reads the body as text; a body that will not `JSON.parse`
  becomes a synthetic envelope with `code: 'BAD_RESPONSE'` and the messages
  "The server returned an unexpected response (HTTP {status})." /
  "أعاد الخادم استجابة غير متوقعة (HTTP {status})." The file's comment says why:
  a framework HTML error page would otherwise raise "a JSException that trips the
  global Blazor error UI."
- **HTTP 401 never reaches the page.** `simfReadEnvelope` intercepts it,
  calls `window.location.assign('/login')` and returns a never-resolving promise
  so "the calling page [cannot act] on a bogus body while the full-page navigation
  to /login is in flight."
- **Toast strategy** (all rendered through the single `SimfAlert`):

| Outcome | Variant | Message source |
|---------|---------|----------------|
| Marked handled | `success` | `Admin.ContactInquiries.Handled` - "Inquiry marked handled." / "تم وضع الرسالة كمُعالجة." |
| Reopened | `success` | `Admin.ContactInquiries.Reopened` - "Inquiry reopened." / "تمت إعادة فتح الرسالة." |
| List or toggle failed, envelope carries an error | `error` | `env.Error.MessageForCurrentCulture()` - the API's own bilingual message, picked by `CultureInfo.CurrentUICulture` |
| List or toggle failed with no usable envelope | `error` | fallback `Admin.ContactInquiries.LoadFailed` - "The inquiries could not be loaded." / "تعذّر تحميل الرسائل." |

  Note the fallback string is shared by both paths: a failed *toggle* with a null
  envelope also shows "The inquiries could not be loaded.", which is the wrong
  sentence for that action. `Admin-Manual.md` §8.11 documents the symptom as
  covering both ("The list call failed, or the mark-handled call failed").

## 7. Edge cases + known limitations

- **The toggle is idempotent.** `MarkHandledAsync` early-returns on
  `if (inquiry.IsHandled == handled) { return; }` before touching the row, so
  re-sending the same state is a no-op that still returns 200 and still shows the
  success toast. Two operators clicking "Mark handled" on the same row do not
  fight, and neither gets a conflict error.
- **Reopening clears the handler attribution.** The same method sets
  `HandledAt = handled ? now : null` and `HandledByUserId = handled ? actorUserId : null`.
  Reopening therefore erases who closed it and when; only `UpdatedAt` / `UpdatedBy`
  survive as evidence.
- **A concurrent delete gives 404, not a silent success.** `SingleOrDefaultAsync`
  followed by `?? throw new ApiException(ErrorCodes.NotFound, 404, ...)` means a
  toggle against a row another session removed surfaces the bilingual "The contact
  inquiry was not found." in the alert.
- **The toggle is rate limited, the list is not.** Only
  `MarkContactInquiryHandledEndpoint` carries
  `Options(rb => rb.RequireRateLimiting("auth"))`, so rapid toggling can return
  429; that status is forwarded verbatim by the BFF.
- **Sort and filter are richer on the server than in the UI.** The service declares
  `name` / `email` / `message` as searchable and accepts `isHandled` / `createdAt`
  as sort keys, but the page marks only `createdAt` sortable and no column
  filterable. An operator therefore cannot search the inbox from the page even
  though the endpoint supports it. This is an asymmetry in what the page exposes,
  not a defect in either layer.
- **The message is only ever shown truncated.** There is no details modal, no
  expand affordance and no export on this page, so a message longer than 80
  characters cannot be read in full anywhere in the Control Panel. Combined with
  "no reply from inside SIMF" (below), the operator must work from the email client.
- **`Admin-Manual.md` §8.11 troubleshooting is out of step with the page.** Two of
  its five rows reference affordances this page does not render: "your column
  filter excludes everything -> Clear the filter" and "Widen the Message column, or
  read the message in the export from the team". There is no filter input (no
  column sets `Filterable`), no column resize, and no export callback wired. Worth
  correcting in the manual, recorded here so a reader of either document is not
  misled.
- **The Figma node id is contested across three documents.** The page's header
  comment cites "Figma 1388:7567"; `DECISIONS_LOG.md` D-517 (line 494) says
  `1388:7567` is the FAQ screen (الأسئلة الشائعة); `PAGE-INDEX.md` line 318 gives
  the mobile contact-us screen as `1388:7711`. All three are quoted as read; this
  doc does not adjudicate which is right.
- **Not built today, by design:** no reply from inside the Control Panel, no
  assignment to an operator, no internal notes, and no delete. `Admin-Manual.md`
  §8.11 lists the same items under "What you cannot do here yet", in three bullets
  (assignment and internal notes share one), and states that "Marking it handled
  is the only close-out."
- **`SubmittedByUserId` is carried but never shown.** The row DTO includes it, so
  the page could distinguish a guest from a signed-in sender, but no column
  renders it and no filter uses it.

## 8. i18n + RTL

- Every visible string on the page comes from `IStringLocalizer<Strings> L`
  (injected in the code-behind) against
  `src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx` and `Strings.ar.resx`.
  The page's own keys are the fifteen under `Admin.ContactInquiries`: `.Title`,
  `.Loading`, `.LoadFailed`, `.None`, `.Col.Name`, `.Col.Email`, `.Col.Message`,
  `.Col.Received`, `.Col.Status`, `.Status.Open`, `.Status.Handled`,
  `.MarkHandled`, `.Reopen`, `.Handled`, `.Reopened`. A sixteenth key,
  `Module.ContactInquiries`, is the nav label set in `CpNavigation.cs` line 153 and
  is not used by the page markup. All sixteen sit together at lines 228-243 of both
  files.
- The grid chrome uses the shared `Grid.*` keys: `Grid.FilterColumn`,
  `Grid.FilterPlaceholder`, `Grid.Prev`, `Grid.Next`, `Grid.First`, `Grid.Last`,
  `Grid.PageSize`, `Grid.Page`, `Grid.Summary`, `Grid.Actions`, `Grid.SelectAll`,
  `Grid.SelectRow`. The page passes all twelve even though `SelectAll` /
  `SelectRow` are unreachable with `Multiselect="false"` and the two filter labels
  are unreachable with no filterable column.
- **Error messages are localised by the API, not by resx.** `MessageForCurrentCulture`
  picks `MessageArabic` when `CultureInfo.CurrentUICulture` is `ar` or any `ar-XX`,
  and `Message` otherwise.
- **Dates do not localise, deliberately.** `FormatSaudi` formats with
  `CultureInfo.InvariantCulture` - "stable, locale-independent digits and
  separators" - so the Received column reads `dd-MM-yyyy hh:mm tt` with Latin
  digits and AM/PM in both languages. `SaudiTime`'s header records the owner
  decision behind it: SIMF stores every instant already on the Saudi wall clock,
  so "converting a stored value now would shift it by three hours, which is
  precisely the bug the conversion previously existed to prevent."
- **RTL:** covered as a spec scenario by E2E-CINQ-008 (header, columns, status and
  action icons mirror; email and date stay LTR). Unverified in a browser - see
  section 3, no render was captured.

## 9. Accessibility

- **Table caption:** the grid renders
  `<caption class="simf-visually-hidden">@Caption</caption>` when `Caption` is set.
  This page sets it to `Admin.ContactInquiries.Title`, so a screen reader announces
  the table as "Contact inquiries".
- **Icon-only row buttons are named.** `SimfToolbarButton` emits
  `title="@Title"` and `aria-label="@(ChildContent is null ? Title : null)"`. The
  two row actions pass no child content, so each gets an `aria-label` of "Mark
  handled" / "Reopen" (or the Arabic equivalents).
- **Sortable header:** the `createdAt` header renders as a `<button>` with a sort
  arrow `<span ... aria-hidden="true">` showing ▲ / ▼ / ↕, so the glyph is not
  read out and the button label is the header text.
- **Pager:** the current page button carries `aria-current="page"` and is
  `disabled`; First / Prev / Next / Last are disabled at the ends and while
  `Loading`. The page-size `<select>` is inside a `<label>` whose text is
  `Grid.PageSize` ("Show" / "عرض").
- **Row selection labelling** (`RowSelectionLabel`, built from `RowLabel`) is
  configured but unreachable: with `Multiselect="false"` the checkbox column is not
  rendered.
- **Keyboard / focus:** `FocusOnNavigate RouteData Selector="h1"` in `Routes.razor`
  moves focus to the page heading on navigation. There are no modals on this page,
  so there is no focus trap to manage and no ESC-to-close behaviour.
- **Colour contrast and focus indicators:** Unverified - no contrast measurement or
  live focus-ring check was performed for this page. The status pills rely on
  `simf-pill--on` / `simf-pill--warn`, but the pill text ("Handled" / "Open")
  carries the meaning, so colour is not the sole signal.

## 10. Related use cases (UCS-001)

**N/A - no use case in `docs/SIMF-UCS-001-Use-Case-Specifications.md` covers this
page.** Searched the document for "contact"; the three matches are a sign-up
message carrying contact details (line 170), "user must contact another admin"
(line 437) and a Contact step in a registration flow (line 663). None describes
the contact-inquiry inbox.

## 11. Related E2E test scenarios

All scenarios live in
[`docs/tests/e2e/cp-contact-inquiries.md`](../../tests/e2e/cp-contact-inquiries.md);
anchors are the scenario ids. Status column mirrors that catalogue's coverage
matrix.

| Scenario | ID | Type | Catalogue status |
|----------|----|------|------------------|
| Page loads; inquiries listed open first / newest first, paged | E2E-CINQ-001 | happy P0 | authored (`ContactInquiryTests` list) |
| An anonymous `POST /app/contact-inquiry` appears in the inbox on next load | E2E-CINQ-002 | happy P0 | authored (`ContactInquiryTests` submit -> list) |
| Mark an open inquiry handled -> success toast, status flips, list reloads | E2E-CINQ-003 | happy P0 | authored (`ContactInquiryTests` mark-handled) |
| Reopen a handled inquiry -> success toast, status flips back | E2E-CINQ-004 | happy P1 | authored (`ContactInquiryTests` reopen) |
| Empty inbox -> "لا توجد رسائل بعد" empty state, no error banner | E2E-CINQ-005 | empty P0 | spec |
| Auth gate - no `ContactInquiries.View` -> 403 and nav item hidden; toggle needs `.Manage` | E2E-CINQ-006 | auth P0 | authored (`PermissionEnforcementTests` + `CpNavigationPermissionTests`) |
| Load wire failure (5xx, or the BFF route missing) -> error toast, empty grid, no crash | E2E-CINQ-007 | resilience P0 | authored, live-verified during the BFF fix |
| RTL render (Arabic) | E2E-CINQ-008 | i18n P1 | spec |
| Element inventory - every control present, named and gated, in LTR and RTL | E2E-CINQ-ELS-001 | element P1 | to author |
| Element health - no dead control, no broken asset, zero console errors, no horizontal overflow | E2E-CINQ-ELS-002 | element P1 | to author |

**One of those statuses does not hold against the test file.** E2E-CINQ-004 is
recorded in the catalogue, and mirrored above, as authored by a
`ContactInquiryTests` "reopen" case. No such case exists: the only toggle in
`tests/SIMF.Api.Tests/ContactInquiryTests.cs` posts `{ handled = true }` once and
never `handled: false`, so reopening is covered by no automated test at any layer.
The E2E-CINQ-001 attribution is thinner than it reads for the same reason - the
list endpoint is exercised, but neither the open-first / newest-first ordering nor
the paging is asserted. The rows above are quoted from the catalogue as it stands;
correcting them belongs in that file.

Integration coverage in `tests/SIMF.Api.Tests/ContactInquiryTests.cs`:
`Anonymous_can_submit_and_admin_sees_it_then_marks_handled`,
`Submit_with_a_blank_name_or_bad_email_returns_400`,
`Inbox_list_rejects_an_anonymous_caller`.

## 12. Related docs

- Admin Manual: [`docs/manuals/Admin-Manual.md`](../../manuals/Admin-Manual.md)
  §8.11 "Contact inquiries". See section 7 for two troubleshooting rows in it that
  no longer match the page.
- E2E catalogue:
  [`docs/tests/e2e/cp-contact-inquiries.md`](../../tests/e2e/cp-contact-inquiries.md)
  (indexed in [`docs/tests/e2e/README.md`](../../tests/e2e/README.md) line 158 as
  E2E-CINQ-001..008).
- Page index: [`docs/pages/PAGE-INDEX.md`](../PAGE-INDEX.md) line 124.
- Permissions: [`docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`](../../manuals/SIMF-Auth-Permissions-Dev-Guide.md)
  and [`docs/SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md);
  the codes themselves are in
  [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs)
  lines 681-684 and 1183-1184.
- Grid component: [`docs/manuals/SIMF-Grid-Lists-Dev-Guide.md`](../../manuals/SIMF-Grid-Lists-Dev-Guide.md);
  component catalogue [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md)
  lists `SimfDataGrid<TItem>` v1/v2, `SimfDataGridColumn<TItem>`, `SimfPill`,
  `SimfEmptyState`, `SimfAlert`.
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) - Unverified:
  searched it for "contact-inquir" and found no section; only the general
  `ApiResult<T>` envelope and error model apply.
- Source: the page
  ([`.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactInquiriesList.razor),
  [`.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ContactInquiriesList.razor.cs)),
  the API
  ([`ContactInquiryEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Support/ContactInquiryEndpoints.cs)),
  the service
  ([`ContactInquiryService.cs`](../../../src/Backend/SIMF.Infrastructure/Support/ContactInquiryService.cs)),
  the entity
  ([`ContactInquiry.cs`](../../../src/Backend/SIMF.Domain/Support/ContactInquiry.cs)),
  the contracts
  ([`ContactInquiry.cs`](../../../src/Shared/SIMF.Contracts/Support/ContactInquiry.cs)),
  the BFF forwarders
  ([`AccountEndpoints.Geography.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.Geography.cs))
  and the client
  ([`SimfAdminClient.Geography.cs`](../../../src/Shared/SIMF.ApiClient/SimfAdminClient.Geography.cs)).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| (undated here) | D-464, as cited by the E2E catalogue and `PAGE-INDEX.md` line 124 | Original - the `ContactInquiries` table, the public submit endpoint, the API inbox endpoints and this CP page. **Not matched to a row in `DECISIONS_LOG.md`:** the log's only `D-464` row (line 546, 2026-06-19) is the CP Site Settings page. Per the log's own "Reading an ID" preamble, read a missing row as "not recorded in this log", never as "did not happen". |
| 2026-07-07 | D-649 (`DECISIONS_LOG.md` line 389) | CP BFF wiring restored. The page, the client JS and the API endpoints all shipped, but `SimfAdminClient.ListContactInquiriesAsync` / `MarkContactInquiryHandledAsync` and the two `group.MapPost` forwarders in `AccountEndpoints` were never added, so `POST /account/api/admin/contact-inquiries/list` fell through to the GET-only Blazor fallback and returned 400 ("incorrect Content-type", `Allow: GET,HEAD`). The page rendered an error banner over an empty grid and the toggle was dead too. Fix added the two client methods and the two forwarders plus the private `SetContactInquiryHandledBody` record. No API, schema, enum, wire, permission or migration change. **Id collision:** a second `D-649` row at line 340 is an unrelated Flutter `scan_visitor` widget test; the log's preamble warns that ids are not unique. |
| 2026-08-19 | (none) | This page reference doc authored from source. No code change. |

---

_Last reviewed:_ 2026-08-19 by Claude, authored from source only. **No live browser
render, build or test run was performed for this doc**, so every statement here is
traceable to a file read and none is traceable to an observed page. If the page has
changed and this doc has not been re-reviewed in 60 days, it is out of date.
