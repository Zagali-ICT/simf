# E2E test catalogue — >=80% match threshold + auto-recommendation push (FR-803)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Page** | [`mobile-meet-people.md`](mobile-meet-people.md) (the surface the push points at) |
| **Routes** | `GET /api/v1/app/account/recommendations/meet-like-you` (unchanged) + `MatchRecommendationPushWorker` |
| **Surface** | Backend ranker + hosted background worker → in-app notification |
| **Test runner** | xUnit (`tests/SIMF.Api.Tests/RecommendationThresholdTests.cs`, `MatchRecommendationPushWorkerTests.cs`) |
| **Auth setup** | None for the worker; the browse read keeps its approved-account gate |
| **Last reviewed** | 2026-07-31 |

## What changed and why

`FR-803-80pct-push`. `RecommendationService` sorted by score and simply `Take`-n'd
the top N — there was no threshold constant and no `0.8` comparison anywhere in the
file, so the weakest possible overlap ranked as a "recommendation" whenever nothing
better existed. `NotificationKind` had no match/recommendation value across 0-58,
and no worker under `Operations/` pushed anything.

Now:

- `RecommendationService.StrongMatchThreshold = 0.80` with a `NormaliseScore` clamp,
  plus `IRecommendationService.StrongMatchesAsync`.
- `NotificationKind.MatchRecommended = 60` (additive, persisted by name — no schema
  or wire change under the D-110 frozen-enum rule).
- `MatchRecommendationPushWorker`, a batched poll worker beside `SessionReminderWorker`.

**The browse read is deliberately unchanged.** `MeetPeopleLikeYouAsync` still
returns the best N regardless of strength, which is right for a surface the user
chose to open. The threshold governs only what the system is allowed to *interrupt*
someone with.

## Why the score is clamped

The raw `Score` is Jaccard overlap plus a `SameProfileTypeBonus` of 0.05, so a
perfect match scores 1.05. "80%" is a percentage and must be compared against a
number that cannot exceed 1.0, hence `NormaliseScore = Math.Clamp(score, 0, 1)`. The
same-tier bonus stays inside the comparison on purpose: at equal overlap, a
candidate in the caller's own tier IS the better match — that is why the bonus
exists.

## Batching and dedup

The ranker does a full candidate scan per caller, so scoring the whole roster in one
tick would be O(n²) in a burst. Each tick takes `BatchSize = 25` callers ordered by
user id, resuming after the previous tick's last id and wrapping at the end. Only
profiles that opted into "Meet People Like You" **and** picked at least one interest
can be matched at all, so only those enter a batch.

The cursor is in-memory on purpose: losing it on restart costs nothing, because
dedup is the D-713 dispatcher guard keyed on (caller, kind, candidate profile) —
re-running a batch is a no-op. No stamp column, no migration.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MRP-001 | Threshold is the stated 80% | happy | P0 | automated |
| E2E-MRP-002 | Score is normalised into [0,1] before comparison | happy | P0 | automated |
| E2E-MRP-003 | Only scores at or above the threshold qualify | happy | P0 | automated |
| E2E-MRP-004 | Each strong match is pushed once per (caller, candidate) | happy | P0 | automated |
| E2E-MRP-005 | Caller with no strong match is not interrupted | happy | P0 | automated |
| E2E-MRP-006 | One caller's ranker failure does not abort the batch | resilience | P1 | automated |
| E2E-MRP-007 | Batching only offers opted-in profiles with interests | privacy | P0 | automated |
| E2E-MRP-008 | The browse read still returns sub-threshold matches | regression | P0 | automated |
| E2E-MRP-009 | The push renders bilingually in the notification list | i18n | P1 | manual |

## Scenarios

### E2E-MRP-001..003 — The threshold itself

```gherkin
Feature: A >=80% match threshold
  As an attendee
  I want to be interrupted only by matches that are actually strong
  So that a recommendation means something

Scenario: The bar is the stated 80%
  Then RecommendationService.StrongMatchThreshold is 0.80

Scenario Outline: The score is a real percentage before it is compared
  When a raw score of <raw> is normalised
  Then the result is <normalised>
  Examples:
    | raw   | normalised |
    | 1.05  | 1.0        |
    | 1.0   | 1.0        |
    | 0.5   | 0.5        |
    | -0.2  | 0.0        |

Scenario Outline: Only strong matches qualify
  When a raw score of <raw> is tested against the threshold
  Then it qualifies: <qualifies>
  Examples:
    | raw  | qualifies |
    | 0.79 | false     |
    | 0.80 | true      |
    | 0.95 | true      |
    | 1.05 | true      |
```

**Evidence captured:** `RecommendationThresholdTests` (all three facts/theories).

### E2E-MRP-004 — One push per (caller, candidate) pair

```gherkin
Feature: Auto-recommendation push
  As an attendee with a strong match in the room
  I want to be told once
  So that I can go and meet them without being nagged

Background:
  Given caller Khalid has two candidates scoring at or above 80%

Scenario: Each pair fires exactly once, ever
  When the push pass runs for Khalid
  Then Khalid has 1 MatchRecommended notification for candidate A
  And 1 for candidate B
  And each carries relatedEntityType "UserProfile" and the candidate's profile id
  When the push pass runs again
  Then the counts are still 1 and 1
```

**Evidence captured:** `MatchRecommendationPushWorkerTests.Each_strong_match_is_pushed_once_per_caller_candidate_pair`.

### E2E-MRP-005 — Nobody is interrupted for nothing

```gherkin
Scenario: A caller with no strong match gets no notification
  Given caller Khalid's best candidate scores 0.4
  When the push pass runs
  Then Khalid has no MatchRecommended notification
```

**Evidence captured:** `MatchRecommendationPushWorkerTests.Caller_with_no_strong_match_is_not_interrupted`.

### E2E-MRP-006 — Containment

```gherkin
Scenario: One caller's failure does not cost the rest of the batch
  Given a batch of 2 callers, the first of whose ranking throws
  When the push pass runs
  Then the second caller still receives their recommendation
  And the failure is logged with the caller id
```

**Evidence captured:** `MatchRecommendationPushWorkerTests.One_callers_failure_does_not_abort_the_batch`.

### E2E-MRP-007 — Opt-out is honoured before any scoring

```gherkin
Scenario: An opted-out attendee is never even a caller
  Given an attendee whose profile has ShowInMeetLikeYou = false
  When the next batch is selected
  Then that attendee's user id is not in the batch
  And the batch is no larger than BatchSize
```

The opt-out is a privacy control, so it is enforced at batch selection — before the
ranker sees the caller at all — as well as inside the ranker's candidate query.

**Evidence captured:** `MatchRecommendationPushWorkerTests.Batching_only_offers_opted_in_profiles_with_interests`.

### E2E-MRP-008 — The browse read is unchanged

```gherkin
Scenario: Opening the screen still shows the best available matches
  Given an approved visitor whose best candidate scores 0.4
  When they GET /api/v1/app/account/recommendations/meet-like-you
  Then the response is 200
  And data.matches contains that 0.4 candidate
```

**Evidence captured:** the pre-existing `RecommendationServiceTests` suite, unchanged.

### E2E-MRP-009 — Bilingual render

```gherkin
Scenario: The push reads correctly in both locales
  Given Khalid has a MatchRecommended notification
  When he opens the app notification list in Arabic
  Then the title reads "شخص يستحق أن تقابله"
  And the body names the candidate in Arabic with the Arabic match reason
  When he switches to English
  Then the title reads "Someone you should meet"
```
