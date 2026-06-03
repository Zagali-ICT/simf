# Page 010 — Logic (تم التسجيل بنجاح · Registration success)

Business rules and behaviour for the registration-success confirmation. The
backend contract is in [Page_010_API.md](Page_010_API.md).

## Nature
This is a **transitional confirmation** screen. It owns **no write operation**
of its own — the account was already created by the Page 009 submit. Its job is
to display the *pending-approval* result and route the user onward.

## State model
The just-registered account is in a **pending-approval** state
(`AccountState` not yet `Approved`). The lifecycle the user sits inside here:

```
Page 009 submit  ──success──▶  [Pending approval]  ──admin approves──▶  [Approved]
       │                              │                                      │
   account created             Page 010 shown                     full app access
```

## Rules
| # | Rule |
|---|---|
| **L-1** | The screen is shown **only** on a successful Page 009 submit. It is never a landing/deep-link target. |
| **L-2** | The account is **pending**; the user is told to wait for admin approval. No retry of the registration is offered from here. |
| **L-3** | **Optional status poll.** If wired, the screen may poll the user's own status (see API E1, **TO BUILD**). On a transition to *Approved*, route the user forward (into the signed-in home) without forcing a fresh sign-up. While still pending, keep showing the confirmation. |
| **L-4** | Navigation is a **replacement** — the sign-up form is removed from the back stack so the user cannot edit a submitted profile by pressing back. |
| **L-5** | The primary "Go to sign in" action always works offline-safe — it is pure client navigation, no network call. |

## Client logic
- On entry: render the static confirmation (title, illustration, body, primary
  button). No fetch is required for the base screen.
- If the status poll is enabled: start a **bounded** poll (interval-based, with a
  stop condition / cap) of E1; stop polling when the screen is left or when
  *Approved* is observed. Never poll forever and never tight-loop.
- Primary button → navigate to the sign-in screen (client-only).

## Server logic
- None owned by this screen for the base flow. The account was persisted by the
  Page 009 submit; approval is an **admin-side** action elsewhere (Control Panel).
- If the status poll ships, the server simply returns the current account state
  for the caller's own `sub` (E1, **TO BUILD**).

## Validation
- No input fields → no field validation on this screen.

## Error / empty / RTL handling
| Case | Behaviour |
|---|---|
| **Base render** | Always succeeds — static content, no network dependency. |
| **Status poll fails / offline** | Silently keep showing the confirmation; do not block the user. The primary "Go to sign in" stays available. Surface at most a quiet, non-blocking notice. |
| **Empty** | Not applicable — there is no list/data to be empty. |
| **RTL** | Arabic locale mirrors the full layout (text alignment, illustration, button direction). Both AR and EN strings are first-class. |

## Dependencies
- **Page 009** (profile completion) — the only legitimate entry point.
- **Sign-in screen** (Page 005) — the primary forward navigation target.
- **Account-status read (E1, TO BUILD)** — only needed if the optional auto-
  advance poll is enabled; the base screen works without it.
