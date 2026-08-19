# Confirm meeting - `/meeting/confirm`

| | |
|--|--|
| **Route** | `/meeting/confirm` (the credential arrives as `?token={secret}`, bound with `[SupplyParameterFromQuery(Name = "token")]`) |
| **Layout** | `MainLayout` (the `DefaultLayout` on `Routes.razor`; the page declares no `@layout`). The page then composes `SimfAuthLayout` + `SimfAuthCard Wide="true"` itself. |
| **Surface** | Website (`src/Website/SIMF.Web`) |
| **Audience** | Anonymous. In practice the recipient of a meeting action email - a speaker, or a member of a target delegation. |
| **Auth** | None. `AllowAnonymous()` on both endpoints; the opaque single-use token in the query is the only credential. Both endpoints carry `Options(b => b.RequireRateLimiting("auth"))`. |
| **Pattern** | Public token landing page. Not a CRUD list, not a BFF page. `@rendermode InteractiveServer` (prerendered). |
| **Status** | Real (D-717; kept deliberately by D-774) |
| **Implements use case(s)** | Unverified - `docs/SIMF-UCS-001-Use-Case-Specifications.md` has no UC id for this page. The nearest catalogued entry is `UC-16 Request a one-to-one meeting (Visitor, FR-804)`, which is the requester side, not this confirmation page. See §10. |
| **Backend endpoints** | `GET /api/v1/app/meeting-actions/{token}`, `POST /api/v1/app/meeting-actions/{token}` |
| **Source file** | [`MeetingConfirm.razor`](../../../src/Website/SIMF.Web/Components/Pages/MeetingConfirm.razor) + [`MeetingConfirm.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/MeetingConfirm.razor.cs) |
| **Tests** | [`docs/tests/e2e/web-meeting-confirm.md`](../../tests/e2e/web-meeting-confirm.md) (E2E-MAC-001..008); `tests/SIMF.Api.Tests/MeetingActionTokenTests.cs`; `tests/SIMF.Api.Tests/DelegationMeetingActionTokenTests.cs`; `tests/SIMF.Web.Tests/PublicSiteRoutesTests.cs` (`Anonymous_meeting_confirm_route_is_kept`); `tests/SIMF.Web.Tests/PageTitleReachesTheHeadTests.cs` (`The_meeting_confirm_page_declares_a_title`) |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

This is the landing page for the meeting action links that SIMF emails. When an
admin accepts a meeting request and binds it to a hall slot, the request moves to
`MeetingRequestStatus.AwaitingSpeaker` and the other party is emailed a link that
points here. Opening the link previews the pending decision - who wants to meet,
about what, when, and where - without changing anything; clicking the single
button applies it. The page exists because the person who has to attend the
meeting is not necessarily a SIMF account holder and may not have the app
installed, so the emailed link has to be self-sufficient. `MeetingActionTokenService`
mints the URL as `{PublicWebBaseUrl}/meeting/confirm?token={secret}`
(`MeetingActionTokenService.BuildUrl`, line 388), so this route is the only place
those emails can land.

D-774 removed the Website's login and account area, and this page was explicitly
kept. The decision row records why: it is anonymous and token-addressed, it
depends on none of the removed authentication plumbing, and "deleting it would
silently strand every emailed confirmation." Two of the kept `Auth.*` resx keys
(`Auth.Footer`, `Auth.LanguageSwitch`) survive only because this page uses them.

## 2. Audience + permissions

- **Who can reach it:** anyone. The route is anonymous and the page carries no
  `@attribute [RequirePermission(...)]`.
- **Who can act on it:** the holder of a valid, unused, unexpired token whose
  request is still `AwaitingSpeaker`. Possession of the token *is* the
  authorisation; there is no account, role or claim check anywhere in the path.
- **Authorisation gates:** N/A - no permission code exists for this page. Both
  FastEndpoints carry `AllowAnonymous()` and no `Policies(...)`:

  | Layer | Gate |
  |-------|------|
  | Page (`MeetingConfirm.razor`) | none - no `RequirePermission`, no `[Authorize]` |
  | `PreviewMeetingActionEndpoint` | `AllowAnonymous()` + `Options(b => b.RequireRateLimiting("auth"))` + `Tags("Public")` |
  | `ConfirmMeetingActionEndpoint` | `AllowAnonymous()` + `Options(b => b.RequireRateLimiting("auth"))` + `Tags("Public")` |

  The `AllowAnonymous` is deliberate and recorded. The endpoint file's own
  doc comment states: "`AllowAnonymous`: the speaker is not signed in, so the
  opaque single-use token in the path is the only credential (an owner-approved
  `AllowAnonymous` exception)." D-717 records it as a "NEW `AllowAnonymous`
  exception (owner-approved OI-I)".

- **Rate limit:** the `"auth"` policy is a fixed window partitioned by
  `httpContext.Connection.RemoteIpAddress` (`Program.cs` line 221), sized from
  `RateLimitOptions.PermitLimit` / `WindowSeconds`, whose defaults are **20
  requests per 60 seconds**.
- **What an unauthenticated user sees:** the page itself, in one of its four
  states (§4.1). There is no redirect and no 401/403 - an unusable or absent
  token renders the neutral "This link is no longer valid." card. The API returns
  HTTP 404 with `MEETING_ACTION_TOKEN_INVALID`; the page never surfaces that
  status to the visitor.

## 3. Screenshots

No screenshots of this page exist in the repository. The table below records the
intended file names and states so a later capture pass has somewhere to land; the
Captured column is honest about the current position.

| State | File | Captured |
|-------|------|----------|
| Preview - Approve token | `docs/screenshots/meeting-confirm-preview-approve.png` | Not captured |
| Preview - Reject token | `docs/screenshots/meeting-confirm-preview-reject.png` | Not captured |
| Outcome - confirmed | `docs/screenshots/meeting-confirm-done-approve.png` | Not captured |
| Outcome - declined | `docs/screenshots/meeting-confirm-done-reject.png` | Not captured |
| Neutral invalid link | `docs/screenshots/meeting-confirm-invalid.png` | Not captured |
| Loading | `docs/screenshots/meeting-confirm-loading.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/meeting-confirm-rtl.png` | Not captured |

The page has nonetheless been driven live. `docs/tests/e2e/README.md` records the
2026-08-14 full-route sweep: the Website pass covered 18 routes in EN and AR
(36 page drives) and included `/meeting/confirm` with no token, machine-checking
`scrollWidth == clientWidth`, zero broken images, zero sub-requests >= 400 and
zero console errors. No image was saved from that run.

## 4. UI affordances

### 4.1 Page header and the four states

The page renders `SimfAuthLayout` with a `SimfBrandPanel` in the `Brand` slot
(`Brand.LogoAlt` / `Brand.Subtitle` / `Brand.Caption`) and a
`SimfAuthCard Title="@L["Meeting.Confirm.Title"]" Wide="true"` in the body.
`SimfAuthCard` renders `SimfWordmark` plus the title as the page `<h1>`; `Wide`
adds `simf-card--wide`, described in the component as "the wider card (the public
Website)". The `TopControls` slot holds `SimfLanguageSwitch` and
`SimfThemeToggle`; the `Footer` slot holds `Auth.Footer`.

There is one `<PageTitle>@L["Meeting.Confirm.Title"]</PageTitle>`. It is load
bearing, and `tests/SIMF.Web.Tests/PageTitleReachesTheHeadTests.cs` pins it -
see §7.

The card body is a four-way branch on two nullable fields and one flag:

| Order | Condition | Renders |
|-------|-----------|---------|
| 1 | `_loading` | `<p class="simf-form__secondary">` with `Meeting.Confirm.Loading` |
| 2 | `_outcome is not null` | `SimfAlert Variant="success"` with `Meeting.Confirm.Done.Approve` or `Meeting.Confirm.Done.Reject`, chosen on `_outcome.Action` |
| 3 | `_preview is null` | `SimfAlert Variant="error"` with `Meeting.Confirm.Invalid`, then `<p class="simf-form__secondary">` with `Meeting.Confirm.InvalidHint` |
| 4 | otherwise | the intro line, the detail block, and the confirm button |

State 4 renders `Meeting.Confirm.Intro.Approve` or `Meeting.Confirm.Intro.Reject`
on `_preview.Action`, then a `<div class="simf-form__fields">` of four label/value
pairs, then a `<div class="simf-form__actions">` holding the single button.

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. There is no toolbar, no multiselect, no Add / Edit /
Details / Delete / Copy / Paste / Duplicate / Import / Export. The page's only
control is the one button below.

| Button | Wired callback | Calls | Notes |
|--------|----------------|-------|-------|
| Confirm (label is `Meeting.Confirm.Approve` or `Meeting.Confirm.Reject`, chosen on `_preview.Action`) | `ConfirmAsync` | `POST /api/v1/app/meeting-actions/{token}` | `SimfButton Type="button" Block="true" Loading="_submitting" LoadingLabel="@L["Meeting.Confirm.Submitting"]"`. Rendered only in state 4. No permission attribute - see §2. |

There is no Cancel or Back control. Deciding not to act is done by closing the
tab, which leaves the token unused and the request in `AwaitingSpeaker` until the
TTL expires.

### 4.3 Grid columns (CRUD pages only)

N/A - not a CRUD list page. There is no `SimfDataGrid` and no table. The preview
is a fixed four-item read-only block:

| Label key | Value | Rendered when |
|-----------|-------|---------------|
| `Meeting.Confirm.Requester` | `_preview.RequesterName` | always in state 4 |
| `Meeting.Confirm.Topic` | `_preview.Subject` | always in state 4 |
| `Meeting.Confirm.When` | `FormatSlot(_preview)` | always in state 4 |
| `Meeting.Confirm.Where` | `_preview.HallName` | only when `!string.IsNullOrWhiteSpace(_preview.HallName)` |

`MeetingActionPreview` also carries `SpeakerName` and `SpeakerNameArabic`. The
page renders neither. The service comment on the delegation branch says so
explicitly: "SpeakerName is unused by the public page".

`FormatSlot` returns `$"{EventTime.DateTimeText(start)}–{EventTime.Time(end)}"`
when both `SlotStart` and `SlotEnd` are non-null, and `Meeting.Confirm.TBD`
("To be scheduled" / "سيُحدَّد لاحقاً") otherwise. `EventTime.Local` is the
identity function: its comment records that "Stored values are already Saudi
wall-clock (owner decision 2026-07-31)", so the helper formats and does not
convert. The output is `dd-MM-yyyy hh:mm tt` for the start and `hh:mm tt` for the
end, in `CultureInfo.CurrentUICulture`.

### 4.4 Pager

N/A - the page shows one meeting request and has no list, no paging and no page
size control.

### 4.5 Form fields

N/A - the page hosts no input field of any kind. There is no `EditForm`, no text
box, no select and no checkbox. The only visitor-supplied value is the `token`
query parameter, and the only interactive controls are the confirm button, the
language switch and the theme toggle.

## 5. Data flow

This page is **not** a BFF page. The Website's `/account/api/*` proxy
(`Endpoints/AccountEndpoints.cs`) was deleted by D-774 along with the login and
account area, so there is no BFF hop and no `SimfAdminClient`. The page calls the
public API directly through the anonymous `SimfPublicClient`, which D-717 records
as a "BFF-less direct call".

```
Email link opened (GET /meeting/confirm?token={secret})
  → OnInitializedAsync (MeetingConfirm.razor.cs) - skipped when Token is blank
  → SimfPublicClient.GetMeetingActionAsync(token)
  → GET /api/v1/app/meeting-actions/{token}  (PreviewMeetingActionEndpoint)
  → IMeetingActionTokenService.PreviewAsync
  → SimfAppDbContext: MeetingActionTokens → SpeakerMeetingRequests → Speakers → Halls
     (or DelegationMeetingActionTokens → DelegationMeetingRequests → Countries → Halls)
  → OperationLog "MeetingActionToken.Viewed"
  → ApiResult<MeetingActionPreview> → _preview → the preview card

Confirm clicked
  → ConfirmAsync → SimfPublicClient.ConfirmMeetingActionAsync(token)
  → POST /api/v1/app/meeting-actions/{token}  (ConfirmMeetingActionEndpoint)
  → IMeetingActionTokenService.ApplyAsync
  → conditional UPDATE token (WHERE UsedAt IS NULL)
  → conditional UPDATE request (WHERE Status = AwaitingSpeaker) → Accepted | Rejected
  → OperationLog "MeetingActionToken.Applied"
  → INotificationDispatcher.TryDispatchAsync (Identity DB, best effort, SendEmail = true)
  → ApiResult<MeetingActionOutcome> → _outcome → the success alert
```

Every backend call the page makes:

| When | Method + path | Request body | Response shape |
|------|---------------|--------------|----------------|
| `OnInitializedAsync`, only when `!string.IsNullOrWhiteSpace(Token)` | `GET /api/v1/app/meeting-actions/{token}` | none | `ApiResult<MeetingActionPreview>` |
| Confirm button (`ConfirmAsync`) | `POST /api/v1/app/meeting-actions/{token}` | none - the action is baked into the token; `SimfPublicClient` uses its "No-body POST" overload | `ApiResult<MeetingActionOutcome>` |

`SimfPublicClient` composes both paths from `BasePath = "api/v1/app/"` plus
`$"meeting-actions/{Uri.EscapeDataString(token)}"`. The token is escaped on the
way out, so a token containing URL-reserved characters cannot break the path.

`MeetingActionPreview` is `(MeetingActionType Action, string SpeakerName, string
SpeakerNameArabic, string RequesterName, string Subject, DateTime? SlotStart,
DateTime? SlotEnd, string? HallName)`. `MeetingActionOutcome` is
`(MeetingActionType Action)`. `MeetingActionType` is `Approve = 0, Reject = 1`.

The contract's own comment explains the shape: "No requester email / no account
ids - the opaque token in the URL is the only credential, and the URL itself
carries no PII."

### 5.1 One page, two token kinds

`PreviewAsync` and `ApplyAsync` each try the **speaker** token first
(`ValidateAsync` against `MeetingActionTokens` / `SpeakerMeetingRequests`) and,
failing that, the **delegation** token (`ValidateDelegationAsync` against
`DelegationMeetingActionTokens` / `DelegationMeetingRequests`). The service
comment gives the reason a single endpoint can serve both: "a 256-bit secret
matches at most one row across the two tables". `StageDelegationConfirmToken`
builds its URL through the same `BuildUrl`, so both kinds of email land here.

The two flows differ in what the page ends up showing and doing:

| | Speaker token | Delegation token |
|--|--|--|
| Token table | `MeetingActionTokens` (two per request, one Approve, one Reject) | `DelegationMeetingActionTokens` (one confirm-only token per request) |
| `Action` in the preview | the token's own `Action` | always `MeetingActionType.Approve` - the shared shape has no Confirm case |
| `RequesterName` | `request.RequesterName` | the requesting delegation's `Country.Name` |
| `SpeakerName` | the speaker's name (unused by the page) | `string.Empty` |
| Applied status | `Accepted` (Approve) or `Rejected` (Reject), plus `SpeakerDecisionAt` | `Accepted`, plus `ConfirmedAt` |
| Requester notification | `MeetingRequestConfirmed` or `MeetingCancelled` | `MeetingRequestConfirmed` |

Because the delegation preview arrives with `Action = Approve`, the page renders
the Approve intro and the "Approve the meeting" label for it. A delegate never
sees a Reject button here; the decline path for a delegation is the authenticated
app screen (`POST /app/delegation-meeting-requests/{id}/decline`, D-771 item B8),
not this page.

**Doc drift to be aware of.** The E2E catalogue's scope note still says the token
"was **not** generalized to delegations", which matches D-760 (2026-07-22) but no
longer matches the code: `MeetingActionTokenService` carries the delegation
branch, and `PublicSiteRoutesTests` describes the route as "The emailed speaker /
delegate confirmation link". `docs/pages/PAGE-INDEX.md` line 190 already records
the generalisation. The catalogue note is stale, not the code.

## 6. Validation + error handling

- **Client-side guards.** `OnInitializedAsync` fires the GET only when
  `!string.IsNullOrWhiteSpace(Token)`, so opening `/meeting/confirm` with no
  token, or a blank one, makes zero API calls and falls straight into the neutral
  card. `ConfirmAsync` returns early when `_submitting || _preview is null ||
  string.IsNullOrWhiteSpace(Token)`, which is a re-entrancy guard on top of
  `SimfButton`'s own `disabled="@(Disabled || Loading)"`.
- **Server-side validation.** There is no FluentValidation validator on this
  path - there is no request body to validate. The whole check is
  `MeetingActionTokenService.ValidateAsync` / `ValidateDelegationAsync`: the
  hashed secret must match a row, `UsedAt` must be null, the expiry must be in
  the future, and the request must still be `AwaitingSpeaker`. Failing any of
  those returns null, and the endpoint throws the neutral error.
- **Error envelope.** One code for every failure:
  `ErrorCodes.MeetingActionTokenInvalid = "MEETING_ACTION_TOKEN_INVALID"`, HTTP
  404, message "This link is no longer valid." / "لم يعد هذا الرابط صالحاً."
  It is raised by a `file`-scoped `MeetingActionErrors.NeutralInvalid()` shared
  by both endpoints. The `ErrorCodes` doc comment states the intent: "Deliberately
  NEUTRAL - the same code for every reason so the response never leaks which one
  it was."
- **Toast strategy.** N/A - the page raises no toast. Outcomes are rendered
  in-card by `SimfAlert`: `Variant="success"` for a completed decision
  (`role="status" aria-live="polite"`) and `Variant="error"` for the neutral
  invalid state (`role="alert"`). The error variant is announced assertively by
  design, per the component's own comment.
- **Audit.** Every step writes `OperationLog` through `IAuditLog`:
  `AuditEvents.MeetingActionTokenMinted` (`"MeetingActionToken.Minted"`),
  `MeetingActionTokenViewed` (`"MeetingActionToken.Viewed"`) on preview, and
  `MeetingActionTokenApplied` (`"MeetingActionToken.Applied"`) on a successful
  apply. The actor id is `Guid.Empty` - there is no signed-in user to record.

## 7. Edge cases + known limitations

- **A GET never consumes the token.** Preview is a read-only path
  (`AsNoTracking`, no `SaveChanges`); only `ApplyAsync` writes. This defeats
  email-scanner and link-preview prefetch, which would otherwise burn a
  single-use link before the recipient ever clicked it. Covered by E2E-MAC-003
  and `Preview_is_GET_safe_and_does_not_consume_the_token`.
- **Double-submit, sibling races and retries all resolve to one decision.**
  `ApplyAsync` claims the token with `ExecuteUpdateAsync` filtered on
  `t.UsedAt == null`, then claims the decision filtered on
  `r.Status == MeetingRequestStatus.AwaitingSpeaker`. Either returning zero rows
  means someone else won, and the method returns null. The comment states the
  rule: "the DB is the single arbiter, not the read in `ValidateAsync`". A losing
  caller sees the neutral card, never a double notification or a
  non-deterministic status.
- **A race between preview and confirm degrades cleanly.** `ConfirmAsync` sets
  `_preview = null` when `_outcome` comes back null, with the comment "Raced /
  expired / already used between preview and confirm → fall through to the
  neutral 'no longer valid' state". Without that line the page would keep
  offering a button that can no longer do anything.
- **Approve and Reject are different tokens.** `MeetingActionType`'s comment:
  the token "is bound to one request AND one action, so a leaked Approve link can
  never Reject and vice versa." The page reads the action off the preview rather
  than offering a choice, which is why there is exactly one button.
- **An API outage is indistinguishable from a dead link.**
  `SimfPublicClient.ReadEnvelopeAsync` catches `HttpRequestException`,
  `TaskCanceledException`, `JsonException` and `NotSupportedException` and returns
  null, exactly as a 404 does. So an unreachable API, a timeout, or a reverse-proxy
  HTML error page all render "This link is no longer valid." The page has no
  distinct server-error state and the E2E coverage matrix has no server-500
  scenario. This is a deliberate simplification on the client (its comment: "the
  page renders its error state and the caller never has to catch"), but the
  consequence is that a recipient hitting an outage will be told their still-valid
  link is dead.
- **Prerendering costs one duplicate GET, and buys the page title.** The render
  mode is `InteractiveServer`, not `InteractiveServerNoPrerender`, so
  `OnInitializedAsync` runs twice and issues two preview reads. The razor comment
  explains the trade: `App.razor` renders `<HeadOutlet />` statically, so a
  `PageTitle` that only exists inside the circuit has no outlet to reach and the
  tab shows a bare URL. The two usual reasons not to prerender do not apply -
  the page injects no per-circuit session state, and the duplicate call is a GET
  that changes nothing. The alternative, an interactive `HeadOutlet`, "would have
  changed head rendering for the 17 static SSR pages to fix this one." Pinned by
  `PageTitleReachesTheHeadTests`, which the test file describes as a ratchet:
  "Nothing failed. It compiled, the page worked, every test passed, and no test
  asserted a title."
- **The route cannot be deleted by accident.**
  `PublicSiteRoutesTests.Anonymous_meeting_confirm_route_is_kept` asserts
  `/meeting/confirm` is still a declared route, with the comment "deleting it
  would strand every emailed confirmation."
- **Unconfigured links are caught upstream, not here.**
  `MeetingActionTokenService.BuildUrl` returns `string.Empty` when
  `MeetingLinksOptions.PublicWebBaseUrl` is blank. The options comment records
  that the approve / resend paths fail loudly with `MEETING_LINKS_NOT_CONFIGURED`
  rather than minting tokens nobody can redeem. In QA and Production the value is
  set via `SIMF_API_MeetingLinks__PublicWebBaseUrl` (`deploy/set-env-api.ps1`
  line 144, `https://web.simrsnf.com`). Note that the option's own XML comment
  still names the pre-split `SIMF_MeetingLinks__PublicWebBaseUrl`; the deploy
  script is the current authority, and the comment has not been updated since the
  per-app env prefixes landed.
- **Token lifetime.** `TokenTtlHours` defaults to **72**, and
  `StageTokensForRequest` floors it with `Math.Max(1, ...)`, so a misconfigured
  zero or negative value still yields a one-hour token rather than an
  already-expired one. An expired token is E2E-MAC-006.
- **`Meeting.Confirm.SlotSaudi` is defined but unused.** The key exists in both
  `Strings.resx` and `Strings.ar.resx` (`{0} – {1} (Saudi time)` /
  `{0} – {1} (بتوقيت السعودية)`) and is referenced nowhere in `src/`. `FormatSlot`
  composes the range from `EventTime` instead, so the rendered slot carries no
  "(Saudi time)" qualifier in either language.
- **The neutral hint enumerates without confirming.**
  `Meeting.Confirm.InvalidHint` reads "It may have expired, already been used, or
  the request may have been cancelled." It lists the possibilities without saying
  which applies, which is consistent with the neutral-404 design rather than a
  leak of it.
- **No "already decided" acknowledgement.** A recipient who confirms, then
  reopens the same link, sees the neutral invalid card rather than "you already
  approved this". That is the price of the neutral error and is deliberate;
  E2E-MAC-004 asserts it.

## 8. i18n + RTL

- Every string the page itself writes comes from `IStringLocalizer<Strings> L`
  against `src/Website/SIMF.Web/Resources/Strings.resx` and `Strings.ar.resx`.
  Both files carry all 17 `Meeting.Confirm.*` keys plus `Auth.Footer`,
  `Auth.LanguageSwitch`, `Brand.LogoAlt`, `Brand.Subtitle` and `Brand.Caption`.
- **One control is not localised.** The page renders `<SimfThemeToggle />` with
  no parameters, so the component's own English defaults stand: the visible label
  is "Light" / "Dark" (`LightLabel` / `DarkLabel`) and the accessible name is
  "Switch theme" (`AriaLabel`). The Website resx files carry no key for any of
  the three, so the theme toggle reads English on the Arabic render. The language
  switch beside it is localised, because the page passes it
  `Label="@L["Auth.LanguageSwitch"]"`.
- Language toggle: `SimfLanguageSwitch Label="@L["Auth.LanguageSwitch"]"` in the
  card's `TopControls`. With no `Href` it derives one, flipping between `en` and
  `ar` and navigating to `/culture?culture={next}&redirectUri={returnPath}`. The
  return path is the current relative URI, so the `?token=` query survives the
  switch.
- RTL: `App.razor` sets `lang` from `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName`
  and `dir` from `TextInfo.IsRightToLeft`, so Arabic renders `<html lang="ar" dir="rtl">`.
  `SimfAuthLayout`'s comment records that its own layout "flips automatically for
  RTL through CSS logical properties; the host sets dir / lang on the document."
- Dates and times follow the current culture: `EventTime.Time` and
  `EventTime.DateTimeText` both format with `CultureInfo.CurrentUICulture`.
- E2E-MAC-007 is the Arabic render scenario. The 2026-08-14 sweep drove this route
  in both languages and machine-checked `dir="rtl"` and zero untranslated resource
  keys on the Arabic pass.

## 9. Accessibility

- **Landmarks:** `MainLayout` wraps `@Body` in `<main id="main-content" tabindex="-1">`,
  its comment citing WCAG 1.3.1 / 2.4.1. `SimfAuthLayout` adds an `<aside>` for the
  brand panel and its own `<main>` for the card column.
- **Heading:** `SimfAuthCard` renders `Title` as the single `<h1 class="simf-card__title">`,
  here `Meeting.Confirm.Title`. `Routes.razor` sets `<FocusOnNavigate RouteData="routeData" Selector="h1" />`,
  so focus lands on that heading on navigation.
- **Live regions:** `SimfAlert Variant="success"` is `role="status" aria-live="polite"`;
  `Variant="error"` is `role="alert"`, announced assertively. So the outcome is
  announced without stealing focus, and the invalid-link message interrupts.
- **Button state:** `SimfButton` sets `disabled="@(Disabled || Loading)"` and
  `aria-busy` while loading, and swaps its label for a spinner
  `<span role="status" aria-label="@LoadingLabel">` - here the localised
  `Meeting.Confirm.Submitting`. So a screen-reader user is told the submit is in
  flight rather than hearing the label vanish.
- **Keyboard:** tab order is the language switch, the theme toggle, then the
  single confirm button. There is no modal, so no focus trap and no ESC handling
  is required.
- **Colour contrast and focus rings:** inherited from
  `_content/SIMF.Components/css/theme.tokens.css`, linked by `App.razor`. Not
  independently measured for this page in this review.
- **Not verified this session:** an axe or Lighthouse accessibility run against
  this route. The 2026-08-14 sweep checked overflow, console errors, broken
  images and failed sub-requests, which is not the same thing.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| N/A | - | `docs/SIMF-UCS-001-Use-Case-Specifications.md` catalogues no use case for this page. Its only reference to the route is in the D-774 note under §7 (Page-level catalogue (cross-reference)), which records that "The anonymous token-addressed `/meeting/confirm` page is NOT affected and is still live." |
| UC-16 | Request a one-to-one meeting (Visitor, FR-804) | The nearest catalogued entry, and the *requester* side of the same journey. It does not cover the confirming party. A UC for the confirmation step is unwritten - author it rather than stretching UC-16. |

## 11. Related E2E test scenarios

All in [`docs/tests/e2e/web-meeting-confirm.md`](../../tests/e2e/web-meeting-confirm.md).

| Scenario | ID | Coverage |
|----------|----|----------|
| Approve link previews, then confirms; request becomes `Accepted` and the requester is notified | E2E-MAC-001 | happy, P0. Authored, backed by `Approve_confirms_the_meeting_and_marks_the_token_used` (API). |
| Reject link confirms; request becomes `Rejected` | E2E-MAC-002 | happy, P0. Authored, backed by `Reject_declines_the_meeting` (API). |
| A GET does not consume the token; a second open still previews | E2E-MAC-003 | edge, P0. Authored, backed by `Preview_is_GET_safe_and_does_not_consume_the_token` (API). |
| A used token, and its sibling, both give the neutral card | E2E-MAC-004 | error, P0. Authored, backed by `A_used_token_and_its_sibling_are_neutral_404s` (API). |
| An unknown or malformed token gives the neutral card | E2E-MAC-005 | error, P1. Authored, backed by `An_unknown_token_is_a_neutral_404` (API). |
| An expired token (>72h) gives the neutral card | E2E-MAC-006 | error, P1. Authored, backed by `An_expired_token_is_a_neutral_404` (API). |
| RTL / Arabic render of the preview and confirm | E2E-MAC-007 | i18n, P1. Authored (browser). |
| No `?token=` at all: neutral state, zero API calls | E2E-MAC-008 | edge, P2. Authored (browser). |
| Element inventory in LTR and RTL against `tools/qa/predicted_inventory.py` | E2E-MAC-ELS-001 | element, P1. **Still `_to author_`.** |
| Element health: no dead control, no broken image, every same-origin link and asset < 400, zero console errors, no horizontal overflow | E2E-MAC-ELS-002 | element, P1. **Still `_to author_`.** |

Two gaps worth naming. There is no scenario for a **delegation** token, even
though the page serves one (§5.1) and `tests/SIMF.Api.Tests/DelegationMeetingActionTokenTests.cs`
exists at the API level. And there is no server-500 or API-outage scenario, which
matters because that case is visually identical to a dead link (§7).

## 12. Related docs

- Route index: [`docs/pages/PAGE-INDEX.md`](../PAGE-INDEX.md) line 190 (the
  `/meeting/confirm` row) and the D-774 "Removed 2026-07-27" section below it.
- E2E index: [`docs/tests/e2e/README.md`](../../tests/e2e/README.md) - the route
  row (E2E-MAC-001..008) and the 2026-08-14 full-route sweep write-up.
- Companion app screen for the delegation decline / in-app confirm:
  [`docs/tests/e2e/mobile-meeting-confirm.md`](../../tests/e2e/mobile-meeting-confirm.md).
- Component catalogue: [`SIMF-CMP-001`](../../SIMF-CMP-001-Component-Catalog.md).
  Components used here: `SimfAuthLayout`, `SimfBrandPanel`, `SimfAuthCard`,
  `SimfWordmark`, `SimfLanguageSwitch`, `SimfThemeToggle`, `SimfAlert`,
  `SimfButton`, `SimfIcon`.
- Use cases: [`SIMF-UCS-001`](../../SIMF-UCS-001-Use-Case-Specifications.md) - see §10.
- Decisions: `docs/decisions/DECISIONS_LOG.md` D-717 (the page and both
  endpoints), D-720 (E2E-MAC-007/008 authored), D-760 (the bi-meeting rework, which
  recorded the token flow as speaker-only), D-771 (the delegation QA batch),
  D-774 (Website auth removed, this page kept).
- Source: [`MeetingConfirm.razor`](../../../src/Website/SIMF.Web/Components/Pages/MeetingConfirm.razor),
  [`MeetingConfirm.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/MeetingConfirm.razor.cs),
  [`MeetingActionEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Programme/MeetingActionEndpoints.cs),
  [`MeetingActionTokenService.cs`](../../../src/Backend/SIMF.Infrastructure/MeetingRequests/MeetingActionTokenService.cs),
  [`MeetingActions.cs`](../../../src/Shared/SIMF.Contracts/Programme/MeetingActions.cs).
- Not linked, because it was not verified this session: an Admin or User Manual
  chapter for this page, and an API-specification section for
  `/app/meeting-actions/{token}`.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-07-09 | D-717 | Page created. Speaker double-opt-in landing page for the emailed Approve / Reject links, with `GET` preview and `POST` confirm on the new anonymous, rate-limited `/api/v1/app/meeting-actions/{token}` endpoints; neutral 404 `MEETING_ACTION_TOKEN_INVALID`; new `MeetingLinks:PublicWebBaseUrl` + `TokenTtlHours` options. The single-use consume was hardened to two conditional `ExecuteUpdateAsync` calls before commit, and the token pair staged into the same `SaveChanges` as the `AwaitingSpeaker` transition. |
| 2026-07-09 | D-720 | E2E-MAC-007 (RTL / Arabic) and E2E-MAC-008 (no `?token=`) authored in the catalogue. Docs only. |
| 2026-07-22 | D-760 | Bi-meeting rework. Recorded that the token flow was **not** generalised to delegations and that the delegation other party confirms from an app screen instead. Superseded for this page by the delegation branch now in `MeetingActionTokenService` - see the next row. |
| 2026-07-26 | D-771 (see note) | The page also redeems a **delegation** confirm token: `DelegationMeetingActionToken` + `StageDelegationConfirmToken` share this preview / apply path, so an emailed delegate with no app installed can confirm here. The D-771 row and `PAGE-INDEX.md` line 190 both attribute this to **D-767**, but the `D-767` row in `DECISIONS_LOG.md` is the hall seat-layout decision, so that id is a collision and the delegation id is **unverified**. |
| 2026-07-27 | D-774 | The Website's login and account area were deleted. This page was explicitly kept, and `Auth.Footer` / `Auth.LanguageSwitch` were the two `Auth.*` resx keys retained for it. Ratchet: `PublicSiteRoutesTests.Anonymous_meeting_confirm_route_is_kept`. |
| 2026-08-14 | Decision id not verified this session | Render mode changed from `InteractiveServerNoPrerender` to `InteractiveServer`, restoring the `<title>`. Found by the full-route sweep recorded in `docs/tests/e2e/README.md`; pinned by the new `tests/SIMF.Web.Tests/PageTitleReachesTheHeadTests.cs`. |

---

_Last reviewed:_ 2026-08-19 by Claude - first authoring of this page doc, from
source. Two items were deliberately left open rather than guessed: the use-case
id (§10) and the decision id for the delegation-token generalisation (§13). If
the page has changed and this doc has not been re-reviewed in 60 days, it is out
of date. Re-walk the page in a browser and update every section that drifted.
