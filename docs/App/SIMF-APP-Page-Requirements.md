# SIMF App — Page Requirements (owner capture, 2026-06-03)

| Field | Value |
|-------|-------|
| Status | Captured verbatim-faithful from owner — **not yet analysed/reconciled** |
| Source | Owner directive, 2026-06-03 ("SAVE ALL MY REQUEST TO DOCS FIRST") |
| Next | Deep-analyse mockup + existing docs + current API → per-page docs → API build → app design plan |

> This file records the owner's requirements **as given**, before analysis. Page numbers
> are the **owner's mockup numbers**; the route-name mapping below is a best guess to be
> reconciled against `Mockup.html` + `route_names.dart` in the analysis step (the 41-vs-49
> page-count question is still open). `[CONFIRM]` marks an interpretation to verify.
> `[API?]` marks an API that must be checked for existence or built.

## Cross-cutting rules (apply across pages)
- **Four app privileges only**: Guest/Not-logged-in, Visitor, Staff, Moderator (محاور).
- **On login the app fetches all data + privileges and caches them** (Page 012); features
  are gated by the cached privilege.
- Lookups and profile reads are **AUTH-only, no role required**.

---

## Page 001 — Splash / app load  *(route: `splash`)*
On launch the app:
1. Shows the **Logo splash** page.
2. Checks the **store (Google Play / Apple App Store) for the last update** (version /
   force-update check). `[API?]` — likely store-native, not a SIMF API; confirm.
3. Checks the **local DB**: is this the **first run** or not.
4. Loads **stored data + session** from local storage.
5. **Opens the page based on the last saved state** (resume to the last screen).
- **Doc action:** "Update all docs."

## Page 002 — Onboarding videos  *(route: `onboarding`)*
- Shows a **loading image**, then plays **3 videos**.
- Videos **preferred from a YouTube channel**.
- Use a **standard, stable name** so the media can be swapped in future without changing
  the reference — e.g. `/*/introd_001, introd_002, introd_003, introd_004, introd_005`.
- **Shown first-time only.**
- **Has NO API.**

## Page 003 — Login / Sign-in  *(route: `signIn`)*
- Login / sign-in with **face-recognition lock** `[CONFIRM: "phase reconicion" = Face ID/biometric]`
  for an already-opened session.
- **Session valid 5 days**; within that window only the **biometric (face)** is required to
  re-open — **no re-login**.
- Login fields: **email (max 50 chars)** + **password (max 32 chars)**.
- **Forgot-password API for Mobile (not CP)** sends an **email OTP** to reset.
  **`[API?]` — MUST verify this API exists** (expected: `/app/auth/forgot-password` +
  `/app/auth/reset-password`, emailed code).
- **Pre-fill email from local store** when the session is expired / lost / after logout —
  then **ask only for the password** to simplify.

## Page 005 & 006 — Sign-up · register + OTP  *(routes: `signUpForm` + `emailOtp`; the old `signUpType` screen was removed — D-332)*
- Step 1 fields: **email, password, confirm-password**.
- **If the account already exists** → show "you already have an account — do you want to
  **reset** or **login**?".
- **If new** → send a **6-digit OTP to the email** to proceed to the next page.
- New account type = **Visitor**, **no privilege**, **under review**, **must fill profile**.
- **At next login the user must fill the profile** (or we can continue to the profile).

## Page 009 — Terms & Conditions  *(route: `terms`)*
- **Only accepts T&C** (accept gate). `[API?]` — T&C content source (CMS?) to confirm.

## Page 007 — Profile completion  *(route: `signUpVisitor` / profile form)*
- Fills the profile using **lookup data** as in the docs: **country, company, organization,
  profileType, interest**.
- **Each lookup must have its own API.** `[API?]` per lookup:
  country, company, organization, profileType, interest.
- Access = **AUTH only, NO role needed.**

## Page 008 — Interests selection  *(route: interests picker)*
- Shows a **list of cards** of interests.
- Select **many — max 10, min 1** — and **save to API**.
- On save the **profile is marked complete**; the API **reads and returns success + user ID
  + contact info**.
- Then show **"wait for approval"** (as in Page 010/011).

## Page 010 — Registration success / wait-for-approval  *(route: `registrationSuccess`)*
- Referenced as the "wait for approve" destination after Page 008. `[CONFIRM scope]`

## Page 011 — Approval-process indicator  *(route: `registrationStatus`)*
- Shows the **approval process indicator**.
- **API: GET status.** `[API?]`
- Access: **logged-in (AUTH), profile ready but NOT yet approved.**

## Page 012 — Home  *(route: `home`)*
- **No login needed**; **no data for now**.
- May show **open-session / product link**; this data **preferred via YouTube streaming**.
- **Some features depend on the app privilege** (Guest/login, Visitor, Staff, …).
- **On login the app gets all data + privilege and caches it.**

## Page 015 — Map (2D)  *(route: `venueMap`)*
- **2D map.** `[API?]` — venue-map nodes (expected: `/app/venue-map`, D-230).

---

## Open items to resolve in analysis
1. **Page-number reconciliation** — owner numbers vs `route_names.dart` (e.g. owner "Page 012
   = Home" vs route table "screen 13 = home"); resolve against `Mockup.html`. 41-vs-49 count.
2. **Face-recognition / biometric** — confirm "phase reconicion" = Face ID/biometric unlock;
   maps to the existing device-key (biometric) sign-in surface.
3. **Forgot-password OTP API** — verify `/app/auth/forgot-password` + `/app/auth/reset-password`
   exist and are mobile-usable (NOT CP-only).
4. **Profile lookups** — confirm an API exists for each of country / company / organization /
   profileType / interest; identify gaps (company lookup may be missing).
5. **Profile-complete endpoint** — confirm an endpoint returns success + userId + contact info
   and marks the profile complete.
6. **Approval-status endpoint** — confirm a GET status / `users/me` registration-status read.
7. **Home privilege bundle** — confirm a single "on login fetch all data + privileges" payload
   to cache, and the YouTube/open-session source.
8. **Store version-check** — confirm this is store-native (no SIMF API).
