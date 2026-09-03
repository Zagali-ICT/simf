# App Store Connect reply, Guideline 2.1 Information Needed

Drafted 2026-09-04 for the SIMF iOS submission (`com.simrsnf.simf`, 1.0.1).
Apple's letter asked six questions and warned separately about bugs, demo
accounts and screenshots.

**Read the pre-flight list at the bottom BEFORE sending any of this.** Every
answer below is a claim a reviewer verifies by opening the app. A claim they
disprove in ninety seconds turns "Information Needed" into a credibility
problem, and the second rejection then cites our own instructions as evidence.

**Placeholders.** `<<CREDENTIALS>>` appears four times (two accounts, in both
Part 1 and Part 2) and `<<CONFIRM: AI provider>>` twice. The reply is not
sendable until every one is resolved. Passwords go into App Store Connect
directly and never into this file or any other file in the repo.

_Last updated 2026-09-04, after the app fixes below landed._

---

## Part 1: the long reply (paste into the ASC message thread)

> Thank you for the review. Answers to all six points follow, and the same
> information has been added to the App Review Information notes.
>
> **1. Screen recording**
>
> A screen recording captured on a physical iPhone running the current version
> of iOS is attached. It begins at app launch and covers, in order: the guest
> browse experience with no account, sign-in with the demo credentials below,
> the programme and session detail, seat reservation, the attendee badge and its
> QR code, the venue map and exhibition, submitting a question to a session, the
> contact exchange, new account registration including the identity step, and
> finally account deletion from inside the app.
>
> **Account deletion.** A signed-in user deletes their account entirely from
> within the app: Profile tab, then My Area, then the red "Delete my account"
> link at the bottom, then confirm. There is no support ticket, no email step
> and no cooling-off period. It is immediate and irreversible. It erases the
> profile's personal data (name in both languages, date and place of birth,
> gender, job title, honorific, mobile numbers, vehicle plate, organisation and
> the badge QR identifier), hard-deletes the stored identity document, destroys
> the uploaded identity-document image and face photograph, revokes every
> session and every enrolled device, and replaces the sign-in address with a
> tombstone so the account can never sign in again. The deletion link is
> available to accounts awaiting organiser approval as well as approved ones.
> Immutable audit records of the deletion request itself are retained, as
> required for a government-organised event.
>
> **User-generated content.** The app has no public feed, comment thread, chat,
> rating display or public question wall. There are three narrow places where
> one attendee sees content another attendee supplied, and all three are
> person-to-person or organiser-curated rather than broadcast:
>
> - a bilateral business-meeting request carries a short free-text subject line,
>   which only the invited counterpart reads when they accept or decline it;
> - the app can send a notification naming a fellow attendee whose declared
>   interests match the recipient's;
> - "Meet People" is an organiser-curated directory of participating speakers,
>   sponsors and exhibitor companies, not a user-created profile listing.
>
> Every account is identity-verified before it is approved, using a government
> identity document and a live face capture, and accounts are approved by the
> event organiser rather than self-activated. Attendees are therefore not
> anonymous to the organiser. Meeting requests are declined or ignored by the
> recipient in the app, and the organiser can disable any account. Questions
> submitted to a session are never shown to other attendees at all: they go to a
> staff moderator, who approves, hides or answers them.
>
> **2. Purpose and audience**
>
> SIMF is the official companion app for the Saudi International Maritime Forum,
> a public maritime industry event organised in Saudi Arabia by the Royal Saudi
> Naval Forces. Its audience is the general public attending the forum:
> visitors, industry delegates, exhibitors and speakers.
>
> The problem it solves is the one every large multi-day, multi-hall conference
> has. Attendees cannot find which session is running where, cannot reserve a
> seat, have to queue at the entrance while paper credentials are checked, and
> cannot exchange contact details without swapping business cards. The app
> carries the whole programme, lets an attendee reserve a seat, issues a
> scannable admission badge so entry is contactless, provides the venue and
> exhibition map, and lets attendees exchange contacts by scanning each other's
> code. It works fully in Arabic and English, right-to-left and left-to-right.
>
> The app also contains a small number of screens used by event staff on site
> (gate scanning, walk-in registration, a moderator desk for session questions).
> These are an operational overlay on a public consumer app, not the product.
> They are invisible to a visitor account, they are assigned by an administrator
> and cannot be self-selected, and the overwhelming majority of the app is the
> public attendee experience. The app is intended for the general public
> attending a public event, which is why it is submitted for public App Store
> distribution rather than a custom distribution channel.
>
> **3. Setup and access**
>
> Most of the app is browsable with no account at all: the programme, speakers,
> sponsors, exhibition, news and venue map are all open to a guest. On the
> sign-in screen, tap "Enter as guest" at the bottom, then "Continue as guest".
>
> A demo account is provided in App Review Information. It has two-factor
> authentication disabled, so sign-in completes with the email address and
> password alone and no emailed code is sent. It is already approved and its
> profile is already complete, so it lands directly on the home screen and is
> never routed into registration or identity capture.
>
> Demo account (Visitor): `<<CREDENTIALS>>`
> Demo account (Moderator): `<<CREDENTIALS>>`
>
> The Visitor account is the app's primary audience role. It can browse the
> programme, open session details, reserve a seat, submit a question, view its
> badge and QR code, exchange contacts and use the assistant. The Moderator
> account additionally shows the session question desk, which is the moderation
> control described in point 1. Gate scanning, walk-in registration and the
> seating desk belong to further staff roles and are not reachable from either
> account; we can supply credentials for those on request.
>
> No sample files are needed. The interface language is switched with the EN/AR
> control at the top of the first screen and of the sign-in screen.
>
> **4. External services**
>
> The app itself connects to:
>
> - **SIMF's own backend** at `edge.simrsnf.com`, operated by the organiser.
>   This serves all event content, accounts, bookings and badges.
> - **Google ML Kit (face detection)**, bundled in the app. It checks that a
>   face is present and framed while the camera is open, and that check runs
>   entirely on the device: no image is sent to Google. The SDK itself sends
>   usage and performance metrics to Google and may contact Google for model
>   updates. This is separate from identity verification below, where the
>   captured photograph IS uploaded to the organiser.
> - **YouTube**, on the live session screen only, through the embedded YouTube
>   player, which is used for the live broadcast and the sign-language feed.
>
> Behind our own backend:
>
> - **`<<CONFIRM: AI provider>>`** processes the in-app assistant. Only the
>   user's typed message, the conversation so far, the current screen context and
>   the language code are sent. No name, email address, identity document or
>   photograph is sent.
> - **Zoho SMTP** (`smtp.zoho.com`) delivers verification and one-time codes.
> - **Identity verification runs on the organiser's own servers**, not on a
>   third-party service. During registration the identity document and a
>   captured face photograph are uploaded to the SIMF backend and stored
>   encrypted so the organiser can verify the attendee and issue a badge. Both
>   are destroyed when the account is deleted.
>
> QR code scanning runs entirely on the device. The app contains no analytics
> SDK, no advertising SDK, no push-notification SDK, no payment processor, no
> crash-reporting service and no location services. It requests no location
> permission on either platform.
>
> **5. Regional differences**
>
> The app applies no country or geographic gating. There is no geo-IP lookup, no
> region-restricted content and no region-conditional feature, so it behaves the
> same for a reviewer in the United States as for a user in Saudi Arabia. The app
> never requests or accesses device location.
>
> Three behaviours are worth stating so nothing looks like a defect:
>
> - All session times display in Riyadh time (UTC+3) for every user regardless of
>   device time zone, because they are the published schedule of a physical event
>   in Saudi Arabia.
> - The app opens in Arabic with a right-to-left layout. An English toggle is on
>   the first screen and the sign-in screen.
> - The forum has not yet taken place. Live broadcast, on-site gate scanning and
>   arrival-based features therefore have no data to show and display an explicit
>   "not yet" notice rather than content. One feature is venue-based rather than
>   country-based: submitting a question during a session in progress can require
>   the attendee to have arrived at that hall. This is a venue check, not a
>   geographic restriction, and it does not apply outside a live session.
>
> **6. Authorisation**
>
> SIMF is organised by the Royal Saudi Naval Forces. A signed authorisation
> letter from the organiser, naming our company as the authorised developer and
> publisher of this app, is attached. We are happy to provide any further
> documentation the review team needs.

