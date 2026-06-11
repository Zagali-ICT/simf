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
| Status | **Flutter screen BUILT (D-296)**; privilege-gated tiles + best-effort bell badge (`…/notifications/unread-count`); on-login bundle `GET /app/bootstrap` **BUILT (D-251)** |

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 13) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> **Owner-ref note:** the owner calls this screen **"Page 012"** in conversation,
> but it is router screen **#13** (`path=/`) in `Mockup.html`. This folder is keyed
> to the mockup number (013) to stay consistent with the rest of `docs/App/Page_NNN/`.
>
> Home is a **privilege-gated landing**. It needs **no login** to open and carries
> **no data of its own for now**. What it shows is shaped by the app privilege
> (`Guest`/`Visitor`/`Staff`/`Moderator`), which comes from the **JWT claim**. On a
> successful login the app fetches **all data + privileges once** and caches them
> (the on-login bootstrap bundle, `GET /app/bootstrap`, is **BUILT** — D-251).

## As-built (D-296)

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
