# SIMF Backend API Clean-Code Review - 2026-08-22

Scope reviewed:

- `src/Backend/SIMF.Api`
- `src/Edge/SIMF.MobileEdge`
- `src/Backend/SIMF.Infrastructure`
- `src/Backend/SIMF.Application`
- `src/Backend/SIMF.Domain`
- Backend/API quality gates in `azure-pipelines.yml`
- Backend-focused tests under `tests/SIMF.Domain.Tests`, `tests/SIMF.Application.Tests`, `tests/SIMF.ApiClient.Tests`, and `tests/SIMF.Api.Tests`

Review lens: bugs, clean code, DRY/no duplication, no AI-generated-code signs, API/edge security posture, infrastructure/domain boundaries, and whether the gates protect the same code that ships.

## Verification

- `dotnet restore src/Backend/SIMF.Api/SIMF.Api.csproj`
  - Passed.
- `dotnet restore src/Edge/SIMF.MobileEdge/SIMF.MobileEdge.csproj`
  - Passed.
- `dotnet build src/Backend/SIMF.Api/SIMF.Api.csproj -c Release --no-restore`
  - Passed: 0 warnings, 0 errors.
- `dotnet build src/Edge/SIMF.MobileEdge/SIMF.MobileEdge.csproj -c Release --no-restore`
  - Passed: 0 warnings, 0 errors.
- `dotnet test tests/SIMF.Domain.Tests/SIMF.Domain.Tests.csproj -c Release --no-restore`
  - Passed: 30.
- `dotnet test tests/SIMF.Application.Tests/SIMF.Application.Tests.csproj -c Release --no-restore`
  - Passed: 130.
- `dotnet test tests/SIMF.ApiClient.Tests/SIMF.ApiClient.Tests.csproj -c Release --no-restore`
  - Passed: 52.
- `dotnet test tests/SIMF.Api.Tests/SIMF.Api.Tests.csproj -c Release --no-restore`
  - Passed: 2,656.
  - Duration: 14m 09s.
- `dotnet list src/Backend/SIMF.Api/SIMF.Api.csproj package --vulnerable --include-transitive`
  - No vulnerable packages reported.
- `dotnet list src/Edge/SIMF.MobileEdge/SIMF.MobileEdge.csproj package --vulnerable --include-transitive`
  - No vulnerable packages reported.

Existing untracked files were left untouched:

- `docs/SIMF-HLD-004-MoD-HLD-External-v1.2.pdf`
- `docs/SIMF-LLD-003-Solution-Design-Document-v1.3.pdf`
- `docs/reviews/SIMF-ControlPanel-Clean-Code-Review-2026-08-21.md`
- `docs/reviews/SIMF-Flutter-App-Clean-Code-Review-2026-08-21.md`

## Executive Summary

The backend is not a low-quality or obviously AI-generated codebase. It has strict compiler gates, nullable enabled, warnings as errors, NuGet audit enabled, strong JWT validation, explicit proxy trust, CORS allow-lists, output caching on public reads, boot-time secret validation, structured error envelopes, and a large integration suite that passed locally.

The strongest clean-code concern is architecture/control surface growth, not lack of engineering care. The backend carries real defect memory, but too much of that memory is embedded directly in production code. The biggest functional risks found are that CI normally does not run the behavior/security suites, the Domain project still depends on ASP.NET Identity, proxy configuration parsing is not fail-fast, and several upload routes buffer whole files before enforcing route-specific size/type rules.

Main risks found:

1. Normal CI green does not mean backend behavior, permissions, or API security tests ran.
2. `SIMF.Domain` is not domain-pure: `SimfUser` and `SimfRole` still inherit ASP.NET Identity types.
3. API and Edge proxy trust boot guards require a non-empty config value but silently ignore invalid proxy entries.
4. Several upload endpoints buffer full files before route-specific size/type validation.
5. Infrastructure/API composition roots and several domain services are too large.
6. Endpoint files pack many request/route/endpoint classes together, and shared authorization policy registration lives inside one specific endpoint file.
7. No generated-code AI marker was found, but production comments are often over-narrated. AI/product stub strings are intentional product behavior, not proof of AI-written code.

