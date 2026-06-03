# Page 005 — Function (إنشاء حساب · Sign up)

What the page does, the elements on it, the user actions, the navigation, and
the acceptance criteria. The contract that backs these actions is in
[Page_005_API.md](Page_005_API.md); the rules behind them are in
[Page_005_Logic.md](Page_005_Logic.md).

## Purpose
Sign-up **step 1**. The visitor supplies the three credentials needed to create
an account. On success the server provisions a new **Visitor** account that has
**no privilege**, is **under review**, and **still has to complete its profile**,
then emails a **6-digit OTP** to the address so the next screen can verify it.

## Privilege / auth gate
| | |
|---|---|
| Who reaches it | **Guest** (unauthenticated). Reachable from the sign-in screen ("Create account"). |
| Already signed in | A signed-in user is routed to their area, not here. |
| What it grants | Nothing yet — it **creates** an account; it does not sign the user in. |
| Result account | **Visitor**, no privilege, `AccountState = UnderReview`, profile incomplete. |

## On-screen elements
| Element | Type | Notes |
|---|---|---|
| Title | Text | AR **إنشاء حساب** · EN **Sign up** |
| Email | Text field | Email keyboard; trimmed + lower-cased before submit |
| Password | Password field | Obscured; show/hide toggle |
| Confirm password | Password field | Obscured; show/hide toggle; must equal Password |
| Submit | Primary button | "Create account / إنشاء حساب"; disabled while submitting |
| Go to sign in | Text link | Back to the sign-in screen for existing users |

## User actions (step by step)
1. User opens the screen from "Create account" on the sign-in page.
2. User types an **email**.
3. User types a **password**.
4. User types the **confirm password**.
5. User taps **Create account**.
6. Client validates locally (format, length, match). On any failure it shows the
   field error and stops — no call is made.
7. On valid input the client calls `POST /app/auth/sign-up` (see API doc).
8. **On success (always a generic 201):** the app navigates to the **OTP /
   "check your email" screen**. This is the **same** screen whether the email is
   new or already registered (D-198 — no `409`, no "you already have an account").
9. **On a network / 5xx error:** the app shows a generic retry toast and keeps
   the form so the user can retry.

## Navigation
| Trigger | Goes to |
|---|---|
| Successful submit (generic 201) | OTP / verify-email screen (step 2) |
| "Go to sign in" link | Sign-in screen |
| System back | Sign-in screen |

## Acceptance criteria
- Submit is blocked until email, password, and confirm are all locally valid.
- Confirm-password mismatch is caught client-side and never sent.
- A successful sign-up always lands on the **generic OTP / check-your-email**
  screen, identically for a new and an already-registered email (D-198).
- The created account is a **Visitor**, no privilege, **under review**, profile
  incomplete — the user is **not** signed in by this screen.
- Email + password fields render and align correctly in **RTL (Arabic)**.
