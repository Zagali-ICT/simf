# Figma pixel verification pass — 2026-07-04

Overlay comparison of each app screen's **rendered golden** against its **Figma
frame** (fetched live via the Figma MCP, file `PSXHhY0UVTAPSaIOf9uNKd`). For each
screen: layout, margins/padding, icons, text, colours and RTL order are checked
against the frame. A **MATCH** means the render is faithful to the frame; a
**FIX** row records the mismatch found + the change made + the re-locked golden.

> Note on mockup vs render differences that are **not** defects: the Figma frames
> include the phone status bar (9:41 / signal / battery) and often show toggles/
> tabs in an illustrative "on" state or a fixed placeholder item count — the app
> render is data-driven, so a different toggle state or list length is expected
> and is not a layout mismatch.

| # | Screen | Node | Result | Notes |
|---|--------|------|--------|-------|
| 1 | terms | 505:1553 | ✅ MATCH | Title + left chevron, معلومات هامة heading, gold-hairline bullet cards (gold dot RTL inline-start), full-width gold موافق. (Figma shows 6 placeholder cards; render is data-driven.) |
| 2 | accessibility | 1116:16630 | ✅ MATCH | Header, العرض + الصوت sections, حجم الخط 4-pill card (متوسط gold), toggle rows, captions gold, bottom nav. (Figma shows high-contrast on; render shows the real default off.) |
| 3 | splash | 159:573 | 🔧 **FIXED** | Logo, tagline, forum name, edition, layout all matched — **but the date used Arabic-Indic digits** (`٢٣–٢٥ … ٢٠٢٦`) while the frame uses Western (`23–25 … 2026`). Changed `splashEventLine` (ar) to Western digits to match the frame; golden re-locked. |
| 4 | share_my_contact | 1701:6062 | ✅ MATCH | Header شارك جهة اتصالي (singular — matches the English + the screen's own-card purpose), gold-bordered white QR card, hint, gold مشاركة action, تدوير الرمز action. Frame shows a bottom-nav bar; this is a **pushed** sub-screen (not a tab), so the nav is mockup chrome, not a defect. |
| 5 | scan_contact | 1701:7080 | ✅ MATCH | Header مسح رمز QR, manual field رمز المشاركة, gold بحث. Frame's "او + camera" section is hidden in the golden by `enableCamera:false` (shows on-device); bottom-nav is mockup chrome (pushed screen). Minor: the shared `QrScanView` field hint wording differs slightly from the frame — flagged, not changed (shared component). |
