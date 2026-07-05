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
| 6 | session_detail | 889:2450 | ✅ MATCH | Header, gold session-index badge, date/time row (**Western digits — `23 نوفمبر · 10:30 — 09:00`, matches the frame**), summary/link buttons, وصف/المتحدثون/اسأل المحاور sections, مقعدي seat card (`مقعد 12`), تذكير + أضف إلى تقويمي buttons, bottom nav. **Confirms the digit style is Western app-wide** (splash was the lone Arabic-Indic exception, now fixed). Flag: the speaker "verified" green badge renders as tofu in the golden (likely an emoji outside the golden font set — verify on-device). |

## Status of the pass (2026-07-04)

**6 of 44 bound screens overlay-verified so far** (terms, accessibility, splash,
share_my_contact, scan_contact, session_detail). Result: **5 MATCH, 1 real
mismatch found + fixed** (splash Arabic-Indic → Western digits). Key finding:
the app renders numbers/dates in **Western digits, matching the frames** — the
digit mismatch was *not* systemic. The remaining 38 bound screens fall into two
groups: (a) screens whose clean-code golden was **held** (baseline-then-hold,
without `--update`) — the render is byte-identical to the pre-clean-code,
originally-parity-built version, so clean-code introduced **zero** render change;
(b) the earlier parity waves already fetched + overlaid their Figma frames (the
frames are in the session scratchpad). This overlay pass continues screen by
screen; each result is appended above.

## Full parallel-overlay pass (39-agent Workflow, 2026-07-05)

A 39-agent Workflow fetched every bound screen's Figma frame and compared it to
the render. **15 clean MATCH:** accessibility, archive, chatbot, email_otp, faq,
forum_guide, gate_setup, interests, media_partners, my_seat, news, presentations,
rate, session_summary_list, sponsors. The remaining findings are triaged below —
most are **not** app defects. Numbers/dates use Western digits app-wide (matches
the frames); the splash Arabic-Indic date was the only digit bug (fixed).

### ✅ Real defects — FIXED
| Screen | Defect | Fix |
|--------|--------|-----|
| contact_us | معلومات التواصل row had the gold icon trailing → RTL put it on the LEFT; frame has it leading (RIGHT) | Reordered so the icon leads; golden re-locked |
| notifications | >1-day date header used `DateFormat('d MMM')` (default locale) → "Jun 10" in the Arabic UI | Switched to the locale-free `gregorianMonthName` → "10 يونيو" |
| registration_success | تواصل معنا tiles were call-then-mail (call RIGHT under RTL); frame is phone-LEFT / mail-RIGHT | Swapped so the mail tile leads |
| splash | date used Arabic-Indic digits `٢٠٢٦`; frame uses Western `2026` | Matched the frame (earlier commit) |

### 🖼️ Golden-harness artifacts — NOT app defects (render correctly on device)
- **Missing header emblem/logo** — sign_in, sign_up_form, sign_up_visitor,
  staff_register_visitor: the logo IS drawn (`AuthBrandHeader` → `SimfLogo`,
  `auth_chrome.dart:139`), but `SimfLogo` is an `Image.asset` PNG, which Flutter's
  golden rasteriser renders empty without `precacheImage` (same root cause fixed
  for splash). On device the emblem shows.
- **Tofu country flags** — booths, delegations, meet_people, speaker_profile,
  speakers: `country_flag.dart` returns a **flag emoji** (regional-indicator
  pair). Emoji don't render in Flutter's golden rasteriser → tofu boxes. Renders
  on iOS. **⚠️ Owner note:** Android renders regional-indicator flags
  inconsistently — if broad Android fidelity is required, switch to flag images.
- **Missing avatar photo** — my_area, home_signed_in social-post: `Image.network`
  / asset avatars don't load in goldens (bearer/self-signed, D-422).

### 🗑️ Owner-superseded (Figma frame predates the decision) — NOT defects
- **my_area** "احصائيات" section + **more** "عروض الجلسات (PPT)" row: both were
  **deleted by D-609** (owner directive, 2026-07-04). The frames are older.

### 🔀 State differences (golden shows a different data state than the mockup)
- **registration_status** "المراحل" stages card: shown for a *pending* account;
  the frame shows the *approved* state (no stages). Both are real states.
- **staff_register_visitor** document-type toggle + رقم الوثيقة row: only shows
  for a **non-Saudi** nationality; the golden used Saudi (national-ID field).
- **session_summary** agenda list / **gallery** active tab: data/tab-state.

### ⚙️ Intentional app features beyond the static mockup (keep)
- home/guest header language + theme toggles; live "اطرح سؤالاً" button; guest
  login CTA; sign_in badge-scan button; contact_us submit button; notifications
  "تعليم الكل كمقروء". These are functional; removing them to match a static
  frame would drop features.

