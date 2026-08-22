# SIMF Flutter App Clean-Code Review - 2026-08-21

Scope reviewed:

- `src/Mobile/simf_app/lib`
- `src/Mobile/simf_app/packages/simf_auth_pkg`
- `src/Mobile/simf_app/packages/simf_data_pkg`
- Flutter quality gates and the SIMF convention checker

Review lens: bugs, clean code, DRY/no duplication, no AI-generation signs,
security-sensitive mobile behavior, and whether the gates actually protect the
same code that ships.

## Findings

### 1. High - A default build can call production with request/response body logging enabled

Evidence:

- `src/Mobile/simf_app/lib/core/env/build_config.dart:15-17` defaults
  `SIMF_BUILD` to `dev`.
- `src/Mobile/simf_app/lib/core/env/build_config.dart:32-35` defaults the API
  base URL to the production mobile edge, `https://edge.simrsnf.com/api/v1`.
- `src/Mobile/simf_app/lib/core/env/build_config.dart:88` enables request
  logging whenever `build != 'prod'`.
- `src/Mobile/simf_app/packages/simf_data_pkg/lib/src/api/simf_api_client.dart:64-68`
  wires `PrettyDioLogger` with `enabled: config.enableRequestLogging` and
  `requestBody: true`. `PrettyDioLogger` also defaults `responseBody` to true.

Impact:

A plain build with no `--dart-define=SIMF_BUILD=prod` points at production but
is still treated as `dev` for logging. That can print sign-in credentials, OTP
payloads, profile data, badge/auth data, and response bodies into device logs.
This is the top release blocker from this pass.

Fix:

- Make logging opt-in with an explicit define such as
  `SIMF_ENABLE_REQUEST_LOGGING=true`, independent from `SIMF_BUILD`.
- Or make `SIMF_BUILD` default to `prod` because the default API URL is already
  production.
- Remove `PrettyDioLogger` from the shared client or configure it with
  `requestBody: false` and `responseBody: false`; keep only the local
  `LoggingInterceptor`, which intentionally logs method/path/status only.
- Add a test that the default `BuildConfig.dataConfig(...)` has
  `enableRequestLogging == false`.

### 2. High - Local package lockfiles are stale and break the full Flutter gates

Evidence:

- `src/Mobile/simf_app/packages/simf_auth_pkg/pubspec.yaml:13` and
  `src/Mobile/simf_app/packages/simf_data_pkg/pubspec.yaml:13` require
  `flutter_riverpod: ^3.4.2`.
- `src/Mobile/simf_app/packages/simf_auth_pkg/pubspec.lock:128` and
  `src/Mobile/simf_app/packages/simf_data_pkg/pubspec.lock:120` still pin
  `flutter_riverpod` to `2.6.1`.
- Nine auth package tests import `package:flutter_riverpod/misc.dart`, for
  example
  `src/Mobile/simf_app/packages/simf_auth_pkg/test/auth_controller_signin_test.dart:4`.
  That file exists in the app's Riverpod 3.4.2 resolution, but not in the stale
  package 2.6.1 resolution.

Observed failures:

- `flutter analyze` from `src/Mobile/simf_app` fails with 9
  `uri_does_not_exist` errors for `package:flutter_riverpod/misc.dart`.
- `flutter test --no-pub` in `packages/simf_auth_pkg` fails to compile 10 test
  loads for the same reason.
- `flutter pub get --enforce-lockfile` fails in both local packages because the
  lockfiles do not satisfy the current pubspec constraints.

Impact:

The app test suite can still pass, but the package-level auth tests and the full
workspace analyzer gate are not reproducible from the checked-in lock state.
That weakens confidence in the auth/session package, which is the most
security-sensitive Dart package here.

Fix:

- Run `flutter pub get` in both local package directories and commit the updated
  package lockfiles.
- Add CI steps for each local package:
  `flutter pub get --enforce-lockfile`, `flutter analyze --no-pub`, and
  `flutter test --no-pub`.
- Do not rely on the app-level `flutter pub get` to make package tests valid.

### 3. Medium - The SIMF convention checker does not scan local package source

Evidence:

- `tool/conventions/lib/src/config.dart:10` defines only
  `src/Mobile/simf_app/lib` as the Flutter Dart source root.
- `tool/conventions/lib/src/engine.dart:14-15` scans that root plus Razor roots;
  it does not scan `src/Mobile/simf_app/packages/*/lib`.
- `src/Mobile/simf_app/packages/simf_auth_pkg/lib/src/data/auth_api.dart:24-264`
  contains 21 raw `/app/...` endpoint literals, while the main app convention
  expects endpoint strings to live in `*_endpoints.dart` files.

Impact:

`dart run bin/simf_conventions.dart --check --strict` passes with zero
violations, but it is not checking the auth/data packages. The auth package can
therefore drift away from the app's endpoint-string and clean-code rules without
tripping the custom gate.

Fix:

- Add package `lib` roots to the convention checker scan targets.
- Add package-aware allowlists for endpoint files.
- Extract an `AuthEndpoints` file in `simf_auth_pkg` and move the 21 auth route
  literals there.

### 4. Medium - Image upload MIME mapping is duplicated and inconsistent

Evidence:

- `src/Mobile/simf_app/lib/features/account/data/profile_repository.dart:118-134`
  maps `.jpg/.jpeg/.png/.webp` and returns `null` for unknown extensions.
