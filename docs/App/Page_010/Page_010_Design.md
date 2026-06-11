# Page 010 — Design (تم التسجيل بنجاح · Registration success)

Flutter screen design for the registration-success confirmation. Visual source
is `Mockup.html` page 10; behaviour is in [Page_010_Logic.md](Page_010_Logic.md).

> **As-built (KSA-Project redesign, 2026-06-11 — D-366, Figma 505:1451):**
> navy `navySurface` surface + decorative sweep, custom header (back chevron +
> centred **تم التسجيل** — no Material app bar); a 104 px `navyDeep` circle
> with a `#22C55E` green ring + check; the success headline and the frame's
> two-line review copy; the **masked reference card** (`#01132D` 80%, radius 8 —
> shows the design's literal `SIMF-2026-xxxx`; owner decision: no fetch, the
> page stays offline-safe, the real reference surfaces on badge/status);
> gold **حالة التسجيل** + accent-outlined **الانتقال للرئيسية** (white text);
> the **تواصل معانا** phone/mail tiles (`#253660` border, radius 10 —
> **visual-only**, owner decision, wiring tracked on the programme board);
> the `@SIMF_RSNF` footer. No-API contract unchanged; the old screen is parked
> in `lib/features/_legacy_mockup/`.

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
│   │          حالة التسجيل          │   │   ← primary button → Page 011
│   └──────────────────────────────┘   │
│         الانتقال للرئيسية            │   ← ghost/secondary → home
│                                      │
└──────────────────────────────────────┘
```

## Components
| Component | Role |
|---|---|
| Success illustration / check icon | Immediate visual confirmation of success |
| Title text | AR **تم التسجيل بنجاح** / EN **Registration success** |
| Body text | Pending-approval / wait-for-admin message (AR + EN) |
| Primary button | "Registration status" → Page 011 (registrationStatus) |
| Secondary action (ghost) | "Go to home" → home screen |

## States
| State | Appearance |
|---|---|
| **Success (default)** | The full confirmation — illustration + title + body + primary button. This is the normal, always-shown state. |
| **Loading** | Only relevant if the optional status poll runs: a quiet inline/secondary spinner. The base confirmation never blocks behind a loader. |
| **Empty** | Not applicable — no list/data. |
| **Error** | Status-poll failure is **non-blocking** — keep the confirmation visible, surface at most a quiet notice; the action buttons stay usable (Logic error-handling). |

## RTL / localization
- Both **AR** and **EN** strings are first-class; no hard-coded text.
- Arabic locale mirrors the entire column: text alignment, illustration position,
  and button content direction all flip to RTL.
- The success mark and spacing stay centred in both directions.

## Navigation
- Entered as a **replacement** of the Page 009 form (Logic L-4) — the sign-up
  steps are removed from the back stack.
- Primary button → Page 011 (registrationStatus); ghost button → home screen.
- If the poll is wired and the account becomes **Approved**, the screen routes
  forward into the signed-in home automatically (Logic L-3).

## Notes
- No inline styles / hard-coded colours — use the app theme tokens and the
  shared component conventions (per project UI rules).
- Keep the screen offline-safe: the base confirmation must render with no network.
