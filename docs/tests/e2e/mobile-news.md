# E2E test catalogue — `News` (`news`)

> **Authority:** SIMF E2E template (D-133). News reads built + anonymous (API
> `tests/SIMF.Api.Tests/NewsTests.cs`). **Flutter screen built (D-308)** — widget
> tests in `src/Mobile/simf_app/test/features/news/news_screen_test.dart`.
>
> **Figma parity (2026-06-19):** the screen is re-skinned to the KSA-Project frame
> **1049:12629 "المركز الاعلامي" (Media center)** — a **two-tab** hub (الشركاء
> الإعلاميون · احدث المستجدات) on the navy KSA shell. احدث المستجدات is the active
> gold pill (white text); the inactive pill routes to media-partners (#31). (The
> معرض الصور tab was dropped per Figma 947/1049; the gallery screen #30 stays in
> the app, reached elsewhere.) **News card to frame 1049:12736** — a borderless
> navy radius-8 card laid out **horizontally**: in RTL the thumbnail (the article's
> **NewsImage** asset, served by the anonymous D-357 route
> `GET /app/assets/NewsImage/{id}/image`, with a gold category chip overlaid + a
> navy bottom-gradient, initials/icon fall-back) sits at the inline-end (LEFT),
> and at the inline-start (RIGHT) a muted category label, a **gold `DD-MM-YYYY`
> date**, then the bold white title (the frame has **no excerpt**). Header title
> is "المركز الاعلامي" / "Media center".

| | |
|--|--|
| **Page** | [`Page_029`](../../App/Page_029/README.md) |
| **Route** | `GET /api/v1/app/news` (+ `/{id}`) · app screen #29 `/news` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-19 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB029-001 | Guest loads the news list (thumbnail · date · title) | happy | P0 | authored ✓ (screen `renders the media-center tabs and a news card`) |
| E2E-MOB029-002 | Tap an item → article screen loads (`/news/{id}` → body) | happy | P0 | authored (screen push + `NewsArticle.fromJson`) |
| E2E-MOB029-003 | Article 404 → "not found" | edge | P1 | covered (article-screen 404 branch) |
| E2E-MOB029-004 | Empty → empty state | edge | P1 | authored ✓ (screen `empty shows the empty state`) |
| E2E-MOB029-005 | Read failure → error state | resilience | P0 | authored ✓ (screen `error shows the error state`) |
| E2E-MOB029-006 | Media-center hub renders 2 tabs, احدث المستجدات active gold | happy | P0 | authored ✓ (screen `renders the media-center tabs`) |
| E2E-MOB029-007 | Tap "الشركاء الإعلاميون" pill → media-partners (#31) | happy | P1 | authored ✓ (screen `tapping the Media-partners tab routes to the partners screen`) |
| E2E-MOB029-008 | _(removed)_ Gallery tab dropped from the media-center hub (Figma 947/1049) | — | — | n/a — gallery screen #30 kept, reached elsewhere |
| E2E-MOB029-009 | Card shows thumbnail (NewsImage asset) · gold date · title; no excerpt | happy | P1 | authored ✓ (screen `renders…a news card` + `…thumbnail from the NewsImage asset route`) |
| E2E-MOB029-010 | No uploaded NewsImage / fetch fails → icon fall-back | edge | P1 | authored ✓ (thumbnail `errorBuilder` → `_NewsImageFallback`) |
| E2E-MOB029-011 | Arabic/RTL: thumbnail at inline-end (LEFT), text at inline-start (RIGHT) | i18n | P0 | authored ✓ (screen `lays the thumbnail left of the text in Arabic`) |
| E2E-MOB029-012 | The article opens by **named route** `/news/{newsId}`, not an imperative push — so it deep-links like every other detail screen | happy | P0 | authored (router `RouteNames.newsArticle`; `route_table_test` + `router_role_matrix_test`) |
| E2E-MOB029-013 | Pull-to-refresh on the article re-reads `GET /app/news/{id}` (owner rule: every data page) | happy | P1 | authored (`SimfPullToRefresh` on the body; short branches in `SimfPullableHost`) |

## Scenarios

```gherkin
Scenario: News render without a token
  When the app calls GET /api/v1/app/news
  Then it returns 200 with items[] (title, category, publishedAt, imageRelativePath)
  And the screen lists each card with its thumbnail, gold DD-MM-YYYY date and title

Scenario: Tapping a card opens the article
  When the visitor taps a news card
  Then the app navigates to the named route "newsArticle" at /news/{id}
  And the article screen calls GET /api/v1/app/news/{id}
  And it renders the category, title and full body
  And a 404 shows the "not found" state

Scenario: The article is reachable by deep link
  Given the visitor opens /news/2f1c4b8a-0000-4000-8000-000000000001 directly
  Then the article screen opens on that id without going through the news list

Scenario: Pull-to-refresh re-reads the article
  Given the article screen is showing a loaded article
  When the visitor pulls down on the body
  Then GET /api/v1/app/news/{id} is called again
  And the gold spinner stays until the re-read completes

Scenario: Pull-to-refresh recovers a failed article read
  Given GET /api/v1/app/news/{id} failed and the error state is showing
  When the visitor pulls down on the error state
  Then the read is re-attempted without leaving the screen

Scenario: Empty → placeholder; failed read → error
  Given no news (or a failed read)
  Then the screen shows "No news" / the error message
```

**Evidence:** `news_screen_test.dart` (4) + `NewsTests` (API).

### E2E-MOB029-006 — Media-center hub: two tabs, احدث المستجدات active

```gherkin
Scenario: The Media-center hub renders the two tabs with احدث المستجدات active
  Given a guest opens app screen #29 (/news)
  Then the header reads "المركز الاعلامي" ("Media center")
  And a row of two pills shows "الشركاء الإعلاميون" and "احدث المستجدات"
  And the "احدث المستجدات" pill is the solid gold active tab (white text, non-tappable)
  And the other pill is a transparent card (beige hairline + beige text)
  And the news list renders below the tab row
```

### E2E-MOB029-007 — "الشركاء الإعلاميون" pill → media-partners (#31)

```gherkin
Scenario: Tapping the media-partners pill routes to the partners screen
  Given the guest is on the Media-center hub (/news)
  When they tap the "الشركاء الإعلاميون" ("Media partners") pill
  Then the app pushReplacement-navigates to the media-partners route (#31)
  And that screen's header reads "المركز الاعلامي" ("Media center")
```

### E2E-MOB029-008 — _(removed)_ Gallery tab dropped

> The معرض الصور والفيديوهات tab was removed from the media-center hub per Figma
> 947/1049. The gallery screen (#30) stays in the app (reached elsewhere), so there
> is no longer a gallery pill on this screen to tap.

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
Scenario: The Media-center hub renders right-to-left in Arabic
  Given the app language is Arabic
  When the guest opens /news
  Then the header "المركز الاعلامي" and the two pills lay out RTL
  And the "الشركاء الإعلاميون" pill sits at the leading (right) edge,
    the active gold "احدث المستجدات" pill to its left
  And the news cards align their chip + title to the right
  And there is no horizontal overflow
```

---

_Last reviewed:_ `2026-06-19` by `SIMF Team`.
