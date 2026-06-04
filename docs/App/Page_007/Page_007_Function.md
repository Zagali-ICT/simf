# Page 007 — Function (إنشاء حساب · زائر · Sign up — visitor)

What this screen does, the user steps, and the auth gate. Business rules are in
[Page_007_Logic.md](Page_007_Logic.md); the contract is in [Page_007_API.md](Page_007_API.md).

## Purpose
Profile completion for a signed-in visitor. The user fills the richer registration
form (name, nationality, mobile, الجهة / organisation, gender, profile-type) and the
**interests sub-step** (cards, min 1 / max 10). On save the profile row is created /
updated, the account is marked **profile-complete**, and the user is routed to the
**wait-for-approval** state.

## Privilege / auth gate
| | |
|---|---|
| App privilege | **AUTH-only** — any signed-in account. **No role, no permission code** (D7). |
| Token | Standard bearer JWT from sign-in; the screen reads `userId` / `email` from the **cached sign-in**, never from a form field. |
| Approval | The screen is reachable **before** approval — completing it is what moves the account toward approval. The lookups + upsert are **not** approval-gated. |
| Not anonymous | Every call requires sign-in; none is `AllowAnonymous`. |

## Elements
| # | Element | Source | Notes |
|---|---------|--------|-------|
| 1 | Arabic name / English name | text | required |
| 2 | Job title | text | optional (D-163) |
| 3 | Nationality | **country lookup** | `GET /app/account/user-profile/countries` |
| 4 | Is-Saudi toggle + national id / iqama / passport | conditional | shape enforced by validator |
| 5 | Saudi mobile / international mobile | text | conditional on is-Saudi |
| 6 | Date of birth | date picker | **required**, registrant must be ≥ 18 (D-197) |
| 6b | Place of birth | text | optional (max 128) |
| 7 | الجهة (Organisation) | **organisation lookup** (typeahead) | `GET /app/organisations?search=&top=` — COMPANY dropped (D6) |
| 8 | Gender | enum picker | optional (D-221) |
| 9 | Profile type | **profile-type lookup** (cards) | `GET /app/account/profile-types`; optional self-pick (D-190) |
| 10 | **Interests** (sub-step) | **interests lookup** (cards) | `GET /app/account/interests`; **min 1 / max 10** (D-050 / D12) |
| 11 | Save | button | triggers the upsert |

## User steps
1. The app opens the screen for a signed-in but profile-incomplete account.
2. On open, the screen calls the four lookups (countries, organisations seed, profile-types, interests) and pre-fills any existing values from `GET /app/account/user-profile`.
3. The user fills the form fields (elements 1–9).
4. The user advances to the **interests sub-step** and picks **at least 1, at most 10** interest cards (this is the owner's "Page 008", rendered inline — D12).
5. The user taps **Save**.
6. The app sends one `POST /app/account/user-profile` carrying the form fields **and** the picked `InterestIds`.
7. On `ApiResult.Ok`, the profile is complete; the app routes to **wait-for-approval**.
8. On a validation error, the app shows the field/toast message and stays on the screen.

## Navigation
- **In:** from the sign-up flow after credentials / verification (visitor branch).
- **Out (success):** wait-for-approval screen.
- **Out (back):** previous sign-up step (no profile write).

## Acceptance criteria
- The screen renders only for a signed-in account; an anonymous open is impossible.
- All four lookups populate their pickers; an empty lookup shows the empty state, not an error.
- Save is blocked until **1–10** interests are picked and the required fields are valid.
- **Date of birth is required** and the registrant must be **at least 18** (D-197): the date picker's selectable range ends at *today − 18 years*, and the server re-validates.
- A successful save returns the upserted profile and the account becomes profile-complete and awaits approval.
- The body never carries a user id / email — the actor comes from the token (D7).
