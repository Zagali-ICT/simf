# Page 013 — Design (الرئيسية · Home)

Flutter screen design for the Home router screen (#13, `path=/`) — layout,
components, states, localization and RTL. The behaviour is in
[Page_013_Function.md](Page_013_Function.md); the rules are in
[Page_013_Logic.md](Page_013_Logic.md).

> **As-built (D-378, 2026-06-13):** rebuilt to the KSA-Project Figma frames —
> **guest = 512:1492** (the owner-picked 2×2 option) and **signed-in =
> 203:1236** — on the shared shell (`lib/app/widgets/ksa_shell.dart` +
> `lib/app/widgets/simf_bottom_nav.dart`). One route, **two layouts** picked
> off the cached auth privilege (`AppRole.guest` vs any signed-in role).

## Layout

### Guest layout (frame 512:1492 — «الرئيسية • ضيف»)
A `KsaPage` on the navy surface (`SimfTokens.navySurface`) with the decorative
rotated sweep (`showSweep: true`), the standard forced-LTR header (circled back
chevron at the physical left — the D-363 chrome pattern; back pops, or pushes
the sign-in screen when there is nothing to pop), title AR **الرئيسية • ضيف** /
EN **Home • Guest**, and a scrolling `ListView`:
1. **Guest banner** — a navy card with a gold hairline border; beige copy with
   the gold-highlighted phrase **البطاقة الذكية** inside it (frame node 512:1499).
2. **2×2 public tile grid** — two `KsaTileRow`s of `KsaNavTile`s:
   **الجلسات** (calendar icon) · **المتحدثون** (mic icon), then
   **الخريطة** (map icon) · **المعرض** (grid icon).
3. **Locked بطاقتي tile** — a full-width `KsaNavTile` with `enabled: false`
   (disabled palette, badge icon, **never tappable**) — a visual cue that
   signing in unlocks the smart badge.
4. **Section «معلومات مفتوحة للجميع»** (`KsaSectionHeader`) with two
   `KsaListRow`s: the **FAQ row** (gold help-icon badge box) and the
   **روح السعودية** discover row (gold "KSA" text badge box).
5. **Gold sign-in button** — a full-width `FilledButton` labelled
   **تسجيل الدخول**.

### Signed-in layout (frame 203:1236 — greeting home)
A `KsaPage` with a **custom header row** (no back button, no sweep) and a
scrolling `ListView`:
1. **Greeting header** (frame node 203:1238) — `KsaAvatar` (gold rounded
   square; photo when available, else name initials), the time-of-day greeting
   (**صباح الخير** before 12:00 local, else **مساء الخير**) over the user's
   display name + `👋` in gold, then a **bell** `IconButton` with the unread
   `Badge.count` and a **menu** `IconButton` at the row end.
2. **LIVE banner** (frame node 210:736) — a red-bordered card: a 60×60 red box
   with **مباشر**, title **الجلسة الافتتاحية تُبث الآن**, subtitle
   **شاهد البث المباشر**, and a gold `arrow_left` chevron. Static config — no
   API (D10). Tap → the live-broadcast screen.
3. **Section «عن الملتقى · المحاور»** with a trailing **المزيد** action →
   About; a 3-up `KsaTileRow`: **المتحدثون** · **الأجنحة** · **الرعاة**.
4. **Section «الأخبار والتغطية»** with **المزيد** → News; a 2-up row:
   **اللقاءات الثنائية** (videocam icon → gallery) · **الأرشيف**.
5. **Section «الميزات الذكية»** with **المزيد** → More; two 2-up rows:
   **قابل أشخاص مثلك** · **المساعد الذكي**, then **ملخص الجلسات** ·
   **بطاقتي الذكية**.
6. **Section «تابعنا»** — the `_SocialRow`: **five** equal bordered buttons
   with the brand glyph PNGs exported from the Figma file (X, Instagram,
   LinkedIn, YouTube, TikTok), then the centred handle caption
   `@SIMF_RSNF · الملتقى البحري السعودي الدولي`. A button whose configured URL
   is empty is **inert** (D-369).
7. **Section «اكتشف»** — the same روح السعودية discover row as the guest
   layout.

The frame's **«أحدث منشوراتنا» X-embed card is omitted** (no API behind it —
owner-approved, D-378).

### Bottom navigation (both layouts)
The shared `SimfBottomNav` v2 (nav component 206:1669): a navy bar with rounded
top corners and an upward gold glow; destinations in reading order **Home ·
الأجندة Agenda · [raised 56px gold QR centre → smart badge] · الخريطة Map ·
الملف الشخصي Profile**. **Only the active tab shows its label** (gold); Home is
the active tab here. The Profile tab **replaced the old News tab** per the
delivered frames (owner-approved, D-378).

