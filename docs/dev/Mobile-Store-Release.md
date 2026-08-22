# Mobile - releasing to Google Play and the App Store

**Status:** controlled note (D-941, 2026-08-22).

The runbook for `azure-pipelines-mobile.yml`. Rationale for the native projects
lives in `docs/dev/Mobile-Android-Release-Build.md` and
`docs/dev/Mobile-iOS-Release-Build.md`; this file is the operational sequence.

**One app identity, both stores: `com.apexium.simf`** (D-940). Play locks it at
first upload and App Store Connect locks it at the first app record. After that
it cannot be changed on either side.

---

## 1. The pipeline in one paragraph

A separate pipeline from `azure-pipelines.yml`, with `trigger: none` - a store
release is started by hand from the Run dialog. Five tick boxes: build Android,
upload to Play (and on which track), build iOS, upload to TestFlight.

**Neither half has ever run.** Android is written against the existing
self-hosted Windows pool, but its first run needs three things that are not in
place yet: the Flutter SDK and the Android SDK provisioned on that agent (the
same open item `azure-pipelines.yml`'s MobileApp stage carries), the Secure File
and variable group in section 3, and the upload key from section 2. **iOS
additionally needs the Mac mini registered**, which is why `buildIos` defaults to
off - an unmatched agent demand queues for ever instead of failing.

---

## 2. Generate the Android upload key

Nothing has ever been uploaded to Play, so this key does not exist yet.
`android/app/build.gradle.kts` falls back to the **debug** key when
`android/key.properties` is missing, which produces a bundle that builds and
uploads cleanly right up to Play's rejection.

**Run this yourself.** It prompts for the password interactively, which is the
point - a `-storepass` argument lands in shell history, and the password must
never appear in an agent transcript or a chat window.

`keytool` is already on PATH on this machine (Microsoft JDK 17):

```bash
keytool -genkeypair -v \
  -keystore simf-upload-keystore.jks \
  -storetype PKCS12 \
  -keyalg RSA -keysize 4096 -validity 10000 \
  -alias simf-upload \
  -dname "CN=Apexium, OU=SIMF, O=Apexium, L=Riyadh, ST=Riyadh, C=SA"
```

Notes that matter:

- **`-validity 10000`** is about 27 years. Play requires an upload key valid well
  beyond the app's life; a short validity cannot be corrected later.
- **PKCS12 uses ONE password.** The store password and the key password are the
  same value, so `androidKeystorePassword` and `androidKeyPassword` in the
  variable group get the same string. (JKS allowed two, but modern `keytool`
  warns that JKS is a proprietary format.)
- **The alias is `simf-upload`** and must match `androidKeyAlias`.
- **Back the `.jks` up somewhere durable and private.** If it is lost after the
  first Play upload, you cannot ship an update to that listing again without
  Google's key-reset process.
- Do **not** put the file in the repository. `platform_projects_tracked_test.dart`
  fails the build if a `.jks` or a `key.properties` appears under `android/`.

Verify what you made:

```bash
keytool -list -v -keystore simf-upload-keystore.jks -alias simf-upload
```

The certificate must **not** say `CN=Android Debug`. The pipeline re-checks this
on every build, against the finished bundle, and fails the run if it does.

---

## 3. One-time Azure DevOps setup

**Secure Files** (Pipelines -> Library -> Secure files). Upload with these exact
names - the pipeline references them by name:

| Secure File | What it is | Needed for |
|---|---|---|
| `simf-upload-keystore.jks` | the key from section 2 | Android build |
| `simf-play-service-account.json` | Google Cloud service-account key | Play upload |
| `simf-appstore-api-key.p8` | App Store Connect API key (see note) | TestFlight upload |
| `simf-distribution.p12` | Apple distribution certificate | iOS build |
| `simf-appstore.mobileprovision` | App Store provisioning profile | iOS build |

Upload the `.p8` exactly as Apple issues it. The `AppStoreRelease` task does
**not** take a path to it - its `apitoken` input is the base64-encoded *content*
of the key - so the pipeline base64-encodes the downloaded file into a secret
variable at run time and passes that. Nothing about the upload changes for you;
this note exists because passing the path looks correct, queues fine, and then
fails authentication after the archive has already been built.

**Variable group** `simf-mobile-release` (Pipelines -> Library). Mark the four
marked SECRET as secret - that is what keeps them out of the logs:

| Variable | Secret | Notes |
|---|---|---|
| `androidKeystorePassword` | yes | the section 2 password |
| `androidKeyPassword` | yes | same value, PKCS12 |
| `androidKeyAlias` | no | `simf-upload` |
| `SIMF_APP_KEY` | yes | production app key |
| `SIMF_SUPPORT_PHONE` | no | shown in the app |
| `SIMF_SUPPORT_EMAIL` | no | shown in the app |
| `SIMF_SOCIAL_X` | no | shown in the app |
| `SIMF_SOCIAL_INSTAGRAM` | no | shown in the app |
| `SIMF_SOCIAL_LINKEDIN` | no | shown in the app |
| `SIMF_SOCIAL_YOUTUBE` | no | shown in the app |
| `SIMF_SOCIAL_TIKTOK` | no | shown in the app |
| `appleTeamId` | no | 10-character Apple team id |
| `appleCertificatePassword` | yes | the `.p12` export password |
| `appStoreKeyId` | no | from App Store Connect |
| `appStoreIssuerId` | no | from App Store Connect |

Every name above is spelled in full deliberately: the pipeline resolves them
verbatim, and a grouped row like ``SIMF_SOCIAL_X` / `_INSTAGRAM`` reads fine
but invites a variable actually named `_INSTAGRAM`, which resolves to nothing
and ships an app with a dead social link rather than failing the build.

`SIMF_API_BASE` is deliberately absent. Its default is already the production
mobile edge, so a build with no override cannot ship a dev host.

**Marketplace extensions**, installed once at organisation level:
`ms-vsclient.google-play` and `ms-vsclient.app-store`. Without them the publish
stages fail at queue time with an unrecognised-task error.

**Environments** `Google-Play` and `Apple-TestFlight` (Pipelines ->
Environments). They exist so a store upload can carry an approval check; add
approvers on `Google-Play` before ever selecting the `production` track.

---

## 4. The first release is manual on both stores

The pipeline cannot do the first one, and no CI product can:

- **Google Play** will not create the app entry, the store listing, the content
  rating or the Data Safety form over the API. All four are console work. From
  release two onward the pipeline is the whole upload.
- **Apple** needs the app record created in App Store Connect, plus the privacy
  questionnaire and the export-compliance answer.

**Export compliance is a legal declaration and is left for the owner to answer.**
`ITSAppUsesNonExemptEncryption` is deliberately **not** set in `Info.plist`, so
App Store Connect asks the question on each upload rather than an engineer
answering it by committing a value.

---

## 5. Three things that will stall a review, none of them code

1. **No privacy policy URL exists anywhere in the repository.** Both stores
   require one, and it is not optional here: the app collects national ID
   numbers, identity-document photographs and face images. RSNF / MoD legal owes
   this before either listing can be submitted.
2. **No reviewer demo account.** Play "App access" and Apple both require working
   credentials. Visitor sign-in is an **emailed OTP** (D-033), and a store
   reviewer cannot receive that mail - so a normal test account is not enough.
   This needs a deliberate answer (a pre-approved reviewer account with a fixed
   code, or a documented review bypass) and it stalls the review even after a
   successful upload.
3. **Listing screenshots cannot be captured on an Android device.**
   `MainActivity.kt` sets `FLAG_SECURE` app-wide, which blocks screenshots and
   screen recording. Capture them from an emulator or on iOS, where the
   screenshot block is an accepted exception (see the iOS note, section 3).

---

## 6. Releasing, once the above is done

1. Bump `version:` in `pubspec.yaml`. The build number must increase on every
   upload or both stores reject it.
2. Run the pipeline: tick `buildAndroid`, leave `publishAndroid` off, and check
   the artifact and the not-debug-signed step.
3. Re-run with `publishAndroid` on and `androidTrack: internal`. Promote through
   the console.
4. Keep the `mapping.txt` from each release artifact. R8 obfuscates the build, so
   without it the store's crash reports are unreadable. The iOS equivalent is
   `dSYMs.zip`.
5. **Leave the server's `AndroidMinVersion` EMPTY at first publish.** The
   version gate is fail-open and needs both a minimum version and a usable store
   URL; setting a minimum above the shipped build forces every install into a
   hard update block waiting for a build that does not exist (D-940).
