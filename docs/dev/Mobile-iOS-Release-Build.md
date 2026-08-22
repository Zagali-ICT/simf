# Mobile - iOS project and release build (rationale for the tracked `ios/`)

**Status:** controlled note (D-941, 2026-08-22).

Companion to `docs/dev/Mobile-Android-Release-Build.md`. That file explains why
each non-default **Android** change exists; this one does the same for **iOS**.
The pipeline that consumes both is `azure-pipelines-mobile.yml`, and the
operational runbook (keystore, Secure Files, first release) is
`docs/dev/Mobile-Store-Release.md`.

---

## 1. There was no iOS project until now

`src/Mobile/simf_app/` shipped with `android/`, `web/` and no `ios/` at all - no
`Info.plist`, no `.xcodeproj`, no `Podfile`. `.metadata` recorded only `root` and
`android`, which is the durable evidence: the platform had never been scaffolded,
so nothing had been lost. `SIMF-MAA-001` section 13 had called for it since the
architecture was written.

**How it was created, and why not the obvious way.** `flutter create` rewrites
`android/` as well, and `android/` is hand-edited: the ML Kit R8 keep-rules, the
`RECORD_AUDIO` removal, the capped `READ_EXTERNAL_STORAGE`, the localized
launcher label and the launcher mipmaps all live there, and BUG-010 exists
because they were lost once already. So the runner was generated in a **scratch
directory** and only `ios/` was copied in:

```bash
flutter create --platforms=ios --project-name simf_app --org com.apexium <scratch>
```

From here `ios/` is hand-edited exactly like `android/`. **Never run
`flutter create` inside `src/Mobile/simf_app`.** The ratchet test
`test/repo/platform_projects_tracked_test.dart` now guards the iOS half too.

---

## 2. What is NOT the flutter-create default

Six things. Most of them fail on a Mac rather than here, which is the worst place
to discover them.

### 2a. Bundle id `com.apexium.simf`

Every `PRODUCT_BUNDLE_IDENTIFIER` in `Runner.xcodeproj` is `com.apexium.simf`
(the `RunnerTests` target carries `com.apexium.simf.RunnerTests`). `flutter
create` derived `com.apexium.simfApp` from the project name; that was corrected
immediately.

This is the **same string as the Android `applicationId`** (D-940), deliberately.
Play locks `applicationId` at first upload and App Store Connect locks the bundle
id at the first app record, so a drift becomes permanent on whichever side ships
second. The ratchet test reads the id out of `build.gradle.kts` and asserts the
Xcode project agrees, so the two cannot separate silently.

### 2b. Deployment target 15.5, not 13.0

`google_mlkit_face_detection` (the guided face-capture / liveness flow) pulls the
GoogleMLKit pods, whose floor is higher than the Flutter template's 13.0. The
value appears in **three** places - `IPHONEOS_DEPLOYMENT_TARGET` in
`Runner.xcodeproj`, and `platform :ios` **plus** the `post_install` override in
`Podfile` - and a mismatch surfaces as a link failure during the archive. The
ratchet test asserts all three agree.

### 2c. `ios/Podfile` is authored, not generated

CocoaPods only runs on a Mac, so `flutter create` never writes this file; it
would otherwise be generated on the first Mac build, **without** the three lines
that matter:

- `use_frameworks! :linkage => :static` - GoogleMLKit ships static frameworks and
  does not link under the dynamic default.
- `use_modular_headers!` - the companion to static linkage, so Swift pods import
  the Objective-C ones through modules. It is absent from the current Flutter
  template, so it is a deliberate addition, not something to tidy back.
- the `IPHONEOS_DEPLOYMENT_TARGET` override in `post_install` - a pod that keeps
  its own older default fails the archive rather than warning.

Committing the Podfile is what stops the first Mac build from silently taking the
defaults and failing in a way that reads as an ML Kit bug. The ratchet asserts
the `:linkage => :static` line is still there, not merely that the file exists.

