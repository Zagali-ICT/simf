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

### 2a. Bundle id `com.simrsnf.simf`

Every `PRODUCT_BUNDLE_IDENTIFIER` in `Runner.xcodeproj` is `com.simrsnf.simf`
(the `RunnerTests` target carries `com.simrsnf.simf.RunnerTests`). `flutter
create` derived `com.simrsnf.simfApp` from the project name; that was corrected
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

Apple Silicon Mac mini joining the existing org-level pool `Default`. Do the
phases in order: **C before D**, for the reason in E.

### A. Prepare the Mac

1. Xcode 26.x needs a recent macOS (26.6 / 26.5 / 26.4.1 want Tahoe 26.2+; 26.3
   and earlier run on Sequoia 15.6+). Check before ordering.
2. Make one local **admin account** (say `buildagent`) to own the agent. The
   agent runs as a per-user LaunchAgent under whoever installs it, so that
   account's PATH and keychain **are** the build environment.
3. Energy settings: "Prevent automatic sleeping when the display is off".
   "Wake for network access" is not a substitute.
4. **Settle FileVault against auto-login now.** A LaunchAgent starts only after a
   user logs in, and Apple disables automatic login when FileVault is on. Either
   FileVault off with auto-login and an immediate screen lock, or FileVault on
   and a human logs in after every reboot. This machine holds signing
   certificates, so it is a real decision, not a formality.

### B. Xcode, command line tools, licence

Full Xcode, not just the Command Line Tools: `xcodebuild` ships only with Xcode.

1. Install Xcode 26.x. developer.apple.com/download/all pins a version; the App
   Store only ever gives you latest. **App Store Connect has required Xcode 26+
   and an iOS 26 SDK since 28 April 2026** - check before you rely on it, Apple
   moves this floor periodically.
2. `sudo sh -c 'xcode-select -s /Applications/Xcode.app/Contents/Developer && xcodebuild -runFirstLaunch'`
3. `sudo xcodebuild -license`, page through, type `agree`. The non-interactive
   `-license accept` form could not be confirmed on any current Apple page, so do
   not script it untested.
4. `xcodebuild -downloadPlatform iOS`
5. `xcode-select --print-path` must print the Xcode.app path, **not**
   `/Library/Developer/CommandLineTools`.

### C. Flutter and CocoaPods

1. Unzip the Apple Silicon bundle into `~/development`, then put
   `export PATH="$HOME/development/flutter/bin:$PATH"` in `~/.zprofile`.
2. **Not the system Ruby.** CocoaPods' own guide recommends rbenv, RVM or
   `brew install ruby`: the system Ruby forces `sudo gem install` and leaves
   root-owned gems the agent account cannot update.
3. `gem install cocoapods`. Still required on Flutter 3.44.
4. `flutter doctor`, then `flutter precache --ios`.
5. Rosetta is not known to be needed anywhere in this chain. Install it only if
   `flutter doctor` asks for it.

### D. Download and register the agent

1. **Organization settings, not Project settings**:
   `https://dev.azure.com/{org}/_settings/agentpools`. The project-level page
   does not offer New agent, which is the usual first wrong turn.
2. New Personal Access Token scoped **Agent Pools (read, manage)** and nothing
   else - use "Show all scopes" to find it. It is used **once**, at registration:
   revoke it after the agent shows Online, and mint a fresh one if you ever need
   `./config.sh remove`.
3. Pool `Default` then Agents then New agent then macOS then Download. **Check
   the filename says `osx-arm64`.** If it hands you the x64 build, take the arm64
   asset from the agent's GitHub releases instead.
4. Unpack with the `mkdir` and `tar` lines that dialog prints - they are not
   published elsewhere, so copy them rather than retyping. Two binding rules: no
   spaces anywhere in the path, and `xattr -c <tarball>` before extracting.
5. `./config.sh` in a normal Terminal, as the agent account, **never with sudo**.
   Prompts in order: the TEE licence (N unless you use TFVC); server URL
   `https://dev.azure.com/{org}` - the **org** root, not the project; auth type
   (Enter for PAT); the PAT; the pool - **type `Default` explicitly**, since the
   built-in suggestion is lowercase; the agent name; the work folder (Enter). It
   ends with "Settings Saved."
6. macOS never asks "run as a service?" - `config.sh` only writes `./svc.sh`.
   Install it **unprivileged**: `./svc.sh install`, `./svc.sh start`, then
   `./svc.sh status` (expect a pid, then 0). The plist lands at
   `~/Library/LaunchAgents/vsts.agent.{org}.{agent}.plist`, logs under
   `~/Library/Logs/`.

### E. Make the SERVICE see the tools - the PATH trap

This is the step that costs an afternoon when it is skipped.

launchd reads neither `~/.zprofile` nor `~/.zshrc`, so Homebrew's
`/opt/homebrew/bin`, rbenv shims and the Flutter export never reach the service.
`config.sh` sources `./env.sh`, which **snapshots your PATH into `.path`**, and
`runsvc.sh` re-exports it at start. **The agent's PATH is frozen at whatever your
shell held when you ran `config.sh`** - so a tool installed afterwards is
invisible to it, however well `which` works in your Terminal.

After installing or moving any tool, from a Terminal where `which flutter` and
`which pod` both resolve:

```bash
cd ~/myagent && ./env.sh && ./svc.sh stop && ./svc.sh start
```