---

## Part 2: the App Review Information notes field

Shorter, because that field is length-capped. Confirm the current cap in the
console before pasting.

```
PURPOSE. Official companion app for the Saudi International Maritime Forum, a
public maritime event in Saudi Arabia organised by the Royal Saudi Naval Forces.
Audience: the general public attending the forum. Carries the programme, seat
booking, a scannable admission badge, venue and exhibition maps, and contact
exchange. Arabic and English, RTL and LTR.

ACCESS. Most of the app needs no account: tap "Continue as guest" on the first
screen. Demo accounts below have two-factor disabled, are pre-approved and have
a complete profile, so email + password signs straight in with no emailed code.
  Visitor:   <<CREDENTIALS>>
  Moderator: <<CREDENTIALS>>
Visitor is the primary role. Moderator additionally shows the session question
desk. Gate scanning and walk-in registration are further staff roles, available
on request.

ACCOUNT DELETION. In-app: Profile tab > My Area > "Delete my account" > confirm.
Immediate and irreversible. Erases profile data, identity document, face photo
and badge, revokes all sessions and devices, tombstones the sign-in address.
Available to pending accounts as well as approved ones.

USER CONTENT. No public feed, chat or comment wall. Session questions go only to
a staff moderator and are never shown to other attendees. Three narrow
person-to-person or organiser-curated surfaces: a meeting request's free-text
subject read by the invited counterpart, an interest-match notification, and an
organiser-curated Meet People directory. Every account is identity-verified and
organiser-approved before activation; the organiser can disable any account.

EXTERNAL SERVICES. SIMF's own backend (edge.simrsnf.com). Google ML Kit for
on-device face detection (image never leaves the device; the SDK reports usage
metrics to Google). YouTube for the live session and sign-language feed.
<<CONFIRM: AI provider>> for the in-app assistant (typed message and
conversation only; no name, email, document or photo). Zoho SMTP for one-time
codes. No analytics, ads, push SDK, payment processor or location services.

REGIONS. No country or geo gating; identical behaviour worldwide. Times display
in Riyadh time (UTC+3). Opens in Arabic; EN toggle on the first screen. The
forum has not happened yet, so live and on-site features show an explicit "not
yet" notice. Asking a question during a live session can require arrival at that
hall: a venue check, not a geographic one.

AUTHORISATION. Organised by the Royal Saudi Naval Forces. Signed authorisation
letter naming our company as authorised developer and publisher is attached.
```

