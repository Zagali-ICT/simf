# SIMF Mobile App (`simf_app`)

The SIMF Maritime Forum Flutter application for Android and iOS.

See `docs/SIMF-MAA-001-Mobile-Application-Architecture.md` for the
architecture in full, and `docs/SIMF-MOB-API-001-Mobile-API-Requirements.md`
for the backend endpoint catalogue.

## What's in this repo

```
src/Mobile/
  simf_app/                  This main app — Android + iOS shell
    android/                 The Android project — TRACKED, hand-edited
    web/                     The web shell (SIMF title/icons) — TRACKED
    packages/
      simf_data_pkg/         The single HTTP layer (dio + interceptors + storage + ApiResult)
      simf_auth_pkg/         The auth feature — depends on simf_data_pkg only
    third_party/
      video_player_android/  Vendored, patched upstream plugin (D-768)
```

## Bootstrap (clean clone)

```bash
# 1. Install Flutter (stable 3.44 / Dart 3.12). https://docs.flutter.dev/get-started/install
# 2. From src/Mobile/simf_app/:
flutter pub get
flutter run
```

That is the whole bootstrap — the native projects come with the clone.

### DO NOT run `flutter create` in this directory (BUG-010)

`android/` and `web/` are **tracked, hand-edited source**, not regenerable
scaffold. `flutter create` OVERWRITES them and silently destroys:

| What | Where | Why it matters |
|------|-------|----------------|
| `android.permission.CAMERA` + `<uses-feature …camera[.front] required="false"/>` | `android/app/src/main/AndroidManifest.xml` | `flutter_zxing` QR scanning (Huawei/HMS-safe, D-426) and the live-preview `camera` liveness flow (D-404). Without it the liveness screen degrades to its gallery fallback. |
| `android.permission.USE_BIOMETRIC` | same manifest | Face-ID / fingerprint device-key sign-in via `local_auth` (D-738). |
| `MainActivity : FlutterFragmentActivity` + `FLAG_SECURE` | `android/app/src/main/kotlin/com/apexium/simf/MainActivity.kt` | `local_auth`'s `BiometricPrompt` needs a `FragmentActivity` (a plain `FlutterActivity` throws `no_fragment_activity`); `FLAG_SECURE` blocks screenshots app-wide (NCA A11-6). |
| Launcher mipmaps + adaptive icon | `android/app/src/main/res/mipmap-*` | The white SIMF mark on navy `#01132D` (D-373/D-388), generated once by `dart run flutter_launcher_icons`. |
| Release signing + R8 keep rules | `android/app/build.gradle.kts`, `android/app/proguard-rules.pro` | Signs from the git-ignored `android/key.properties` (NCA A11-16) and keeps ML Kit's face-detection classes alive under R8. |
| SIMF title, description, icons, brand colours | `web/index.html`, `web/manifest.json` | The deployed `simf_app` web shell. |

`test/repo/platform_projects_tracked_test.dart` asserts every one of these is
still on disk, so re-ignoring or regenerating the folder fails the test suite.

Never committed (git-ignored, owner-provided): `android/key.properties`, any
`*.jks` / `*.keystore`, `android/local.properties`.

**App identity.** `applicationId` and `namespace` are both `com.apexium.simf`,
and `MainActivity.kt` lives under the matching `kotlin/com/apexium/simf/` path.

Changed from `dod.simf.visitor_app` on 2026-08-22, superseding D-867, after the
owner confirmed nothing had ever been uploaded to a Play Console listing.
`applicationId` is immutable once a listing exists, so that confirmation was the
whole precondition — **this cannot be changed again after the first upload.**

A changed `applicationId` means a new build installs *alongside* the old one
rather than upgrading it, so every device carrying a pre-rename build needs a
one-time `adb uninstall dod.simf.visitor_app`.

**TLS (D-872).** There is no `networkSecurityConfig` and no pinned certificate.
The app uses ordinary platform TLS validation against the system trust store;
`test/repo/platform_projects_tracked_test.dart` asserts the removed bypass files
and the manifest attribute stay gone. An earlier row in the table above claimed
`res/raw/simf_api_cert.pem` was present and must be preserved — it had been
deleted months earlier, and that row was removed on 2026-08-22.

**Launcher name (D-699, applied under D-867).** `android:label="@string/app_name"`
with `res/values/strings.xml` (`app_name` = `SIMF`) and `res/values-ar/strings.xml`
(`app_name` = `الملتقى البحري`), so Android shows the Arabic name on Arabic-locale
devices and the English one elsewhere — matching `AppL10n.appName`. This was lost
once while `android/` was git-ignored, so `test/repo/platform_projects_tracked_test.dart`
now fails the build if either file or the `@string/app_name` reference goes missing.

### iOS — not yet on disk

`SIMF-MAA-001` scopes Android **and** iOS, but no `ios/` Xcode project has ever
been created. It must be authored on a Mac and committed here (with
`NSCameraUsageDescription` + `NSFaceIDUsageDescription` in `ios/Runner/Info.plist`)
before the iOS build is deliverable.

## Environment configuration

The app reads its base URL, app key, and device type from
`SimfDataConfig`. The `lib/core/env/build_config.dart` file declares one
config per build flavour. Override via `--dart-define`:

```bash
flutter run \
  --dart-define=SIMF_BUILD=dev \
  --dart-define=SIMF_API_BASE=https://api.dev.simf.example/api/v1 \
  --dart-define=SIMF_APP_KEY=...
```

Never commit a production `X-App-Key` value.

## Tests

```bash
flutter test                                 # this app
(cd packages/simf_data_pkg && flutter test)  # the local HTTP package
(cd packages/simf_auth_pkg && flutter test)  # the local auth package
```

Or use a workspace runner (melos / dart workspaces) once one is configured.

## Phase 2 / Phase 3 notes

This skeleton is **Phase 1** (foundation): packages compile, the route table
declares all 41 screens (auth and home/profile point at the right widgets,
the rest at `ComingSoonScreen`).

- **Phase 2** replaces the auth-screen `ComingSoonScreen`s with real auth
  screens in `simf_auth_pkg/lib/src/presentation/`.
- **Phase 3** replaces the remaining `ComingSoonScreen`s with placeholder
  mockup screens under `simf_app/lib/mkp/` with the `mkp_` filename prefix.
  These get deleted wholesale when the designer's screen designs land
  (SIMF-MAA-001 v1.2 §12.4).
