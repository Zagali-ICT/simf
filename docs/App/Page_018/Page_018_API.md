# Page 018 — API (مقعدي · خريطة الجلوس · My Seat map)

Authoritative backend contract for this page. Inherits the `ApiResult<T>`
envelope, headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001
§3–§4. The status-derivation rules are in [Page_018_Logic.md](Page_018_Logic.md).

> **Status:** **BUILT — no new API (D-267).** The full seat grid + status + the
> user's own seat come from the existing `GET /app/sessions/{id}/seats`
> (`SessionSeatMap`, D-175); the reserve / release path reuses the existing
> reserve endpoints; navigation (→ Map 15) and share are **client-local**.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the routes below are `GET /api/v1/app/sessions/{id}/seats` etc.

## E1 — `GET /app/sessions/{sessionId}/seats`  (the seat map)  **(BUILT — D-175)**
| | |
|---|---|
| Full route | `GET /api/v1/app/sessions/{sessionId:guid}/seats` |
| Access | **`RequireApprovedAccount`** — approved, signed-in (login-only page) |
| Returns | `ApiResult<SessionSeatMap>` — the whole grid + `myCell` |

```jsonc
// SessionSeatMap
{
  "sessionId": "guid", "hallId": "guid",
  "hallCapacity": 96, "sessionCapacity": null,
  "rowLabels": ["A","B","C","D","E","F","G","H"],   // ← grid rows
  "seatsPerRow": 12,                                  // ← cells per row
  "reservedCells": [                                  // ← the OCCUPIED seats only
    { "reservationId": "guid", "rowLabel": "C", "seatNumber": 5, "kind": "UserBooking" },
    { "reservationId": "guid", "rowLabel": "A", "seatNumber": 1, "kind": "AdminReservedRow" }
  ],
  "myCell": {                                         // ← the user's own seat ("Main"); null if none
    "reservationId": "guid", "rowLabel": "B", "seatNumber": 12, "kind": "UserBooking"
  },
  "activeReservedCount": 13
}
```

### Status derivation (client)
| Cell state | Rule |
|---|---|
| **mine** (`مقعدك`) | `(row,seat) == myCell` |
| **reserved** (`محجوز`) | `(row,seat) ∈ reservedCells` (any `kind`) |
| **available** (`متاح`) | within `rowLabels × seatsPerRow` and **not** in `reservedCells` |

`kind` values: `UserBooking` / `RandomAssignment` (a visitor's seat) ·
`AdminReservedRow` (an admin-blocked row — unpickable).

## E2 — reserve / release (for the optional interactive picker)  **(BUILT — D-175 / D-227)**
| Route | Verb | Does |
|---|---|---|
| `/api/v1/app/sessions/{id}/seats/reserve` | POST | self-pick a free `{rowLabel, seatNumber}` (body `ReserveSeatRequest`) → `MySeatReservation` (held `Pending`) |
| `/api/v1/app/sessions/{id}/seats/reserve-random` | POST | server allocates a free seat → `MySeatReservation` |
| `/api/v1/app/sessions/{id}/seats/mine` | DELETE | release the user's held seat |

All `RequireApprovedAccount`. Used **only** when the grid runs as a picker
(Page_018_Logic L-4); the read-only map needs just E1. After any of these, the app
re-reads E1 to repaint. Server guards (already enforced): `SEAT_ALREADY_RESERVED`
(409), `SEAT_ALREADY_OWNED_BY_SESSION` (409), `SEAT_SESSION_FULL` (409),
admin-blocked row (409).

## E3 — navigation + share are client-local (no endpoint)
| Action | Behaviour | Server call |
|---|---|---|
| `إرشادي إلى مقعدي` (navigate) | screen-navigate to **Map (15, Page_015)** | **none** (turn-by-turn seat routing is deferred, D-211) |
| `مشاركة الموقع` (share) | open the **native share sheet** | **none** |

## Error responses
| HTTP | When |
|------|------|
| 401 | no/expired token — the route is auth-gated, the screen is not reachable |
| 403 | account not approved (pending/rejected) |
| 404 | session missing / soft-deleted |
| 409 | (picker only) seat taken / already own a seat / session full / row blocked |

## Build dependencies
**None.** All endpoints exist and are tested
(`tests/SIMF.Api.Tests/SeatReservationsTests.cs` — reserve/release/random/admin-row,
the `MyCell` read (Page_017, D-265), and the **full grid** read (D-267)). No schema
change, no migration, no new permission — this page is a **reuse** of the shipped
seat surface.
