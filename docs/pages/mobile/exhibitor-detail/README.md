# Exhibitor detail (العارض) — mobile `/exhibitors/:boothId`

| Field | Value |
|---|---|
| Route | `/exhibitors/:boothId` (`RouteNames.exhibitorDetail`, page #220) · **public** — not in `_authenticatedRoutes` and not in `_routeRoles`, so a guest opens it without signing in |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/booths/exhibitor_detail_screen.dart` (`ExhibitorDetailScreen`, 123 lines, `ConsumerWidget`) |
| Widgets | None of its own. It is a thin wrapper over the shared exhibition template: `features/exhibition/widgets/entity_detail_scaffold.dart` (`EntityDetailScaffold`) → `entity_identity_card.dart` · `entity_about_card.dart` · `entity_link_row.dart`, plus `entity_logo_image.dart` (`EntityLogoImage`). Pure helpers in `features/exhibition/entity_detail_helpers.dart`. |
| Figma node | `1439:11881` ("العارض") — the same template as the sponsor frame `1439:11826` |
| Shell | `SimfPageShell` (title العارض), applied inside `EntityDetailScaffold` |
| API | `GET /app/booths/{id}` → `BoothDetail` (path from `VenueMapEndpoints.boothById`; the read lives on the venue-map repository, which already owns the booth endpoints). Logo images come from the anonymous asset route `{base}/app/assets/ExhibitorLogo/{exhibitorId}/image` with `{base}/app/assets/CompanyLogo/{exhibitorContactId}/image` as the fallback. |
| Providers | `exhibitorDetailProvider(boothId)` (`features/booths/data/booths_repository.dart`) → `venueMapRepositoryProvider.getBoothDetail` · `simfDataConfigProvider` for the asset base URL |
| Tests | `test/features/booths/exhibitor_detail_screen_test.dart` (2). **No golden** — neither entity-detail screen has a render lock (see §9). E2E [`mobile-exhibitor-detail.md`](../../../tests/e2e/mobile-exhibitor-detail.md) |
| Status | ✅ Real — Wave 3; **clean-code frozen (D-619)**: the screen and the sponsor screen were near-identical copies and were collapsed onto `EntityDetailScaffold` + shared helpers |

## 1. Purpose

The public profile of one exhibitor, opened from a booth: logo, name, city·country
line, a full-width tier pill, the stand code with a "show me on the map" hop, the
"نبذة عن العارض" paragraph, and the website row.

## 2. Audience & access

Anonymous. The route carries no auth gate and `GET /app/booths/{id}` is a public
read, so a guest reaches it straight from the booths list.

## 3. Entry points

| From | Code |
|---|---|
| Booths list (#22) — tapping a booth card | `booths_screen.dart:35` → `pushNamed(RouteNames.exhibitorDetail, pathParameters: {boothId})` |
| Meet People → partner directory | `features/meet/widgets/partner_directory_list.dart:78` |

## 4. UI & behaviour

`EntityDetailScaffold` renders one `ListView` inside `SimfPullToRefresh`:

1. **Identity card** (`EntityIdentityCard`, borderless `navyDeep`) — the logo box,
   the name, the gold `City، Country` line with the country flag when `countryId`
   is present, the tier pill, and the stand row.
2. **About card** (`EntityAboutCard`) — header + beige divider + paragraph. Hidden
   entirely when `description` / `descriptionArabic` is blank.
3. **Website row** (`EntityLinkRow`) — `navyDeep` fill, the stroked globe glyph
   (`AppAssets.authGlobe`, Figma 1439:11927), label above value. Hidden when the
   website is blank.

Field resolution, all in `_build`:

- **Name** — `localizedExhibitor(...)` first, falling back to the booth's own
  `localizedName(...)` when the booth carries no exhibitor name.
- **Logo** — `EntityLogoImage` tries the exhibitor's own `ExhibitorLogo` asset,
  then the legacy Contact `CompanyLogo` (so an exhibitor that has not re-uploaded
  still shows a mark), then a two-letter initials tile from `entityInitials`. The
  mark renders `BoxFit.contain` through the shared `SimfLogoImage`, and tapping it
  opens the logo full size.
- **Location line** — `entityLocationLine(city, country, isArabic:)` joins with the
  Arabic comma `،` in Arabic and `,` in English; null when both sides are blank.
- **Tier pill** — shown only when `tier` is non-null **and** `tierName` is
  non-empty, formatted by `l10n.exhibitorTierPill`.
- **Stand row** — the gold `code` under the muted "موقع الجناح على الخريطة" label.
  `onMap` is **null when `code` is empty**, which makes the row inert rather than
  pushing a map with no target.

## 5. Actions

| Control | Handler | Effect |
|---|---|---|
| Back | `backOrHome(context)` | Pops, or goes Home when the stack is empty (deep link) |
| Pull-to-refresh | `refreshAsync(ref, exhibitorDetailProvider(id).future)` | Re-reads the detail; swallows the failure so the screen's own error branch shows it |
| Stand code row | `pushNamed(RouteNames.boothMap, {boothId})` | Opens the venue map focused on this booth (#112) |
| Website row | `_openWebsite` → `entityHttpUri` → `confirmThenLaunchExternal` | Parses the URL (prepending `https://` when the scheme is missing), then shows the shared external-link confirmation before leaving the app |
| Retry (error state) | `ref.invalidate(exhibitorDetailProvider(id))` | Re-fetches |

## 6. Data contract (`BoothDetail`, `GET /app/booths/{id}`)

Wire keys the app decodes (D-219 frozen): `id` · `code` · `name` · `nameArabic` ·
`exhibitorName` · `exhibitorNameArabic` · `sector` · `sectorArabic` ·
`description` · `descriptionArabic` · `hallName` · `hallNameArabic` ·
`officerName` · `officerPhone` · `officerEmail` · `exhibitorContactId` ·
`countryId` · `countryName` · `countryNameArabic` · `city` · `cityArabic` ·
`tier` · `tierName` · `website` · `exhibitorId`.

## 7. States

| State | Render |
|---|---|
| Loading | `SimfPageShell` + centred gold `CircularProgressIndicator` |
| Error | `SimfRefreshableMessage` wrapping `SimfErrorState` (`l10n.entityDetailError` + retry) — pullable, because a short error body would otherwise not accept the gesture |
| Loaded | The scaffold above; every optional block hides rather than rendering an empty labelled box |

There is no distinct empty state: a booth that exists always has an id and a name,
and a missing booth surfaces through the error branch.

## 8. i18n / RTL

Every string goes through `AppL10n` (`exhibitorDetailTitle` العارض ·
`exhibitorAboutHeader` نبذة عن العارض · `websiteLabel` · `exhibitorTierPill` ·
`entityDetailError` · `retryLabel`). Bilingual payload fields are picked by
`l10n.isArabic` with an other-language fallback. The scaffold uses token spacing
and `EdgeInsets` symmetric padding, so the layout mirrors with the app's
directionality.

## 9. Findings (recorded, not changed)

1. **`sector` / `hallName` / `officerName` / `officerPhone` / `officerEmail` are
   decoded but not rendered here.** They belong to the booths *list* card widgets
   (`booth_contact_boxes.dart`, `booth_hall_box.dart`, `booth_officer_row.dart`);
   `EntityDetailScaffold` has no slot for them. That is the template's shape, not
   an oversight — but it means the detail page is *less* informative than the list
   row that opened it.
2. **No render lock.** `EntityDetailScaffold` — the template both detail
   screens depend on — has no golden. The sponsors golden covers the *list*
   frame `922:2824`, a different widget tree, so a layout regression in the
   shared scaffold would be caught by neither detail screen's tests.
3. **An empty `code` silently disables the map hop** rather than hiding the stand
   row, so the label can render with no value and no tap.
