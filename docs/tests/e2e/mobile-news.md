# E2E test catalogue — `News` (`news`)

> **Authority:** SIMF E2E template (D-133). News reads built + anonymous (API
> `tests/SIMF.Api.Tests/NewsTests.cs`). **Flutter screen built (D-308)** — widget
> tests in `src/Mobile/simf_app/test/features/news/news_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_029`](../../App/Page_029/README.md) |
| **Route** | `GET /api/v1/app/news` (+ `/{id}`) · app screen #29 `/news` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB029-001 | Guest loads the news list (category · title · excerpt) | happy | P0 | authored ✓ (screen `lists news with category + excerpt`) |
| E2E-MOB029-002 | Tap an item → article screen loads (`/news/{id}` → body) | happy | P0 | authored (screen push + `NewsArticle.fromJson`) |
| E2E-MOB029-003 | Article 404 → "not found" | edge | P1 | covered (article-screen 404 branch) |
| E2E-MOB029-004 | Empty → empty state | edge | P1 | authored ✓ (screen `empty shows the empty state`) |
| E2E-MOB029-005 | Read failure → error state | resilience | P0 | authored ✓ (screen `error shows the error state`) |

## Scenarios

```gherkin
Scenario: News render without a token
  When the app calls GET /api/v1/app/news
  Then it returns 200 with items[] (title, category, excerpt, publishedAt)
  And the screen lists each card with its category chip + 2-line excerpt

Scenario: Tapping a card opens the article
  When the visitor taps a news card
  Then the article screen pushes and calls GET /api/v1/app/news/{id}
  And it renders the category, title and full body
  And a 404 shows the "not found" state

Scenario: Empty → placeholder; failed read → error
  Given no news (or a failed read)
  Then the screen shows "No news" / the error message
```

**Evidence:** `news_screen_test.dart` (4) + `NewsTests` (API).

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
