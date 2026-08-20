# Sponsor detail (الراعي) — mobile `/sponsors/:sponsorId`

| Field | Value |
|---|---|
| Route | `/sponsors/:sponsorId` (`RouteNames.sponsorDetail`, page #221) · **public** — no auth gate, no role gate |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/sponsors/sponsor_detail_screen.dart` (`SponsorDetailScreen`, 97 lines, `ConsumerWidget`) |
| Widgets | None of its own — the shared exhibition template `features/exhibition/widgets/entity_detail_scaffold.dart` (`EntityDetailScaffold`) → `entity_identity_card.dart` · `entity_about_card.dart` · `entity_link_row.dart`, plus `entity_logo_image.dart`. Pure helpers in `features/exhibition/entity_detail_helpers.dart`. |
| Figma node | `1439:11826` ("الراعي") — the same template as the exhibitor frame `1439:11881` |
| Shell | `SimfPageShell` (title الراعي), applied inside `EntityDetailScaffold` |
| API | `GET /app/sponsors/{id}` → `SponsorDetail` (`SponsorsEndpoints.byId`). The logo comes from the anonymous asset route `{base}/app/assets/SponsorLogo/{sponsorId}/image`. |
| Providers | `sponsorDetailProvider(sponsorId)` (`features/sponsors/data/sponsors_repository.dart`, calls `simfApiClientProvider` directly) · `simfDataConfigProvider` for the asset base URL |
| Tests | `test/features/sponsors/sponsor_detail_screen_test.dart` (5) + `sponsor_detail_models_test.dart` (2); **no golden** — `test/golden/sponsors_golden_test.dart` (`goldens/sponsors_922-2824.png`) renders the sponsors **list** (`SponsorsScreen`), which does not use `EntityDetailScaffold`, so it locks nothing here (see §9). E2E [`mobile-sponsor-detail.md`](../../../tests/e2e/mobile-sponsor-detail.md) |
| Status | ✅ Real — Wave 3; **clean-code frozen (D-619)**: collapsed with the exhibitor screen onto one `EntityDetailScaffold` + shared helpers |

## 1. Purpose

The public profile of one sponsor: logo, name, city·country line, the tier pill
("راعي بلاتيني" and friends), the "نبذة عن الراعي" paragraph, and the website row.
It is the exhibitor detail's twin — same template, one field fewer.

## 2. Audience & access

Anonymous. `GET /app/sponsors/{id}` is a public read (D-199), matching the
sponsors list.

## 3. Entry points

| From | Code |
|---|---|
| Sponsors grid (#23) | `features/sponsors/widgets/sponsor_grid.dart:47` |
| Sponsors tier list (#23) | `features/sponsors/widgets/sponsor_tier_list.dart:84` |
| Meet People → partner directory | `features/meet/widgets/partner_directory_list.dart:72` |

## 4. UI & behaviour

Identical structure to the exhibitor page, with **no stand row** — the sponsor
detail passes no `standLabel` / `standCode` / `onMap`, so `EntityIdentityCard`
drops that block:

1. **Identity card** — logo box, name, gold `City، Country` line with the country
   flag when `countryId` is set, full-width tier pill.
2. **About card** — hidden when `about` / `aboutArabic` is blank.
3. **Website row** — hidden when `url` is blank.

Field resolution, all in `_build`:

- **Name** — `localizedName(isArabic:)` over `nameAr` / `nameEn`.
- **Logo** — `EntityLogoImage` with a single URL (the `SponsorLogo` asset keyed by
  the sponsor id); no fallback URL, so a 404 falls straight to the initials tile
  from `entityInitials`. `BoxFit.contain` via the shared `SimfLogoImage`; tapping
  opens the logo full size.
- **Location line** — `entityLocationLine(city, country, isArabic:)`.
- **Tier pill** — shown when `tierName` is non-empty (the sponsor DTO's `tierName`
  is non-nullable and defaults to `''`), formatted by `l10n.sponsorTierPill`.

## 5. Actions

| Control | Handler | Effect |
|---|---|---|
| Back | `backOrHome(context)` | Pops, or Home on an empty stack |
| Pull-to-refresh | `refreshAsync(ref, sponsorDetailProvider(id).future)` | Re-reads; the failure is swallowed so the screen's error branch owns the message |
| Website row | `_openWebsite` → `entityHttpUri` → `confirmThenLaunchExternal` | `https://` is prepended when the scheme is missing; the shared confirmation dialog runs before leaving the app |
| Retry (error state) | `ref.invalidate(sponsorDetailProvider(id))` | Re-fetches |
| Logo tap | `SimfLogoImage` | Full-size logo viewer, titled with the sponsor name |

## 6. Data contract (`SponsorDetail`, `GET /app/sponsors/{id}`)

Wire keys the app decodes (D-219 frozen): `id` · `nameEn` · `nameAr` · `tier` ·
`tierName` · `logoRelativePath` · `url` · `about` · `aboutArabic` · `city` ·
`cityArabic` · `countryId` · `countryNameEn` · `countryNameAr`.

## 7. States

| State | Render |
|---|---|
| Loading | `SimfPageShell` + centred gold `CircularProgressIndicator` |
| Error | `SimfRefreshableMessage` wrapping `SimfErrorState` (`l10n.entityDetailError` + retry) |
| Loaded | The scaffold above; blank optional fields hide their whole block |

## 8. i18n / RTL

`AppL10n` keys: `sponsorDetailTitle` (الراعي) · `sponsorAboutHeader`
(نبذة عن الراعي) · `sponsorTierPill` · `websiteLabel` · `entityDetailError` ·
`retryLabel`. Bilingual payload fields pick by `l10n.isArabic` with an
other-language fallback; token spacing and directional padding throughout.

## 9. Findings (recorded, not changed)

1. **`logoRelativePath` is decoded and never used.** The screen builds the logo URL
   from `AssetUrls.image(baseUrl, AssetKind.sponsorLogo, sponsor.id)` instead. The
   field stays on the model because the wire contract is append-only (D-219) and
   removing a decoded key is not a safe unilateral change — but nothing reads it.
2. **No render lock** on `EntityDetailScaffold` — see
   [exhibitor-detail §9](../exhibitor-detail/README.md).
3. **A blank `tierName` silently drops the pill**, so a sponsor the CP has not
   tiered renders with no tier affordance at all rather than a neutral one.
