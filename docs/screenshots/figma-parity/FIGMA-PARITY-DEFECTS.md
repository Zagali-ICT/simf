# SIMF App — Figma Parity Defect Log

Source audit: [`PARITY-RESULTS.md`](PARITY-RESULTS.md) (2026-06-18). Proof sheets: [`sidebyside/`](sidebyside/).
Figma source of truth: file `PSXHhY0UVTAPSaIOf9uNKd`, page "Ready for Test".

**Status legend:** `OPEN` · `FIXING` · `FIXED` · `NEEDS-DECISION` (owner input) · `NEEDS-DATA` (backend/CP content) · `NOT-VERIFIABLE` · `WONTFIX`

> No defect below has been fixed yet — this is the report-first log. Update the Status
> column as each is addressed; cite the commit when set to FIXED.

---

## P6 — Sponsors (frame `922:2824` Shepherds) · `features/sponsors/sponsors_screen.dart`

| ID | Severity | Class | Defect | Status |
|----|----------|-------|--------|--------|
| PAR-S1 | High | UI/localization | Tier band labels render the **English** `tierName` ("Platinum / Gold / Silver") in the Arabic RTL screen. Figma shows Arabic descriptors "الرعاية الاستراتيجية / رعاة بريميوم / رعاة ذهبيون". Also the tier *scheme* differs (Strategic/Premium/Gold vs Platinum/Gold/Silver). | OPEN |
| PAR-S2 | High | RTL layout | Sponsor card internal order is **mirrored**: app = logo box at inline-END (left) + chevron at inline-START (right); Figma = logo at inline-START (right) + chevron at inline-END (left). | OPEN |
| PAR-S3 | Low | UI/data | Hero + premium cards omit the **subtitle/description** Figma shows (e.g. "الراعي الاستراتيجي · شريك التحول الدفاعي…"). Confirm whether sponsor description is populated. | OPEN |

## P6 — Booth (frame `922:2458` Halls) · booths screen under `features/venuemap/`

| ID | Severity | Class | Defect | Status |
|----|----------|-------|--------|--------|
| PAR-B1 | High | UI/data | Booth card omits the **officer block** (officer name + avatar + "المسؤول في الجناح") and the **email/phone contact row** Figma shows. Verify the API returns officer/contact fields. | OPEN |
| PAR-B2 | Med | UI/data | Middle row shows **sector + booth-code**; Figma shows **booth-code + HALL name**. App drops the hall name. | OPEN |
| PAR-B3 | High | RTL layout | Logo box mirrored to inline-END (left); Figma has it at inline-START (right). Same pattern as PAR-S2. Search icon also on the opposite side. | OPEN |
| PAR-B4 | Low | UI/data | Card subtitle **duplicates** the company name (white name + identical gray subtitle). | OPEN |

## P6 — Archive (frame `925:3079`) · `features/archive/archive_screen.dart`

| ID | Severity | Class | Defect | Status |
|----|----------|-------|--------|--------|
| PAR-A1 | Low | RTL/order | The three stat cards are in **reversed order** vs Figma — app: events \| attendees \| speakers; Figma: speakers \| attendees \| events. | OPEN |
| PAR-A2 | — | NOT-VERIFIABLE | Gallery / session-titles / past-speakers sections empty in prod (`gallery:[]`, `sessionTitles:[]`, `pastSpeakers:[]`) → app correctly hides them; cannot verify vs Figma. | NEEDS-DATA |

## P6 — Session detail (frame `889:2450` Choose Sessions) · session-detail screen under `features/sessions/`

| ID | Severity | Class | Defect | Status |
|----|----------|-------|--------|--------|
| PAR-D1 | Med | UI | Code badge crams the **raw code "S-001"** into a small circular badge (text overflows/wraps); Figma shows a clean 2-digit **ordinal** ("02"). | OPEN |
| PAR-D2 | Low | RTL/order | Bottom-button order **reversed** — gold "أضف إلى تقويمي" on left (app) vs right (Figma); "تذكير" mirrored opposite. | OPEN |
| PAR-D3 | Low | UI/data | Missing **session-type pill** ("جلسة رئيسية"); app shows only the hall pill (session `categoryName` is null → partly data). | OPEN |
| PAR-D4 | — | NOT-VERIFIABLE | "المتحدثون" speaker rows + "مقعدي" seat card hidden (session has no speakers; guest has no booking). | NEEDS-DATA |

---

## P1–P5 (out of current scope — fix after P6)

| ID | Screen | Severity | Class | Defect | Status |
|----|--------|----------|-------|--------|--------|
| PAR-P4a | P4 Speakers (`908:1744`) | Med | UI | Host **STAR** glyph not rendered in the list (anchor-for-all). Known **D-432**. | OPEN |
| PAR-P4b | P4 Speakers + P3 CV (`908:2110`) | High | data/localization | **English rank** ("Admiral") shown in Arabic UI; API has no `rankArabic` (Figma shows Arabic role + org). | NEEDS-DATA |
| PAR-P3a | P3 Speaker-CV | — | NOT-VERIFIABLE | CV shows 1 full-width pill vs Figma's 4 (only bio populated). The tuned 4-pill row unverified. | NEEDS-DATA |
| PAR-P3b | P3 My-seat (`898:2873`) | — | NOT-VERIFIABLE | Auth-gated; needs a booking. | NOT-VERIFIABLE |
| PAR-P5a | P5 Live (`934:3450`) | — | NOT-VERIFIABLE | No live session in prod → empty state only; populated player/captions/geofence/upcoming unverified. Geofence notice deferred (G-OI-2). | NOT-VERIFIABLE |
| PAR-P1a | P1 Media partners (`958:2246`) | Low | UI | Active-tab label 1-line (app) vs 2-line (Figma); minor. | OPEN |

## Cross-cutting

| ID | Severity | Class | Defect | Status |
|----|----------|-------|--------|--------|
| PAR-X1 | Med | UI/owner | Global **bell + hamburger** app-bar appears on media-hub / live / sponsors / booths / archive; those Figma content frames don't depict it (the signed-in Home frame *does*). Confirm intent. | NEEDS-DECISION |
| PAR-X2 | — | NOT-VERIFIABLE | **0 media assets** in prod → all logos/photos/news thumbnails are fallbacks; real-image (`Image.network` success) path unverified. Upload 1 asset per `AssetCategory` via CP to test. | NEEDS-DATA |
| PAR-X3 | Low | UI | Sign-in adds a 2nd alt-login button "الدخول بمسح الشارة" not in frame `758:2555` (additive; real feature — likely intended). | NEEDS-DECISION |
| PAR-X4 | — | NOT-VERIFIABLE | Signed-in **Home** (`758:1134`) unverified — audited as guest; app shows the gated guest-home layout. | NOT-VERIFIABLE |