---

## Part 3: the recording shot list

Apple asked for launch, the typical flow, registration, login and deletion.
Record on a physical iPhone on the current iOS major version. A recording made
on an older iOS can be bounced on that alone.

1. Cold launch from the home screen. Let the onboarding play.
2. "Enter as guest" on the sign-in screen, then "Continue as guest". Browse the
   programme, open a session, open speakers, open the venue map and the
   exhibition. This shows how much works with no account, which is the answer to
   the business-model question.
3. Leave guest mode. Sign in with the Visitor demo account. Show that no
   emailed code is requested.
4. Home, then My Area. Show the badge and its QR code.
5. Reserve a seat in a session. Show the booking in My Sessions.
6. Submit a question to a session.
7. Contact exchange: open Share my contact, then the scanner.
8. Switch the language to English and back, to show both directions.
9. Registration: sign out, create a NEW account, and go through the identity
   step. See the traps below first.
10. Account deletion: My Area, "Delete my account", confirm, and show the app
    returning to sign-in.

**Traps, in the order you will hit them.**

- **Recording the deletion destroys the account you hand Apple.** Use a
  throwaway account for step 10, or re-provision the demo account afterwards and
  re-verify it signs in before you send the reply.
- **Registration puts a real face and a real identity document on camera.** The
  liveness flow asks for a smile, then a turn right, then a turn left. Use a test
  identity you are willing to have in a recording Apple retains.
- **A self-registered account lands as pending, not approved.** If you register
  on camera, the account you create will show the pending state, not the full
  app. Either narrate that the organiser vets accounts, or approve it off camera
  between cuts. This is why the pre-approved demo account is what makes step 3
  work at all.
- **Do not enrol Face ID on the demo account.** iOS blanks a recording while the
  system passcode sheet is up after a failed biometric attempt.
- **The badge screen shows a lock icon and "pending approval"** unless the
  account is approved and a badge has been issued. Check this before recording.
- **The live screen shows "not being streamed yet"** unless a session has a
  stream URL configured. Either configure one for the review window or do not
  film that screen.

---

## Part 4: pre-flight, before a single word is sent

### Blocking, and none of it is code

- [ ] **Verify the demo accounts on PRODUCTION, then sign in from the physical
      iPhone yourself.** Not a simulator, not a description of what should
      happen. Six things must be true, and four are invisible from the app:
  - [ ] `SimfUser.AccountState == Approved` in `SIMF_Identity`. The app reads the
        Identity column, not the profile's admission state.
  - [ ] `TwoFactorEnabled == 0`, or an emailed code goes to a mailbox Apple
        cannot read.
  - [ ] `PasswordChangeRequired == 0`. A seeded or admin-rotated password is a
        hard 403 on the app surface.
  - [ ] No admin role. Admin accounts are refused on the app audience.
  - [ ] `profileComplete` is true. It is FOUR facts, not one, and a demo
        account can satisfy three and still bounce the reviewer into the
        registration form: both name fields, **at least one interest**, the
        identity document (`IdImageFileId`, every registrant), and the face
        photo (`SimfUser.AvatarFileId`, **men only** - women are exempt). The
        interest is the one people forget; the validator demands one to ten on
        every save, so an account seeded straight into the database can easily
        have none.
  - [ ] `QrId` minted, or the badge screen records as a lock icon.
  - [ ] Production iOS minimum version empty or at most 1.0.1, and no app-key
        gate configured, or the reviewer's build is hard-blocked with no way for
        us to see it.
