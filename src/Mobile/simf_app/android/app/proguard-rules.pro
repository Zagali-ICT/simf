# SIMF app — R8 / ProGuard keep rules for the release build.
#
# Flutter 3.44+ enables R8 code-shrinking by default for `--release`. R8 renames
# and removes ML Kit's reflectively-instantiated on-device vision classes, which
# makes the face detector throw a NullPointerException at runtime in the release
# build only (it works in debug, where R8 does not run). The face-detection /
# liveness screen (identity_verification_screen.dart) then never advances.
#
# The crash (logcat MethodChannel#google_mlkit_face_detector) is the Flutter
# plugin handler (com.google_mlkit_face_detection.FaceDetector) failing because
# its helpers were stripped — R8's usage.txt showed the plugin packages
# (com.google_mlkit_commons.InputImageConverter, the plugin classes) and the
# bundled face model (com.google.android.gms.internal.mlkit_vision_face_bundled)
# were removed. Keep ALL ML Kit packages — both the upstream `com.google.mlkit`
# / `com.google.android.gms.*.mlkit_*` and the Flutter-plugin `com.google_mlkit_*`
# packages (note the underscores). The rest of the app is still
# minified/obfuscated. (D-437-follow-up, 2026-06-16.)

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
