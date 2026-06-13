# Page 007 — Function (إنشاء حساب · Sign up — profile data)

What this screen does, the user steps, and the auth gate. Business rules are in
[Page_007_Logic.md](Page_007_Logic.md); the contract is in [Page_007_API.md](Page_007_API.md).

> Last updated: 2026-06-13 — as-built conformance pass (D-368 KSA rebuild;
> D-371/D-373/D-374/D-375 amendments).

> **Reworked (D-332), rebuilt to the KSA frame (D-368).** This is the **profile
> data** screen (mockup 05 → Figma 168:2972). The interests picker moved to its own
> screen — **[Page 007‑01](../Page_007-01/README.md)** — and the **save** happens
> there (the API requires interests on the single upsert). This screen ends with
> **التالي / Next**, carrying the collected data forward as a `SignUpProfileDraft`.

## Purpose
Profile data capture for a signed-in visitor (just after OTP, or routed back in
while `profileComplete = false` — D-374). The user picks the **نوع التسجيل
(Visitor / Other)** tab and fills the registration fields; under **Other** a
**ProfileType** pick is required, under **Visitor** the type auto-locks to
**"Normal" (عادي)** (C5, D-371). **Next** carries the data (+ any captured photo)
to the interests screen; nothing is saved on this screen.

## Privilege / auth gate
| | |
|---|---|
| App privilege | **AUTH-only** — any signed-in account. **No role, no permission code** (D7). |
| Token | Standard bearer JWT from sign-up/verify; the screen reads `userId` / `email` from the **cached session**, never from a form field. |
| Approval | Reachable **before** approval — completing the profile is what moves the account toward approval. The lookups are **not** approval-gated. |
| Not anonymous | Every call requires sign-in; none is `AllowAnonymous`. |

## Elements (as-built, in form order — `sign_up_visitor_screen.dart`)
| # | Element | AR | Source | Notes |
|---|---------|----|--------|-------|
| 1 | **نوع التسجيل (type)** | زائر / أخرى | static beige segmented tabs | Visitor / Other = `ProfileType.IsForVisitor`; **filters** element 2; **not** sent to the server; default **Visitor** |
| 2 | التصنيف / ProfileType | التصنيف | **profile-type lookup** | **(C5, D-371)** Visitor → locked to the single **"Normal" (عادي)** type, picker hidden; Other → `?isVisitor=false` dropdown shown and a pick is **required**. **(D-375)** the field always surfaces its fetch state — loading spinner / inline retry on failure or empty — never silently hidden |
| 3 | Arabic name | الاسم الكامل (بالعربية) | text | required, ≤ 256 |
| 4 | English name | الاسم الكامل (بالإنجليزية) | text (LTR) | required, ≤ 256 |
| 5 | Gender | الجنس | two radio pills — ذكر / أنثى | **default Male** on a first-time profile (D-373); no "unspecified" option on the UI |
| 6 | الجهة (Organisation) | الجهة / المنظمة | **organisation lookup** (typeahead) | `GET /app/organisations?search=&top=20`, 350 ms debounce, top 8 shown; **required** (B3/D-221); **(D-375)** spinner while searching + inline retry on failure |
| 7 | Job title | المسمى الوظيفي (اختياري) | text | optional, ≤ 128 (D-163) |
| 8 | Nationality | الجنسية | **country lookup** — searchable bottom sheet (D-373) | `GET /app/account/user-profile/countries`; default **SA**; the pick **derives `isSaudi`** (the old toggle is removed) and switching SA↔non-SA clears the document fields |
| 9 | Document (derived from nationality) | رقم الهوية الوطنية / نوع الوثيقة + رقم الوثيقة | conditional | SA → national-ID field (number keyboard, 10 digits, `^1\d{9}$` + Luhn); non-SA → الإقامة / جواز السفر segmented tabs + number field (Iqama `^2\d{9}$` + Luhn / passport 6–9 alphanumeric) |
| 10 | Mobile | رقم الجوال (اختياري) / رقم الجوال الدولي (اختياري) | text (phone keyboard, LTR) | **one** conditional field — Saudi shape when SA, E.164 otherwise; optional; **(C4, D-371)** standard shapes |
| 11 | Date of birth | تاريخ الميلاد | date picker | **required, ≥ 18** (D-197); selectable range *today − 120y* … *today − 18y* |
| 12 | Place of birth | مكان الميلاد (اختياري) | text | optional, ≤ 128 (D-163) |
| 13 | Plate number | رقم اللوحة (اختياري) | text (LTR) | **(C6, D-371)** optional; when filled — 3 letters (Arabic or Latin) + 1–4 digits, ≤ 7 chars excl. separators; last input before the attach box (D-373) |
| 14 | Photo attachment | المرفقات (صورة الهوية / الإقامة / الجواز) — إرفاق ملف | **camera capture** | **(C7, D-371)** mandatory for **gender = male** (unless one is already stored server-side), optional for women; **camera only** (no gallery); on-device ML Kit face check before accept (server re-checks on upload); attached → thumbnail + name + إزالة |
| 15 | Terms link | الموافقة على الشروط والأحكام؟ | underlined link | opens **Page 009** (standalone read — not a consent gate) |
| 16 | **Next** | التالي | gold button | → [Page 007‑01](../Page_007-01/README.md) carrying the `SignUpProfileDraft` |

