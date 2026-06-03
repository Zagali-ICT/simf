# Page 011 — Logic (حالة التسجيل · Registration status)

Business rules behind the screen. The user flow is in
[Page_011_Function.md](Page_011_Function.md); the wire contract is in
[Page_011_API.md](Page_011_API.md).

## Data source
The screen is driven by a **single read**: `GET /app/users/me`
(**TO BUILD, in-progress this wave** — see [Page_011_API.md](Page_011_API.md)). The
Flutter app already calls this endpoint; the backend route is being built now. The
relevant field is `registrationStatus`.

## L-1 — Status mapping (AccountState → registrationStatus)
The API does **not** expose the raw internal `AccountState`. It maps the lifecycle
state to a small public tri-state:

| `registrationStatus` (public) | Backing `AccountState` | Meaning on this screen |
|------------------------------|------------------------|------------------------|
| `Approved` | `Approved` | Account is approved → leave the screen |
| `Pending` | `Registered`, `EmailVerified`, `PendingApproval` | Still in the approval process → wait / re-check |
| `Rejected` | `Rejected` | Application was declined |

Only these three values are valid on the wire. The client treats any unknown value as
an error state (see L-6), not as a silent default.

## L-2 — Client state machine
```
            open / re-check
              │  GET /app/users/me
              ▼
        ┌───────────┐
        │  Loading  │
        └─────┬─────┘
   success    │    failure
   ┌──────────┼──────────┐
   ▼          ▼          ▼
Approved   Pending    Rejected        Error (L-6)
   │          │
Continue   Re-check ──┐ (re-fetch, back to Loading)
   ▼          └───────┘
leave screen
```
- **Loading** → shown on first open and on every Re-check while the call is in flight.
- **Pending** → terminal-on-screen until the user re-checks or the status changes.
- **Approved** → enables **Continue**; the router then moves the user out.
- **Rejected** → renders the rejected copy; no Continue.
- **Error** → call failed (network / 401 / 5xx / unknown value) — retry affordance.

## L-3 — State transitions
| Current | Event | Next |
|---------|-------|------|
| Loading | success, `Approved` | Approved |
| Loading | success, `Pending` | Pending |
| Loading | success, `Rejected` | Rejected |
| Loading | failure / unknown value | Error |
| Pending | Re-check tapped | Loading |
| Approved | Continue tapped | (leaves screen) |
| Error | Retry tapped | Loading |

The screen never transitions **out of** Approved back into Pending on its own — once
approved, the router takes over.

## L-4 — Re-check (polling) rule
- Re-check is **user-initiated** (button), not an automatic background poll, to keep
  it deterministic and avoid hammering the endpoint.
- Each Re-check is a fresh `GET /app/users/me`; the screen re-renders purely from the
  returned `registrationStatus`.
- If the design later wants auto-refresh, it should be a bounded interval — out of
  scope for the current wave.

## L-5 — Approval reference number + date (D11) — DECORATION
Per **D11**, the approval **reference number** and **date** shown on this screen are
**presentational decoration only**. They are **not built** and **not returned** by
`GET /app/users/me`. The client must **not** depend on them, parse them, or block any
transition on them. If shown, they are static layout, never live data.

## L-6 — Validation & error handling
| Condition | Handling |
|-----------|----------|
| `GET /app/users/me` returns non-success `ApiResult` | Show **Error** state with retry; do not assume a status |
| `registrationStatus` missing / unknown value | Treat as **Error** (no silent fallback to Pending) |
| `401 Unauthorized` (token expired) | Route to sign-in — the pending session is no longer valid |
| Network failure / timeout | **Error** state with retry; previous status not falsely retained |
| Approved arrives mid-session | Allow **Continue**; router re-evaluates privilege |

No silent fallback: an unreadable status is an **error**, never a guessed `Pending`.

## L-7 — Edge cases
- **Already approved on first load** — the boot flow should not route here, but if it
  does and the API returns `Approved`, the page immediately offers **Continue**.
- **Status flips Pending → Rejected** between re-checks — the screen updates to the
  rejected state; no stale pending copy remains.
- **Status flips Rejected → Pending/Approved** (admin reversal) — a Re-check reflects
  the new value; the screen is always a pure render of the latest read.
- **Decoration ref/date absent** — never an error; the screen renders without them.

## Dependencies
- **`GET /app/users/me`** — the only backing call (**TO BUILD, in-progress**).
- App router / privilege gate — owns entry (non-approved in) and exit (approved out);
  this page does not self-route except via its Continue / Sign-out actions.
