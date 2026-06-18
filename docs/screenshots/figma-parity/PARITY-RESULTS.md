# SIMF App — Figma Pixel-Parity Audit Results

**Date:** 2026-06-18 · **Branch:** `feature/app-cp-api-split` · **Auditor:** Claude (pixel-parity pass)
**Figma source of truth:** file `PSXHhY0UVTAPSaIOf9uNKd` (KSA-Project · page "Ready for Test")

## Method

- **Figma side:** each frame pulled via the Figma MCP (`get_screenshot` / `get_metadata`)
  at its native resolution. Frames are **375-wide phone screens**.
- **App side:** the **deployed Flutter web app** (`https://simf_app.zagali-ict.com`,
  pointed at the **prod API** `https://simf_api.zagali-ict.com/api/v1`) rendered in Chrome
  at a **375 CSS-px viewport** — matched to the Figma frame width so the two are
  apples-to-apples. Navigated by go_router hash deep-links (`/#/<route>`); guest session.
- **Comparison:** element-by-element (layout, order, RTL inline direction, spacing,
  colour, typography, content). Side-by-side proof sheets in [`sidebyside/`](sidebyside/).
- **Why no raw pixel-diff %:** a numeric `compare -metric AE` would be dominated by
  confounds that are not parity defects — web font rendering ≠ Figma font, the iOS
  status bar baked into every frame (9:41), Figma sample data vs live data, and the
  asset-fallback problem below. The valid comparison here is layout/element-level.

## ⚠ Data/environment limits (why some screens can't be fully verified)

Prod is **content-sparse**, so the image-heavy and signed-in parts of the design
**could not be exercised**. This is the single biggest caveat:

| Limit | Effect |
|---|---|
| **0 media assets uploaded** (`hasPhotoAsset:false` everywhere, `Asset` table empty) | Every logo / speaker photo / news thumbnail renders the **fallback** (initials-on-gold / anchor / glyph). Figma's **real imagery is never matched** — the `Image.network` success path is unverified. |
| **No live session "now"** (sessions dated 2026-06-14 past / 06-20 future) | `/live` shows the **empty state** only; the populated player + AI-caption strip + geofence notice + upcoming list (frame 934:3450) are unverified. |
| **Archive children empty** (`gallery:[]`, `sessionTitles:[]`, `pastSpeakers:[]`) | App correctly hides 3 sections Figma shows; they're a **data gap**, not a defect. |
| **Session has no speakers** (`speakers:[]`) + guest has no seat | Session-detail "المتحدثون" + "مقعدي" sections hidden — unverified. |
| **Speaker has only a bio** (qualifications/experience/awards null) | CV shows **1 tab** vs Figma's 4 — data gap; the tuned 4-pill row is unverified. |
| **Guest session only** (no approved prod account) | Signed-in **Home** + **My-seat** can't be reached. |

## Per-screen verdict

| # | Screen | Figma frame | Verdict | Key deltas |
|---|--------|-------------|---------|------------|
| Sign-in | `758:2555` Login | ✅ **Near-exact** | App adds a 2nd alt-login button "الدخول بمسح الشارة" (additive; real feature). Everything else matches. |
| P1 | Media partners | `958:2246` | ✅ **Match (layout)** | +global bell/hamburger app-bar not in frame; initials vs empty placeholder (asset); active-tab label 1-line (app) vs 2-line (Figma). |
| P2 | News card | `957:2197` | ✅ **Match** | Only the thumbnail (glyph fallback vs real photo — asset). Layout, gold DD-MM-YYYY date, badge overlap, right-aligned title all match. |
| P3 | Speaker-CV | `908:2110` About Speaker | ◑ **Partial** | Avatar (gold ring + anchor) matches; **English rank** "Admiral" vs Arabic "القبطان البحري"; 1 pill vs 4 (data). |
| P3 | My-seat | `898:2873` Your seat | ⚠ **Not verifiable** | Auth-gated (needs a booking). |
| P4 | Speakers list | `908:1744` | ◑ **Match w/ defects** | **Host STAR glyph missing** (anchor-for-all, known D-432); **English rank** vs Arabic role+org; anchor vs photo (asset); rows ~10% taller. |
| P5 | Live | `934:3450` Live Video | ⚠ **Not verifiable** | Empty state only (no live session); geofence notice deferred (G-OI-2); captions provider-stubbed; +bell/hamburger. |
| P6 | Sponsors | `922:2824` Shepherds | ✗ **Defects** | **English tier labels** (Platinum/Gold/Silver) vs Arabic (الرعاية الاستراتيجية/رعاة بريميوم/رعاة ذهبيون); **RTL card mirroring** (logo+chevron sides swapped); missing card subtitle; +bell/hamburger. |
| P6 | Booth | `922:2458` Halls | ✗ **Defects** | **Missing officer block + email/phone contacts**; middle row shows sector+code vs Figma code+**HALL name**; **RTL mirroring** (logo on left); subtitle duplicates name; +bell/hamburger. |
| P6 | Archive | `925:3079` | ◑ **Partial** | Top half (banner→stats) matches; gallery/session-titles/past-speakers empty (data, not verifiable); **stat-card order reversed**; +bell/hamburger. |
| P6 | Session detail | `889:2450` Choose Sessions | ◑ **Match w/ defects** | **Code badge overflow** (raw "S-001" vs ordinal "02"); **bottom-button order reversed** (gold on left vs right); missing session-type pill; speakers/seat not verifiable. |
| — | Home | `758:1134` / `203:1236` | ⚠ **Not verifiable** | Figma frame is the **signed-in** home; app session is guest → a different, gated layout. |

