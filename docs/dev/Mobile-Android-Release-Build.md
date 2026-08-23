# Mobile — Android release-build config (rationale for the tracked `android/`)

**Status:** controlled note (D-438, 2026-06-16; premise updated for BUG-010,
2026-07-26).

`src/Mobile/simf_app/android/` used to be **git-ignored**, so the native Android
build files were not in source control and were recreated locally with
`flutter create`. That is fixed: the folder is now **tracked**, every item below
is committed, and `flutter create` must NOT be run in the app directory — it
overwrites the folder and destroys exactly these customisations. See
`src/Mobile/simf_app/README.md` and the ratchet test
`src/Mobile/simf_app/test/repo/platform_projects_tracked_test.dart`.

This file stays the source of truth for **why** each non-default Android change
exists. Read it before editing `android/`; it is no longer a re-apply checklist.

Companions: `docs/dev/Mobile-iOS-Release-Build.md` does the same job for `ios/`,
and `docs/dev/Mobile-Store-Release.md` is the operational runbook for
`azure-pipelines-mobile.yml` (upload keystore, Secure Files, first release).

---

## 1. Release build: keep ML Kit under R8 (face detection) — REQUIRED

**Symptom if missing:** the guided face-capture / liveness screen
(`identity_verification_screen.dart`, used by the My-Area avatar and the
sign-up "Face photo") opens and shows the camera preview, but **never advances**
on smile / head-turn in a **release** APK (it works in **debug**). On the Huawei
the only way "forward" was to back out and re-open per step.

**Root cause:** Flutter 3.44+ runs **R8 code-shrinking by default** for
`--release`. R8 renames/removes ML Kit's reflectively-instantiated on-device
vision classes, so the face detector's method-channel handler throws a
`NullPointerException` at runtime. Logcat shows
`E MethodChannel#google_mlkit_face_detector: ... NullPointerException`, and R8's
`usage.txt` listed ~2,947 stripped ML Kit classes (the Flutter plugin packages
`com.google_mlkit_commons` / `com.google_mlkit_face_detection`, the bundled face
model `com.google.android.gms.internal.mlkit_vision_face_bundled`, etc.).

**Fix:** keep the ML Kit packages. Two parts.

### 1a. `android/app/proguard-rules.pro` (create with this content)

```proguard
# Flutter ML Kit plugin packages (underscore — the method-channel handlers).
-keep class com.google_mlkit_commons.** { *; }
-keep class com.google_mlkit_face_detection.** { *; }

# Upstream ML Kit SDK + on-device vision / bundled face model.
-keep class com.google.mlkit.** { *; }
-keep interface com.google.mlkit.** { *; }
-keep class com.google.android.gms.internal.mlkit_** { *; }
-keep class com.google.android.gms.internal.mlkit_vision_face_bundled.** { *; }
-keep class com.google.android.gms.vision.** { *; }
-keep class com.google.android.odml.** { *; }

-dontwarn com.google_mlkit_commons.**
-dontwarn com.google_mlkit_face_detection.**
-dontwarn com.google.mlkit.**
-dontwarn com.google.android.gms.internal.mlkit_**
-dontwarn com.google.android.odml.**
```

### 1b. `android/app/build.gradle.kts` — wire the rules into the `release` build type

```kotlin
buildTypes {
    release {
        // Keep R8 minify/shrink on (obfuscation for the handover) but apply the
        // keep rules so R8 does not strip ML Kit's face-detection classes.
        isMinifyEnabled = true
        isShrinkResources = true
        proguardFiles(
            getDefaultProguardFile("proguard-android-optimize.txt"),
            "proguard-rules.pro",
        )
    }
}
```

The `signingConfig` line is deliberately NOT reproduced here. It used to be, as
`signingConfigs.getByName("debug")`, and it went stale the moment the conditional
landed - a reader checking whether a release build is debug-signed got the wrong
answer from the doc. `android/app/build.gradle.kts` lines 47-67 are the one copy:
it uses the real keystore when `key.properties` exists and falls back to the
debug key when it does not.
```

**Verify after a release build:** in
`build/app/outputs/mapping/release/seeds.txt` the ML Kit classes are kept
(`grep -c mlkit_vision_face_bundled seeds.txt` should be in the thousands), and
`usage.txt` should no longer list the `com.google_mlkit_*` plugin classes or the
bundled face model as removed. Then confirm on a device that the liveness steps
advance live in the **release** APK.

---

## 2. Other android customisations already in place

These are committed with the folder (BUG-010); keep them when editing it:

- `android/app/build.gradle.kts` — `compileSdk = 36` (the plugins' androidx deps
  require API 36 on this toolchain), `JavaVersion.VERSION_17` /
  `kotlin { jvmTarget = JVM_17 }`, and the CONDITIONAL release signing described
  above, which keeps `flutter run --release` working with no keystore present
  while a real `key.properties` produces a store-signable artefact.
- The camera / `<queries>` manifest entries the plugins need are **committed**
  in `android/app/src/main/AndroidManifest.xml`, not generated at run time
  (BUG-010), and are pinned by `test/repo/platform_projects_tracked_test.dart`.
  Cleartext traffic is permitted only in the **debug** manifest.

## 3. The release build command

Google Play requires an **App Bundle** (`.aab`) for a new app; an APK cannot be
uploaded. Build the store artefact with:

```bash
flutter build appbundle --release \
  --dart-define=SIMF_BUILD=prod \
  --dart-define=SIMF_APP_KEY=<production value, never committed> \
  --dart-define=SIMF_SUPPORT_PHONE=<...> \
  --dart-define=SIMF_SUPPORT_EMAIL=<...> \
  --dart-define=SIMF_SOCIAL_X=<...>          # + INSTAGRAM / LINKEDIN / YOUTUBE / TIKTOK
```

That flag form is for a build **by hand**. `azure-pipelines-mobile.yml` passes the
same values through `--dart-define-from-file` instead, because `SIMF_APP_KEY` is
secret and `--dart-define=KEY=value` puts it in the child process's argv where a
local process listing can read it. Same values, same result; if you add a define,
add it in both places.

`SIMF_API_BASE` is deliberately **not** passed. Its default is already the
production mobile edge (`https://edge.simrsnf.com/api/v1`, `build_config.dart`),
so a build with no overrides runs against production and cannot fall back to a
dev host. Passing it by hand is how a stale host gets shipped — which is what the
previous version of this section did: it named `https://api.simrsnf.com/api/v1`,
the host the edge superseded, and it built an APK.

`SIMF_BUILD=prod` turns off one interceptor that logs HTTP **method, path and
status** — never a header, body or query string. It is the only consumer of that
flag. Pass it for cleanliness, not because a leak exists.

Empty support/social defines leave those tiles inert by design (D-369): supply
real values or those buttons do nothing in the shipped app.

**Signing.** The release build signs from the git-ignored `android/key.properties`
and falls back to the **debug** key when that file is absent. A debug-signed
bundle is rejected by Play at upload, so prove the signer before submitting:

```bash
keytool -printcert -jarfile build/app/outputs/bundle/release/app-release.aab
```

The Owner line must be your release identity, not `CN=Android Debug`.

**Keep `build/app/outputs/mapping/release/mapping.txt`** for each release and
upload it to Play, or every R8-obfuscated crash report is unreadable.
