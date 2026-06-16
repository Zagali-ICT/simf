# E2E test catalogue — `News` (`news`)

> **Authority:** SIMF E2E template (D-133). News reads built + anonymous (API
> `tests/SIMF.Api.Tests/NewsTests.cs`). **Flutter screen built (D-308)** — widget
> tests in `src/Mobile/simf_app/test/features/news/news_screen_test.dart`.
>
> **Figma parity (2026-06-16):** the screen is re-skinned to the KSA Wave-2 frame
> **958:2246 "التغطية الإعلامية" (Media coverage)** — a three-tab hub (الأخبار ·
> الشركاء الإعلاميون · معرض الصور والفيديوهات) on the navy KSA shell. الأخبار is
> the active gold pill; the news list is navy cards (gold category chip · white
> title · beige excerpt) and the two inactive pills route to media-partners (#31)
> and gallery (#30). Header title is "التغطية الإعلامية" / "Media coverage".

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
| E2E-MOB029-006 | Media-coverage hub renders 3 tabs, الأخبار active gold | happy | P0 | _to author_ |
| E2E-MOB029-007 | Tap "الشركاء الإعلاميون" pill → media-partners (#31) | happy | P1 | _to author_ |
| E2E-MOB029-008 | Tap "معرض الصور والفيديوهات" pill → gallery (#30) | happy | P1 | _to author_ |
| E2E-MOB029-009 | News card shows gold category chip · white title · beige excerpt | happy | P1 | _to author_ |
| E2E-MOB029-010 | RTL render of the Media-coverage hub (Arabic) | i18n | P1 | _to author_ |

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

### E2E-MOB029-006 — Media-coverage hub: three tabs, الأخبار active

```gherkin
Scenario: The Media-coverage hub renders the three tabs with الأخبار active
  Given a guest opens app screen #29 (/news)
  Then the header reads "التغطية الإعلامية" ("Media coverage")
  And a row of three pills shows "الأخبار", "الشركاء الإعلاميون"
    and "معرض الصور والفيديوهات"
  And the "الأخبار" pill is the solid gold active tab (white text, non-tappable)
  And the other two pills are bordered navy cards (beige hairline + beige text)
  And the news list renders below the tab row
```

### E2E-MOB029-007 — "الشركاء الإعلاميون" pill → media-partners (#31)

```gherkin
Scenario: Tapping the media-partners pill routes to the partners screen
  Given the guest is on the Media-coverage hub (/news)
  When they tap the "الشركاء الإعلاميون" ("Media partners") pill
  Then the app pushReplacement-navigates to the media-partners route (#31)
  And that screen's header reads "الشركاء الإعلاميون" ("Media partners")
```

### E2E-MOB029-008 — "معرض الصور والفيديوهات" pill → gallery (#30)

```gherkin
Scenario: Tapping the gallery pill routes to the media gallery screen
  Given the guest is on the Media-coverage hub (/news)
  When they tap the "معرض الصور والفيديوهات" ("Media gallery") pill
  Then the app pushReplacement-navigates to the gallery route (#30)
  And that screen's header reads "معرض الصور والفيديوهات" ("Media gallery")
```

### E2E-MOB029-009 — News card anatomy (chip · title · excerpt)

```gherkin
Scenario: A news row renders the gold chip, white title and beige excerpt
  Given GET /api/v1/app/news returns an item
    with category "أخبار المنتدى" / "Forum news", a title and an excerpt
  When the guest views the news list
  Then the card shows the category in a gold-bordered navy chip (gold text)
  And the title renders in bold white below the chip
  And the excerpt renders in muted beige, clamped to 2 lines
  And an item with no excerpt renders the chip + title only (no excerpt row)
  And tapping the card pushes NewsArticleScreen (GET /app/news/{id})
```

### E2E-MOB029-010 — RTL render of the hub (Arabic)

```gherkin
Scenario: The Media-coverage hub renders right-to-left in Arabic
  Given the app language is Arabic
  When the guest opens /news
  Then the header "التغطية الإعلامية" and the three pills lay out RTL
  And the active gold "الأخبار" pill sits at the leading (right) edge
  And the news cards align their chip + title to the right
  And there is no horizontal overflow
```

---

_Last reviewed:_ `2026-06-16` by `SIMF Team`.
