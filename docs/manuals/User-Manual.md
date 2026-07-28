# SIMF — User Manual

| | |
|--|--|
| **Audience** | SIMF visitors |
| **Surfaces covered** | Mobile App (Flutter) — the account surface. The public Website (`https://simf.local`) is information-only and has no account area (D-774). |
| **Authority** | D-133 (2026-05-28) — extended each release; account surface re-pointed by D-774 (2026-07-27) |
| **Bilingual** | Yes — Arabic translations land alongside each chapter (translator pending) |
| **Companion docs** | [`Admin-Manual.md`](Admin-Manual.md), [`Developer-Guide.md`](Developer-Guide.md), [`PAGE-INDEX.md`](../pages/PAGE-INDEX.md) |

This manual is for **the visitor** — the person attending the Saudi
International Maritime Forum. It walks you through everything you do
with your SIMF account: register, fill your profile, get your QR badge,
manage notifications, recover if you lose access. Every chapter has a
Most-common-tasks section, screenshots, and a Troubleshooting subsection
for the top three things that go wrong.

> **Where you do this — updated 2026-07-27 (D-774).** The public SIMF
> **Website is information-only**: it has no sign-in and no account area.
> Every account journey in sections 2–9 below is performed in the **SIMF
> mobile app**; administrators use the Control Panel. The route names shown
> (`/login`, `/account/profile`, `/account/notifications`, …) name the
> equivalent app screens. The one page the Website still serves without an
> account is `/meeting/confirm`, the link emailed to a speaker or delegate
> to confirm a meeting. Re-writing the screenshots and wording for the app
> is a tracked follow-up.

---

## 1. Contents

1. Introduction (this section)
2. How to register
3. After-registration: pending approval, rejection, troubleshooting
4. Your profile + your QR badge
5. Sign in (returning visitor)
6. Forgot password / reset password
7. Lost authenticator code / recovery codes
8. Notifications inbox
9. Sign out + delete account
10. _(planned)_ Mobile App walkthrough (when the Flutter App ships)
11. Glossary

---

## 2. How to register

### 2.1 What you need

