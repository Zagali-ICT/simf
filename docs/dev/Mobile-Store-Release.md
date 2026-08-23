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
- **The alias must match `androidKeyAlias`** in the variable group. The command
  above suggests `simf-upload`, but the key that was actually generated on
  2026-08-23 uses the alias **`upload`** - so that is the value to put in the
  variable group. Confirm yours rather than trusting either name:
  `cd src/Mobile/simf_app/android && ./gradlew signingReport` prints the alias,
  the store path and the certificate for the `release` variant without building
  the app.
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

**As built, 2026-08-23** (from `./gradlew signingReport`, which reads the real
config rather than the intent): the `release` variant resolves to the `release`
signing config, not the debug fallback; the keystore lives **outside the
repository**; the alias is `upload`; and the certificate is valid until
**7 January 2054**. Its SHA-256 fingerprint is
`FD:9C:94:15:00:85:03:70:D2:30:6E:B6:C0:49:2C:68:26:1F:73:5F:4F:41:F4:3F:E9:A8:F2:A2:55:CC:D3:E3` -
not a secret (it is public in every signed artefact and shown in Play Console),
and worth keeping here so a future upload signed with a different key is
recognisable as such immediately.

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
| `androidKeyAlias` | no | `upload` (as generated 2026-08-23; verify with `signingReport`) |
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

## 5. Review prerequisites: two resolved, two still open

Owner decisions taken 2026-08-23.

### 5.1 Privacy policy - RESOLVED

**https://mod.gov.sa/ar/pages/privacyPolicy.aspx** (verified reachable, HTTP 200).
Enter it in **both** consoles: Play Console -> App content -> Privacy policy, and
App Store Connect -> App Privacy -> Privacy Policy URL. It is mandatory in both
because the app collects national ID numbers, identity-document photographs and
face images.

**Still open, and a listing can be rejected for it:** the app has **no in-app
privacy link**. Google requires a policy link inside apps handling sensitive
data, not only on the listing, and a grep of `lib/` finds no privacy route,
string or URL anywhere. Adding one is a small UI change (a row in the More /
About screen opening this URL through `url_launcher`, which is already a
dependency) and it is NOT in this changeset.

### 5.2 Reviewer demo account - RESOLVED, with work owed

Account: **zagali.sust@gmail.com**.

**The password is deliberately NOT in this repository, and must never be.** It
belongs in exactly two places, both of which are private form fields, not files:
Play Console -> App content -> **App access** (provide credentials for the
sign-in-protected flows), and App Store Connect -> the version's **App Review
Information** -> Sign-in required. Keep the value in a password manager. Anyone
adding it to a doc, a README or a pipeline variable has widened the blast radius
of a store listing to the whole repository history.

**The OTP bypass is owed work, not a setting.** Visitor sign-in is an emailed OTP
(D-033) and a store reviewer cannot receive that mail, so this account needs a
path through the second factor. Whatever form it takes, it is a deliberate hole
in the authentication surface and must be built as one:

- **scoped to this single account**, matched on the stored user id - never on a
  domain, a prefix, a role, or a build flag that a wider set could satisfy;
- **auditable** - every use written to the operation log like any other sign-in,
  so the bypass is visible in the audit trail rather than silent;
- **removable** - one row or one constant to revoke, and revoked once the app is
  live and the store no longer needs it;
- covered by a test asserting **no other account** can take that path.

`tests/SIMF.Api.Tests/BusinessFlow13PermissionMatrixTests.cs` pins the anonymous
auth surface; whatever is built here must not widen that set without being argued
for in the same changeset.

### 5.3 In-app account deletion - OWNER WILL FIX

Apple **Guideline 5.1.1(v)** requires an app that supports account creation to
support account deletion in-app. This app has full in-app sign-up and **no delete
path at all** - no route, no screen, no endpoint. It is an outright rejection,
not a warning, and no pipeline work substitutes for it. Owner has taken this;
it is product work across the app, the API and a retention decision (what is
erased, what is retained for the audit trail, and for how long).

### 5.4 Listing screenshots - STILL OPEN

`MainActivity.kt` sets `FLAG_SECURE` app-wide, which blocks screenshots and screen
recording on Android. Capture the store screenshots from an **emulator**, or on
iOS, where the absence of a screenshot block is the accepted exception recorded
in `docs/dev/Mobile-iOS-Release-Build.md` section 3.

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