### 2d. Permission purpose strings, bilingual, in `Info.plist`

iOS **kills the process** the first time a permission is used with no purpose
string, and App Review rejects the build before it gets that far. Six keys:

| Key | Required by |
|---|---|
| `NSCameraUsageDescription` | `camera` (face capture) + `flutter_zxing` (badge QR) |
| `NSPhotoLibraryUsageDescription` | `image_picker` - profile picture, identity document |
| `NSFaceIDUsageDescription` | `local_auth` biometric sign-in |
| `NSMicrophoneUsageDescription` | the camera plugin links the symbol; see below |
| `NSCalendarsUsageDescription` | `add_2_calendar` session reminders |
| `NSCalendarsWriteOnlyAccessUsageDescription` | `add_2_calendar` on iOS 17 and later |

That list is exactly what the app asks for, checked against ground truth rather
than guessed: the Android manifest requests only `CAMERA`, `INTERNET`,
`READ_EXTERNAL_STORAGE`, `RECORD_AUDIO` (removed at merge) and `USE_BIOMETRIC`.
No location, no notifications, no Bluetooth, no contacts.

**The microphone entry is not a mistake.** The app never opens a microphone -
`platform_projects_tracked_test.dart` asserts no `CameraController` sets
`enableAudio: true`, which is the same invariant that justifies removing
`RECORD_AUDIO` on Android. But Apple's ITMS-90683 check scans **linked symbols**,
not behaviour, and the camera plugin links them. Omitting the key gets the upload
rejected; the string says plainly that the microphone is never opened.

### 2e. Launcher name `SIMF`, and the Arabic that is still missing

`CFBundleDisplayName` and `CFBundleName` are `SIMF`; `flutter create` wrote
`Simf App` and `simf_app`. The ratchet asserts the display name.

**The Arabic launcher name is NOT carried across, and this is the one place iOS
is behind Android.** D-699 gives Android a bilingual label through
`values-ar/strings.xml`, ratcheted. The iOS equivalent is
`ar.lproj/InfoPlist.strings` inside an Xcode **variant group**, which cannot be
added by hand-editing `project.pbxproj` safely. `CFBundleLocalizations` does
declare `ar` (2f), so the omission looks deliberate when read quickly - it is
not, it is deferred.

**On the Mac, do this in the same sitting as the first build:** add
`ios/Runner/ar.lproj/InfoPlist.strings` carrying `CFBundleDisplayName` and the
six purpose strings in Arabic, register the variant group, and extend the ratchet
to assert the file exists. Until then the home-screen name and every permission
prompt are the bilingual single-string form, Arabic first.

### 2f. `CFBundleLocalizations` declares `ar` and `en`

So the App Store listing reports both languages. The app itself is bilingual
through Flutter's own localization, not the iOS bundle's.

---

## 3. NCA A11-6 on iOS: an accepted, documented exception

`MainActivity.kt` sets `FLAG_SECURE` app-wide on Android, which blocks
screenshots and screen recording for the whole app.

**iOS has no equivalent.** There is no supported API that disables screenshots;
the approaches that exist are `UITextField` secure-entry overlay tricks that
Apple does not sanction and that break across iOS versions.

**The owner accepted this as a documented exception on 2026-08-22.** iOS ships
without the screenshot block. Recorded here rather than silently dropped, so that
an NCA reviewer comparing the two platforms finds the decision instead of a gap.
Note the same `FLAG_SECURE` is why Play store listing screenshots cannot be
captured on an Android device - see `simf-device-verification-traps`.

---

## 4. Registering the Mac mini as an Azure Pipelines agent

The iOS stage of `azure-pipelines-mobile.yml` demands `Agent.OS -equals Darwin`
in the `Default` pool. `Agent.OS` is a built-in capability, so no manual
capability setup is needed - but the demand matches nothing until a Mac joins the
pool, and an unmatched demand **queues for ever rather than failing**. That is
why `buildIos` defaults to `false`.