- `src/Mobile/simf_app/lib/features/staff/data/staff_repository.dart:6-20`
  duplicates the same mapping and also returns `null`.
- `src/Mobile/simf_app/lib/features/myarea/data/myarea_repository.dart:49-58`
  has a third mapper, but it defaults every unknown extension to `image/jpeg`.
- `src/Mobile/simf_app/packages/simf_data_pkg/lib/src/api/simf_api_client.dart:202-203`
  omits the multipart content type when the mapper returns `null`.

Impact:

The same image-upload problem has three behaviors: strict known-type mapping,
no content type, and forced JPEG. That is a DRY issue and a user-facing bug
risk: unsupported files can be sent to the server in different ways depending on
which screen uploaded them.

Fix:

- Introduce one shared helper, for example `ImageUploadMime.fromFilename(...)`,
  used by account, staff, and My Area.
- Make unsupported formats an explicit result, not `null` or default JPEG.
- Validate locally before upload and show a localized unsupported-file message.
- Keep the server's magic-byte/type/size checks as the final authority.

### 5. Low - Test output is noisy even when the app suite passes

Evidence:

- `flutter test --no-pub` passes 1554 tests, but prints repeated warnings that
  tag `golden` is not declared.
- There is no `src/Mobile/simf_app/dart_test.yaml`.
- 60 files under `src/Mobile/simf_app/test/golden` use
  `@Tags(<String>['golden'])`.
- `src/Mobile/simf_app/test/features/account/sign_up_form_screen_test.dart:166-168`
  taps the "Create account" button without first making it visible; two tests
  emit Flutter hit-test warnings because the button center is below the default
  800x600 test viewport.

Impact:

These are not failing today, but they dilute the signal of a green test run and
can become failures if `WidgetController.hitTestWarningShouldBeFatal` is enabled
or test logging becomes stricter.

Fix:

- Add `dart_test.yaml` declaring the `golden` tag.
- Update `_tapCreate` to `ensureVisible` before tapping, or pump the test at a
  viewport where the button is visible.

### 6. Low - Convention checker docs/baseline are stale after the split

Evidence:

- `dart run bin/simf_conventions.dart --check --strict` now passes with zero
  violations.
- `tool/conventions/README.md:35` and `tool/conventions/README.md:48-76` still
  say strict mode fails on `sign_up_visitor_screen.dart`.
- `tool/conventions/baseline.json:5` still contains the old
  `sign_up_visitor_screen.dart` fingerprint.

Impact:

The code is cleaner than the docs say. The stale baseline makes the handoff
look worse than it is and leaves future contributors guessing whether strict
mode is safe to enforce.

Fix:

- Regenerate or delete the empty baseline.
- Update the README to state that strict mode is now green.
- Move the pipeline to `--check --strict` if it is not already doing so.

## Clean-Code Assessment

Positive signals:

- Production Dart code is analyzer-clean:
  `flutter analyze --no-pub lib packages/simf_auth_pkg/lib packages/simf_data_pkg/lib`
  passed.
- App tests passed: `flutter test --no-pub` completed with 1554 passing tests.
- `simf_data_pkg` package tests passed: 15 passing tests.
- The SIMF convention checker passes in normal and strict modes with zero
  current violations.
- Most app features use dedicated `*_endpoints.dart` files and repository
  classes, so route strings are generally centralized in the main app.
- Session/auth code has meaningful coverage for single-flight refresh,
  cold-start restore, route gating, idle timeout, and keep-alive behavior.
- No direct AI-generation markers were found in shipped Flutter code. The only
  AI-related matches are legitimate product/domain names such as `ai_summary`
  and `generatedByAi`.

Main clean-code risks:

- Release logging configuration couples environment name and logging in a way
  that contradicts the production default URL.
- Package lockfiles and package convention coverage are out of sync with the
  rest of the app's quality program.
- Upload MIME logic is repeated instead of expressed once.
- The custom convention gate is currently stronger for `lib/` than for local
  packages.

## Verification Run

Environment:

- Flutter 3.44.0 stable
- Dart 3.12.0

Commands run:

- `flutter pub get` in `src/Mobile/simf_app`: passed.
- `flutter analyze` in `src/Mobile/simf_app`: failed with 9
  `flutter_riverpod/misc.dart` errors from auth package tests.
- `flutter analyze --no-pub lib packages/simf_auth_pkg/lib packages/simf_data_pkg/lib`:
  passed.
- `flutter test --no-pub` in `src/Mobile/simf_app`: passed, 1554 tests.
- `flutter test --no-pub` in `packages/simf_data_pkg`: passed, 15 tests.
- `flutter test --no-pub` in `packages/simf_auth_pkg`: failed to compile because
  the package lock resolves Riverpod 2.6.1.
- `flutter pub get --enforce-lockfile` in both local packages: failed because
  the lockfiles are stale relative to the pubspec constraints.
- `dart pub get` in `tool/conventions`: passed.
- `dart run bin/simf_conventions.dart --check`: passed with zero violations.
- `dart run bin/simf_conventions.dart --check --strict`: passed with zero
  violations.

## Recommended Fix Order

1. Fix the production logging default first.
2. Refresh and commit the local package lockfiles, then run package analyze/test
   in CI.
3. Extend the SIMF convention checker to scan local packages.
4. Extract auth endpoint constants and shared image MIME validation.
5. Clean test warnings and update the convention README/baseline.
