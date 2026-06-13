# Page 005 — Function (إنشاء حساب · Sign up)

What the page does, the elements on it, the user actions, the navigation, and
the acceptance criteria. The contract that backs these actions is in
[Page_005_API.md](Page_005_API.md); the rules behind them are in
[Page_005_Logic.md](Page_005_Logic.md).

*Last updated: 2026-06-13 — as-built conformance pass (W2-1, D-370).*

## Purpose
Sign-up **step 1**. The visitor supplies the three credentials needed to create
an account. On success the server provisions a new **Visitor** account that has
**no privilege**, is in the **`Registered`** state (email not yet verified —
review/approval comes later), and **has no profile yet**, then emails a
**6-digit OTP** to the address so the next screen can verify it.

## Privilege / auth gate
| | |
|---|---|
| Who reaches it | **Guest** (unauthenticated). Reachable from the sign-in screen ("Create account"). |
| Already signed in | A signed-in user is routed to their area, not here. |
| What it grants | Nothing yet — it **creates** an account; it does not sign the user in. |
| Result account | **Visitor**, no privilege, `AccountState = Registered` (unverified), no profile yet. |

## On-screen elements
| Element | Type | Notes |
|---|---|---|
| Back chevron | Icon button | Top-left (forced LTR); pops, or goes to sign-in when there is nothing to pop |
| Language toggle | Icon button (globe) | Top-right (forced LTR); switches AR ↔ EN and persists the choice (D-363) |
| Logo + forum name | Header | `SimfLogo` + AR **الملتقى الدولى البحرى** · EN **International Maritime Forum** |
| Card head | Text | AR **إنشاء حساب** · EN **Sign up** |
| Email | Text field | Email keyboard; LTR-pinned; trimmed + lower-cased before submit; input capped at 50 chars |
| Password | Password field | Obscured; show/hide toggle; input capped at 32 chars |
| Confirm password | Password field | Obscured; show/hide toggle; must equal Password; keyboard submit triggers the form |
| Inline error | Text (red) | The server's bilingual failure message (or the offline message); shown only after a failed call |
| Submit | Primary button | AR **إنشاء حساب** · EN **Create account**; gold, full width; spinner + disabled while submitting |
| Go to sign in | Text link | AR **لديك حساب؟ تسجيل الدخول** · EN **Have an account? Sign in** — back to the sign-in screen |

## User actions (step by step)
1. User opens the screen from "Create account" on the sign-in page.
2. User types an **email**.
3. User types a **password**.
4. User types the **confirm password**.
5. User taps **Create account** (or submits from the confirm-password keyboard).
6. The form validates locally (email format; password ≥8 chars with a letter
   and a digit; confirm match). On any failure the field error shows and the
   submit stops — no call is made. (Fields also re-validate as the user types,
   after first interaction.)
7. On valid input the client calls `POST /app/auth/sign-up` with
   `{ email, password, confirmPassword }` (see API doc), email trimmed +
   lower-cased.
8. **On success (always a generic 201):** a "check your email" SnackBar shows
   and the app pushes the **email-OTP screen** (`/sign-up/otp`) carrying the
   address. This is the **same** step whether the email is new or already
   registered (D-198 — no `409`, no "you already have an account").
9. **On any failure** (network, 400 validation, 403 registration-closed,
   429 rate-limit, 5xx): the app shows the server's bilingual message — or the
   offline message when the network is down — **inline in red** inside the
   card, keeps the form, and re-enables Submit.

## Navigation
| Trigger | Goes to |
|---|---|
| Successful submit (generic 201) | Email-OTP / verify-email screen (`/sign-up/otp`, step 2) — pushed with the email as a query parameter |
| "تسجيل الدخول 'Sign in'" foot link | Sign-in screen |
| Back chevron / system back | Previous screen (pop); falls back to the sign-in screen when there is nothing to pop |
| Globe toggle | Stays on the page; switches the app language AR ↔ EN |

## Acceptance criteria
- Tapping Submit with invalid input shows the per-field error(s) and makes
  **no** API call. (The button itself stays enabled; validation runs on tap and
  on user interaction.)
- Confirm-password mismatch is caught client-side for instant feedback;
  `confirmPassword` is still sent and re-validated server-side
  (`ConfirmPassword == Password`; D-270).
- A successful sign-up always lands on the **generic email-OTP /
  check-your-email** screen, identically for a new and an already-registered
  email (D-198).
- The created account is a **Visitor**, no privilege, **`Registered`**
  (awaiting email verification), no profile yet — the user is **not** signed in
  by this screen.
- Failures render the server's bilingual message inline (no error toast); the
  form is kept so the user can retry.
- The card renders correctly in **RTL (Arabic)**: labels/errors right-aligned,
  the email text LTR-pinned, the top chrome forced LTR (chevron left, globe
  right).
