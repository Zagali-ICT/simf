# Page 007‑01 — اهتماماتي · Sign up — interests (new · D-332)

Per-page documentation folder. The interests step of sign-up — split out of Page 007
to match `Mockup.html` screen **5‑01** (D-332, reversing D12).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_007-01_Function.md](Page_007-01_Function.md) | Elements, user steps, navigation, acceptance criteria |
| Logic | [Page_007-01_Logic.md](Page_007-01_Logic.md) | Auth gate, the 1–10 rule, the combined save, edge cases |
| API | [Page_007-01_API.md](Page_007-01_API.md) | The interests lookup + the single profile upsert (the Save) |
| Design | [Page_007-01_Design.md](Page_007-01_Design.md) | Flutter screen design — layout, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **5‑01** (`Mockup.html`) — اهتماماتي / interests |
| Route | `RouteNames.signUpInterests` → `/sign-up/interests` *(to add in step 2 — Flutter)* |
| Titles | AR **اهتماماتي** · EN **My interests** |
| Section | 1 — Onboarding / account |
| Nature | **Interests pick (1–10) + the single profile save** |
| App privilege | **AUTH-only** — any signed-in account; **no role / no permission code** (D7) |
| Status | **🟠 Docs created (D-332) — Flutter build pending.** |

## What this screen does
- **Step 4** of the corrected sign-up flow: Register (`Page_005`) → OTP (`Page_006`) →
  **Data ([Page 007](../Page_007/README.md))** → **interests (this screen)** → save →
  Confirmation (`Page_010`).
- Receives the **profile form state collected on Page 007** (in memory), shows the
  **interest cards**, and requires the user to pick **1–10** (a `n/10` counter).
- **Owns the single save.** On **Save** it fires one
  `POST /app/account/user-profile` carrying the **Page-007 data + the picked
  `interestIds`** (the API requires interests on the upsert — there is no separate
  interests write). On success the profile is complete → the app shows the
  **"please wait" / Confirmation** state (`Page_010`).
- The optional **ID-document image** (picked on Page 007) is uploaded **after** the
  profile row exists, here on save.

## Why it is its own screen
The mockup has interests as a separate frame (5‑01); the owner's flow lists "Select
Interests" as its own step. D-332 **reverses D12** (which had folded it into Page 007).
The API was always a single `POST` — so the data screen (007) and the interests screen
(007‑01) both feed one save, fired here at the end.

## Sources of truth
`Mockup.html` (visual, screen 5‑01) · `SIMF_Screen_Guide_and_User_Journey` (Screen 5‑01) ·
SIMF-MOB-API-001 · SIMF-MAA-001 · DECISIONS_LOG D-050 / D12 / **D-332**.
