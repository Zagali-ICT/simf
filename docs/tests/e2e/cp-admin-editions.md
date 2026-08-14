# E2E test catalogue — Event edition (`/admin/editions`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Page** | [`cp/admin-editions.md`](../../pages/cp/admin-editions.md) |
| **Route** | `/admin/editions` |
| **Surface** | Control Panel (Blazor Server) |
| **Permission** | `Editions.View` page + read; `Editions.Open` the action |
| **Auth setup** | Control-Panel admin sign-in; the TOTP step uses the `Get-Totp` helper, never a literal secret |
| **Last reviewed** | 2026-08-14 |

## What this page is for

The forum recurs. An admin opens a year here, which closes the current one into
history — and **clears every attendee's badge**, because a badge is only valid
in the year it was issued for and refusing last year's at a gate is only correct
if the holder has a route to this year's.

Most of the scenarios below are about that consequence being impossible to miss
and impossible to trigger by accident, because that is where this page can do
real damage: run it mid-event by mistake and the whole population is locked out
until every badge is re-issued.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CPED-001 | The page shows the open year, when it opened, and the last re-issue count | happy | P0 | automated + driven |
| E2E-CPED-002 | Opening a year requires confirmation and states the consequence | safety | P0 | automated + driven |
| E2E-CPED-003 | Cancelling the dialog changes nothing | safety | P0 | manual |
| E2E-CPED-004 | Opening a year clears every badge and reports how many | happy | P0 | automated + driven |
| E2E-CPED-005 | Re-opening the open year is refused and the dialog stays open | negative | P1 | automated + driven |
| E2E-CPED-006 | An earlier year is refused and the dialog stays open | negative | P1 | manual |
| E2E-CPED-007 | A malformed year is refused before the dialog opens | validation | P1 | automated |
| E2E-CPED-008 | A load failure offers Retry rather than reading "Loading…" forever | resilience | P1 | automated |
| E2E-CPED-009 | View-only cannot open a year (Editions.Open gate) | auth-gate | P0 | automated + driven |
| E2E-CPED-010 | The page renders correctly in Arabic (RTL) | i18n | P1 | driven |

## Scenarios

```gherkin
Scenario: E2E-CPED-001 The page shows the open year, when it opened, and the last re-issue count
  Given an admin holding "Editions.View"
   When they open "/admin/editions"
   Then the open year is shown
    And the moment it was opened is shown in Saudi local time
    And "Badges cleared by the last opening" shows a number
    And when no year has ever been closed, the previous-close row reads
        "No year has been closed yet" rather than being blank

Scenario: E2E-CPED-002 Opening a year requires confirmation and states the consequence
  Given an admin holding "Editions.Open" on "/admin/editions"
    And the open year is 2026
   When they enter 2027 and press "Open the year"
   Then no year is opened yet
    And a confirmation appears naming BOTH years
    And it states that every attendee's badge will be cleared
    And its confirm button reads "Open the year and clear every badge"

Scenario: E2E-CPED-003 Cancelling the dialog changes nothing
  Given the confirmation is open
   When the admin cancels
   Then the dialog closes
    And the open year is unchanged
    And no badge has been cleared

Scenario: E2E-CPED-004 Opening a year clears every badge and reports how many
  Given the open year is 2026 and two attendees hold badges
   When the admin opens 2027 and confirms
   Then a success message names 2027 and the number of badges cleared
    And the page reloads showing 2027 as the open year
    And "Badges cleared by the last opening" shows that same number
    And both attendees still exist, with their QR cleared

Scenario: E2E-CPED-005 Re-opening the open year is refused and the dialog stays open
  Given the open year is 2027
   When the admin enters 2027 and confirms
   Then the API answers 409
    And the error is shown
    And the dialog REMAINS open, so the correction is made where the mistake was
    And no badge has been cleared

Scenario: E2E-CPED-006 An earlier year is refused and the dialog stays open
  Given the open year is 2027
   When the admin enters 2026 and confirms
   Then the API answers 409 - re-opening a closed year would make every badge
        issued since valid again
    And no badge has been cleared

Scenario: E2E-CPED-007 A malformed year is refused before the dialog opens
   When the admin enters "202", or "1999", or "abcd", or leaves the field empty,
        and presses "Open the year"
   Then an inline error asks for a four-digit year between 2000 and 2999
    And NO confirmation appears - the typo is corrected while the field is still
        in front of them

  # A five-digit year is deliberately absent: the field caps at four characters,
  # so "20265" reaches the component as "2026" and is not a state the page can
  # be in. The server still bounds the range - see EventEditionTests - and 1999
  # exercises that same guard through a value the field CAN hold.

Scenario: E2E-CPED-008 A load failure offers Retry rather than reading "Loading…" forever
  Given the read API is failing
   When the admin opens "/admin/editions"
   Then an error is shown with a "Try again" button
    And the page does not sit reading "Loading…"

Scenario: E2E-CPED-009 View-only cannot open a year
  Given an account WITH "Editions.View" but WITHOUT "Editions.Open"
   When it opens "/admin/editions"
   Then the year and the history are readable
    And the "Open the year" button is NOT rendered
    And calling the open API directly returns 403

Scenario: E2E-CPED-010 The page renders correctly in Arabic (RTL)
  Given the interface language is Arabic
   When the admin opens "/admin/editions"
   Then the page, the warning and the dialog render right-to-left
    And there is no horizontal overflow (scrollWidth == clientWidth)
    And the console has no errors and no request failed
```

## Run on 2026-08-14

Driven at `localhost:5158` against a database built from scratch by this
branch's migrations. `automated` marks a scenario pinned in
`tests/SIMF.ControlPanel.Tests/EventEditionPageTests.cs`; `driven` marks one
exercised in the browser on that run.

- **004** — opening 2027 reported 8 badges cleared, which matched the 8 counted
  in SQL beforehand exactly; all 45 attendees remained, still labelled 2026.
- **005** — re-opening 2027 answered 409 "That year is already open", the dialog
  stayed open, and the year was unchanged.
- **009** — signed in as a role holding ONLY `Editions.View`: the year was
  readable, **no** action button was rendered, and `POST .../editions/open`
  answered **403**. Both halves matter — a hidden button over an open API would
  be theatre.
- **010** — Arabic rendered `dir=rtl` with no untranslated keys, no horizontal
  overflow and no broken images; English LTR likewise. The only console error on
  the whole run was the 409 that scenario 005 asked for.

**003 and 006 were not driven** and remain manual: cancelling the dialog, and an
earlier year. Both are covered server-side by `EventEditionTests`.

## Not covered here

- The gate-side effect of the year — a badge from a closed edition being refused
  at a door is the gate catalogue's, not this page's.
- The offline roster and the badge payload, which carry the same year but are
  separate surfaces.
