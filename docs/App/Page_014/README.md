# Page 014 — منطقتي · My Area (dashboard)

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_014_Function.md](Page_014_Function.md) | What the page does — elements, user actions, navigation, acceptance criteria |
| Logic | [Page_014_Logic.md](Page_014_Logic.md) | Business rules — counter definitions, role gating, data sources, edge cases, dependencies |
| API | [Page_014_API.md](Page_014_API.md) | The backend endpoints + DTOs that serve this page (authoritative contract) |
| Design | [Page_014_Design.md](Page_014_Design.md) | Flutter screen design — layout, data binding, states, share intents, localization |

## Identity
| | |
|---|---|
| Mockup page | **14** (`Mockup.html`) |
| Route | `RouteNames.myArea` → `/my-area` |
| Titles | AR **منطقتي** · EN **My Area** |
| Section | 2 — Core screens |
| Nature | **Personal dashboard** (identity + counters + today's schedule + share) |
| App privilege | **Visitor** and above (signed-in-pending = limited) |
| Status | **Flutter screen BUILT (D-297), redesigned to KSA Wave-2 frame 512:1780 (D-378)**; API **BUILT (D-249)** — dashboard + `.ics`/`.vcf` exports |

## Sources of truth
`Mockup.html` (visual) · `SIMF_Screen_Guide_and_User_Journey` (narrative, Screen 14) ·
SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile architecture).

> This folder is the first instance of the per-page documentation structure
> (`docs/App/Page_NNN/`). It supersedes the per-screen detail that previously sat
> inside the monolithic SIMF-MOB-API-001 §6 / SIMF-MOB-SDS-001, which now point here.

## As-built (D-297)

The Flutter `MyAreaScreen` (`features/myarea/my_area_screen.dart`) replaces the
`ComingSoonScreen` placeholder. An **Approved** user loads
`GET /app/account/dashboard` (`MyAreaRepository.getDashboard`) and sees the
identity card, the two counters, today's merged schedule, the two share tiles,
and the Badge/Settings utility links. A signed-in **pending/rejected** user — and
the 403 edge — falls back to the **limited card** from the cached identity with no
dashboard call (Logic L-5). **Share** is wired for real: the `.vcf`/`.ics` exports
are fetched as **raw text** via the new additive `SimfApiClient.getText()`, written
to a `Directory.systemTemp` temp file, and handed to the native share sheet
(`share_plus`). Schedule **Session** rows route to Session detail (17); **Meeting**
rows are non-tappable (no detail page yet). **Interim UI:** the avatar is rendered
as **initials** and the `pageColor` tier accent uses the token accent — the carried
`avatarUrl`/`pageColor` are deferred to SIMF-VID-001 to keep the skeleton free of a
network-image fetch. Tests: `my_area_screen_test.dart` (7) +
`myarea_models_test.dart` (2).

**Mockup toggles relocated (D-334) — superseded by D-378:** the interim build
dropped the theme/language tiles (navy-always D-331; language on Page 038,
D-327). The KSA Wave-2 frame **512:1780** brings both tiles back on this
screen: the **العربية • English tile is wired** (same locale controller as
Page 038 — the Accessibility control remains too), and the **المظهر tile is
visible but DISABLED** (owner decision — no light theme exists; building one
without light-mode frames would be invention).

## As-built — KSA Wave-2 redesign (D-378)

Rebuilt to frame **512:1780 "منطقتي"** (owner-picked over 213:963 — its stats
map 1:1 onto the API counters) on the shared shell (`KsaPage`, profile tab
active). Identity card: `KsaAvatar` 64 (now renders `avatarUrl` with an
initials fallback), name, tier · enrolled line, gold `#qrId`, bordered gold
**مشاركة** button (= the `.vcf` contact share). Tile grid 2×3: language toggle
/ disabled theme tile / **مشاركة ملفي → `/contacts/share`** (FDS-014 QR) /
**مشاركة جهة اتصال** (.vcf) / the two stat tiles (`meetingsCount`,
`bookedSessionsCount`). جدولي اليوم rows (time at the inline start, gold star
at the inline end; Session rows → detail). المزيد rows: بطاقتي الذكية,
اعدادات الحساب, plus the **function-preserving** مشاركة جدولي (.ics) and
تسجيل الخروج (D-373) rows the frame's non-exhaustive list omits. Pending /
403-limited / error+retry behaviour unchanged (L-5). Old screen + test parked
in `_legacy_mockup/`. Tests: `my_area_screen_test.dart` (9) +
`myarea_models_test.dart` (2).
