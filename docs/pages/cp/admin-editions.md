# CP — Event edition (`/admin/editions`)

| | |
|---|---|
| **Route** | `/admin/editions` |
| **Component** | `Components/Pages/Admin/EventEdition.razor` (+ `.razor.cs`) |
| **Permission** | `Editions.View` (Administrator baseline) gates the page and the read API; `Editions.Open` gates the one action and its API — split because opening a year clears every badge in the system, which is not an authority a viewer should hold |
| **Nav** | System → **Event edition** (`Module.EventEdition`) |
| **Decision** | D-879, D-886 |
| **E2E** | [`cp-admin-editions.md`](../../tests/e2e/cp-admin-editions.md) (E2E-CPED-001..010) |

## Purpose

The forum recurs: an admin opens a year, content and registrations accumulate
against it, then the year is closed into history and the next one opens. That is
the permanent operating model, not a one-off.

Before this, nothing live carried a year at all. The only other year in the
domain is hand-typed archive content with no foreign key to any live row, so a
minted QR had **no expiry of any kind** — last year's badge opened this year's
gate.

## The page is mostly about one consequence

Opening a year **clears every attendee's badge**. That is not a side effect to
be discovered afterwards: refusing last year's badge at a gate is only correct if
the holder has a route to this year's, so the expiry and the re-issue are one
operation. The page is shaped around making that impossible to miss:

- a standing warning above the field, not only inside the dialog;
- the primary button opens a **confirmation**, never the year;
- the confirm dialog states the closing year, the opening year, and that every
  badge will be cleared, and its own button says so too;
- the year is **typed**, not incremented by a stepper — the next year is
  pre-filled as a convenience, and still has to be confirmed;
- the resulting **count** is surfaced on success and kept on the page, because it
  is the only evidence an operator has that the re-issue actually ran, and the
  first thing they will be asked when a returning attendee finds their badge dead.

## Data flow

- **Read** — `GET /account/api/admin/editions/current` →
  `AdminEventEditionResponse` (year, opened-at, previous-close, last re-issue
  count). Loaded on init; three distinct states (loading / failed-with-retry /
  loaded), never two.
- **Open** — `POST /account/api/admin/editions/open` (`{ Year }`) →
  `AdminOpenEditionResponse` (year, badges cleared). The page reloads afterwards
  so the displayed year and count come from the server rather than from the
  request.

Both are mapped explicitly in `AccountEndpoints.Programme.cs`. This host is a BFF
with **no catch-all proxy**: a page calling `/account/api/...` with no mapping
compiles cleanly and 404s at runtime.

## Refusals the page surfaces rather than hides

The dialog stays **open** when the server refuses, so the correction is made
where the mistake was:

| Case | Server | Why |
|---|---|---|
| The year is already open | 409 | Nothing to do, and clearing every badge for a no-op would be the worst possible outcome |
| A year earlier than the open one | 409 | Re-opening a closed year would make every badge issued since valid again, which is the opposite of what closing it meant |
| Outside 2000–2999 | 400 | The year is two bytes inside the badge; a typo has to be refused before it is printed onto anything |

The four-digit range is also checked client-side before the dialog opens, so an
obvious typo is corrected while the field is still in front of the operator.

## Not on this page

- **Closing a year on its own.** There is no separate close: a year is closed by
  opening the next one, because a forum with no open edition is not a state the
  gate can answer for.
- **Per-attendee re-issue.** The clear is system-wide; badges are re-issued
  through the ordinary approval and bulk-order paths.
