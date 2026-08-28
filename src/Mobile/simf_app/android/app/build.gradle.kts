import java.io.FileInputStream
import java.util.Properties

plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// A11-16 (NCA Secure App-Dev Standard) — release signing credentials live in
// android/key.properties (git-ignored; owner-provided), NOT in source. When the
// file is absent (local dev / CI without the keystore) the release build falls
// back to debug signing so `flutter run --release` still works.
val keystoreProperties = Properties()
val keystorePropertiesFile = rootProject.file("key.properties")
if (keystorePropertiesFile.exists()) {
    keystoreProperties.load(FileInputStream(keystorePropertiesFile))
}

android {
    namespace = "com.simrsnf.simf"
    // Pinned to 36 (> flutter.compileSdkVersion=33 on this toolchain) because
    // the plugins' androidx deps (core 1.18, activity 1.12, browser 1.9) require
    // compiling against API 36. android-36 is installed.
    compileSdk = 36
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        // Immutable once published: Google Play keys the listing on this string
        // and will not accept a later change, so it is set to the real identity
        // rather than the Flutter scaffold's com.example.* placeholder (which
        // Play rejects outright).
        applicationId = "com.simrsnf.simf"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        create("release") {
            if (keystorePropertiesFile.exists()) {
                keyAlias = keystoreProperties["keyAlias"] as String
                keyPassword = keystoreProperties["keyPassword"] as String
                storeFile = file(keystoreProperties["storeFile"] as String)
                storePassword = keystoreProperties["storePassword"] as String
            }
        }
    }

    buildTypes {
        release {
            // A11-16 — sign release with the real keystore when key.properties is
            // present (owner-provided, git-ignored); otherwise fall back to the
            // debug keys so local `flutter run --release` still works.
            signingConfig = if (keystorePropertiesFile.exists()) {
                signingConfigs.getByName("release")
            } else {
                signingConfigs.getByName("debug")
            }
            // Flutter 3.44+ runs R8 by default for release. Keep minify/shrink on
            // (obfuscation for the NCA handover) but apply our keep rules so R8
            // does not strip ML Kit's on-device face-detection classes — without
            // these the release face detector throws a NullPointerException while
            // debug works (D-437-follow-up). See proguard-rules.pro.
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}

dependencies {
    implementation("androidx.core:core:1.18.0")
}
