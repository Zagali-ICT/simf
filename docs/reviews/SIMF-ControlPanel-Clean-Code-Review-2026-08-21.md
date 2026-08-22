# SIMF Control Panel Clean Code Review

Date: 2026-08-21

Scope: `src/ControlPanel/SIMF.ControlPanel`, `tests/SIMF.ControlPanel.Tests`, and the shared API-client upload/auth code used directly by the Control Panel.

## Verification

- `dotnet test tests/SIMF.ControlPanel.Tests/SIMF.ControlPanel.Tests.csproj --no-restore`
  - Passed: 662
  - Skipped: 1
  - Failed: 0
- `dart --version` and `dart run bin/simf_conventions.dart --check` were attempted from `tool/conventions`, but both hung with no output and were stopped. The repo has a convention checker, but it could not be completed in this environment.
- Existing untracked files were left untouched:
  - `docs/SIMF-HLD-004-MoD-HLD-External-v1.2.pdf`
  - `docs/SIMF-LLD-003-Solution-Design-Document-v1.3.pdf`

## Executive Summary

The CP project is not a low-quality or obviously AI-generated codebase. It has serious regression tests, permission ratchets, localization checks, markup hygiene checks, download-error guards, and destructive-action guards. The strongest clean-code concern is not lack of care; it is that the BFF endpoint layer has grown into a large repeated surface.

Main risks found:

1. Cookie permission/account-state claims are refreshed only at sign-in, while API tokens rotate later.
2. Upload content types are trusted and can throw before the API-client error handling begins.
3. Many upload routes buffer entire files into memory and repeat the same multipart boilerplate.
4. BFF route coverage is incomplete; one known class of missing-route bug needed a one-off test.
5. Endpoint code repeats access-token extraction around 404 times and repeats large `using` blocks across partials.
6. Some production comments read like incident/test-history prose. They are not "AI signs", but they are comment debt.

## Findings

### 1. Stale Cookie Claims After Token Refresh

Severity: High

Evidence:

- Initial sign-in copies permission and state claims from the access token into the cookie in `src/ControlPanel/SIMF.ControlPanel/Endpoints/AuthEndpoints.cs:157`, `:159`, `:168`, `:171`.
- Token refresh stores only `access_token`, `refresh_token`, and `expires_at` in `src/ControlPanel/SIMF.ControlPanel/SimfCookieRefreshHandler.cs:64` and `:133`.
- CP navigation and state routing read cookie claims in `src/ControlPanel/SIMF.ControlPanel/Components/Layout/CpShellLayout.razor.cs:70` and `:75`.

Impact:

If an admin's permissions or account state changes while their session is active, the CP shell may continue using old claims until sign-out or cookie expiry. A revoked permission can still leave menu/page gates visible; a newly granted permission may stay hidden; a user moved to `PendingApproval` or `Rejected` may not be routed by the shell guard. The API still appears to enforce actual authorization through the forwarded token, so this is primarily stale UI/session authorization state rather than a direct data bypass, assuming the API is correct.

Fix:

After a successful refresh, parse the refreshed access token and replace role, permission, `account_state`, and `user_type` claims in the cookie principal, using one shared claim-sync helper rather than duplicating the extraction logic from `AuthEndpoints`. If refreshed `account_state` is not `Approved`, either reject the principal or force the next request to the proper state page. Add tests proving permission removal, permission addition, and account-state changes update the cookie.

### 2. Invalid Upload Content Types Can Become CP 500s

Severity: High/Medium

Evidence:

- CP endpoints pass browser-provided `file.ContentType` directly into shared clients, for example:
  - `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.SelfService.cs:119`
  - `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.UserDocuments.cs:95`, `:118`, `:143`, `:166`, `:191`
  - `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.MediaAndPartners.cs:135`, `:214`, `:266`
- Shared clients construct `new MediaTypeHeaderValue(contentType)` outside their transport try/catch:
  - `src/Shared/SIMF.ApiClient/SimfAccountClient.cs:86`, `:242`
  - `src/Shared/SIMF.ApiClient/SimfAdminClient.Users.cs:691`, `:728`
  - `src/Shared/SIMF.ApiClient/SimfAdminClient.MediaAndPartners.cs:126`, `:143`
  - `src/Shared/SIMF.ApiClient/SimfAdminClient.Catalogue.cs:87`, `:222`
  - `src/Shared/SIMF.ApiClient/SimfAdminClient.Programme.cs:150`
  - `src/Shared/SIMF.ApiClient/SimfAdminClient.Organization.cs:66`

Impact:

An empty or malformed upload content type can throw `FormatException` before the API-client error path runs. That means a bad upload can surface as a CP server exception instead of a validation/API envelope. Because the value comes from multipart input, the CP should treat it as untrusted.

Fix:

Centralize multipart file creation in the API client. Normalize blank content types to route-specific defaults such as `application/octet-stream`, `image/jpeg`, or `video/mp4`, and use `MediaTypeHeaderValue.TryParse` before setting the header. Add API-client tests for empty, whitespace, and malformed content types.

### 3. Upload Routes Repeat Full In-Memory Buffering

Severity: Medium

Evidence:

Several BFF endpoints do:

- `ReadFormAsync`
- `new MemoryStream()`
- `file.CopyToAsync(stream)`
- `stream.ToArray()`

Examples:

