# Media partners — الشركاء الإعلاميون (Page 031, `#31`)

- **Route:** `/media-partners` (`RouteNames.mediaPartners`). Access: **Guest+ (public)** — `GET /app/media-partners` is anonymous (D-199).
- **Figma:** **958:2246** (المركز الاعلامي — media coverage, partners tab; card 958:2263). **Clean-code freeze:** D-630 (2026-07-04).

## Purpose

The "الشركاء الإعلاميون" tab of the two-tab media-coverage hub (الشركاء الإعلاميون
· احدث المستجدات — the inactive pill replaces to the news route). The body is a
two-column grid of partner cards — a gold rounded-square logo holder over the
partner name — the logo fetched from the public anonymous D-357 asset route
(`…/app/assets/MediaPartnerLogo/{id}/image`) with a spinner + an
initials-on-gold fallback when the partner has no logo / the fetch fails.

## Structure (post-decomposition)

| File | Holds |
|------|-------|
| `media_partners_screen.dart` (126) | `MediaPartnersScreen` (`ConsumerWidget`) — reads `mediaPartnersProvider` + the base URL, `onRefresh`, and the tab strip + loading/error/empty/grid dispatch. Re-exports `data/` so the test's `MediaPartner`/`mediaPartnersProvider` imports keep resolving. |
| `data/media_partner_models.dart` | `MediaPartner` (+ `localizedName`, `logoAssetUrl` [the D-357 route shape], `fromJson`). |
| `data/media_partners_repository.dart` | `mediaPartnersProvider` (`GET /app/media-partners`, public). |
| `widgets/partner_card.dart` (`PartnerCard` + `_PartnerLogo`/`_InitialsTile`) | One partner card — the navy card, the gold logo holder (network logo + spinner + initials fallback), the centred name. |
| `app/widgets/media_coverage_tabs.dart` (shared) | The **shared** `MediaCoverageTabs(active: partners)` — this screen now consumes it (its local `_MediaTabs`/`_MediaTab` deleted). |

## DRY (this freeze — completes D-629)

The local `_MediaTabs`/`_MediaTab` was **byte-identical** to the copy News
carried; the shared **`MediaCoverageTabs`** was extracted into the app catalogue
in the News pass (D-629) and this screen's local copy is now **replaced with it +
deleted** (`active: partners`). The tab-order widget test (`partners→latest
right-to-left`) passes unchanged, confirming the shared strip renders identically.
The data layer moved to `data/` (D-545); the screen was already fully tokenised
(no raw `Color(0x..)`).

## L4 Figma parity (frame 958:2246)

Captured `media_partners_958-2246.png` (@375×750, ar, 4 partners) as the
**baseline before** the refactor, then **held it WITHOUT `--update`** after —
proving the data-layer move + the shared-tabs swap + the `PartnerCard` extraction
byte-identical. Golden read: المركز الاعلامي header, the tab strip (الشركاء
الإعلاميون active-gold right / احدث المستجدات left), the 2-column partner grid
(gold logo tiles + names), RTL, no tofu.

## Level-F

Wired: the inactive tab replaces to news; pull-to-refresh + retry re-fetch; the
logo renders the D-357 asset or the initials fallback. Reads
`GET /app/media-partners`. No missing API.

## Tests

`test/golden/media_partners_golden_test.dart` (frame 958:2246, @375×750, ar) +
`test/features/media_partners/media_partners_screen_test.dart` (tabs order,
`fromJson`, `logoAssetUrl` route). E2E: `docs/tests/e2e/mobile-media-partners.md`.

## Related decisions

- **D-630** (this clean-code freeze — data move + shared `MediaCoverageTabs` adoption + `PartnerCard` + first golden; completes the D-629 tab dedup).
- **D-306** (screen built), **D-199** (public endpoint), **D-357** (unified media-asset route for the logo).