## Prioritised REAL defects (app deviates from Figma)

**Significant**
1. **Sponsors tier labels in English** — "Platinum/Gold/Silver" in an Arabic RTL screen; Figma uses Arabic tier descriptors. (`features/sponsors/sponsors_screen.dart` — renders API `tierName`.) Also the tier *scheme* differs (Strategic/Premium/Gold vs Platinum/Gold/Silver) → data + UI.
2. **RTL card mirroring on Sponsors & Booth** — app puts the logo box at inline-**end** (left) and the disclosure chevron at inline-**start** (right); Figma has logo at inline-**start** (right), chevron at inline-**end** (left). The card's internal order is reversed vs the design. (`sponsors_screen.dart`, `features/venuemap/…` booths screen.)
3. **Booth card missing officer + contacts** — Figma shows the booth-officer (name + avatar + "المسؤول في الجناح") and an email/phone row; the app renders neither, and drops the **hall name** in favour of a sector pill. (May be partly data — confirm officer/contact are populated.)
4. **Speakers list: English rank instead of Arabic** — API returns `rank:"Admiral"` with no Arabic rank field, so the Arabic UI shows English text (list + CV header). Backend gap (needs `rankArabic` or Arabic role+org per the design).

**Moderate / minor**
5. **Speaker list host STAR not rendered** (anchor-for-all) — known/deferred **D-432**, now confirmed against the frame.
6. **Session code badge overflow** — raw code "S-001" crammed into a circular badge (text wraps); Figma shows a clean 2-digit ordinal.
7. **Session bottom-button order reversed** — gold "أضف إلى تقويمي" on left (app) vs right (Figma).
8. **Archive stat-card order reversed** — app: events|attendees|speakers; Figma: speakers|attendees|events.
9. **Missing subtitles** — sponsor hero/premium card descriptions and the session-type pill are absent (partly data-driven).

## Owner decisions (not clear-cut defects)
- **Global notify+menu app-bar** (bell + hamburger) appears on media-hub / live / sponsors / booths / archive; those specific Figma content frames don't depict it (the signed-in Home frame *does*). Confirm whether the shared chrome is intended on these screens.
- **Sign-in extra "الدخول بمسح الشارة"** badge-scan button — additive vs the frame; badge sign-in is a real feature, so likely intended.

## Asset-fallback (NOT defects — but real imagery is unverified)
Every logo/photo/thumbnail across P1, P2, P3, P4, P6 (sponsors/booth) renders the
designed fallback because prod has **0 uploaded assets**. The fallback styling roughly
matches Figma's placeholder intent (e.g. acronym boxes), **but the app's match to
Figma's real logos/photos has not been proven**. To verify: upload one asset per
`AssetCategory` via the CP and re-run the image-bearing screens.

## Artifacts
- Figma frames: `figma-*.png` · App captures: `app-*.png` · Proof sheets: `sidebyside/01..10-*.png`
