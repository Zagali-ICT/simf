# Page 013 — الرئيسية · Home (router screen)

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_013_Function.md](Page_013_Function.md) | What the page does — elements, user actions, navigation, privilege gate, acceptance criteria |
| Logic | [Page_013_Logic.md](Page_013_Logic.md) | Client + server logic, state transitions, privilege gating, validation, error/empty/RTL handling |
| API | [Page_013_API.md](Page_013_API.md) | The backend calls behind this page (authoritative contract) |
| Design | [Page_013_Design.md](Page_013_Design.md) | Flutter screen design — layout, components, states, localization, RTL |

## Identity
| | |
|---|---|
| Mockup page | **13** (`Mockup.html`) — owner refers to it as **"Page 012"** |
| Route | `RouteNames.home` → `/` |
| Titles | AR **الرئيسية** · EN **Home** |
| Section | 1 — Entry / router screen |
| Nature | **Home landing** (router screen 13; entry surface after boot) |
| App privilege | **All privileges** — Guest / Visitor / Staff / Moderator. **No login required.** |
| Status | **Flutter screen BUILT (D-296), redesigned to the KSA Wave-2 frames (D-378, 2026-06-13)**; guest vs signed-in layouts + best-effort bell badge (`…/notifications/unread-count`); on-login bundle `GET /app/bootstrap` **BUILT (D-251) but not called by the shipped app** |

## Sources of truth
**KSA-Project Figma frames 512:1492 (guest) + 203:1236 (signed-in)** (visual, D-378) ·
`docs/SIMF-App-Redesign-Program.md` (W2-2/W2-3 rows) ·
`SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 13) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture).
`Mockup.html` page 13 is the **superseded** pre-redesign visual.

> **Owner-ref note:** the owner calls this screen **"Page 012"** in conversation,
> but it is router screen **#13** (`path=/`) in `Mockup.html`. This folder is keyed
> to the mockup number (013) to stay consistent with the rest of `docs/App/Page_NNN/`.
>
> Home is a **privilege-gated landing**. It needs **no login** to open and carries
> **no data of its own** beyond the best-effort unread count. What it shows is
> shaped by the app privilege (`Guest`/`Visitor`/`Staff`/`Moderator`), read from
> the **cached auth session** (`AuthStateSignedIn.session.user.appRole`; no
> session ⇒ `Guest`). The session is cached at **sign-in**; the on-login bootstrap
> bundle `GET /app/bootstrap` is **BUILT (D-251)** on the backend but the shipped
> app does **not call it**.

## As-built (D-296) — superseded by the D-378 redesign below

The Flutter `HomeScreen` (`features/home/home_screen.dart`) replaces the
`ComingSoonScreen` placeholder. It reads the **privilege from the cached auth
state** (`AuthStateSignedIn.session.user.appRole`, else `Guest`) and paints an
interim functional layout: a Discover header, a static **LIVE banner** (no API —
D10), a **Guest-only** sign-in prompt, and a **privilege-gated tile grid** (public
tiles for everyone + Visitor+ tiles when signed in) wired to the existing routes.
The bell badge (signed-in only) reads the **best-effort** unread count via
`NotificationsRepository.getUnreadCount()` → `unreadNotificationCountProvider`
(guest/any error → `0`, silent). The rich-mockup pieces with no backing
(bilateral-meetings tile, social feed, bottom-nav shell) are intentionally omitted
— final visuals come from SIMF-VID-001. Tests: `home_screen_test.dart` (6) +
`notifications_repository_test.dart` (3).

## As-built — KSA Wave-2 redesign (D-378)

The screen was rebuilt to the delivered KSA-Project frames — **guest =
512:1492** (the owner-picked 2×2 option) and **signed-in = 203:1236** — on the
shared shell (`KsaPage` + `SimfBottomNav` v2 + `KsaNavTile`/`KsaListRow`/
`KsaSectionHeader`, `lib/app/widgets/ksa_shell.dart`). One route, two layouts
off the cached privilege:

- **Guest:** "الرئيسية • ضيف" header, the gold-highlight browse banner, 2×2
  public tiles (الجلسات / المتحدثون / الخريطة / المعرض → booths), the **locked
  بطاقتي card** (disabled palette, inert), the "معلومات مفتوحة للجميع" rows
  (**FAQ → the About page** — no app FAQ endpoint exists yet, tracked
  follow-up; **روح السعودية** → the configured Visit-Saudi link), and the gold
  sign-in button.
- **Signed-in:** greeting header (avatar initials, time-of-day greeting, name,
  bell + unread badge, menu), the static red LIVE banner (D10 unchanged),
  three tile sections (عن الملتقى / الأخبار والتغطية / الميزات الذكية), the
  **تابعنا** row (5 brand buttons — config-driven URLs, inert while unset per
  the D-369 contract), and the discover روح السعودية card. The frame's
  "أحدث منشوراتنا" X-embed card is **omitted** (no API — owner-approved).

The bottom nav (all pages) swapped the News tab for **Profile** per the frames
(owner-approved); News keeps the bar with no active tab. The old mockup screen
+ test are parked in `_legacy_mockup/`. Tests: `home_screen_test.dart` (11) +
`ksa_shell_test.dart` (11).
