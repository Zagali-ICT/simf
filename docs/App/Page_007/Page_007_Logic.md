# Page 007 — Logic (إنشاء حساب · زائر · Sign up — visitor)

Client + server logic, validation, state transitions, and edge handling. The
contract lives in [Page_007_API.md](Page_007_API.md); the user flow in
[Page_007_Function.md](Page_007_Function.md).

## L-1 — Auth gate
AUTH-only. Every call requires a signed-in bearer token; **no role, no permission
code, not approval-gated, not `AllowAnonymous`** (D7). The four lookups and the
profile upsert all sit in the `auth` rate-limit bucket and resolve the actor from
the `sub` claim — the request body never carries a user id / email.

## L-2 — Lookup sources (read-on-open)
| Picker | Endpoint | Rows |
|--------|----------|------|
| Nationality | `GET /app/account/user-profile/countries` | active countries (code + AR/EN name) |
| الجهة / Organisation | `GET /app/organisations?search=&top=20` | active organisations (typeahead; COMPANY dropped — D6, uses the D-220 lookup) |
| Profile type | `GET /app/account/profile-types` | active, non-Admin profile types (D-190) |
| Interests | `GET /app/account/interests` | active interests, ordered by `DisplayOrder` then `Name` (D-050) |

Pre-fill: `GET /app/account/user-profile` returns any existing values (including
`ProfileTypeId`, `OrganisationId`, `InterestIds`, `QrId`) so a re-entry shows the
saved state. Every field is empty / null on a first-time profile.

## L-3 — Interests sub-step (owner "Page 008", D12)
The interests picker is a **sub-step of this screen**, not a separate route. The
selected ids are held in screen state and submitted **inside the same profile
upsert** — there is no standalone interests write (D7). Rule: **min 1, max 10**,
distinct, all active.

## L-4 — Validation (client mirrors server)
Server rules (`UpsertUserProfileRequestValidator`) the client mirrors:
- **Interests:** required, **1–10**, no duplicates, no unknown / deactivated ids.
- **Organisation:** optional; if present must be a valid, active organisation id.
- **Profile type:** optional self-pick; rejects unknown / inactive / Admin-scope rows. Admin pre-pick wins over the user self-pick (`UserProfileService.UpsertMineAsync`).
- **Identity-doc shape:** national id / iqama / passport consistency keyed off the is-Saudi flag.

The client blocks **Save** until the required fields and the 1–10 interests rule
pass; the server re-validates as defence-in-depth and returns a field error on
violation.

## L-5 — State transitions
```
profile-incomplete ──[fill form + pick 1–10 interests]──▶ ready
ready ──[POST upsert → ApiResult.Ok]──▶ profile-complete ──▶ wait-for-approval
ready ──[POST upsert → validation error]──▶ stay on screen (field/toast)
```
The upsert is idempotent: the first call creates the row, every later call updates
it. Marking the profile complete is a consequence of a successful save, not a
separate call.

## L-6 — Error / empty / RTL handling
- **Empty lookup:** show the picker's empty state, never a blocking error.
- **Validation error:** map the API error code to the offending field; keep the user on the screen with their input intact.
- **Network / 500:** show a retry toast; the unsent form state is preserved.
- **RTL:** Arabic is the primary locale; pickers, toggles, and the interests grid mirror; AR/EN labels come from each lookup row.

## L-7 — Dependencies
- Country / organisation / profile-type / interest lookup data must be seeded for the pickers to populate (organisation via the D-220 module; interests via D-050; profile types via D-190).
- Account must be signed-in and profile-incomplete for the screen to apply.
