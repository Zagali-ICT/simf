# Page 004 — إنشاء حساب — النوع · Sign up — type

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_004_Function.md](Page_004_Function.md) | What the page does — elements, user actions, navigation, acceptance criteria |
| Logic | [Page_004_Logic.md](Page_004_Logic.md) | Business rules — type gating, why App accounts are Visitor-only, state transitions, edge cases |
| API | [Page_004_API.md](Page_004_API.md) | Backend contract for this page — there is **no** SIMF API; this is a client-only UI gate |
| Design | [Page_004_Design.md](Page_004_Design.md) | Flutter screen design — layout, components, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **4** (`Mockup.html`) · owner page **004** |
| Route | `RouteNames.signUpType` → `/sign-up/type` |
| Titles | AR **إنشاء حساب — النوع** · EN **Sign up — type** |
| Section | 1 — Onboarding / authentication |
| Nature | **Account-type chooser** (a UI gate, not a data step) |
| App privilege | **Guest** (pre-auth — reachable before any account exists) |
| Status | **Built** (Flutter screen — client-only type gate, **no API**; Visitor enabled, Exhibitor/Sponsor disabled per D-199; Continue → Page 005 carrying `type=visitor`) · D-268 |

## Purpose
Lets a guest choose which kind of account to create. In SIMF, **App accounts are
Visitor-only** — exhibitor and sponsor are **Control-Panel concepts** (CP-managed
Company + accounts, D-199), never self-registered from the App. So in the shipped
App this screen is **largely a UI gate**: the visitor path is the only one that
proceeds, leading to the sign-up form on **[Page 005](../Page_005/README.md)**.
There is **no SIMF API** call on this screen.

## Mockup divergence (bug-check finding, D-268)
The `Mockup.html` layout splits the sign-up flow differently from this doc:
mockup **screen 04** is an email/password/confirm **credentials** form, and the
account-**type** chips (`زائر` / `جهة عارضة`) are embedded at the **top of mockup
screen 05**'s profile form — there is no standalone type-chooser frame in the
mockup. This controlled doc **restructures** that into a dedicated type gate
(screen 4) + form (screen 5), grounded in **D-199** (Visitor-only self-registration;
Exhibitor/Sponsor are CP-only). The **doc governs** (CLAUDE.md: controlled docs
override the mockup, which is a visual reference); the built screen follows the doc.
The final visuals come from the external designer (SIMF-VID-001) regardless.

## Sources of truth
`Mockup.html` (visual, pages 4–5) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 4) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> Part of the per-page documentation structure (`docs/App/Page_NNN/`). The
> exhibitor/sponsor = CP-only rule is recorded in `docs/decisions/DECISIONS_LOG.md`
> (D-199); the four App privileges (Guest/Visitor/Moderator/Staff) are the App's
> own enum, separate from the CP `UserType`.