- [ ] **Read the live `SIMF_Ai__DefaultProvider` off the production API host** and
      fill the `<<CONFIRM>>` placeholder. Three sources in this repo disagree:
      `deploy/set-env-api.ps1` says Anthropic but was committed as a stripped
      template whose own comment says the provider stays Echo until a key is
      supplied, `appsettings.json` says Echo, and D-953 records the delivered
      phase-one design as an on-site GPT OSS 120B model. Item 4 is a statement
      about data leaving the country. Do not guess it.
- [ ] **Confirm the authorisation letter names the Apple account's legal entity**
      exactly as it appears on the developer account, and names the bundle id
      `com.simrsnf.simf`.
- [ ] **Publish the AI assistant in the privacy policy.** The policy has no
      mention of an assistant, a model provider, or conversation content being
      transmitted or retained. Apple checks the policy against the app.
- [ ] **Check the support address resolves.** `nslookup -type=MX simrnsf.com`.
      The in-app support address is a transposition of the `simrsnf.com` every
      other host uses. If it bounces, Apple's follow-up on this thread goes
      nowhere. App Store Connect also requires a Support URL, and no such URL is
      recorded anywhere in this repo.
- [ ] **Rebuild and upload.** Everything under "Fixed in the app" below is in
      the working tree, not in the binary Apple has. The build they reviewed
      still has the four dead accessibility switches and no deletion link for a
      pending account. Increment the build number, upload, and attach the new
      build to the submission before replying.

### Blocking, and it is content in the production database

**None of this needs a developer, a migration or SQL.** All four are Control
Panel pages an admin edits. The evidence for each is a seed file under
`docs/migrations/2026/`, which is a script in the repo and NOT proof that anyone
ran it against production.

- [ ] **Step zero: open the four pages on the PRODUCTION Control Panel and look.**
      If a seed was never run there, the item does not exist and there is
      nothing to fix. Do this before spending time on any of the four.
- [ ] **Delete the twelve fabricated delegation heads** - `/admin/delegates`.
      Invented named individuals presented as serving heads of foreign navies
      ("James Whitfield, First Sea Lord"), reachable from the signed-in home.
      Each is a `UserProfile` row with `IsDelegate = 1`, plus the country's
      `HeadOfDelegationUserProfileId` pointer. **Deleting them leaves nothing
      broken**: the seed's own comment (`SIMF_App_SeedGaps.sql:166-168`) records
      that the head and the member count resolve from real delegate
      registrations at read time, so the screen simply shows countries with no
      head yet, which is the truth before the event.
- [ ] **Replace the three `placehold.co` media-partner logos** -
      `/admin/media-partners`. The app renders the placeholder-image-service URL
      verbatim, so a reviewer sees three grey stub images for three named
      companies. Upload the real logos, or deactivate the three entries until
      they exist.