- Generic grid import: `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs:185`, `:196`, `:198`
- User document/avatar/VIP-photo/import routes: `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.UserDocuments.cs:81`, `:93`, `:104`, `:116`, `:129`, `:141`, `:152`, `:164`, `:177`, `:189`, `:380`, `:392`, `:506`, `:518`, `:528`, `:540`
- Media/assets/presentations/images: `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.MediaAndPartners.cs:118`, `:133`, `:200`, `:212`, `:252`, `:264`

The video upload paths are better: they set per-route multipart limits and stream via `OpenReadStream` in `AccountEndpoints.Programme.cs:152` and `AccountEndpoints.Settings.cs:109`.

Impact:

Small files are fine, but the pattern does not encode the per-route size contract. A large upload can pressure memory before the API rejects it. It also creates many places where the content-type fix above must be repeated if not centralized.

Fix:

Introduce a CP upload helper that:

- Reads the form with explicit per-route `FormOptions.MultipartBodyLengthLimit`.
- Validates file presence and size before buffering.
- Streams to the shared API client where possible.
- Uses a single localized error-envelope builder for missing/invalid files.

### 4. BFF Route Coverage Is Not General Enough

Severity: Medium

Evidence:

- `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs:1` points to `AccountEndpointsTests.cs (todo)`.
- The code explicitly notes there is no catch-all proxy in `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.Programme.cs:208`.
- `tests/SIMF.ControlPanel.Tests/SpeakerMeetingRequestsReopenProxyTests.cs:102` exists because one page action posted to a missing CP proxy route.
- `tests/SIMF.ControlPanel.Tests/CpPageEndpointReachabilityTests.cs:71` skips unmapped calls when checking page/API permission reachability.

Impact:

A page can call a `/account/api/...` URL that has no CP route and still pass most component/API tests. This creates silent 404 regressions unless each new action gets its own route-existence test.

Fix:

Add a generalized BFF route parity test:

- Scan CP source for literal `/account/api/...` paths.
- Materialize `MapAccountEndpoints`.
- Match concrete page URLs against route templates.
- Check expected HTTP verbs where the JS helper identifies the verb.
- Keep a small explicit allow-list for dynamic URLs that cannot be statically resolved.

Add endpoint execution tests for: missing token, malformed multipart, missing file, invalid content type, and download failure status.

### 5. Endpoint Layer Violates DRY

Severity: Medium

Evidence:

- `GetTokenAsync("access_token")` appears 404 times in `src/ControlPanel/SIMF.ControlPanel/Endpoints`.
- Endpoint partials repeat large `using` blocks across many files.
- File upload and missing-file envelopes repeat with small differences.

Impact:

This is the main clean-code issue. The behavior is not automatically wrong, but the same security-sensitive patterns are hand-written hundreds of times. That increases the chance that one fix lands in 20 places and misses the 21st.

Fix:

Add a small BFF helper layer:

- `WithAccessToken(HttpContext, Func<string, Task<IResult>>)`
- `ForwardJson<T>(...)`
- `ForwardDownload(...)`
- `ReadRequiredFileAsync(...)`
- Route-specific map helpers for CRUD resources where the shape is repeated.

Keep abstractions thin. The goal is not cleverness; it is one tested path for token extraction, upload normalization, and failure envelopes.

### 6. Production Comments Carry Too Much Defect History

Severity: Low

Evidence:

Examples include long production comments in:

- `src/ControlPanel/SIMF.ControlPanel/Endpoints/AccountEndpoints.cs`
- `src/ControlPanel/SIMF.ControlPanel/Program.cs`
- `src/Shared/SIMF.ApiClient/ApiEnvelope.cs`

Impact:

The comments are mostly purposeful and often explain real production incidents. They are not explicit AI-generated signs. The clean-code problem is that production code sometimes reads like a test report or incident log. That makes the code feel heavier than needed and can hide the actual invariant.

Fix:

Keep production comments to the invariant and the why. Move long incident stories to tests or docs. For example, a production helper needs "download failures must carry a body because status-code re-execution rewrites bodiless 4xx/5xx"; the QA narrative can live in the regression test.

## Positive Notes

- CP xUnit suite is strong: 663 total tests, 662 passing.
- There are good ratchets for destructive actions, permission rendering, localization keys, mojibake, inline styles, status-code download behavior, and silent failures.
- Data Protection key-ring startup validation in `Program.cs` is a good production-safety guard.
- Download failure handling is intentionally centralized and tested.
- The codebase shows human defect memory, not generic AI filler. The issue is volume, not absence of intent.

## Suggested Fix Order

1. Fix auth claim refresh and add tests for permission/state changes during token rotation.
2. Centralize multipart content-type handling in shared API clients and test bad content types.
3. Add upload-size/form helpers in CP endpoints, then migrate memory-buffered upload routes.
4. Add generalized CP BFF route parity tests.
5. Refactor repeated endpoint token/forwarding code.
6. Clean production comments after behavior is protected by tests.

## Clean Code Assessment

- Bugs: two important bug classes found: stale refreshed claims and unsafe upload content type handling.
- DRY/no duplication: endpoint layer needs work; repeated token/upload/proxy code is the largest debt.
- No AI sign: no direct ChatGPT/Copilot/generated-code marker found. Some comments are over-narrated and should be trimmed, but they are tied to real defects and tests.
- Test health: CP tests pass. Convention checker could not be run in this environment.