## Findings

### 1. CI Green Does Not Mean Backend Tests Ran

Severity: High

Evidence:

- The pipeline parameter `runTests` defaults to `false` in `azure-pipelines.yml:186-189`.
- The pipeline comments explicitly state that a normal green run means only compile/publish, not behavior, permissions, or security surface verification in `azure-pipelines.yml:145-159`.
- Fast suites are behind `condition: ... runTests == true` in `azure-pipelines.yml:603-628`.
- The full API integration suite is also behind `condition: ... runTests == true` in `azure-pipelines.yml:659-666`.
- The skipped API suite is the one that covers the admin/app API surface and anonymous endpoint allow-list, called out in `azure-pipelines.yml:152-154`.

Impact:

The local backend is green, but the default CI signal is weaker than it looks. A merge can compile and publish while skipping the exact tests that catch permission, anonymous endpoint, app/admin API behavior, and LocalDB-backed integration regressions. The pipeline is honest about this being an accepted owner risk, but the clean-code assessment cannot treat normal CI green as a full backend quality gate.

Fix:

Do not treat ordinary pipeline green as backend-clean evidence. Require release/merge evidence that includes the locally run backend suites, especially `SIMF.Api.Tests`, or a separate owner-approved validation run with `runTests=true`. Keep the existing pipeline comments and skipped-step visibility if the owner directive remains unchanged.

### 2. Domain Layer Still Depends On ASP.NET Identity

Severity: High/Medium

Evidence:

- `SIMF.Domain.csproj` references `Microsoft.Extensions.Identity.Stores` in `src/Backend/SIMF.Domain/SIMF.Domain.csproj:8`.
- `SimfUser` derives from `IdentityUser<Guid>` in `src/Backend/SIMF.Domain/IdentityAccess/SimfUser.cs:16`.
- `SimfRole` derives from `IdentityRole<Guid>` in `src/Backend/SIMF.Domain/IdentityAccess/SimfRole.cs:5`.
- `tests/SIMF.Api.Tests/DomainPurityTests.cs:1-29` documents that this is known architecture debt.
- The same test file currently asserts the known-bad state so the suite stays green in `tests/SIMF.Api.Tests/DomainPurityTests.cs:54-79`.
- It only prevents the leak from widening in `tests/SIMF.Api.Tests/DomainPurityTests.cs:82-103`.

Impact:

This violates clean architecture/domain purity. The domain model is coupled to an ASP.NET Identity persistence/framework shape, making future identity-store changes harder and keeping application/domain concepts tied to infrastructure concerns. The tests are honest and useful, but inverted tests are a warning sign: the codebase is carrying a known architectural defect as a green guard.

Fix:

Complete the planned domain-purity split:

- Move ASP.NET Identity base types and EF store concerns to Infrastructure.
- Model account/role domain data as POCOs or domain-owned records.
- Adapt repositories/mappers so Application depends on SIMF-owned abstractions only.
- Flip `DomainPurityTests` to assert that Identity references are absent.
- Update the architecture plan in the same change so docs and code agree.

### 3. Invalid Proxy Entries Can Pass Boot Guards

Severity: Medium

Evidence:

- API production boot requires `ReverseProxy:KnownProxies` to be non-empty in `src/Backend/SIMF.Api/Program.cs:510-516`.
- API later parses each proxy with `IPAddress.TryParse` and only adds valid IPs in `src/Backend/SIMF.Api/Program.cs:653-658`.
- Edge production boot requires `ReverseProxy:KnownProxies` to be non-empty in `src/Edge/SIMF.MobileEdge/Program.cs:119-127`.
- Edge later parses each proxy with `IPAddress.TryParse` and silently skips invalid values in `src/Edge/SIMF.MobileEdge/Program.cs:150-155`.

Impact:

