# Page 007‑01 — Function (اهتماماتي · Sign up — interests)

What this screen does, the user steps, and the auth gate. Business rules are in
[Page_007-01_Logic.md](Page_007-01_Logic.md); the contract is in
[Page_007-01_API.md](Page_007-01_API.md).

> **New (D-332).** The interests step (mockup 5‑01), split out of Page 007. It **owns
> the single profile save** — the upsert carries the Page-007 data **+** the picked
> interests.

## Purpose
Let a signed-in visitor pick their **interests (1–10)** and then **save the whole
profile** (the data collected on Page 007 + the interests) in one request, completing
registration and moving to wait-for-approval.

## Privilege / auth gate
| | |
|---|---|
| App privilege | **AUTH-only** — any signed-in account. **No role, no permission code** (D7). |
| Token | Bearer JWT from sign-up/verify; actor from `sub` (D7) — body carries no user id. |
| Approval | Reachable before approval — this save is what completes the profile. |

## How the user reaches it
- From **Page 007 (profile data)** → tap **Next** (the Page-007 form state is carried in memory).

## Elements
| # | Element | AR | Source | Notes |
|---|---------|----|--------|-------|
| 1 | Title + helper | اختر اهتماماتك · 1–10 | static | "pick 1–10 — used to suggest people & sessions" |
| 2 | Interest cards | — | **interests lookup** | `GET /app/account/interests`; multi-select, ordered by `displayOrder` |
| 3 | Counter | n / 10 | derived | live selected count |
| 4 | **Save** | حفظ / إنهاء | button | one `POST /app/account/user-profile` (data + interestIds); disabled until 1–10 |

## User steps
1. The screen opens with the Page-007 form state in memory and calls `GET /app/account/interests`.
2. The user selects **at least 1, at most 10** interest cards (the counter updates; Save enables at ≥ 1).
3. The user taps **Save**.
4. The app sends **one** `POST /app/account/user-profile` carrying the Page-007 fields **and** the picked `interestIds`; if an ID image was picked on Page 007, it is uploaded after the row exists.
5. On `ApiResult.Ok` → the profile is complete; the app shows **"please wait"** and routes to **Confirmation (Page 010)**.
6. On a validation error → show the field/toast message and stay on the screen.

## Navigation
- **In:** from **Page 007 (Next)** with the form state.
- **Out (success):** **Page 010** (registration success / "please wait" → confirmation), then **Page 011** (registration status).
- **Out (back):** **Page 007** (the data is preserved in memory so the user can edit and return).

## Acceptance criteria
- AC1 — Renders only for a signed-in account; an anonymous open is impossible.
- AC2 — The interest cards populate from the lookup; an empty lookup shows the empty state, not an error.
- AC3 — **Save is disabled until 1–10 interests are picked**; picking an 11th is blocked.
- AC4 — Save fires **exactly one** `POST /app/account/user-profile` carrying the Page-007 data **and** `interestIds`; there is no separate interests write.
- AC5 — On success the user sees the **"please wait" / Confirmation** state and the account is profile-complete, awaiting approval.
- AC6 — On a server validation error the user stays on the screen with the selection intact.
- AC7 — Full **RTL** in Arabic; labels from resources + lookup rows.