PATH belongs in `.path`, never `.env`. That restart is also what republishes the
agent's capabilities. Diagnostic: if `./run.sh` succeeds and the service fails,
it is `.path` - `run.sh` inherits your real shell and the service does not.

### F. Keychain and signing

- The pipeline uses `keychain: temp`, which builds a throwaway keychain per run
  and imports with `security import ... -A`. That `-A` is what stops `codesign`
  blocking on a prompt nobody can answer on a headless machine. `keychain:
  default` would instead need the agent account's login password as a pipeline
  variable, which is a good reason to keep `temp`.
- The identity is **`Apple Distribution`**, not the older `iPhone Distribution`.
  Check before you rely on it.
- The `ExportOptions.plist` method is **`app-store-connect`**; the older
  `app-store` still works but is deprecated. `xcodebuild -help` lists the values
  the Xcode you actually installed accepts, under "Available keys for
  -exportOptionsPlist".
- **The provisioning-profile directory moved.** The Azure task writes only to
  `~/Library/MobileDevice/Provisioning Profiles`, while current Xcode reads
  `~/Library/Developer/Xcode/UserData/Provisioning Profiles`. The pipeline copies
  it into both and removes the copy afterwards. Without that the archive fails
  with "No profiles were found" on CI while the same profile works on a
  developer Mac - which reads as a machine fault rather than a path fault, and
  is where the time goes.

### G. Verify, and the first run to queue

In Agent pools then `Default` then Agents, the agent must read **Online**, and
its Capabilities tab must show `Agent.OS = Darwin`,
`Agent.OSArchitecture = ARM64`, `InteractiveSession = False`, and `xcode`. Note
that the agent registers the `xcode` capability by probing `xcode-select -p`, so
it can appear with only the Command Line Tools installed: necessary, not
sufficient.

**Queue a toolchain check before enabling the iOS stage.** It settles the PATH
question in a minute rather than inside a signing run:

```yaml
pool:
  name: Default
  demands:
  - Agent.OS -equals Darwin
steps:
- script: flutter --version && xcodebuild -version && pod --version
  displayName: Toolchain check
```

Then tick `buildIos` with `publishIos` off, and only then both.

**Cost:** self-hosted agents are free and unlimited; concurrency is what is
metered, and a private project gets one free self-hosted parallel job. One Mac
building one pipeline at a time costs nothing.

### H. Traps

| Trap | How it presents | Fix |
|---|---|---|
| Agent pools opened under Project settings | No "New agent" button where the docs say | Use the org-level `/_settings/agentpools` |
| PAT scoped Build or Full access | `config.sh` fails at the token step | Agent Pools (read, manage) only |
| x64 tarball on Apple Silicon | Reports `Agent.OSArchitecture X64`, or will not start | Take the `osx-arm64` asset |
| Waiting for a "run as service?" prompt | Config ends at "Settings Saved"; the agent dies with the Terminal | `./svc.sh install` then `./svc.sh start` |
| `sudo ./svc.sh install`, a Linux habit | "Must not run with sudo", exit 1 | Install unprivileged |
| Tool installed after the agent | `which flutter` works, the pipeline says "command not found" | `./env.sh`, then stop and start the service |
| Re-running `./svc.sh install` after editing `runsvc.sh` | Customisations vanish and the failure returns | Keep them in `.path` / `.env` |
| FileVault on, or default sleep | Agent offline after a reboot, or offline at odd hours | Settle A3 and A4 before racking it |
| Profile in the legacy directory only | "No profiles were found" on CI, fine on a dev Mac | The pipeline's copy step (F) |
| Unmatched demand | Stage sits at "waiting for an agent", no error | Confirm `Darwin` and `xcode` on Capabilities |

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
- **Archive signing is supplied at build time and has never been exercised** -
  but the mechanism is verified, and that was the part most likely to be wrong.
  The committed `Runner.xcodeproj` carries no `DEVELOPMENT_TEAM`, and a
  `CODE_SIGN_IDENTITY[sdk=iphoneos*] = "iPhone Developer"` appears three times -
  a *development* identity, which cannot sign a distribution archive. The
  pipeline writes `ios/Flutter/Signing.xcconfig` (team,
  `CODE_SIGN_STYLE = Manual`, `Apple Distribution`, the profile name) and
  `Release.xcconfig` pulls it in with an optional `#include?`, so a developer's
  local `flutter run` is unaffected and nothing team-specific is committed.

  **Why that actually overrides the committed identity**, since an xcconfig does
  not always win. Xcode resolves a build setting target-pbxproj, then
  target-xcconfig, then project-pbxproj, then project-xcconfig. Read out of
  `project.pbxproj`: all three `iPhone Developer` entries sit in **PROJECT**-level
  configuration blocks; the **Runner target's** own Release and Profile blocks
  carry no `CODE_SIGN_IDENTITY` and no `CODE_SIGN_STYLE` at all; and
  `Release.xcconfig` is the `baseConfigurationReference` of the Runner **target's**
  Release *and* Profile configurations. So `Signing.xcconfig` sits one level above
  the committed default and wins. Had that identity been a target-level setting
  instead, the xcconfig would have been silently ignored and the archive would
  have been signed for development.

  If the first archive still fails on signing, that file is where to look -
  and re-check this precedence claim against the project as it stands, because
  it depends on where those settings live, not on the file names.
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