A typo such as a hostname, malformed IP, or CIDR notation in `KnownProxies` can satisfy the boot guard because the array is non-empty, then be ignored by forwarded-header setup. The app starts, but `X-Forwarded-For` is not trusted. That collapses rate limiting and audit attribution to the proxy/edge address instead of the real client IP, which is exactly the failure mode the comments say the boot gate should prevent.

Fix:

Parse and validate proxy config before startup completes:

- Reject any invalid `KnownProxies` entry with a clear error.
- Require at least one parsed proxy or network outside Development/Testing.
- If CIDR ranges are expected, support `KnownNetworks` explicitly rather than accepting strings that `IPAddress.TryParse` will skip.
- Add boot/config tests for empty, invalid, valid IP, and optional network cases for both API and Edge.

### 4. Upload Routes Buffer Before Route-Specific Limits

Severity: Medium

Evidence:

- Generic file upload copies the full `IFormFile` into a `MemoryStream`, then calls `ToArray()` in `src/Backend/SIMF.Api/Endpoints/Files/FileEndpoints.cs:167-176`.
- The central stored-file service enforces type-specific limits after it already receives the byte array in `src/Backend/SIMF.Infrastructure/Files/StoredFileService.cs:52-72`.
- Generic media asset upload copies the full file before calling the service in `src/Backend/SIMF.Api/Endpoints/Assets/AssetEndpoints.cs:158-166`.
- Admin avatar upload copies the full file before `SetAvatarAsync` performs its 2 MB validation in `src/Backend/SIMF.Api/Endpoints/Admin/AdminAvatarEndpoints.cs:62-67`.
- The platform already has a streaming seam for large files in `src/Backend/SIMF.Application/Files/Abstractions/IFileService.cs:29-32` and `src/Backend/SIMF.Infrastructure/Files/StoredFileService.cs:147-188`.
- Session recording and organization hero video are better: they set per-request body/multipart ceilings and stream through `OpenReadStream` in `src/Backend/SIMF.Api/Endpoints/Admin/SessionRecordingEndpoints.cs:65-103` and `src/Backend/SIMF.Api/Endpoints/Admin/AdminOrganizationHeroVideoEndpoints.cs:75-107`.

Impact:

Small uploads are fine, and the server still has general request-size limits. The weakness is that route-specific limits and file-type limits are not always encoded before buffering. That creates unnecessary memory pressure and duplicated upload handling. It is also easy for one endpoint to forget a pre-copy size check while another endpoint has it.

Fix:

Create one backend upload helper or policy layer:

- Validate `file.Length` before buffering when the route's max size is known.
- Apply `FormOptions.MultipartBodyLengthLimit` consistently for upload routes.
- Reuse the streaming storage path for any route that can accept larger content.
- Keep magic-byte/type validation in the service as the final authority, but do cheap size rejection before allocation.
- Add tests for oversized uploads on avatar, asset, generic file, presentation, recording, and hero-video routes.

### 5. Composition Roots And Services Are Too Large

Severity: Medium

Evidence:

- `src/Backend/SIMF.Api/Program.cs` is 753 lines and owns logging, rate limiting, Swagger docs, CORS, JWT, boot guards, migrations, seeding, middleware, serialization, and health checks.
- `src/Backend/SIMF.Infrastructure/DependencyInjection.cs` is 821 lines; `AddInfrastructure` starts at `src/Backend/SIMF.Infrastructure/DependencyInjection.cs:84` and registers DB contexts, Identity, options, workers, AI, files, reporting, seeders, notifications, gates, and most module services.
- Several service classes are very large:
  - `src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs`: 2,242 lines, class begins at line 31.
  - `src/Backend/SIMF.Infrastructure/Identity/AdminAccountService.Bulk.cs`: 1,707 lines.
  - `src/Backend/SIMF.Infrastructure/Identity/AdminAccountService.cs`: 1,572 lines.
  - `src/Backend/SIMF.Infrastructure/Programme/AdminSessionService.cs`: 1,444 lines.
  - `src/Backend/SIMF.Infrastructure/AccessControl/GateOperatorService.cs`: 1,051 lines.
