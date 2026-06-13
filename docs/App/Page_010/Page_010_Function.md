# Page 010 — Function (تم التسجيل بنجاح · Registration success)

What this page does, the elements on it, what the user can do, and how it is reached.

> Last updated: 2026-06-13 — conformance pass to the as-built code (D-366 / D-369 / D-373).

## Purpose
Terminal step of the sign-up journey. After the user completes the profile save
(Page 007 data form → Page 007-01 interests step, one save), the profile exists
and the account sits in a **pending-approval** state. This screen confirms the
submission succeeded, shows the user their **registration reference number**
(`SIMF-YYYY-NNNNNNNN`, issued by the save — D-373), tells them a confirmation
email is coming, and offers support contact tiles.

It is a **transitional / confirmation** screen — it presents a result, it does
not collect input.

## How it is reached
| | |
|---|---|
| Entered from | Page 007-01 (interests step) on a successful profile save |
| Trigger | `POST /app/account/user-profile` (the single profile save) returned success; the response's `referenceNumber` is passed along as the route extra (D-373) |
| Route | `RouteNames.registrationSuccess` → `/registration/success` (auth-gated) |

The screen is entered via **`goNamed` replacement** (not stacked) — pressing
back must **not** return the user into the multi-step sign-up form. The header
chevron pops if a stack exists, otherwise it goes to home.

## Elements
| Element | AR | EN | Notes |
|---|---|---|---|
| Header title | تم التسجيل | Registered | 56 px header band with back chevron (no Material app bar) |
| Success mark | — | — | 104 px navy circle, green (`#22C55E`) ring + check |
| Headline | تم التسجيل بنجاح | Registration success | `registrationSuccessTitle` |
| Review copy | تم استلام طلبك ومراجعته⏎ستصلك رسالة تأكيد على بريدك الإلكتروني. | Your request was received and is under review.⏎A confirmation email will reach your inbox. | `registrationSuccessMessage` (two lines, `\n`) |
| Reference card label | رقم البطاقة المرجعي | Reference badge number | `referenceNumberLabel` |
| Reference card value | — | — | The real `referenceNumber` route extra (e.g. `SIMF-2026-00000001`, D-373); falls back to the literal `SIMF-2026-xxxx` mask when absent. Rendered LTR in both locales |
| Primary action | حالة التسجيل | Registration status | Gold `FilledButton` → Page 011 (`registrationStatus`) |
| Secondary action | الانتقال للرئيسية | Go to home | Accent-outlined button → home (`/`) |
| Contact section title | تواصل معانا | Contact us | `contactUsTitle` |
| Contact tiles (phone / mail) | — | — | Open the OS dialer / mail app via `url_launcher` when `BuildConfig.supportPhone` / `supportEmail` is supplied; **inert** when the value is empty (D-369) |
| Footer | ‎@SIMF_RSNF · الملتقى البحري السعودي الدولي | @SIMF_RSNF · Saudi International Maritime Forum | `simfSocialFooter` |

## User actions
1. **Read the confirmation + note the reference number** — no input required.
2. **Registration status** — primary button takes the user to Page 011
   (registrationStatus), where the page itself reads `registrationStatus` and
   shows pending / approved / rejected.
3. **Go to home** — outlined secondary button takes the user to the home screen.
4. **Call / email support** — when the build supplies `SIMF_SUPPORT_PHONE` /
   `SIMF_SUPPORT_EMAIL`, the tiles open the OS dialer / mail app (best-effort —
   a failed launch leaves the user on the page). Unconfigured tiles do nothing.
5. **Back chevron** — pops if possible, otherwise goes home; it never re-opens
   the sign-up form.

## Privilege / auth gate
- The user is **signed in but pending approval** — the account exists, the
  session/token is issued, but `registrationStatus` is **not yet Approved**.
- The route is in the router's `_authenticatedRoutes` set (route number 10) —
  signed-out access redirects to `/sign-in`.
- No admin permission code applies (this is an App onboarding screen, not a
  Control Panel page).

## Acceptance criteria
- [ ] Reached via replacement navigation after a successful Page 007-01 profile
      save, with the save's `referenceNumber` carried as the route extra.
- [ ] Back navigation does **not** re-open the sign-up form.
- [ ] The reference card shows the real `SIMF-YYYY-NNNNNNNN` value when the
      extra is present, and the `SIMF-2026-xxxx` mask when it is not — never a
      fetch, never a blank.
- [ ] Header, headline, copy, labels and footer render correctly in **both AR
      and EN**; the reference value and chevron glyph stay LTR.
- [ ] Primary "حالة التسجيل" routes to Page 011; outlined "الانتقال للرئيسية"
      routes to home.
- [ ] A contact tile with a configured value launches `tel:`/`mailto:`; an
      unconfigured tile is inert; a failed launch never crashes the screen.
