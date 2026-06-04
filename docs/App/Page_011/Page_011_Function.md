# Page 011 — Function (حالة التسجيل · Registration status)

What this page does, who reaches it, and the step-by-step user flow. Business rules
(status mapping, polling, transitions) are in [Page_011_Logic.md](Page_011_Logic.md);
the backend contract is in [Page_011_API.md](Page_011_API.md).

## Privilege / auth gate
| | |
|---|---|
| Who sees it | A user who is **signed in**, has a **ready profile**, but is **NOT yet approved** |
| App privilege | Pending account (below Visitor) — i.e. registration in progress |
| Auth | Requires a valid signed-in session (bearer token). NOT `AllowAnonymous`. |
| Entry condition | The boot / sign-in flow resolves `registrationStatus != Approved` and routes here |
| Exit condition | `registrationStatus == Approved` → app leaves this screen for the main experience |

This is a **gate screen**: an approved user must never be parked here, and an
unauthenticated user must never reach it (they belong on sign-in). The app's router
owns that gating; this page only renders the current status it is given.

## Elements on the page
| Element | Purpose |
|---------|---------|
| Status illustration / icon | Visual cue per state — pending (hourglass), approved (check), rejected (cross) |
| Status headline | Bilingual title for the current state (e.g. "حالتك قيد المراجعة" / "Your account is under review") |
| Status message | One short explanatory line per state |
| Stages tracker (المراحل · Stages) | Static four-step progress tracker reflecting the registration stages — 1) إرسال البيانات (Data submitted) · 2) تأكيد البريد الإلكتروني (Email confirmed) · 3) مراجعة فريق SIMF (SIMF team review) · 4) تفعيل الحساب (Account activation). The current step is driven by `registrationStatus`: steps 1–2 complete throughout, step 3 (review) is current while `Pending`, and all four show complete on `Approved`. |
| Approval reference + date | **Decoration only (D11)** — not backed by the API; shown as static layout if present |
| Re-check / refresh button | Pending state: re-calls `GET /app/users/me` to pull the latest status |
| Continue button | Approved state: proceeds into the app |
| Sign-out link | Lets the user leave the pending session |

## User actions (step by step)
1. **Arrive on the screen.** The app has already signed the user in and detected that
   `registrationStatus` is not `Approved`, so it shows this page.
2. **The page loads the status.** On open it calls `GET /app/users/me` and reads
   `registrationStatus` (one of `Approved` / `Pending` / `Rejected`).
3. **The page renders the matching state:**
   - **Pending** → "under review" message + a **Re-check** button.
   - **Approved** → "you're approved" message + a **Continue** button.
   - **Rejected** → "not approved" message (and whatever recovery copy the design
     specifies).
4. **Pending — the user taps Re-check.** The page re-calls `GET /app/users/me`. If the
   status is still `Pending`, it stays; if it is now `Approved`, it transitions to the
   approved state; if `Rejected`, it shows the rejected state.
5. **Approved — the user taps Continue.** The app routes out of this screen into the
   main experience (the router re-evaluates the now-approved privilege).
6. **At any time — the user can sign out** to abandon the pending session.

## Navigation
| From | To | Trigger |
|------|----|---------|
| Sign-in / boot flow | **Registration status (011)** | session resolves with `registrationStatus != Approved` |
| Registration status (011) | Main experience | `registrationStatus == Approved` → **Continue** |
| Registration status (011) | Sign-in | **Sign out** |

## Acceptance criteria
- A signed-in, **non-approved** user landing here sees the correct state for their
  `registrationStatus` value.
- The **Re-check** button re-fetches `GET /app/users/me` and re-renders the state from
  the fresh value with no app restart.
- When the status is/becomes **Approved**, the user can leave the screen via
  **Continue** and is never returned to it.
- A **Rejected** status renders the rejected state, not a pending or approved one.
- The approval **reference number + date** is presentational only (D11) and is never
  expected to carry real data from the API.
- The screen never appears for an **already-approved** user or for an
  **unauthenticated** visitor.
