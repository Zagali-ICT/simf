# E2E test catalogue — `Meet people` (`meet-people`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue —
> `GET /app/account/recommendations/meet-like-you` (`RequireApprovedAccount`).
> Pixel-parity to KSA Figma frame `1072:13409` (D-448): the navy `KsaPage` shell,
> a smart-suggestions **header card** (title + subtitle + three topic chips, frame
> `1082:15269`) and a per-match **card** (frame `1082:15273`) — the gold **% match**
> (from the scorer's `score`) over the `تطابق` label, the name, the profile-type
> line, the match reason and a gold initials avatar.
> **Backend gap (planned, D-448):** the Figma's exact per-match reason lines
> ("نفس جلستين · 3 اهتمامات مشتركة") need a generated `matchReason` (+ optional
> `sharedSessionCount`) on the recommendation contract; until then the reason is
> composed from `sharedInterestCount` (`_reason` is the single swap point) and the
> % comes from `score`. Widget-tested in
> `src/Mobile/simf_app/test/features/meet/meet_people_screen_test.dart`; the model
> decode is in `meet_models_test.dart` (`Recommendation.listFromData`).

| | |
|--|--|
| **Page** | [`Page_035`](../../App/Page_035/README.md) |
| **Route** | `GET /api/v1/app/account/recommendations/meet-like-you` · app screen #35 `/meet` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Approved account** — `RequireApprovedAccount`. Sign in with `Get-Totp` (never a literal secret); route 35 is auth-gated. |
| **Last reviewed** | 2026-06-19 (D-448 — Figma `1072:13409` parity) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB035-001 | Header card (title + subtitle + 3 topic chips) + match card (name, profile-type, reason, % from score, gold avatar) | happy | P0 | authored ✓ (screen `renders the smart header and a match card with the % score`) |
| E2E-MOB035-002 | Empty list keeps the header and shows the empty notice | edge | P1 | authored ✓ (screen `empty list keeps the header and shows the empty notice`) |
| E2E-MOB035-003 | Load failure → error state | auth/edge | P1 | authored ✓ (screen `error shows the error state`) |
| E2E-MOB035-004 | RTL: the % block sits to the inline-end (left) of the name | rtl | P1 | authored ✓ (screen `Arabic: the % block sits to the left of the name`) |

## Scenarios

### E2E-MOB035-001 — Smart header + match card

```gherkin
Feature: Meet people like you
  As an approved visitor
  I want suggested people scored by shared interests
  So that I can find relevant attendees to meet

Scenario: The header and a match card render with the % score
  Given the recommendations endpoint returns one match (score 0.82, 3 shared interests, type "Captain")
  When the /meet screen renders
  Then the header card shows "Smart suggestions based on your interests" + the three topic chips
  And the match card shows the name, "Captain", "3 shared interests" and "82%" over "match"
  And a gold rounded-square initials avatar is shown
```

**Evidence:** screen test `renders the smart header and a match card with the % score`.

### E2E-MOB035-002 / 003 / 004 — Empty, error, RTL

```gherkin
Scenario: An empty result keeps the header
  Given the endpoint returns no matches
  Then the header card is still shown and a "No matches yet" notice appears

Scenario: A load failure shows the error
  Given the endpoint fails
  Then "Could not load your matches." is shown

Scenario: RTL layout pins the % to the inline end
  Given the device locale is Arabic
  Then the "82%" block is laid out to the left of the match name (inline end under RTL)
```

**Evidence:** screen tests `empty list keeps the header and shows the empty notice`,
`error shows the error state`, `Arabic: the % block sits to the left of the name`.

---

_Last reviewed:_ `2026-06-19` by `SIMF Team`.