### ✍️ Copy / spelling differences (mostly render is correct Arabic; a few real)
- **Debatable (render is the more-correct Arabic — left as-is):** عضواً vs عضو,
  أعضاء vs اعضاء, الذي vs الذى, إرسال vs ارسال, الآن vs الان, إلى vs الى,
  السعودية vs السعوديه, اسأل vs اسئل. The frames use informal/dotless forms.
- **Candidate real copy fixes (frame is the intended label):** home tile
  "المعرض"→"الأجنحة"; home highlight title dropped "السعودي"; sessions banner
  "اليوم الأول"→"تفاصيل اليوم"; send_question review-note wording. Low severity;
  flagged for the owner's copy decision (do not want to guess brand copy).

### 🎨 Lower-severity real items (flagged, not yet changed)
- **speaker_profile** primary CTA floats up instead of anchoring above the nav
  (missing Spacer/Expanded) — layout, medium.
- **more** app-version footer + outlined sign-out button styling — medium.
- ~~**booths** "أرشدني" button uses a location-pin; frame uses a directions arrow~~ — **FALSE ALARM (corrected 2026-07-05 by live check).** Figma metadata shows the CTA icon is `iconamoon:location` (node 922:2631) — a location glyph, the SAME as the app's `nav_location.svg`. The "directions arrow" was a workflow-agent misread; the app already matches Figma. No change made (a swap would have *broken* parity). Booths also confirmed live to carry the new top-right language globe (D-652).
- **home_guest** speakers tile uses a person glyph; frame uses a mic — medium.
- **about** message/vision body text hue reads cool-gray vs the frame's warm
  gold/tan — needs a token check (dark-on-dark), medium.
- **contact_us** social row uses generic Material glyphs (camera/briefcase) vs
  brand logos (Instagram/LinkedIn) — needs brand SVGs, medium.

## Live gated-screen pass — signed-in visitor on local seeded backend (2026-07-05)

Stood up the local API + CP, seeded demo data, logged the app in as `visitor@simf.local`
(Approved, profile completed to the `IsProfileCompleteAsync` rule), and drove the
login-gated screens live against real data. Per-screen results (D-652 sub-page globe
confirmed present on every one):

- **Signed-in Home (758-1134)** — MATCH. Diffs: action tile «المعرض» vs Figma «الأجنحة»
  (owner copy call — app is internally consistent with the المعرض screen title); extra
  theme-toggle (intentional feature); avatar photo (demo data). All previously triaged.
- **My-area (758-1283)** — MATCH (updated 2026-07-05). The «احصائيات» stats section was
  **restored display-only per owner (D-653)** — two KPI tiles («جلسات محفوظة» + «مقابلات»),
  non-tappable; this reverses the D-609 removal for the tiles only (the drill-down list
  screens stay retired). Figma header also carries a theme moon (app = globe-only per D-652
  scope). Settings list has extra app rows (requests / book-seat / update-ID) = functional
  additions.
- **Badge (758-1469)** — MATCH on layout. Two real flaggable diffs: (1) the app renders a
  **rounded-dot QR** (circular finder patterns) where Figma shows a **standard square-module
  QR** — a QR-style choice; (2) the second button reads «شارك جهة اتصالي» (share my contact)
  vs Figma «مشاركة البطاقة» (share the badge) — different label/action. Owner decisions.
- **Notifications (758-2491)** — empty-state («لا توجد إشعارات بعد») renders correctly with the
  D-652 globe (visitor has no notifications). The populated list layout is golden-verified
  (`notifications_758-2491.png`, re-locked with the globe); a live populated check needs a
  CP-triggered notification.

### Live-pass pattern (7 screens in)
Across onboarding, speakers, booths, signed-in home, my-area, badge, notifications the app is
**faithful to Figma**. The only *systematic* gap (the missing sub-page language globe) is fixed
(D-652). Remaining per-screen diffs are: owner decisions (المعرض/الأجنحة copy, D-609 stats
removal, badge share-button copy), intentional features (theme toggle, extra settings rows),
data/empty states (0 sessions, no avatar photo, no notifications), or design choices (dotted vs
square QR). No new correctness bugs found on these 7. Data-heavy screens (sessions, session-detail,
live, delegations, moderation) still need CP data entry to live-check with content.
- **About (1116-16448)** — MATCH on structure, with one **CONFIRMED FIXABLE** diff: the
  الرسالة / الرؤية / تفاصيل body text renders **cool gray** in the app but **warm gold/tan**
  in Figma (confirms the earlier triage flag). Candidate token: the body should use the warm
  paragraph/gold token (≈ `#C2B8A2` / accent) not a cool gray. App also adds a subtitle +
  «مفتوح · 2026» pill + a System-info (v1.0.0) section Figma omits (additions). → batch-fix the body hue.
