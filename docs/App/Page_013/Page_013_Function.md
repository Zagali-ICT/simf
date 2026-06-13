# Page 013 — Function (الرئيسية · Home)

What the user does on this screen, step by step, and the privilege/auth gate that
shapes it. Visual + state detail is in [Page_013_Design.md](Page_013_Design.md);
the rules behind each decision are in [Page_013_Logic.md](Page_013_Logic.md).

## Purpose
Home is the app's **router/landing screen (#13, `path=/`)**. It is the surface the
user lands on after boot. It **requires no login** and carries **no data of its own**
beyond the best-effort unread-notification count — its content is shaped by the
**cached app privilege**: one route, **two layouts** (guest vs signed-in, the KSA
frames 512:1492 / 203:1236 — D-378).

## Privilege / auth gate
| | |
|---|---|
| Login required to open | **No** — Home opens for everyone, including `Guest` (signed-out). |
| Privilege source | The **cached auth session** (`AuthStateSignedIn.session.user.appRole`). No session ⇒ `Guest`. No per-screen fetch. |
| What the privilege controls | Which **layout** renders: the guest layout, or the signed-in greeting layout (same for `Visitor`/`Staff`/`Moderator` — see Logic L-2). |
| Data fetched by this screen | Only the **unread-notification count** (signed-in, best-effort, non-blocking). |
| On-login caching | The session (user + privilege) is cached at **sign-in**; Home reads that cache. The backend on-login bundle `GET /app/bootstrap` is **BUILT (D-251)** but the shipped app does **not call it** — Home needs nothing beyond the sign-in session (Logic L-3). |

## Elements

### Guest layout (frame 512:1492)
| Element | Description | Action |
|---|---|---|
| Header | Title AR **الرئيسية • ضيف** / EN **Home • Guest**, circled back chevron | Back pops; with nothing to pop it opens the **sign-in** screen |
| Guest banner | «أنت تتصفح كضيف…» navy card with the gold **البطاقة الذكية** highlight | — (not tappable) |
| 2×2 public tiles | **الجلسات** / **المتحدثون** / **الخريطة** / **المعرض** | Tap → sessions / speakers / venue map / booths (exhibition) |
| Locked **بطاقتي** tile | Disabled-palette badge card — signing in unlocks it | **None** — never tappable as a guest |
| Section **معلومات مفتوحة للجميع** | Two open-info rows | — |
| FAQ row | **الأسئلة الشائعة** · «FAQ • معلومات الموقع والفعالية» | Tap → the **About the forum** page (no app FAQ endpoint exists yet — tracked follow-up, D-378) |
| **روح السعودية** row | «Visit Saudi · استكشف الرياض», "KSA" badge | Tap → opens the configured Visit-Saudi URL in the external browser |
| Sign-in button | Gold full-width **تسجيل الدخول** | Tap → the sign-in screen |

### Signed-in layout (frame 203:1236) — all signed-in roles
| Element | Description | Action |
|---|---|---|
| Greeting header | Avatar (photo/initials), **صباح الخير**/**مساء الخير** + name `👋` | — |
| Bell + unread badge | `Badge.count` from `GET /app/account/notifications/unread-count` (hidden when `0`) | Tap → notifications screen |
| Menu button | Trailing hamburger | Tap → the **More** screen |
| LIVE banner | **مباشر** box + «الجلسة الافتتاحية تُبث الآن» — static config, **no API (D10)** | Tap → the live-broadcast screen |
| Section **عن الملتقى · المحاور** (+ **المزيد** → About) | 3 tiles: **المتحدثون** / **الأجنحة** / **الرعاة** | Tap → speakers / booths / sponsors |
| Section **الأخبار والتغطية** (+ **المزيد** → News) | 2 tiles: **اللقاءات الثنائية** / **الأرشيف** | Tap → gallery / archive |
| Section **الميزات الذكية** (+ **المزيد** → More) | 4 tiles: **قابل أشخاص مثلك** / **المساعد الذكي** / **ملخص الجلسات** / **بطاقتي الذكية** | Tap → meet-people / chatbot / AI summary / smart badge |
| Section **تابعنا** | 5 brand buttons (X / Instagram / LinkedIn / YouTube / TikTok) + handle caption | Tap → opens the configured URL externally; an **unconfigured button is inert** (D-369) |
| Section **اكتشف** | The same روح السعودية row | Tap → the configured Visit-Saudi URL |

### Bottom navigation (both layouts)
**Home (active) · الأجندة · [gold QR centre] · الخريطة · الملف الشخصي** — the QR
centre opens the smart badge; the Profile tab replaced the old News tab
(owner-approved, D-378). Only the active tab shows its label.

## User steps
1. App boots and routes to Home (`/`). No blocking network call is required to render.
2. The app reads the **privilege from the cached auth session** (no session ⇒ `Guest`)
   and paints the matching layout (Logic L-1/L-2).
3. **Signed-in only:** in the background the app reads the **unread-notification
   count** (`GET /app/account/notifications/unread-count`) and paints the bell badge.
   A failure here is silent — the count resolves to `0` and the badge hides (Logic L-5).
4. The **LIVE banner** (signed-in) renders from static config (no API, D10).
5. The user taps a tile / row / bell / menu / nav tab / the sign-in button and
   navigates on; social and Visit-Saudi rows open the **external browser**
   (best-effort — a missing handler never crashes the page, Logic L-7).

## Navigation
- **From:** boot/splash, the bottom-nav Home tab, or any back-to-home action.
- **To (guest):** sessions, speakers, venue map, booths, About (FAQ row), the
  external Visit-Saudi link, sign-in.
- **To (signed-in):** notifications, More, live broadcast, About, speakers, booths,
  sponsors, News, gallery, archive, meet-people, chatbot, AI summary, smart badge,
  the external social/Visit-Saudi links.
- **Bottom nav (both):** sessions (Agenda), smart badge (QR), venue map, My area
  (Profile).

## Acceptance criteria
- Home opens with **no login** for every privilege, including `Guest`.
- The layout matches the cached privilege exactly: guest layout for `Guest`, the
  greeting layout for any signed-in role (Logic L-2).
- The notification bell shows the **unread count** when signed in; it degrades
  silently to a hidden badge on error; a guest **never calls** the count endpoint.
- The locked **بطاقتي** tile is visibly disabled and not tappable as a guest.
- A social / Visit-Saudi button with **no configured URL is inert** — never a dead
  or crashing intent (D-369).
- The screen renders correctly with **no data** (nothing on Home blocks on a fetch).
- Full **RTL** in Arabic (guest header chrome stays forced-LTR per D-363); both
  AR/EN labels resolve from localization.
