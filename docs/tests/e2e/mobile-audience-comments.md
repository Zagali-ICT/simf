# E2E test catalogue — `Audience comments` (`audience-comments`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> per-session comment endpoints are built + `RequireApprovedAccount` (D-223). The
> **Flutter screen is built** and tested in
> `src/Mobile/simf_app/test/features/comments/audience_comments_screen_test.dart`
> (no-id empty, feed renders, empty, error→retry, submit calls repo + toast, like
> toggles) plus the model tests in `comment_models_test.dart`.

| | |
|--|--|
| **Page** | [`Page_028`](../../App/Page_028/README.md) |
| **Route** | `GET/POST /api/v1/app/sessions/{id}/comments` · `POST`/`DELETE .../like` · app screen #28 `/live/comments?sessionId={id}` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Approved account** — `RequireApprovedAccount`. Sign in as an approved visitor (TOTP via `Get-Totp`, never a literal secret). |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB028-001 | Approved visitor opens a session's comments and sees the feed | happy | P0 | authored ✓ (screen `renders the comment feed`) |
| E2E-MOB028-002 | Submitting a comment → "awaiting moderation" toast + refresh | happy | P0 | authored ✓ (screen `submitting calls the repo + shows the moderation toast`) |
| E2E-MOB028-003 | Liking then unliking a comment updates that row's count + icon | happy | P0 | authored ✓ (screen `tapping like calls like then unlike updates the row`) |
| E2E-MOB028-004 | No `sessionId` → "open from a live session" empty state, no feed read | edge | P1 | authored ✓ (screen `no sessionId shows the open-from-live empty state`) |
| E2E-MOB028-005 | Empty feed → "No comments yet" empty state | edge | P1 | authored ✓ (screen `empty feed shows the empty state`) |
| E2E-MOB028-006 | A read failure → error + Retry that re-fetches | resilience | P0 | authored ✓ (screen `a read failure shows error + retry, which re-fetches`) |
| E2E-MOB028-007 | Auth gate — a signed-out / unapproved caller is blocked | security | P0 | covered (route 28 in `_authenticatedRoutes`; API `RequireApprovedAccount`) |

## Scenarios

### E2E-MOB028-001 — Feed loads

```gherkin
Feature: Audience comments (live session)
  As an approved visitor in a live session
  I want to read and post comments
  So that I can take part in the audience discussion

Scenario: The comment feed renders for a session
  Given I am signed in as an approved visitor
  When the app calls GET /api/v1/app/sessions/{id}/comments
  Then it returns 200 with the approved comments
  And each card shows the author, the body and the like count
```

**Evidence:** screen test `renders the comment feed`.

### E2E-MOB028-002 — Submit (held for moderation)

```gherkin
Scenario: Submitting a comment holds it for moderation
  When I type a comment and tap send
  Then POST /api/v1/app/sessions/{id}/comments is called with { body }
  And a freshly submitted comment may be Pending (status 0)
  And a toast says the comment is awaiting moderation
  And the feed is refreshed
```

**Evidence:** screen test `submitting calls the repo + shows the moderation toast`.

### E2E-MOB028-003 — Like / unlike toggle

```gherkin
Scenario: Liking and unliking updates the row
  Given a comment I have not liked
  When I tap the like button
  Then POST .../like returns the new likeCount + likedByMe and the icon fills
  When I tap it again
  Then DELETE .../like returns the decremented count and the icon hollows
```

**Evidence:** screen test `tapping like calls like then unlike updates the row`.

### E2E-MOB028-004 — No session / E2E-MOB028-005 — Empty / E2E-MOB028-006 — Error+retry

```gherkin
Scenario: No sessionId shows the open-from-live state
  Given the screen is opened with no sessionId
  Then it shows "Open this from a live session." and reads no feed

Scenario: No comments shows the empty state
  Given the feed read returns an empty list
  Then the screen shows the "No comments yet" placeholder

Scenario: A failed read offers a retry
  Given the comment feed read fails
  Then an error + Retry are shown, and Retry re-runs the read
```

**Evidence:** screen tests `no sessionId shows the open-from-live empty state`,
`empty feed shows the empty state`, `a read failure shows error + retry, which
re-fetches`.

### E2E-MOB028-007 — Auth gate

```gherkin
Scenario: A signed-out caller cannot read or post comments
  Given no approved-account token
  When the app would open /live/comments
  Then the router redirects to sign-in (route 28 is authenticated)
  And the API rejects the call (RequireApprovedAccount)
```

**Evidence:** route 28 added to `_authenticatedRoutes`; API endpoints gated
`RequireApprovedAccount` (D-223).

---

_Last reviewed:_ `2026-06-06` by `SIMF Team`.