- `SeatReservationService` alone spans public reservation, random seating, open seating, movement, admin row blocking, layout management, active bookings, no-show release, staff lookups, walk-in holds, badge-seat resolution, validation, notification, and persistence flows, visible from method declarations such as `src/Backend/SIMF.Infrastructure/SeatReservations/SeatReservationService.cs:93`, `:206`, `:296`, `:350`, `:434`, `:683`, `:865`, `:922`, `:1053`, `:1114`, `:1157`, `:1283`, `:1303`, `:1435`, `:1473`, and `:1532`.

Impact:

This is the largest clean-code/DRY concern. The code is often well documented and tested, but too many unrelated responsibilities live in a small number of files. That makes reviews slower, increases merge conflict risk, and makes security-sensitive changes harder to reason about. Large service classes also tend to hide repeated query/validation/notification patterns.

Fix:

Refactor by stable ownership boundaries, not by arbitrary file length:

- Split API host setup into extension methods such as logging, rate limits, swagger, authentication, boot guards, and request pipeline.
- Split `AddInfrastructure` into module registration extensions: Identity, Files, AI, Programme, Gates, Meetings, Reporting, Notifications, Seeders.
- Split large services along workflow seams where tests already exist, for example reservation commands, seat-layout commands, no-show release, staff lookup, and seat-map read models.
- Keep one public facade where needed, but move query builders, validators, and notification helpers behind focused collaborators.

### 6. Endpoint Files And Authorization Policy Ownership Are Hard To Discover

Severity: Medium/Low

Evidence:

- Endpoint files commonly contain many classes:
  - `src/Backend/SIMF.Api/Endpoints/Sessions/SeatReservationEndpoints.cs`: 27 classes.
  - `src/Backend/SIMF.Api/Endpoints/BusinessMeetings/BusinessMeetingEndpoints.cs`: 24 classes.
  - `src/Backend/SIMF.Api/Endpoints/Admin/AiPromptAdminEndpoints.cs`: 16 classes.
  - `src/Backend/SIMF.Api/Endpoints/Admin/GateEndpoints.cs`: 16 classes.
  - `src/Backend/SIMF.Api/Endpoints/Admin/RatingConfigEndpoints.cs`: 16 classes.
- The global `AuthorizationPolicies` class is declared inside `src/Backend/SIMF.Api/Endpoints/Admin/ResetTwoFactorEndpoint.cs:47`, after the `ResetTwoFactorEndpoint` class at `src/Backend/SIMF.Api/Endpoints/Admin/ResetTwoFactorEndpoint.cs:21`.
- `Program.cs` imports that endpoint namespace and calls `.AddSimfAuthorization()` in `src/Backend/SIMF.Api/Program.cs:478-480`.

Impact:

This builds and the policies are registered correctly, but discoverability is weak. A global auth policy object living inside one admin endpoint file looks accidental. New reviewers must know to search inside an endpoint instead of `Authorization/`, and endpoint files with 10-27 classes make it easy for route/policy/review ownership to blur.

Fix:

- Move `AuthorizationPolicies` to `src/Backend/SIMF.Api/Authorization/AuthorizationPolicies.cs`.
- Keep endpoint files grouped only where the workflows are tightly coupled.
- For large endpoint files, split by command/query or by public/admin surface.
- Consider a thin base/config helper for repeated route tags, rate limits, and policy chains, especially where many endpoints repeat `PermissionCatalog.PolicyFor(...)` plus `RequireApprovedAccount`.

### 7. Comments Are Useful But Too Often Carry Incident History

Severity: Low

Evidence:

- `src/Backend/SIMF.Api/Program.cs:32-47`, `:145-172`, and `:581-642` contain long operational-history comments.
- `src/Edge/SIMF.MobileEdge/Program.cs:1-23` carries product/deployment rationale directly in source.
- `tests/SIMF.Api.Tests/DomainPurityTests.cs:1-29` is useful and honest, but reads like an architecture decision memo.
- Many source files start with `// Tests: ...`, which is helpful, but mixed with long incident history can make production code feel like a review log.

Impact:

