# SIMF App — API Gap Analysis (per page)

| Field | Value |
|-------|-------|
| Date | 2026-06-03 |
| Method | 11-agent deep analysis: owner requirements × Mockup.html × Screen Guide × current `/app` API |
| Headline | Most app APIs already exist under `/api/v1/app/*`. Genuine build items are few; several **owner decisions** block documentation/build. |

Legend: ✅ exists · 🟡 partial (exists but a gap) · ❌ missing (build) · — no API needed

## Per-page status

| Page | Screen | Core APIs | Status |
|------|--------|-----------|--------|
| **001** Splash | splash | session resume `/app/auth/refresh` ✅, identity `/app/account/profile` ✅; version-check = **store-native** (—); first-run/resume = local (—) | ✅ (app-side) |
| **002** Onboarding videos | onboarding | none (owner: no API). Optional remote swap via existing `/app/content/{key}` ✅ | ✅ / — |
| **003** Login | signIn | sign-in ✅, refresh ✅, biometric device-key trio ✅, forgot-password ✅, reset-password ✅ — **all exist** | ✅ + decisions |
| **004/006** Sign-up | signUp/emailOtp | sign-up ✅, verify-email (6-digit) ✅, resend ✅ | ✅ + **1 conflict** |
| **007** Profile | signUpVisitor | country ✅, profile-type ✅, interest ✅, organisation ✅, **company ❌(not needed)**, save `/app/account/user-profile` ✅ | ✅ + decision |
| **008** Interests | interests | interests list ✅, max10/min1 server-enforced ✅; **no interests-only save** 🟡, **response lacks userId+contact** 🟡, **no "complete" flag** 🟡 | 🟡 + decision |
| **009** T&C | terms | content `/app/content/{key}` ✅ but **no terms.* block seeded** 🟡; **acceptance not recorded server-side** ❌ | 🟡 + decision |
| **011** Approval status | registrationStatus | **`GET /app/users/me` does NOT exist** ❌ — the Flutter app already calls it | ❌ **build** |
| **012** Home | home | privilege from JWT claim ✅, unread-count ✅, agenda/news deep-links ✅; **no on-login bundle** 🟡, **no live/YouTube banner** ❌(D7) | 🟡 + decision |
| **015** Map 2D | venueMap | venue-map nodes ✅, booths ✅+detail ✅; **booth popup logo/hall-name** 🟡 | ✅ + minor |

## Clear build items (small, low-risk)
1. **`GET /api/v1/app/users/me`** — returns live `AccountState` (registrationStatus) + identity, for Page 011 approval polling. The Flutter app **already calls this route**; it just isn't built. **No schema change** (AccountState exists). Must align wire names (backend `PendingApproval`/`EmailVerified` vs app `Pending`/`Approved`/`Rejected`; app never receives `Approved` today).
2. **Seed a `terms.*` content block** (Page 009) — reuse the existing CMS; **no new endpoint**, just a seed (mirrors the shipped `cyber.*`).

## Owner decisions needed (block doc/build; with recommendation)

> **RESOLVED 2026-06-03 → `DECISIONS_LOG` D-249.** The owner accepted the
> recommendations ("recommended", "do"). Per-item outcome: **D2** = client-side
> caps only (server stays 256/128, frozen-schema aligned); **D4** Nafath, **D8**
> T&C consent record (Identity freeze-lift), **D10** live provider, **D11** mockup
> decorations = **DEFERRED**; all others accepted as written. Builds that follow:
> `GET /app/users/me`, D1 refresh-lifetime config-bind (→5d), `GET /app/bootstrap`,
> and the `Page_014` aggregates. See D-249 for the full resolution.
| # | Page | Decision | Recommendation |
|---|------|----------|----------------|
| **D1** | 003 | "Session 5 days" vs implemented **30-day** refresh/device-key (hardcoded `TimeSpan.FromDays(30)`, not config). | Make it **config-bound** + set to your number (5d?). |
| **D2** | 003/004 | Field caps: owner email ≤50 / pwd ≤32 vs validators email ≤256 / no pwd-max (sign-up pwd 8–128). | Align validators to **50/32** (small change). |
| **D3** | 003 | 2FA email-OTP branch (`verify-otp`) at sign-in — app must handle it. | App handles; no API change. |
| **D4** | 003 | **Nafath** button in mockup, **not** in your spec, no endpoint. | Confirm: **drop** for now, or scope a Nafath integration (large). |
| **D5** | 004 | ⚠️ **Conflict:** you want **409 "already have account → reset/login"**; backend (D-198) is **enumeration-resistant** — returns generic 201, no 409 (emails owner out-of-band). App has a dead 409 path. | **Keep D-198** (security) + rewrite app UX to the generic "check your email" screen. Building a 409 re-introduces account enumeration. |
| **D6** | 007 | **Company** lookup — visitor profile has **no company** (company = exhibitor/sponsor CP concept). | **Drop** company from the visitor profile (or freeze-lift to add it — confirm intent). |
| **D7** | 008 | Interests save: today bundled into the **full profile upsert** (can't save interests alone); response lacks **userId+contact**; "complete" is an `AccountState` side-effect. | Either **app reads userId/email from cached sign-in** (no build), or extend the save response + add a profile-complete flag (small build). |
| **D8** | 009 | T&C **acceptance record** — client-only today; an auditable consent touches the **frozen Identity schema**. | Recommended for NCA: additive `TermsAcceptedAtUtc`+version (freeze-lift) — your call. |
| **D9** | 012 | **On-login bundle** `GET /app/bootstrap` (one call caches all) vs app composes from many `/app/*` reads. | Build **`/app/bootstrap`** — matches your "fetch all + cache" rule, fewer round-trips. |
| **D10** | 012 | **Live/open-session YouTube** banner — no API; blocked on live-provider procurement (D7/D-211). | Ship home **without** live API now (your "no data for now"); revisit at procurement. |
| **D11** | 011/015 | Mockup extras: approval **reference # + submitted date** (011); booth popup **logo + hall name** (015) — not in the API today. | Confirm real vs decoration; add fields only if real. |
| **D12** | all | **Page-number reconciliation**: your numbers vs route table (e.g. Home you call 012, route table 13). 41-vs-49 count. | I'll standardise on the **Mockup.html** numbers; confirm. |
