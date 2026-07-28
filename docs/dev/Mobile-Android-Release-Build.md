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
        signingConfig = signingConfigs.getByName("debug")
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
  `kotlin { jvmTarget = JVM_17 }`, and the debug-key signing for `release` so
  `flutter run --release` / `build apk` works without a release keystore.
- The cleartext / camera / `<queries>` manifest entries the plugins need land in
  the generated folders during the local run step.

Build command used for the prod-pointed APK:

```bash
flutter build apk --release \
  --dart-define=SIMF_BUILD=prod \
  --dart-define=SIMF_API_BASE=https://simf_api.zagali-ict.com/api/v1
```