These comments do not look like AI-generated filler. Most explain real invariants, incidents, or security decisions. The clean-code problem is volume. When source code carries too much history, the invariant becomes harder to see and future contributors may cargo-cult the comment style rather than protecting behavior with tests/docs.

Fix:

Keep production comments short and invariant-focused. Move long incident narratives to tests, decision logs, or runbooks. A good target is: source says what must remain true and why; tests prove the bug does not return; docs hold the operational story.

## Positive Notes

- Release builds for API and Edge pass with warnings-as-errors enabled.
- `Directory.Build.props` enables nullable, implicit usings, warnings-as-errors, and NuGet audit.
- JWT validation is strong: issuer, audience, lifetime, signing key, HS256 pinning, short clock skew, and security-stamp revocation are configured in `src/Backend/SIMF.Api/Authentication/JwtBearerSetup.cs:28-47`.
- Stream tokens use a separate audience and scheme in `src/Backend/SIMF.Api/Authentication/JwtBearerSetup.cs:59-88`.
- Permission policies are dynamically materialized and approval-gated by construction in `src/Backend/SIMF.Api/Authorization/PermissionAuthorization.cs:54-68`.
- Swagger is disabled by default and production Swagger requires Basic credentials when enabled in `src/Backend/SIMF.Api/Program.cs:519-530`.
- Production boot guards check JWT signing key, super-admin default password, super-admin TOTP seed, AI prompt-hash secret, PII encryption, and file-store encryption.
- Edge publishes only `/api/v1/app/{**catch-all}` in `src/Edge/SIMF.MobileEdge/appsettings.json:10-16`, keeping the admin surface out of the mobile edge route table.
- API and Edge both clear default forwarded-header trust lists before adding known proxies.
- The full API suite passed locally with 2,656 tests.
- No direct generated-code marker such as "Generated by ChatGPT", "as an AI", or "Copilot generated" was found in backend/edge source.

## AI-Sign Assessment

The backend contains intentional AI product features, providers, prompts, an offline Echo provider, and visible strings such as "generated by AI" or "offline AI stub provider". Those are product/domain terms, not evidence that the code itself was generated by AI.

Clean-code verdict on "AI sign":

- No direct AI-generated-code marker found in shipped backend/edge source.
- AI stub output is deliberately labeled and guarded. `AdminSessionSummaryService` stamps stub drafts and blocks publishing/review of placeholder stub text, including the hard error at `src/Backend/SIMF.Infrastructure/Programme/AdminSessionSummaryService.cs:551`.
- Comments are sometimes over-narrated. This is comment debt, not an AI marker.

## Suggested Fix Order

1. Treat the CI test-gate skip as an explicit release risk; require backend test evidence when merging or releasing.
2. Fix proxy config parsing so invalid `KnownProxies` entries fail at boot.
3. Pre-check upload sizes and centralize upload handling before buffering.
4. Move `AuthorizationPolicies` out of `ResetTwoFactorEndpoint.cs`.
5. Split `DependencyInjection`, `Program.cs`, and the largest services by module/workflow.
6. Complete the Domain/Identity split and flip `DomainPurityTests`.
7. Trim long incident-history comments after the relevant behavior is protected by tests/docs.

## Clean-Code Assessment

Positive signals:

- Backend builds and tests are green locally.
- The API has a serious integration test suite, including API surface, permission, auth, files, AI hardening, and deployment-template checks.
- Security-sensitive controls are usually fail-closed and documented.
- Secrets are not committed in appsettings; production values are expected through prefixed environment variables.
- Edge is intentionally small and allow-list based.

Main clean-code risks:

- Normal CI does not run the backend behavior/security suites unless explicitly requested.
- Domain purity is not complete.
- Upload handling has repeated memory-buffering patterns.
- Service/composition-root files are too large.
- Endpoint grouping and shared policy placement hurt discoverability.
- Comments should be pruned toward invariants rather than incident narration.

Overall answer: the backend is cared-for and test-backed, but it is not fully clean/DRY yet. It is clean enough to build and verify locally today; the next cleanup should focus on the structural hotspots above rather than cosmetic renaming.