- A working **email address** (your account is identified by this — pick
  one you'll keep access to).
- A **password** that's at least 12 characters and contains at least one
  digit, one uppercase letter, one lowercase letter, and one special
  character.
- (Optional) An **authenticator app** on your phone (e.g. Google
  Authenticator, Microsoft Authenticator) — recommended for stronger
  account security. If you don't have one, SIMF can fall back to a code
  sent to your email at each sign-in.

### 2.2 Step-by-step

1. Open `https://simf.local` (or whatever address SIMF gives you).
2. You land on the sign-in page. Click **Register** (the link below the
   Sign-in button — pending UI update, this currently routes you to the
   admin-side flow).
3. Fill the registration form:
   - **Email** — the address you'll log in with and receive notifications at.
   - **Password** — choose a strong one (12+ chars, mixed case, digit, special).
   - **Confirm password** — type it again.
4. Click **Create account**.
5. SIMF sends a **6-digit code** to your email. The code is valid for
   15 minutes.
6. Type the code → **Verify**.
7. Your account is now created and **PendingApproval**. An administrator
   reviews and approves it. See §3 for what happens next.

### 2.3 Tip: pick the right language up-front

Use the **العربية / English** toggle in the top header to switch interface
language. The toggle works on every page, but registering in your preferred
language means the welcome email and the rejection-reason (if any) arrive
in that language.

### 2.4 Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "An account with this email already exists" | You already registered | Use **Sign in** or **Forgot password** instead |
| Email code never arrives | Spam folder OR your provider is slow | Wait 5 minutes, check spam; if still missing, request a new code (request rate-limited to 3 / minute / email) |
| Password rejected as "too weak" | Doesn't meet the complexity rules | Add a digit, a capital letter, and a special character; aim for 16+ chars |
| Verification code says "expired" | Code is older than 15 minutes | Click **Request a new code** |

---

## 3. After-registration: states + waiting

### 3.1 Pending approval — `/account/pending`

> Screen reference (D-774 — this is an app screen; the Website page was removed):
> [`docs/tests/e2e/mobile-sign-in.md`](../tests/e2e/mobile-sign-in.md)

When you sign in for the first time after registering, you may land on a
page that says **"Your account is awaiting approval."** This is normal —
an administrator must approve every new account.

- Approval usually happens within 24 hours.
- You'll receive an email when your account is approved.
- You can sign out from this page; no further action is required from you.

### 3.2 Approved — what changes

- You can sign in normally and land on `/account/profile`.
- Your QR badge is minted automatically and appears on your profile page.
- You start receiving notifications.

### 3.3 Rejected — `/account/rejected`

> Screen reference (D-774 — this is an app screen; the Website page was removed):
> [`docs/tests/e2e/mobile-sign-in.md`](../tests/e2e/mobile-sign-in.md)

If an administrator rejects your registration, you'll see a page with:

- The bilingual **rejection reason** the administrator wrote (10–500
  characters).
- The rejection **timestamp**.
- A **Sign out** button.

You can sign in to read this page anytime. If you believe the rejection is
in error, reach out to the SIMF organising team via the contact email in
your welcome / rejection notice.

---

## 4. Your profile + your QR badge

### 4.1 Filling your profile — `/account/profile`

> Screen reference (D-774 — this is an app screen; the Website page was removed):
> [`docs/pages/mobile/`](../pages/mobile/)

Once approved, you land on the profile page. Fill every section:

| Section | What's there |
|---------|--------------|
| **Identity** | Full name in English + Arabic, the name you want on your badge, date of birth, place of birth |
| **Nationality + ID** | Saudi / Non-Saudi toggle; Saudi → 10-digit national ID starting with 1; Non-Saudi → country picker + Iqama (10 digits starting with 2) or Passport (≤ 20 characters) |
| **Contact** | Mobile (Saudi format `+9665XXXXXXXX` or international) + your sign-in email |
| **ID document** | Optional photo of your national ID / Iqama / passport. Stored encrypted at rest. PNG / JPEG / WebP, ≤ 5 MB. |
| **Interests** | Pick up to 10 topics you care about — drives session recommendations |

Click **Save** when done. A green confirmation toast appears.

### 4.2 Your QR badge

When your account is **Approved**, a QR card appears at the top of the
profile page with:

- Your name and badge color.
- A scannable QR code.
- The QR id underneath (12 characters).

**At the event:** show this QR (or the printed badge — staff can print it
at the registration desk) at the venue gate. Scanning it proves your
identity. **Do not share** the QR — it's effectively your access key.

### 4.3 What happens if your QR is lost?

- Show your ID at the SIMF registration desk; staff will look you up by
  email and reprint a fresh badge from the same QR id (via
  `/admin/print-bag`). The QR id itself doesn't change.

### 4.4 Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Save fails: "Invalid national ID" | Saudi ID doesn't start with 1 OR isn't 10 digits | Re-check; the regex is strict |
| Save fails: "Invalid Iqama number" | Iqama doesn't start with 2 OR isn't 10 digits | Re-check |
| QR card missing | Your account isn't Approved yet | Wait for approval; the QR appears as soon as it's minted |
| ID document upload fails | File > 5 MB OR wrong format | Compress / re-export as PNG/JPEG/WebP |

---

## 5. Sign in (returning visitor) — `/login`

> Screen reference (D-774 — this is an app screen; the Website page was removed):
> [`docs/tests/e2e/mobile-sign-in.md`](../tests/e2e/mobile-sign-in.md)

1. Email + password → **Sign in**.
2. If you have an authenticator app paired: enter the current 6-digit
   code on the verification page.
3. If you opted into email-OTP: SIMF emails you a 6-digit code; enter it.
4. You land on `/account/profile`.

### 5.1 Troubleshooting

| Symptom | Fix |
|---------|-----|
| "Invalid email or password" | Re-type; if still failing, try **Forgot password** (§6) |
| "Account pending approval" | See §3.1 |
| "Account rejected" | See §3.3 |
| "Too many attempts — try again in N minutes" | Rate limit (5 fails / 5 min); wait it out |

---

## 6. Forgot password / reset password

> Screen reference (D-774 — these are app screens; the Website pages were removed):
> [`docs/pages/mobile/forgot-password/`](../pages/mobile/forgot-password/README.md),
> [`docs/pages/mobile/reset-password/`](../pages/mobile/reset-password/README.md)

1. On the sign-in page, click **Forgot password**.
2. Type your email → **Send code**.
3. Check your inbox (and spam folder) for the 6-digit reset code. Valid
   for 15 minutes.
4. On the reset-password page, type the code + your new password (12+
   chars + complexity rules).
5. Click **Reset password** → toast → routed to `/login`.
6. Sign in with the new password.

> **Security note:** SIMF always shows "Code sent" regardless of whether
> the email exists in our system — this prevents attackers from probing
> for valid email addresses. If you typed the wrong email, the code
> simply never arrives.

---

## 7. Lost authenticator / recovery codes

If your phone is lost or you cleared your authenticator app:

1. On the TOTP verify page, click **Use a recovery code instead**.
2. Enter one of the 10 single-use recovery codes you saved at pairing
   (see your password manager / printed list).
3. The code burns on use.
4. As soon as you're in, go to **Profile → Reset my 2FA** to re-pair.

If you've lost **all** recovery codes too, you'll need an administrator
to reset your 2FA. Reach out via the support channel in your welcome
email; the admin uses `/admin/reset-2fa` and you'll re-pair on next
sign-in.

---

## 8. Notifications inbox — `/account/notifications`

> Screen reference (D-774 — this is an app screen; the Website page was removed):
> [`docs/pages/mobile/notifications/`](../pages/mobile/notifications/README.md)

Reachable from the **Notifications** link in your profile header (added
in D-132).

| Want to | Do |
|---------|----|
| See every notification | Open the page |
| Read full body of one | Click **Details** |
| Dismiss one | Click the trash icon |
| Dismiss several at once | Tick rows + click **Delete** in the toolbar |
| Mark all unread as read | Click **Mark all as read** below the grid |

---

## 9. Sign out + delete account

- **Sign out** — the button in the top right of every signed-in page.
- **Delete account** — not exposed self-service today. Email the SIMF
  organising team and they'll process the deletion (admin uses
  `/admin/visitors` → row Delete with reason). Your data — except the
  audit trail required for compliance — is soft-deleted.

---

## 10. _(planned)_ Mobile App walkthrough

The Flutter App build is on the Programme Plan. When it ships, this
chapter will cover: app install, on-device sign-in, QR badge offline
display, push notifications, and event-day workflows. Per-page docs land
under [`docs/pages/mobile/`](../pages/mobile/) at that point.

---

## 11. Glossary

- **QR badge** — your access key for the event venue. Minted when your
  account is approved; never changes.
- **TOTP** — Time-based One-Time Password. The 6-digit code your
  authenticator app generates every 30 seconds.
- **Recovery code** — one-time emergency code (10 generated at pairing)
  that signs you in if you lose your authenticator.
- **PendingApproval** — your account exists but an administrator hasn't
  reviewed it yet.
- **Approved** — your account is live; QR badge minted; sign-in unlocked.
- **Rejected** — an administrator turned down your registration; you can
  sign in to see the reason but nothing else.
- **Iqama** — Saudi residency permit identification number for non-Saudi
  residents (starts with 2, 10 digits).
- **Profile type** — the badge category an administrator assigns you to
  (e.g. General, VIP, Press, Speaker). Drives the colour stripe on your
  printed badge.

---

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 5).
_Arabic translation pass:_ pending translator.