- [ ] **Fix the archive** - `/admin/archive`, the 2024 edition. Two things, and
      the second is the one people miss: replace the five filler speakers ("Mr.
      Ali", "Dr. Khalid", "Eng. Ahmed", "Ms. Sara", "Eng. Fahd"), **and correct
      the headline counters**, which the seed hard-set to `Speakers = 250,
      Sessions = 30` with a comment saying they were aligned to the Figma mock.
      They are design numbers, not 2024's real figures. Replacing the names and
      leaving the counters still ships invented data.
- [ ] **Populate the media gallery** - `/admin/media` (with
      `/admin/media-library` for the uploads). It has no seed at all and opens
      on an empty state. Removing the tab instead is a code change, not an
      admin one.

**If you can only do two, do delegations and media partners.** They are the
likeliest to be opened, and fabricated named individuals attributed as serving
foreign military officers is a materially worse look than an empty gallery. An
empty gallery before an event is defensible; twelve invented admirals are not.

### Blocking, and it is the store listing

- [ ] **Remove the three onboarding-carousel screenshots** from every size. They
      are title art, which is the exact failure Apple's letter named. Five
      in-use shots remain, and Apple accepts one to ten.
- [ ] **Decide iPad.** The app ships universal but has never been built or run
      on an iPad, and the uploaded 13-inch screenshots are an Android phone
      capture letterboxed with navy bars. Dropping iPad removes both problems at
      once. This is a project-settings change and is the owner's to make, so the
      exact edits are written out rather than applied:

  1. `ios/Runner.xcodeproj/project.pbxproj`, lines **367, 493 and 546**:
     `TARGETED_DEVICE_FAMILY = "1,2";` becomes `TARGETED_DEVICE_FAMILY = 1;`.
     All three build configurations carry it, or the Release build still ships
     universal.
  2. `ios/Runner/Info.plist`, delete lines **79 to 85**, the
     `UISupportedInterfaceOrientations~ipad` array. This is why the iPad risk is
     real rather than theoretical: it declares **landscape**, and there is no
     `UIRequiresFullScreen` key, so iOS treats the app as Split View and Slide
     Over capable and **ignores the Flutter portrait lock**. The app is
     portrait-locked everywhere except the one device nobody has run it on.
  3. Delete `store-screenshots/appstore-ready/ipad-13/` (8 files) and remove the
     13-inch set in App Store Connect.
  4. Bump the build number and upload. The device family lives in the binary, so
     App Store Connect only stops demanding iPad screenshots once a
     non-universal build is attached.

  Both files are tracked in git, so this goes through CI normally, and the
  repo's iOS ratchet test pins only the six permission usage strings, so
  removing the iPad orientations does not trip it. **Do it before first
  release**: Apple restricts removing a device family from an app that is
  already live, and nothing has shipped yet. Worth confirming against current
  App Store Connect behaviour before relying on it.

  **What keeping iPad costs**, so the choice is fair: a Mac to build on, an iPad
  to run it on, real 13-inch captures, a decision on multitasking versus adding
  `UIRequiresFullScreen`, and at least one real bug fixed first -
  `seat_map_repository.dart:127` calls `Share.share` with no
  `sharePositionOrigin`, which every other share site in the app passes, so on
  iPad "Share my seat" silently does nothing. It works on iPhone, which is why
  nobody noticed.

### Fixed in the app, and NOT yet in a build Apple has

Committed 2026-09-04 (D-955). `flutter analyze lib test integration_test
packages` reports no issues; 1776 app tests, 70 in `simf_auth_pkg`, 17 in
`simf_data_pkg`. One golden, `email_otp_verify_golden_test`, fails on `main`
independently of this work and was reproduced there before it started.

- [x] **Account deletion is reachable by a pending account**, not only an
      approved one. A live 5.1.1(v) violation, and the subject of Apple's own
      question 1: the one account state you can create in the app was the one
      state you could not delete from it.
- [x] **Four non-functional accessibility controls withdrawn** - high contrast,
      reduce motion, the screen-reader announcer, captions. They were reachable
      SIGNED OUT, so a reviewer needed no credentials to find them.
- [x] **Their EFFECTS withdrawn too.** Removing only the switches left a stored
      `true` applied with nothing able to turn it off, and for a signed-in
      account the server copy replays onto every fresh install. Pinned by
      `test/repo/withdrawn_accessibility_flags_test.dart`.
- [x] **Text size composes with iOS Dynamic Type** instead of discarding it, and
      the chips **disable themselves with a line saying why** once the device
      setting alone meets the app's ceiling - rather than four live-looking
      pills that all render the same size, which is the same complaint again.
- [x] **The Face ID button names the account it opens** and no longer appears on
      a device with nothing enrolled. Deleting the account clears the credential
      too, or the sign-in screen would advertise the erased address.
- [x] **The meeting day card** no longer overflows at the two larger text sizes.
- [x] **The live caption strip** draws nothing rather than an empty placeholder
      box.

### Still open, not blocking this reply

- The "Ask the moderator" tile on the signed-in home pushes the question screen
  with no session, so it always renders the empty state, and pull-to-refresh
  there dereferences a null session id. Reachable in the first five minutes, and
  the strongest remaining candidate for a bugs-and-crashes finding.
- `lib/app/widgets/screen_announcer.dart` is now orphaned: nothing mounts it.
  Delete it, or rebuild it as a `NavigatorObserver` so it fires on navigation
  rather than on mount. Left in place because this change did not create it.
- The `email_otp` golden test fails on `main`, independently of this work.
- Nothing pins that `DELETE /app/account` accepts a token from a non-approved
  account. The endpoint's own remarks say it is ungated for exactly that holder,
  and the app now mounts the button for them, so the pairing deserves a test.

---

_Drafted 2026-09-04. Sources: the App Store Review Guidelines as cited in Apple's
letter, and the code paths named above, each verified in this repository._