On the Mac, in order:

1. Xcode from the App Store, then `sudo xcodebuild -license accept` and
   `xcode-select --install`.
2. CocoaPods and the Flutter SDK on `PATH` **for the user the agent runs as** -
   a login-shell `PATH` is not the service `PATH`, which is the usual reason a
   working manual build fails under the agent.
3. Azure DevOps -> Project settings -> Agent pools -> `Default` -> New agent ->
   macOS. Download the tarball, then:
   ```bash
   mkdir ~/azagent && cd ~/azagent
   tar zxf ~/Downloads/vsts-agent-osx-*.tar.gz
   ./config.sh          # server URL, a PAT, pool `Default`, an agent name
   ./svc.sh install && ./svc.sh start
   ```
4. Confirm the agent shows **Online** in the pool and that its capabilities list
   `Agent.OS = Darwin`.
5. Only then tick `buildIos` on a run.

**Commit `ios/Podfile.lock` after the first successful `pod install`.** It is not
git-ignored and it is the only record of which GoogleMLKit build was actually
linked; without it two machines resolve different pod versions from the same
Podfile. Treat a later change to it as a reviewable diff, the way `pubspec.lock`
is already treated.

**Pin the build to one machine if the pool grows.** `azure-pipelines.yml`
documents the `SIMF_ROLE` user-capability trick. The Android jobs in the mobile
pipeline demand `Agent.OS -equals Windows_NT` for the same reason in reverse:
once the Mac joins `Default`, an undemanded Windows job can be scheduled onto it.

---

## 5. What in here is NOT verified

Stated plainly, because none of it can be checked from Windows:

- **Nothing under `ios/` has ever been compiled.** No `pod install`, no
  `xcodebuild`, no archive. The project is generated-plus-reviewed, not built.
- **The 15.5 floor is ML Kit's documented minimum, not an observed one.** The
  first `pod install` on the Mac is what confirms it; if CocoaPods asks for a
  higher floor, raise it in **all three** places - `platform :ios`, the
  `post_install` override, and `Runner.xcodeproj` - because the ratchet fails if
  only one moves.
- **Archive signing is supplied at build time and has never been exercised.**
  The committed `Runner.xcodeproj` carries no `DEVELOPMENT_TEAM`, and its
  `CODE_SIGN_IDENTITY` is the flutter-create default `iPhone Developer`, which is
  a *development* identity and cannot sign a distribution archive. The pipeline
  writes `ios/Flutter/Signing.xcconfig` (team, `CODE_SIGN_STYLE = Manual`,
  `Apple Distribution`, the profile name) and `Release.xcconfig` pulls it in with
  an optional `#include?`, so a developer's local `flutter run` is unaffected and
  nothing team-specific is committed. If the first archive fails on signing, that
  file is where to look.
- **The iOS stages of the pipeline have never run.** `InstallAppleCertificate`,
  `InstallAppleProvisioningProfile` and `AppStoreRelease` are written from their
  documented inputs.
- **The `AppStoreRelease` task is a Marketplace extension**, not a built-in. It
  must be installed at organisation level or the stage fails at queue time with
  an unrecognised-task error. If it proves unmaintained, the fallback is
  `xcrun altool --upload-app` with the same App Store Connect API key.

**One thing here IS verified, and was a real defect:** the app icons. The
committed `AppIcon.appiconset` initially held the stock Flutter logo, because
`flutter create` writes that and nothing had regenerated it. `dart run
flutter_launcher_icons` is pure Dart and needs no Mac, so it was run - and it
warned that the source carries an alpha channel, which the App Store rejects
outright (ITMS-90717). `remove_alpha_ios: true` is now set in `pubspec.yaml`, and
the committed 1024x1024 icon is verified RGB with no alpha.

Treat the first green iOS run as the point at which this section gets rewritten.
