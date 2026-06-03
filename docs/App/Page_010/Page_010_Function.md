# Page 010 — Function (تم التسجيل بنجاح · Registration success)

What this page does, the elements on it, what the user can do, and how it is reached.

## Purpose
Terminal step of the sign-up journey. After the user completes their profile
(Page 009), the account is created in a **pending-approval** state. This screen
confirms the submission succeeded and tells the user to **wait for an
administrator to approve the account** before full access is granted.

It is a **transitional / confirmation** screen — it presents a result, it does
not collect input.

## How it is reached
| | |
|---|---|
| Entered from | Page 009 (profile completion) on successful submit |
| Trigger | The create/complete-profile call returned success (account now pending) |
| Route | `RouteNames.registrationSuccess` → `/registration/success` |

The screen is pushed as a **replacement** (not stacked) — the back button must
**not** return the user into the multi-step sign-up form.

## Elements
| Element | AR | EN | Notes |
|---|---|---|---|
| Title | تم التسجيل بنجاح | Registration success | Page heading |
| Success illustration / check | — | — | Visual confirmation (check-mark / illustration) |
| Body message | تم استلام طلبك وهو قيد المراجعة من قبل الإدارة | Your request was received and is under admin review | Explains the pending-approval wait |
| Primary action | الذهاب إلى تسجيل الدخول | Go to sign in | Returns to the sign-in screen |
| Secondary (optional) | تحديث الحالة | Refresh status | Only if the status poll is wired (see Logic / API) |

## User actions
1. **Read the confirmation** — no input required.
2. **Go to sign in** — primary button takes the user to the sign-in screen
   (Page 005). They can attempt to sign in; until approved they remain limited /
   blocked per the account-state rules.
3. **Refresh status (optional)** — if the status poll is enabled, re-checks
   whether the account moved from *pending* to *Approved* and routes forward when
   it has.

## Privilege / auth gate
- The user is **signed in but pending approval** — the account exists, the
  session/token may be issued, but `AccountState` is **not yet Approved**.
- No admin permission code applies (this is an App onboarding screen, not a
  Control Panel page).
- The screen itself requires no special permission; it is shown to the just-
  registered user as the result of their own action.

## Acceptance criteria
- [ ] Reached only after a successful Page 009 submit.
- [ ] Back navigation does **not** re-open the sign-up form.
- [ ] Title and body render correctly in **both AR and EN** with full RTL.
- [ ] Primary "Go to sign in" routes to the sign-in screen.
- [ ] If the status poll is wired, a transition to *Approved* routes the user
      forward without forcing a re-sign-in (see Logic L-3).