## User steps
1. The app opens the screen for a signed-in, profile-incomplete account (just after OTP, or via the D-374 `profileComplete` gate after sign-in / cold start).
2. On open, the screen runs **four reads concurrently** — the pre-fill (`GET /app/account/user-profile`), countries, profile-types (`?isVisitor=true`) and the top-20 organisations — behind a full-screen spinner. Existing values pre-fill the form (gender defaults to Male, nationality to SA on an empty profile — D-373). *(Interests are loaded on Page 007‑01, not here; any existing interest ids are carried in the draft for pre-selection.)*
3. The user picks **نوع التسجيل (Visitor / Other)** — Visitor hides the ProfileType picker and locks the type to "Normal"; Other shows the filtered picker and requires a pick (C5).
4. The user fills the form fields (elements 3–14).
5. The user taps **التالي / Next** → client validation runs; if valid the app navigates to **[Page 007‑01](../Page_007-01/README.md)** carrying the collected data (+ any captured photo bytes) as a `SignUpProfileDraft` route extra. **No API write happens here.**

## Navigation
- **In:** from **Page 006 (OTP)** after email verification, or routed here post-sign-in / on cold start while the server-computed `profileComplete` is false (D-374).
- **Out (next):** **Page 007‑01** (interests) carrying the draft.
- **Out (terms):** **Page 009** via the underlined link (push; returns here).
- **Out (back):** the back chevron pops (falls back to `/` when nothing to pop); no profile write — the in-memory form is discarded.

## Acceptance criteria
- AC1 — The screen renders only for a signed-in account; an anonymous open is impossible.
- AC2 — **نوع التسجيل** shows exactly **two** tabs — **زائر / Visitor** and **أخرى / Other**. Visitor hides the ProfileType picker (auto-locked to "Normal"); Other re-queries `?isVisitor=false` and shows the required picker (C5, D-371).
- AC3 — The lookups populate their pickers; a failed or empty API-fed picker shows a **visible loading / inline-retry / empty state** (D-375) — never a silently missing control and never a blocking error. **No interests picker appears on this screen.**
- AC4 — **Date of birth is required** and the registrant must be **≥ 18** (D-197): the picker's selectable range ends at *today − 18 years*.
- AC5 — **Next** advances only when the data fields are valid — required: Arabic name, English name, **organisation** (B3/D-221), DOB, the conditional identity-doc, a ProfileType pick under Other (C5), and the photo for men (C7); optional-but-shape-checked: mobiles (C4) and plate (C6). Tapping it routes to **Page 007‑01** with the draft. **No `POST` is issued on this screen.**
- AC6 — Full **RTL** in Arabic (the top back/globe row is deliberately forced LTR to match the frame); labels from `AppL10n` resources (static) + lookup rows (data); never hard-coded strings. English name / mobile / plate inputs render LTR.
