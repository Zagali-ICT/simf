# App Store Connect reply, Guideline 2.1 Information Needed

Drafted 2026-09-04 for the SIMF iOS submission (`com.simrsnf.simf`, 1.0.1).
Apple's letter asked six questions and warned separately about bugs, demo
accounts and screenshots.

**Read the pre-flight list at the bottom BEFORE sending any of this.** Every
answer below is a claim a reviewer verifies by opening the app. A claim they
disprove in ninety seconds turns "Information Needed" into a credibility
problem, and the second rejection then cites our own instructions as evidence.

Two placeholders are marked `<<CONFIRM>>` and one is marked `<<CREDENTIALS>>`.
The reply is not sendable until all three are resolved. Passwords go into App
Store Connect directly and never into this file or any other file in the repo.

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
  - [ ] `UserProfile.IdImageFileId` set (and an avatar if the profile is male),
        or `profileComplete` is false and the reviewer is dumped into ID capture.
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

### Blocking, and it is content in the production database

- [ ] **Delete the twelve fabricated delegation heads.** The Delegations screen
      is seeded with invented named individuals presented as serving heads of
      foreign navies. This is reachable from the signed-in home.
- [ ] **Replace the three `placehold.co` media-partner logos.** The app renders
      the placeholder-image-service URL verbatim.
- [ ] **Replace the filler archive speakers** ("Mr. Ali", "Dr. Khalid",
      "Eng. Ahmed") and the headline counters copied from the Figma mock.
- [ ] **Populate the media gallery or remove the tab.** It has no seed at all and
      opens on an empty state.

### Blocking, and it is the store listing

- [ ] **Remove the three onboarding-carousel screenshots** from every size. They
      are title art, which is the exact failure Apple's letter named. Five
      in-use shots remain, and Apple accepts one to ten.
- [ ] **Decide iPad.** The app ships universal but has never been built or run on
      an iPad, and the uploaded 13-inch screenshots are an Android phone capture
      letterboxed with navy bars. Dropping iPad support removes both problems at
      once and is the honest option given nothing has been tested there. This is
      a project-settings change and is the owner's to make.

### Fixed in this changeset

- [x] Account deletion is now reachable by a pending account, not only an
      approved one. It was a live 5.1.1(v) violation, and it is the subject of
      Apple's own question 1.
- [x] Four non-functional accessibility controls withdrawn. They were reachable
      signed out, so a reviewer needed no credentials to find them.
- [x] The day card in the meeting request sheet no longer overflows at the two
      larger text sizes.
- [x] The live caption strip no longer draws an empty placeholder box.
- [x] Text size now composes with iOS Dynamic Type instead of ignoring it.
- [x] The Face ID button no longer appears on a device with nothing enrolled,
      and names the account it will open.

### Still open, not blocking this reply

- The "Ask the moderator" tile on the signed-in home pushes the question screen
  with no session, so it always renders the empty state, and pull-to-refresh
  there dereferences a null session id. Reachable in the first five minutes.
- The `email_otp` golden test fails on `main`, independently of this work.

---

_Drafted 2026-09-04. Sources: the App Store Review Guidelines as cited in Apple's
letter, and the code paths named above, each verified in this repository._
