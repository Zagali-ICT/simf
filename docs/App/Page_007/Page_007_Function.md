# Page 007 — Function (إنشاء حساب · Sign up — profile data)

What this screen does, the user steps, and the auth gate. Business rules are in
[Page_007_Logic.md](Page_007_Logic.md); the contract is in [Page_007_API.md](Page_007_API.md).

> **Reworked (D-332).** This is the **profile data** screen (mockup 05). The interests
> picker moved to its own screen — **[Page 007‑01](../Page_007-01/README.md)** — and the
> **save** happens there (the API requires interests on the single upsert). This screen
> ends with **Next**, carrying the collected data forward.

## Purpose
Profile data capture for a signed-in visitor (just after OTP). The user picks the
**نوع التسجيل (Visitor / Other)** category, the **ProfileType**, and fills the
registration fields. **Next** carries the data to the interests screen; nothing is
saved on this screen.

## Privilege / auth gate
| | |
|---|---|
| App privilege | **AUTH-only** — any signed-in account. **No role, no permission code** (D7). |
| Token | Standard bearer JWT from sign-up/verify; the screen reads `userId` / `email` from the **cached session**, never from a form field. |
| Approval | Reachable **before** approval — completing the profile is what moves the account toward approval. The lookups are **not** approval-gated. |
| Not anonymous | Every call requires sign-in; none is `AllowAnonymous`. |

## Elements
| # | Element | AR | Source | Notes |
|---|---------|----|--------|-------|
| 1 | **نوع التسجيل (type)** | زائر / أخرى | static 2 chips | Visitor / Other = `ProfileType.IsForVisitor`; **filters** element 2; **not** sent to the server |
| 2 | التصنيف / ProfileType | — | **profile-type lookup** | **(C5, D-371)** Visitor → locked to the single **"Normal" (عادي)** type, picker hidden; Other → `GET /app/account/profile-types?isVisitor=false` picker shown and a pick is **required** (supersedes the optional self-pick) |
| 3 | Arabic name / English name | الاسم الكامل | text | required |
| 4 | Gender | الجنس | enum picker | optional (D-221) |
| 5 | الجهة (Organisation) | الجهة | **organisation lookup** (typeahead) | `GET /app/organisations?search=&top=` (D-220 / D-221) |
| 6 | Job title | المسمى الوظيفي | text | optional (D-163) |
| 7 | Document: is-Saudi toggle → national id / iqama / passport | نوع/رقم الوثيقة | conditional | shape enforced by validator |
| 8 | Mobile (Saudi / international) | رقم الجوال | text | optional; **(C4, D-371)** validates to the standard — Saudi `05XXXXXXXX`/`+9665XXXXXXXX`, international E.164 |
| 9 | Nationality | الجنسية | **country lookup** | `GET /app/account/user-profile/countries` |
| 10 | Date of birth · Place of birth | — | date picker / text | **DOB required ≥ 18 (D-197)** + place optional (D-163) — *additive to mockup 05* |
| 11 | Profile image | المرفقات | **camera capture** | **(C7, D-371)** mandatory for **gender = male**, optional for women; **camera only** (no gallery); on-device human-face check before accept; server re-checks on upload (uploaded after the row exists, on save) |
| 11b | Plate number | رقم اللوحة | text | **(C6, D-371)** optional; when filled — 3 letters + 1–4 digits, ≤ 7 chars (Saudi standard) |
| 12 | **Next** | التالي | button | → [Page 007‑01](../Page_007-01/README.md) carrying the form state |

## User steps
1. The app opens the screen for a signed-in, profile-incomplete account (just after OTP).
2. On open, the screen calls the lookups it needs — **countries, organisations, profile-types** — and pre-fills any existing values from `GET /app/account/user-profile`. *(Interests are loaded on Page 007‑01, not here.)*
3. The user picks **نوع التسجيل (Visitor / Other)** — this filters the ProfileType list — then optionally picks a **ProfileType**.
4. The user fills the form fields (elements 3–11).
5. The user taps **Next** → the app navigates to **[Page 007‑01](../Page_007-01/README.md)** carrying the collected data in memory. **No API write happens here.**

## Navigation
- **In:** from **Page 006 (OTP)** after email verification (the account is signed in).
- **Out (next):** **Page 007‑01** (interests) carrying the form state.
- **Out (back):** previous step (no profile write; data discarded).

## Acceptance criteria
- AC1 — The screen renders only for a signed-in account; an anonymous open is impossible.
- AC2 — **نوع التسجيل** shows exactly **two** options — **زائر / Visitor** and **أخرى / Other** — and selecting one filters the ProfileType picker via `?isVisitor=`.
- AC3 — The three lookups (countries, organisations, profile-types) populate their pickers; an empty lookup shows the empty state, not an error. **No interests picker appears on this screen.**
- AC4 — **Date of birth is required** and the registrant must be **≥ 18** (D-197): the picker's selectable range ends at *today − 18 years*.
- AC5 — **Next** is enabled once the required fields (Arabic name, English name, nationality, DOB, and the conditional identity-doc) are valid; tapping it routes to **Page 007‑01** with the form state. **No `POST` is issued on this screen.**
- AC6 — Full **RTL** in Arabic; labels from resources (static) + lookup rows (data); never hard-coded strings.
