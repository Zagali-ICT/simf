# Page 004 — Logic (إنشاء حساب — النوع · Sign up — type)

Business rules for the account-type chooser. Function/elements are in
[Page_004_Function.md](Page_004_Function.md); the (non-)contract is in
[Page_004_API.md](Page_004_API.md).

## Why this screen is a UI gate, not a data step
- **App accounts are Visitor-only.** Self-registration from the mobile App creates a
  **Visitor** account and nothing else.
- **Exhibitor and sponsor are Control-Panel concepts** (CP-managed Company + accounts,
  D-199). They are never created by App self-registration. Showing them here is purely
  to set the user's expectation and route them correctly.
- Therefore this screen makes **no server decision** and stores **no server state**.
  Its only job is to confirm `type = Visitor` and forward it to the sign-up form.

## Privilege / auth gate
| | |
|---|---|
| Entry privilege | **Guest** — reachable before any account or token exists. |
| Auth required | **None.** No token, no `Authorization` header, no API call. |
| Result on exit | Carries the in-memory selection `type = Visitor` to Page 005. |

> The App's four privileges are **Guest / Visitor / Moderator / Staff** (the App's own
> enum, separate from the CP `UserType`). This screen runs entirely at **Guest**.

## Client logic / state machine
```
[Enter]
   selection = none
   Visitor = enabled+selectable
   Exhibitor = disabled (info)
   Sponsor   = disabled (info)
   Continue  = DISABLED

[Tap Visitor]      → selection = Visitor ; Continue = ENABLED
[Tap Exhibitor]    → selection unchanged ; show CP-only note ; Continue unchanged
[Tap Sponsor]      → selection unchanged ; show CP-only note ; Continue unchanged
[Tap Continue]     → if selection == Visitor: navigate Page 005 (type=Visitor)
                     else: show "Choose an account type" hint ; stay
[Back]             → pop to previous screen ; selection discarded
```

## Validation
| Rule | Behaviour |
|------|-----------|
| V1 — A type must be selected before Continue | Continue stays disabled; if forced, inline hint "اختر نوع الحساب / Choose an account type". |
| V2 — Only Visitor is a valid selection | Exhibitor/Sponsor cannot be selected (disabled controls), so no invalid type can ever leave this screen. |

## Data sources
- **None.** No SIMF API, no local DB read, no cached lookup. The type list is a
  **static, in-code constant** (Visitor enabled; Exhibitor/Sponsor disabled/info).

## Edge cases
| Case | Handling |
|------|----------|
| No network | Screen is fully functional offline — it issues no request. |
| User taps a disabled option repeatedly | Re-shows the CP-only note; no state change, no error. |
| User backs out and returns | Selection resets to none; idempotent. |
| Deep-link straight to `/sign-up/type` | Allowed at Guest; behaves identically. |
| Already signed in (token present) | Out of normal flow; the App should route a signed-in user away from sign-up — this screen does not itself call the API to check. |

## RTL / localization
- Both label sets come from app **resources** (AR + EN). No hard-coded strings.
- In Arabic the whole screen mirrors (RTL): app-bar back affordance and option-row
  chevrons flip side; text aligns right.

## Dependencies
- **Page 005** (`/sign-up`) — the only forward destination; receives `type = Visitor`.
- **Page 003** (`/sign-in`) — back/return destination.
- Decision **D-199** — exhibitor/sponsor are CP-only (the rule this screen enforces visually).
