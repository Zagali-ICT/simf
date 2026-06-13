# Page 010 — Design (تم التسجيل بنجاح · Registration success)

Flutter screen design for the registration-success confirmation. Visual source
is the KSA-Project Figma frame **505:1451** (D-366); behaviour is in
[Page_010_Logic.md](Page_010_Logic.md).

> Last updated: 2026-06-13 — conformance pass to the as-built code (D-366 / D-369 / D-373).
>
> **As-built (KSA-Project redesign — D-366, Figma 505:1451; tiles wired D-369;
> real reference D-373):** navy `navySurface` surface + decorative rotated
> sweep; custom header band (back chevron + centred **تم التسجيل** — no
> Material app bar); a 104 px `navyDeep` circle with a `#22C55E` green ring +
> check; the success headline and the frame's two-line review copy; the
> **reference card** (`#01132D` 80%, radius 8) — renders the **real DB-issued
> `SIMF-YYYY-NNNNNNNN` reference** carried from the save via the route extra
> (D-373 superseded the D-366 always-masked rule); the literal `SIMF-2026-xxxx`
> mask remains only as the no-data fallback so the page stays offline-safe;
> gold **حالة التسجيل** + accent-outlined **الانتقال للرئيسية** (white text);
> the **تواصل معانا** phone/mail tiles (`#253660` border, radius 10) — **wired
> via config** (D-369): non-empty `BuildConfig.supportPhone`/`supportEmail`
> opens the OS dialer/mail through `url_launcher`, empty keeps the tile inert;
> the `@SIMF_RSNF` footer. Zero-API contract unchanged; the old screen is
> parked in `lib/features/_legacy_mockup/`.

## Layout
A single confirmation column (max width 400, centred) inside a
`SingleChildScrollView`, under a fixed 56 px header band.

```
┌──────────────────────────────────────┐
│  ‹            تم التسجيل              │   ← header band: back chevron + centred title
│                                      │
│            ⊙ ✓ (green ring)           │   ← 104px navyDeep circle, #22C55E ring + check
│                                      │
│        تم التسجيل بنجاح               │   ← success headline
│   تم استلام طلبك ومراجعته              │   ← two-line review copy
│   ستصلك رسالة تأكيد على بريدك الإلكتروني. │
│                                      │
│   ┌──────────────────────────────┐   │
│   │     رقم البطاقة المرجعي        │   │   ← reference card (#01132D 80%, r8)
│   │      SIMF-2026-00000001      │   │   ← real reference (D-373) or mask fallback
│   └──────────────────────────────┘   │
│   ┌──────────────────────────────┐   │
│   │          حالة التسجيل          │   │   ← gold FilledButton → Page 011
│   └──────────────────────────────┘   │
│   ┌──────────────────────────────┐   │
│   │       الانتقال للرئيسية        │   │   ← accent-outlined button → home
│   └──────────────────────────────┘   │
│            تواصل معانا                │
│   ┌── 📞 ──┐        ┌── ✉ ──┐        │   ← contact tiles (#253660 border, r10, D-369)
│   └────────┘        └───────┘        │
│   ‎@SIMF_RSNF · الملتقى البحري السعودي الدولي │   ← footer
└──────────────────────────────────────┘
```

## Components
| Component | Role |
|---|---|
| Header band (56 px) | Back chevron (`arrow_back_ios_new`, forced LTR glyph) — pops if possible, else goes home; centred title AR **تم التسجيل** / EN **Registered** |
| Decorative sweep | Rotated (28.28°) translucent white block (`0x0AFFFFFF`, radius 40), top-right (Figma 505:1453) |
| Success mark | 104 px `navyDeep` circle, `#22C55E` 2.4 px ring + 40 px `check_rounded` (screen-local green — intentionally not `SimfTokens.success`) |
| Headline | AR **تم التسجيل بنجاح** / EN **Registration success** (24 w700, white) |
| Review copy | The frame's two-line message (14, `beigeBorder`, height 1.5) — exact strings in Function |
| Reference card | `#01132D` at 80%, radius 8; label AR **رقم البطاقة المرجعي** / EN **Reference badge number** (`beigeBorder`); value in `accent` gold, w700, **forced LTR** — the real `referenceNumber` route extra, or `SIMF-2026-xxxx` when null (D-373) |
| Primary button | Gold `FilledButton` **حالة التسجيل / Registration status** → Page 011 |
| Secondary button | `OutlinedButton`, `accent` border + white text, `radiusSmall`, height 48 — **الانتقال للرئيسية / Go to home** → home |
| Contact tiles | Two bordered tiles (`#253660`, 0.8 px, radius 10, height 52, white icons `call_outlined` / `mail_outline`, Figma 522:2223) under **تواصل معانا / Contact us** — config-gated `tel:` / `mailto:` launch (D-369) |
| Footer | **@SIMF_RSNF · الملتقى البحري السعودي الدولي** (12, `beigeBorder`) |

## States
| State | Appearance |
|---|---|
| **Success with reference (default in-flow)** | The full confirmation; the reference card shows the real `SIMF-YYYY-NNNNNNNN` value carried from the save (D-373). |
| **Success, mask fallback** | Route extra absent (offline / out-of-flow arrival): identical screen, the card shows the literal `SIMF-2026-xxxx` mask. Never blocks, never fetches. |
| **Contact tile — active** | `BuildConfig.supportPhone` / `supportEmail` non-empty: tap opens the OS dialer / mail app (best-effort; a failed launch is swallowed, the user stays put). |
| **Contact tile — inert** | Config value empty (the current default — owner values pending): `onTap` is null, the tile renders but does nothing. |
| **Loading / Error / Empty** | Not applicable — the screen makes no network call and renders no list. |

## RTL / localization
- All strings come from `AppL10n` (`app_l10n.dart`) — AR and EN first-class, no
  hard-coded text.
- The column is centred, so AR/EN render symmetrically; the back chevron icon
  glyph is pinned `TextDirection.ltr` and the reference value is pinned
  `TextDirection.ltr` so the `SIMF-…` code never visually reverses in Arabic.

## Navigation
- Entered as a **`goNamed` replacement** from the Page 007-01 interests save
  (Logic L-4) — the sign-up steps are off the back stack; the reference number
  rides in as the route extra.
- Header chevron: `pop()` when the stack allows, otherwise `go('/')`.
- Primary button → Page 011 (`registrationStatus`); outlined button → home (`/`).
- There is **no auto-advance poll** — watching the status is Page 011's job.

## Notes
- Theme tokens used: `SimfTokens.navySurface`, `navyDeep`, `beigeBorder`,
  `accent`, `radiusSmall`. Four screen-local colours are deliberate (Figma
  values not yet shared by a second screen): `#22C55E` green, `#253660` tile
  border, `#01132D` @80% card fill, `0x0AFFFFFF` sweep tint.
- Keep the screen offline-safe: the confirmation renders with no network; the
  mask fallback exists exactly for that case.
