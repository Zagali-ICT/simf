# E2E test catalogue — `Media partners` (`media-partners`)

> **Authority:** SIMF E2E template (D-133). The media-partners read is built +
> anonymous (D-199; API `tests/SIMF.Api.Tests/MediaPartnersTests.cs`). **Flutter
> screen rebuilt to the KSA-Project Figma frame 947:3764 (Figma exact-parity)** —
> navy "المركز الاعلامي" (Media center) shell + the shared **two-tab** media-center
> hub (الشركاء الإعلاميون · احدث المستجدات — the معرض الصور tab was dropped per
> Figma 947/1049; the gallery screen #30 stays in the app, reached elsewhere) + a
> two-column partner grid whose logo is the partner's uploaded asset,
> served by the existing **anonymous** D-357 route
> `GET /api/v1/app/assets/MediaPartnerLogo/{id}/image` (no new endpoint — the
> unified media-asset pipeline already serves it and the CP already uploads it),
> with an initials fall-back. Widget tests in
> `src/Mobile/simf_app/test/features/media_partners/media_partners_screen_test.dart`.

| | |
|--|--|
| **Page** | [`Page_031`](../../App/Page_031/README.md) |
| **Route** | `GET /api/v1/app/media-partners` (list) · `GET /api/v1/app/assets/MediaPartnerLogo/{id}/image` (logo) · app screen #31 `/media-partners` |
| **Auth setup** | **None** — both reads are `AllowAnonymous`. |
| **Last reviewed** | 2026-06-19 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB031-001 | Guest loads the media-partner grid (logo + name) | happy | P0 | authored ✓ (screen `renders the media-center header, the two tabs and the partners`) |
| E2E-MOB031-002 | Empty → empty state | edge | P1 | authored ✓ (screen `empty shows the empty state`) |
| E2E-MOB031-003 | Read failure → error + retry | resilience | P0 | authored ✓ (screen `a read failure shows the error + retry`) |
| E2E-MOB031-004 | Each card builds its logo from the anonymous asset route | happy | P0 | authored ✓ (screen `builds each logo from the public asset route`) |
| E2E-MOB031-005 | No uploaded logo / fetch fails → initials fall-back | edge | P1 | authored ✓ (logo `errorBuilder` → initials tile) |
| E2E-MOB031-006 | Tab hub: tapping احدث المستجدات navigates to the news screen | nav | P1 | authored ✓ (screen `tapping the Latest-updates tab navigates to the news route`) |
| E2E-MOB031-007 | Arabic/RTL: tabs lay out partners (right) → latest-updates (left) | rtl | P0 | authored ✓ (screen `lays the tabs partners→latest right-to-left in Arabic`) |
| E2E-MOB031-008 | Pressing a partner card opens the logo full size (FR-LGO-003) | happy | P1 | authored ✓ (widget test) |

## Scenarios

```gherkin
Scenario: Media partners render without a token
  When the app calls GET /api/v1/app/media-partners
  Then it returns 200 with items[] (id, name/nameArabic, url)
  And the screen shows each partner on a navy card: its uploaded logo
    (fetched from GET /api/v1/app/assets/MediaPartnerLogo/{id}/image) over its name

Scenario: A partner with no uploaded logo
  Given a partner whose MediaPartnerLogo asset route 404s
  Then its card shows the partner's initials on the gold tile (no broken image)

Scenario: Two-tab media-center hub
  Given the media-partners tab is active (solid gold pill, white text)
  When the guest taps "احدث المستجدات" (Latest updates)
  Then the app navigates to the news screen (#29)

Scenario: Empty → placeholder; failed read → error + retry
  Given no partners (or a failed read)
  Then the screen shows the "No media partners" placeholder / the error message + Retry

Scenario: Arabic right-to-left tab order
  Given the app locale is Arabic
  Then the tab row reads, right→left, الشركاء الإعلاميون · احدث المستجدات (frame 947:3764)
```

**Evidence:** `media_partners_screen_test.dart` (8) + `MediaPartnersTests` (API list) +
`AssetEndpointsTests` (the anonymous logo serve).

### E2E-MOB031-008 — The partner card is a tap target (FR-LGO-003)

```gherkin
Scenario: Pressing a partner card opens its logo full size
  Given the media-partners grid lists "Al Arabiya"
  When the guest presses anywhere on that card — including its NAME
  Then the shared full-size viewer opens with the partner's logo
      ({base}/app/assets/MediaPartnerLogo/{id}/image)
  And the viewer is titled with the partner name
  And pinch / double-tap zooms it
  When the guest taps the gold close control
  Then the viewer dismisses back to the grid

Scenario: One target, not two nested ones
  Given each card carries the press-to-enlarge affordance
  Then the inner 48px logo box does NOT carry a second one
# Media partners have no detail route (the frame defines none), so the card
# used to be completely inert: no onTap at all.
```

**Evidence:** `media_partners_screen_test` — "FR-LGO-003 — the whole card is a tap
target that opens the logo full size", "FR-LGO-003 — the logo box does not claim a
second, nested tap target".

---

_Last reviewed:_ `2026-07-27` by `SIMF Team` — added E2E-MOB031-008 for FR-LGO-003
(the card-wide press-to-enlarge). _Prior:_ `2026-06-19` by `SIMF Team`.
