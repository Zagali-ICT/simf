# Page 007‑01 — اهتماماتي · Sign up — interests (new · D-332)

*Last updated: 2026-06-13 — conformance pass against the as-built KSA-Project screen (D-365).*

Per-page documentation folder. The interests step of sign-up — split out of Page 007
to match `Mockup.html` screen **5‑01** (D-332, reversing D12). Rebuilt 2026-06-11 to
the **KSA-Project Figma frame 505:1083** (D-365).

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_007-01_Function.md](Page_007-01_Function.md) | Elements, user steps, navigation, acceptance criteria |
| Logic | [Page_007-01_Logic.md](Page_007-01_Logic.md) | Auth gate, the 1–10 rule, the combined save, edge cases |
| API | [Page_007-01_API.md](Page_007-01_API.md) | The interests lookup + the single profile upsert (the Save) |
| Design | [Page_007-01_Design.md](Page_007-01_Design.md) | Flutter screen design — layout, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **5‑01** (`Mockup.html`) — اهتماماتي / interests · now styled to KSA-Project Figma **505:1083** (D-365) |
| Route | `RouteNames.signUpInterests` → `/sign-up/interests` (router sentinel number **701**) |
| Titles | AR **اهتماماتي** · EN **My interests** |
| Section | 1 — Onboarding / account |
| Nature | **Interests pick (1–10) + the single profile save** |
| App privilege | **AUTH-only** — any signed-in account; **no role / no permission code** (D7) |
| Status | **🟢 Built — redesigned to the KSA-Project frame 505:1083 (D-365, 2026-06-11).** Live check N/A (auth+draft-gated); widget tests stand in. |

## What this screen does
- **Step 4** of the corrected sign-up flow: Register (`Page_005`) → OTP (`Page_006`) →
  **Data ([Page 007](../Page_007/README.md))** → **interests (this screen)** → save →
  Success (`Page_010`).
- Receives the **`SignUpProfileDraft` built on Page 007** (in memory, via the route
  `extra`), loads the **interest pills** from the lookup, and requires the user to
  pick **1–10** (a live `n / 10` counter; ids already on the draft are pre-selected).
- **Owns the single save.** On **متابعة (Continue)** it fires one
  `POST /app/account/user-profile` carrying the **Page-007 data + the picked
  `interestIds`** (the API requires interests on the upsert — there is no separate
  interests write). On success it shows the saved toast and routes to
  **Page 010 (registration success)**, passing the freshly issued
  **registration reference** (`referenceNumber`, D-373) as the route extra.
- The optional **ID-document image** (picked on Page 007) is uploaded **after** the
  profile row exists, here on save. An upload failure is **non-blocking**: the
  profile is saved, a warning toast shows, and the flow still reaches Page 010.

## Why it is its own screen
The mockup has interests as a separate frame (5‑01); the owner's flow lists "Select
Interests" as its own step. D-332 **reverses D12** (which had folded it into Page 007).
The API was always a single `POST` — so the data screen (007) and the interests screen
(007‑01) both feed one save, fired here at the end.

## Sources of truth
KSA-Project Figma frame **505:1083** (visual, D-365) · `Mockup.html` (flow, screen 5‑01) ·
`SIMF_Screen_Guide_and_User_Journey` (Screen 5‑01) · SIMF-MOB-API-001 · SIMF-MAA-001 ·
DECISIONS_LOG D-050 / D12 / **D-332** / **D-365** / D-373.
