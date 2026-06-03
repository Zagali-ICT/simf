# Page 010 — Design (تم التسجيل بنجاح · Registration success)

Flutter screen design for the registration-success confirmation. Visual source
is `Mockup.html` page 10; behaviour is in [Page_010_Logic.md](Page_010_Logic.md).

## Layout
A single, vertically-centred confirmation column — no scrolling list, no form.

```
┌──────────────────────────────────────┐
│                                      │
│            ✓  (success mark)          │   ← success illustration / check icon
│                                      │
│        تم التسجيل بنجاح               │   ← title (AR/EN)
│      Registration success            │
│                                      │
│   تم استلام طلبك وهو قيد المراجعة      │   ← body: pending-approval message
│   Your request is under admin review │
│                                      │
│   ┌──────────────────────────────┐   │
│   │     الذهاب إلى تسجيل الدخول     │   │   ← primary button
│   └──────────────────────────────┘   │
│        تحديث الحالة (optional)        │   ← secondary, only if poll wired
│                                      │
└──────────────────────────────────────┘
```

## Components
| Component | Role |
|---|---|
| Success illustration / check icon | Immediate visual confirmation of success |
| Title text | AR **تم التسجيل بنجاح** / EN **Registration success** |
| Body text | Pending-approval / wait-for-admin message (AR + EN) |
| Primary button | "Go to sign in" → sign-in screen (Page 005) |
| Secondary action (optional) | "Refresh status" — shown only when the status poll (API E1, TO BUILD) is enabled |

## States
| State | Appearance |
|---|---|
| **Success (default)** | The full confirmation — illustration + title + body + primary button. This is the normal, always-shown state. |
| **Loading** | Only relevant if the optional status poll runs: a quiet inline/secondary spinner near the "Refresh status" action. The base confirmation never blocks behind a loader. |
| **Empty** | Not applicable — no list/data. |
| **Error** | Status-poll failure is **non-blocking** — keep the confirmation visible, surface at most a quiet notice; the primary "Go to sign in" stays usable (Logic error-handling). |

## RTL / localization
- Both **AR** and **EN** strings are first-class; no hard-coded text.
- Arabic locale mirrors the entire column: text alignment, illustration position,
  and button content direction all flip to RTL.
- The success mark and spacing stay centred in both directions.

## Navigation
- Entered as a **replacement** of the Page 009 form (Logic L-4) — the sign-up
  steps are removed from the back stack.
- Primary button → sign-in screen (Page 005), pure client navigation.
- If the poll is wired and the account becomes **Approved**, the screen routes
  forward into the signed-in home automatically (Logic L-3).

## Notes
- No inline styles / hard-coded colours — use the app theme tokens and the
  shared component conventions (per project UI rules).
- Keep the screen offline-safe: the base confirmation must render with no network.