## Components
| Component | Role | Notes |
|---|---|---|
| `KsaPage` | Navy page scaffold | Guest: title + back + sweep; signed-in: custom `header` row; both: `tab: SimfTab.home` |
| `SimfBottomNav` | Bottom nav v2 | Home/Agenda/QR/Map/Profile; only-active-label; QR centre → badge |
| `_GuestBanner` | Guest browse notice | Navy card, gold-highlighted **البطاقة الذكية** phrase; guest only |
| `KsaNavTile` (in `KsaTileRow`) | Navigation tile | Gold icon over white label, navy card with beige hairline; `enabled: false` = locked palette + no tap (the بطاقتي card) |
| `KsaSectionHeader` | Section title row | Optional trailing **المزيد** action (signed-in sections) |
| `KsaListRow` | FAQ / روح السعودية rows | 72×64 gold badge box + title + muted subtitle + gold `arrow_left` |
| `_GreetingHeader` | Signed-in header | `KsaAvatar` + greeting + name `👋` + bell (`Badge.count`) + menu; replaces the app bar |
| `_LiveBanner` | LIVE promo card | Static config, no API (D10); tap → live broadcast |
| `_SocialRow` / `_SocialButton` | تابعنا brand buttons | 5 asset glyphs; empty configured URL ⇒ `onTap: null` (inert, D-369) |
| `_DiscoverSaudiRow` | Visit-Saudi link | "KSA" gold badge; opens `BuildConfig.visitSaudiUrl` externally |
| `FilledButton` sign-in CTA | Guest affordance | Label **تسجيل الدخول**; tap → sign-in; guest only |

## Data binding
- **Privilege** ← the cached auth state (`AuthStateSignedIn.session.user.appRole`;
  no session ⇒ `Guest`). Picks the guest vs signed-in layout (Logic L-1/L-2) —
  no fetch.
- **Display name** ← `session.user.displayName` (greeting header + avatar
  initials).
- **Unread count** ← `unreadNotificationCountProvider` →
  `GET /app/account/notifications/unread-count` (signed-in only, best-effort;
  Logic L-5). Any error resolves to `0`; the badge hides when `0`.
- **LIVE banner** ← static l10n strings, no API (D10, Logic L-6).
- **Social + Visit-Saudi links** ← compile-time `--dart-define`s
  (`SIMF_SOCIAL_X/_INSTAGRAM/_LINKEDIN/_YOUTUBE/_TIKTOK`,
  `SIMF_VISIT_SAUDI_URL` defaulting to `https://www.visitsaudi.com`) — empty
  value keeps that button inert (D-369, Logic L-7).

## States
| State | Trigger | Visual |
|---|---|---|
| **Guest** | No signed-in session | Guest layout: banner + 2×2 public tiles + locked بطاقتي + open-info rows + gold sign-in button. The unread count is **never requested** |
| **Signed-in** | Cached session exists (any role) | Greeting layout: header + LIVE banner + three tile sections + تابعنا + اكتشف |
| **Loading** | App routed to `/` | The layout paints immediately from the cached state; **no blocking spinner**. The bell shows without a badge while the count call is in flight |
| **Empty** | — | **Normal** state — the tiles ARE the content; no empty-state placeholder |
| **Error** | Unread-count call failed | The provider resolves to `0` → badge hidden; **no error UI** (silent, Logic L-5) |
| **Success** | Count returned | `Badge.count` shows the number (hidden when `0`) |
| **Inert link** | A social/Visit-Saudi URL not configured | That button renders but does nothing (`onTap: null`) — never a dead intent |

## Localization
All strings resolve from `AppL10n` (`lib/app/localization/app_l10n.dart`) — no
hard-coded copy. Key strings as built:
- Guest title: AR **الرئيسية • ضيف** · EN **Home • Guest**; nav-tab title:
  AR **الرئيسية** · EN **Home**.
- Guest banner: «أنت تتصفح كضيف، سجّل دخولك للوصول إلى **البطاقة الذكية**،
  طلبات المقابلات، والإشعارات الشخصية.»
- Tiles: **الجلسات / المتحدثون / الخريطة / المعرض / بطاقتي / الأجنحة / الرعاة /
  اللقاءات الثنائية / الأرشيف / قابل أشخاص مثلك / المساعد الذكي / ملخص الجلسات /
  بطاقتي الذكية**.
- Sections: **معلومات مفتوحة للجميع / عن الملتقى · المحاور / الأخبار والتغطية /
  الميزات الذكية / تابعنا / اكتشف**; trailing action **المزيد**.
- Rows: FAQ **الأسئلة الشائعة** + «FAQ • معلومات الموقع والفعالية»; discover
  **روح السعودية** + «Visit Saudi · استكشف الرياض».
- Greetings: **صباح الخير** / **مساء الخير**; LIVE: **مباشر**,
  **الجلسة الافتتاحية تُبث الآن**, **شاهد البث المباشر**.
- Handle caption (identical in both languages):
  `@SIMF_RSNF · الملتقى البحري السعودي الدولي`.
- CTA: **تسجيل الدخول** / **Sign in**.

## RTL
- The page body lays out with the ambient direction (RTL in Arabic): tile rows,
  list rows, the greeting header and the section headers all mirror.
- **Exception (D-363 chrome):** the guest header row is **forced LTR** — the
  circled back chevron sits at the **physical left** with a centred title in
  both languages, matching the frames.
- The forward chevron on the list rows and the LIVE banner is the fixed
  `Icons.arrow_left` glyph (the frames' gold left-pointing arrow).

## Accessibility
- The bell `IconButton` carries the tooltip **الإشعارات** and the unread count
  via `Badge.count`; the menu button carries **المزيد**.
- Bottom-nav items wrap in `Semantics(button, selected, label)`; the active tab
  is a no-op.
- The locked بطاقتي tile has no tap target (disabled palette) — it cannot be
  activated.
- Tile labels ellipsize at one line; the name line in the greeting header
  ellipsizes rather than overflowing.
