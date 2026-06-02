# Page 014 — Design (منطقتي · My Area) — Flutter

Screen design for the Flutter app. Layout from `Mockup.html` (lines ~1066–1114);
data binds to [Page_014_API.md](Page_014_API.md); rules in [Page_014_Logic.md](Page_014_Logic.md).

## Layout (top → bottom)
1. **App bar** — back ‹, centered title منطقتي.
2. **Profile card** — circular avatar; full name; role line "`{tier}` · مسجّل في `{N}` جلسات";
   reference line `#{qrId}`; brass **Share** button top-right.
3. **Two quick-share tiles** — مشاركة جهة اتصال (Share contact) · مشاركة ملفي (Share my profile).
4. **Two stat tiles** — `{bookedSessionsCount}` جلسات محفوظة · `{meetingsCount}` مقابلات مؤكدة.
5. **جدولي اليوم** — vertical list; each row = time · title · ★.
6. **Two utility links** — بطاقتي الذكية · إعدادات الحساب.
7. **Bottom nav** — الرئيسية / الأجندة / (center) / الخريطة / التغطية الإعلامية.

## Data binding — `GET /account/dashboard`
| UI element | API field |
|---|---|
| Avatar | `identity.avatarUrl` |
| Name | `identity.fullNameAr` / `identity.fullNameEn` |
| Tier word | `identity.tierNameAr` / `identity.tierNameEn` |
| Tier accent colour | `identity.pageColor` |
| Reference `#…` | `identity.qrId` |
| "مسجّل في N جلسات" | `counters.bookedSessionsCount` |
| Tile — جلسات محفوظة | `counters.bookedSessionsCount` |
| Tile — مقابلات مؤكدة | `counters.meetingsCount` |
| Schedule rows | `todaySchedule[]` → local-time(`startUtc`), `titleAr`/`titleEn`, `kind`, `status` |

## Actions & navigation
| Trigger | Behaviour |
|---|---|
| Share / مشاركة جهة اتصال / مشاركة ملفي | `GET /account/contact-card.vcf` → native share intent (vCard); and/or `GET /account/calendar.ics` for calendar. |
| Schedule row tap | `kind == Session` → Session detail (17, `/agenda/{sessionId}`); `kind == Meeting` → meeting detail (TBD). |
| بطاقتي الذكية | → Badge QR (32); QR rendered client-side from `qrId`. |
| إعدادات الحساب | → More / settings (41). |

## States
- **Loading** — skeleton: card + 2 tiles + 3 schedule rows.
- **Empty** — counters show `0`; empty schedule → "no items today".
- **Error** — single inline retry (one aggregate call).
- **Pending approval** (effective Guest) — card shown; counters/schedule/badge hidden or disabled.

## Localization & direction
AR primary (RTL), EN secondary. Pick bilingual fields per active locale. Times in device tz.

## Design notes
- QR is **client-side** from `qrId`; no server image.
- Tier name/colour come from the dashboard `identity` block (no extra call).
- A still-Pending booking row may carry a small "pending" badge from `status`.
