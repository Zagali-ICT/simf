# Play Data Safety + App content — the answers, derived from the code

**Status:** working note, 2026-08-22. Companion to `docs/dev/Mobile-Store-Release.md`,
which covers the release *process*; this file answers the *forms*.

Every row below was traced to source, not recalled. Where a judgement call is
yours rather than mine, it says so.

---

## Account deletion — CLOSED (was the blocker this file opened with)

Both halves are built. **In-app:** My Area → حذف حسابي → confirm
(`DeleteAccountTile` → `DELETE /app/account`), shipped in versionCode 19 and
present in versionCode 20 — verified by decoding the bundle's `libapp.so`.
**Web:** https://web.simrsnf.com/privacy#delete-account.

So **App content → Data deletion** answers *yes*, with that URL.

This section used to open "There is no account-deletion path anywhere" on the
strength of `grep deleteAccount lib packages/*/lib → nothing`. That was true on
2026-08-22 and stopped being true the next day. The requirement it described is
unchanged and still worth reading:

Google Play requires that **any app offering account creation also offers
account deletion**, and the declaration has two parts:

1. a **publicly reachable web URL** where a user can request deletion *without
   installing the app* — this is the part people miss, and it is mandatory even
   when an in-app path exists;
2. ideally an in-app route as well.

This sits in Play Console under **App content → Data deletion**, and it gates the
Data safety section, which gates **every** track — internal testing included.

**Cheapest compliant answer:** a public page on the existing website (e.g.
`web.simrsnf.com/account-deletion`) stating what a deletion request removes, what
is retained and why, and how to make one (an email address is acceptable). It
can ship alongside the privacy policy, from the same legal owner. Building
in-app deletion is the better answer and is a separate piece of work.

---

## Data safety — what to declare

The app sends **everything to `edge.simrsnf.com`** and to nothing else. There is
no analytics SDK, no crash reporter, no advertising SDK, no Firebase, and no
`google-services.json` anywhere in the tree — so "shared with third parties" is
**No** for every row below, subject to the YouTube note at the end.

| Play category | Collected | Type | Required? | Purpose | Where in code |
|---|---|---|---|---|---|
| Personal info → **Name** | Yes | Arabic + English | Required | App functionality, Account management | `sign_up_visitor_form.dart` |
| Personal info → **Email address** | Yes | | Required | Account management, sign-in, OTP | `auth_api.dart` sign-up / verify-email |
| Personal info → **Phone number** | Yes | Saudi + international | Required | App functionality | `sign_up_visitor_form.dart`, `phone_validation.dart` |
| Personal info → **Other info** | Yes | **National ID / passport / iqama number**, date + place of birth, gender, nationality, organisation, **vehicle plate** | Required | App functionality (venue admission + security screening) | `sign_up_visitor_form.dart` |
| **Photos and videos → Photos** | Yes | **identity-document image** and a **face photo** (liveness selfie) | Required (face photo: mandatory for men, optional for women) | App functionality, identity verification | `sign_up_profile_draft.dart`; `POST /app/account/user-profile/id-image`, `POST /app/account/avatar` |
| **Contacts** | Yes | contact cards scanned/saved from other attendees' badges | Optional | App functionality | `/app/contacts`, `/app/contacts/resolve`, `/app/contacts/save` |
| Messages → **Other in-app messages** | Yes | AI-assistant conversation content | Optional | App functionality | `/app/ai/assistance` |
| **Location** | **No** | — | — | — | no location plugin, no location permission in the merged release manifest |
| **Device or other IDs** | *Your call* | a **self-minted random** value, SHA-256'd and truncated, used only as a human-readable device label | Optional | Security (device-key sign-in) | `device_label.dart:85-110` |
| Analytics / crash logs / ads | **No** | — | — | — | no such SDK in `pubspec.yaml` |

**On "Device or other IDs":** Play's definition targets hardware/advertising
identifiers (advertising ID, Android ID, IMEI, MAC). This value is none of those
— the app generates a random one and hashes it. I would declare it anyway, since
it is persistent per install and reaches the server; over-declaring is not
penalised, under-declaring is.

### Security practices

- **Encrypted in transit:** **Yes.** Ordinary TLS validation against the system
  trust store, with no bypass — the old trust-all was deleted (D-872) and a
  ratchet test (`platform_projects_tracked_test.dart`) fails the build if it
  returns. Release builds carry no cleartext permission.
- **Can users request deletion:** see the blocker above. Answer **Yes** only once
  the URL exists.
- **Committed to the Play Families policy / independent security review:** No.

### Two judgement calls, flagged rather than guessed

1. **YouTube.** Live sessions embed the YouTube IFrame player
   (`youtube_player_iframe`). The user's webview contacts Google directly, which
   some reviewers treat as third-party data sharing. It is not the app sending
   user data, and no SIMF data is passed — but if you want the conservative
   answer, declare nothing collected *by you* and note the embed in the privacy
   policy.
2. **Face images and liveness.** The selfie is uploaded and retained server-side,
   so it is collected **Photos**, not merely processed on-device. The on-device
   ML Kit check is a convenience; the server's gate is the authority. Combined
   with government ID numbers, expect Play to take a closer look at this app than
   at an ordinary one — have the privacy policy explicit about retention.

---

## App content → App access (the other thing that stalls review)

Almost every screen is behind sign-in, and visitor sign-in is **emailed OTP**
(D-033). A Google reviewer cannot receive that mail, so without instructions the
review sits until it times out.

Provide, in the **App access** section:

- a working account's email + password;
- **how the OTP is satisfied for that account** — either a fixed code, or a
  mailbox the reviewer can read. This is the part that must actually work; a
  demo account that still demands an unreachable OTP is the same as no account.

State plainly that the account is pre-approved, since an unapproved account is
treated as Guest (D-666) and a reviewer would see a fraction of the app and may
conclude it is broken.

---

## Content rating (IARC)

A short questionnaire. This app has no violence, no user-to-user open chat beyond
the AI assistant and contact exchange, no gambling, no purchases. Answer honestly
and it will come back in the low bands; the certificate is issued immediately.

---

## Order to fill things in

1. **App content → Data deletion** (needs the URL — the blocker above)
2. **App content → Privacy policy** (needs the URL from legal)
3. **App content → App access** (demo account + how its OTP is satisfied)
4. **App content → Content rating**
5. **Data safety** (the table above)
6. Store listing assets, then upload the bundle to **internal testing**

1–5 are console work that needs no build. They can all be done *while* the
keystore and bundle are being sorted, and they are what will actually set the
date.
