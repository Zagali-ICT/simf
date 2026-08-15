# E2E test catalogue — Badge-to-account creation (`POST /api/v1/app/auth/badge-activation/*`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Page** | [`mobile-badge-activation.md`](mobile-badge-activation.md) (the app screen that drives it) |
| **Route** | `POST /api/v1/app/auth/resolve-badge` · `…/badge-activation/start` · `…/badge-activation/complete` |
| **Surface** | Public auth API (anonymous — this runs before any token exists) |
| **Test runner** | xUnit + `SimfApiFactory` (`tests/SIMF.Api.Tests/BadgeAccountCreationTests.cs`) |
| **Auth setup** | None. The badge QR + control of an emailed inbox are the two factors. |
| **Last reviewed** | 2026-08-14 |

## What changed and why

D-885. `UserProfile.UserId` became nullable in D-877, which made "approved
attendee, no account" the ordinary state — a badge is printed and handed out long
before anyone decides they also want the app. `BadgeAuthService` refused exactly
that state, so such a person could never reach the app at all.

The flow now resolves to a **holder**: the attendee always, the account only when
there is one. When there is none, start and complete verify an emailed code and
then create the account and link it to the attendee.

D-878 then retired the placeholder accounts entirely, so this is the only route
from a printed badge to an app account.

## The self-claim guard — the security property this page exists to protect

A **walk-in** badge is in open circulation. Anyone who photographs one across a
room could otherwise claim a full app account from the picture — sign-in,
contacts, meeting requests. A **bulk-order** badge was handed to a named person
under a controlled distribution, so possession is evidence.

The test is `BadgeBatchId != BadgeBatch.DirectRegistrationId`, and it is
deliberately **not** "has an order": after D-878 everyone has one, and whoever
arrived on their own is filed under the seeded direct-registration order.

A refused claim returns the **same** `BADGE_NOT_FOUND` an unknown QR returns, so
this is never an oracle for which badges exist.

`WalkInMode.BadgeActivationAllowedForWalkIns` still overrides it when an operator
deliberately arms it.

## Verify-then-attach with no account to stash on

The account path stashes the nominated address as an authentication token on the
user. With no user, the completing request would have had to supply the address —
and then whoever held a code could bind an address the code was never sent to,
which is the entire property the stash exists to provide.

The address is pinned on the **code row** (`AccountCode.PendingEmail`) at the
start step instead. The completing request carries the code and no address at all.

## Two databases, no shared transaction

The account is created in Identity and linked in App as two writes. The window
between them can leave an account no attendee points at. Rather than let that
brick a badge at the venue behind a permanent 409, the next attempt **adopts** an
unclaimed account for an address it has just proven, replacing the password so
one set by any other route cannot survive the adoption.

The link and the profile fill are one App write, for the same reason the account
path fills the profile first: a retry must never be refused with the captured
details never written.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-BAC-001 | A bulk badge with no account creates one and signs in | happy | P0 | automated |
| E2E-BAC-002 | A walk-in badge with no account cannot be claimed by its holder | security | P0 | automated |
| E2E-BAC-003 | Complete attaches the pinned address, not one supplied later | security | P0 | automated |
| E2E-BAC-004 | A second start supersedes the first code and its address | happy | P1 | automated |
| E2E-BAC-005 | An account left unlinked by an interrupted attempt is adopted on retry | resilience | P1 | automated |
| E2E-BAC-006 | Once the badge has an account it routes to the normal sign-in | happy | P1 | automated |
| E2E-BAC-007 | Armed walk-in mode admits an accountless walk-in badge | config | P2 | manual |
| E2E-BAC-008 | An expired code is refused and distinguishable from an unknown one | negative | P1 | manual |

## Scenarios

```gherkin
Scenario: E2E-BAC-001 A bulk badge with no account creates one and signs in
  Given a bulk order "Ministry of Interior Team" has minted one Visitor badge
    And that badge's attendee record is Approved and holds no Identity account
   When the app posts the badge QR to /app/auth/resolve-badge
   Then the response is found=true, hasPassword=false, needsEmail=true
    And maskedEmail is null
   When the holder starts activation with "claimed@example.com"
    And completes it with the emailed code and a policy-valid password
   Then an account exists for claimed@example.com, Approved, EmailConfirmed, UserType=Visitor
    And the attendee record's UserId points at it
    And signing in with claimed@example.com and that password succeeds

Scenario: E2E-BAC-002 A walk-in badge with no account cannot be claimed by its holder
  Given an Approved attendee whose order is the seeded direct-registration order
    And BadgeActivationAllowedForWalkIns is NOT armed
   When the holder starts activation with any email
   Then the response is 404 BADGE_NOT_FOUND
    And it is byte-identical to the response for a QR that does not exist

Scenario: E2E-BAC-003 Complete attaches the pinned address, not one supplied later
  Given activation was started for a bulk badge with "pinned@example.com"
   When the holder completes with the emailed code
   Then the created account's email is pinned@example.com
    And the complete request carried no email field at all

Scenario: E2E-BAC-004 A second start supersedes the first code and its address
  Given activation was started with "first@example.com"
   When activation is started again with "second@example.com"
   Then the first code no longer completes
    And completing with the second code creates the account on second@example.com

Scenario: E2E-BAC-005 An account left unlinked by an interrupted attempt is adopted on retry
  Given an account exists for "orphan@example.com" that no attendee record points at
   When the holder starts and completes activation for that same address
   Then the SAME account is adopted rather than a second one created
    And the attendee record's UserId points at it

Scenario: E2E-BAC-006 Once the badge has an account it routes to the normal sign-in
  Given a badge whose activation has completed
   When the app posts the badge QR to /app/auth/resolve-badge
   Then the response is found=true, hasPassword=true, needsEmail=false
    And maskedEmail is populated
   When activation is started again
   Then the response is 409 BADGE_ALREADY_ACTIVATED

Scenario: E2E-BAC-007 Armed walk-in mode admits an accountless walk-in badge
  Given BadgeActivationAllowedForWalkIns is armed on the API
    And an Approved attendee in the direct-registration order
   When the holder starts activation with a valid email
   Then the code is sent and activation completes normally

Scenario: E2E-BAC-008 An expired code is refused and distinguishable from an unknown one
  Given activation was started and the code lifetime has elapsed
   When the holder completes with that code
   Then the response is 400 AUTH_RESET_CODE_EXPIRED
    And a wrong-but-live code returns AUTH_RESET_CODE_INVALID instead
```

## Not covered here

- The account-holding activation path (a placeholder or real-email account) —
  that is [`api-badge-self-claim-profile.md`](api-badge-self-claim-profile.md).
- Gate admission for an accountless attendee — that is the gate catalogue.
