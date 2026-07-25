# Page 014 — Design (منطقتي · My Area) — Flutter

Screen design for the Flutter app. Layout from the KSA-Project Figma frame
**512:1780 "منطقتي"** (D-378, 2026-06-13 — supersedes the `Mockup.html` layout);
data binds to [Page_014_API.md](Page_014_API.md); rules in [Page_014_Logic.md](Page_014_Logic.md).
As-built: `features/myarea/my_area_screen.dart` on the shared KSA shell
(`app/widgets/ksa_shell.dart` + `app/widgets/simf_bottom_nav.dart`).

## Layout (top → bottom)
1. **Shared KSA shell** (`KsaPage`, decorative sweep on) — navy surface; forced-LTR
   header row: circled back chevron (pop, else home) + centred title **منطقتي**.
2. **Identity card** (frame node 512:2047) — `KsaAvatar` **64** (photo from
   `avatarUrl`, initials fallback); name; line "`{tier}` · مسجّل في `{N}` جلسات"
   (tier omitted when null); gold reference `#{qrId}` (LTR, shown only when present);
   bordered gold 48×48 **مشاركة** button (share icon + label) = contact-vCard share.
3. **Tile row 1** — **العربية · English** (wired language toggle, globe icon) ·
   **المظهر · ليلي/نهاري** (visible but **DISABLED** — no light theme exists, owner
   decision D-378; locked palette, no tap).
4. **Tile row 2** — **مشاركة ملفي** (→ `/contacts/share` QR screen) · **مشاركة جهة
   اتصال** (.vcf native share).
5. **Tile row 3 (stats)** — `{meetingsCount}` مقابلات مؤكدة · `{bookedSessionsCount}`
   جلسات محفوظة (gold number over white label; tap → **Coming soon**, owner 2026-06-21).
6. **جدولي اليوم** (`KsaSectionHeader`) — rows (frame node 512:2116): bold 12-hour
   time (`hh:mm a`, LTR-pinned) at the inline start, title (+ hall when present)
   end-aligned, gold star at the inline end. Session rows tappable; empty list →
   "لا يوجد لديك مواعيد اليوم".
7. **المزيد** (`KsaSectionHeader`) — four rows (frame node 512:2126: label + white
   chevron): **بطاقتي الذكية** · **اعدادات الحساب** · **مشاركة جدولي** (.ics share) ·
   **تسجيل الخروج** (confirm dialog, D-373). The last two are function-preserving
   rows the frame's non-exhaustive list omits.
8. **Bottom nav** (`SimfBottomNav`) — الرئيسية / الأجندة / (gold QR centre) /
   الخريطة / **الملف الشخصي tab active**.

## Data binding — `GET /app/account/dashboard`
| UI element | API field |
|---|---|
| Avatar | `identity.avatarUrl` (initials from the name when null / load error) |
| Name | `identity.fullNameAr` / `identity.fullNameEn` per active locale (cross-language fallback) |
| Tier word | `identity.tierNameAr` / `identity.tierNameEn` (line omits the tier when both empty) |
| Reference `#…` | `identity.qrId` (hidden when null) |
| "مسجّل في N جلسات" | `counters.bookedSessionsCount` |
| Tile — جلسات محفوظة | `counters.bookedSessionsCount` |
| Tile — مقابلات مؤكدة | `counters.meetingsCount` |
| Schedule rows | `todaySchedule[]` → local-time(`start`), `titleAr`/`titleEn` (falls back to `subject` when empty), `hallNameAr`/`hallNameEn`, `kind` |

`identity.pageColor` and each item's `status`/`end` are decoded but **not
bound** in the KSA design — the accent is the token gold, and rows carry no
status badge.

## Actions & navigation
| Trigger | Behaviour |
|---|---|
| مشاركة button (card) / مشاركة جهة اتصال tile | `GET /app/account/contact-card.vcf` (raw text) → temp file → native share sheet (`simf.vcf`, `text/vcard`). |
| مشاركة ملفي tile | → `/contacts/share` (share-my-contact QR screen, FDS-014). |
| العربية · English tile | `LocaleController.toggle()` — flips AR ↔ EN, persisted to prefs. |
| المظهر tile | Disabled — no action. |
| Schedule row tap | `kind == Session` → Session detail (17, `/sessions/{sessionId}`); `kind == Meeting` → non-tappable (no meeting detail page). |
| بطاقتي الذكية | → Badge QR (32, `/badge`); QR rendered client-side from `qrId`. |
| اعدادات الحساب | → More / settings (41, `/more`). |
| مشاركة جدولي | `GET /app/account/calendar.ics` (raw text) → temp file → native share sheet (`simf.ics`, `text/calendar`). |
| تسجيل الخروج | Confirm dialog (إلغاء / تسجيل الخروج, D-373) → `signOut()` (revokes the server session) → `/sign-in`. |

## States
- **Loading** — centred `CircularProgressIndicator` (no skeleton).
- **Empty** — counters show `0`; empty schedule → "لا يوجد لديك مواعيد اليوم".
- **Error** — `KsaErrorState`: "تعذّر تحميل منطقتك." + إعادة المحاولة retry (one aggregate call).
- **Pending approval** (and the 403 edge) — limited view from the cached identity,
  **no dashboard call**: identity card (cached display name + "حسابك قيد المراجعة…"
  note) plus only the اعدادات الحساب and تسجيل الخروج rows. No counters / schedule /
  badge / share.
- **Share failure** — snackbar "تعذّرت المشاركة. حاول مرة أخرى.".

## Localization & direction
AR primary (RTL), EN secondary; the on-page tile toggles and persists the language.
Pick bilingual fields per active locale (cross-language fallback when one side is
empty). Times in device tz, 12-hour `hh:mm a`, LTR-pinned (as is the `#qrId`).

## Design notes
- QR is **client-side** from `qrId`; no server image (rendered on the Badge page).
- Tier name comes from the dashboard `identity` block (no extra call); `pageColor`
  is carried but unused — the KSA design's accent is the token gold.
- Stat tiles are display-only (frame 213:963's "الأرشيف" stat was not built — no API field).
- The old mockup screen + its test are parked in `_legacy_mockup/`.
