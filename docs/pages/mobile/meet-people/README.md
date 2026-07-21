# Meet people - قابل أشخاص مثلك (Page 035, `#35`)

- **Route:** `/meet` (`RouteNames.meetPeople`). Access: **Visitor (login-only, approved account)** - `RequireApprovedAccount`.
- **Build #13 rework (2026-07-22):** this screen was the AI "% match" recommender; it is now the curated + opt-in **partner directory**. This doc **supersedes** the old recommender behavior (the smart-suggestions header card + per-match "% تطابق" cards + backend match reason are gone). No pinned Figma node - the screen reuses the existing speakers/sponsors list chrome (`SimfIdentityCell`), owner-approved.

## Purpose

Show an approved member a directory of people and organisations worth meeting at
the forum - not an algorithmic recommendation. The list is the **deduped union**
of the curated exhibition entities plus opted-in members:

| Kind (`entry.kind`) | Who | Tap navigates to |
|---------------------|-----|------------------|
| `speaker` | Curated Speakers (name, rank, photo, country) | Speaker profile (`RouteNames.speakerProfile`) |
| `sponsor` | Sponsors, shown by (Contact-first) company name + tagline | Sponsor detail (`RouteNames.sponsorDetail`) |
| `booth` | Exhibition booth companies (`id` = booth id, name = exhibitor company name, sector subtitle) | Exhibitor detail (`RouteNames.exhibitorDetail`) |
| `person` | Opted-in "Other"-type members (name + job title) | Nothing - the row is non-tappable |

**Who never appears:** Normal and VIP visitors. A `person` row is included only
when the account is **Approved + non-Admin + `UserProfile.ShowInMeetLikeYou == true`
+ `ProfileType.IsForVisitor == false`**. De-dup: a person who is also a curated
Speaker (linked `UserProfileId`) appears **once, as the speaker** - the curated
entity wins.

## Data source

`partnerDirectoryProvider` → **`GET /api/v1/app/networking/partner-directory`**
(`RequireApprovedAccount`, no permission code) → `ApiResult<PartnerDirectoryResponse>`
where `PartnerDirectoryResponse { entries: PartnerDirectoryEntry[] }`. Each
`PartnerDirectoryEntry` carries `kind, id, name, nameArabic, subtitle,
subtitleArabic, logoRelativePath, logoContactId, countryId, countryNameEn,
countryNameAr`. Logos follow the existing projection convention (a relative path
or the owning contact id, never an absolute URL); the client builds the per-kind
asset URL.

## CP control flag

The whole feature is gated by the CP switch
**`OrganizationProfile.PartnerDirectoryEnabled`** (default true), edited on the CP
Site-Settings page (`/admin/site-settings`, `Configuration.Edit`). The flag rides
the public `GET /app/site-settings` payload (`SiteSettingsResponse.partnerDirectoryEnabled`).
When **off**:

- the endpoint returns an **empty** list, and
- the Home "Meet People" tile is **hidden** (read off the same site-settings flag).

## Opt-in (Other-type members)

An opted-in `person` only appears if they turned themselves on. The opt-in is a
checkbox on the **My-interests edit** screen (`RouteNames.myInterests`, the
`sign_up_interests_screen` in edit mode), surfaced **only to "Other"-type members**
(`widget.editMode && !isForVisitor`). It toggles `UserProfile.ShowInMeetLikeYou`
and is re-sent on the profile save. The app gates the checkbox off the append-only
`UserProfileResponse.isForVisitor` field, so audience visitors never see it.

## Structure

| File | Holds |
|------|-------|
| `meet_people_screen.dart` | `MeetPeopleScreen` (`ConsumerWidget`) - reads `partnerDirectoryProvider`, `onRefresh`, the loading / error / empty / data dispatch, and the per-kind tap routing (`_onTapFor`). Rows are the shared `SimfIdentityCell`. Re-exports `data/meet_repository.dart`. |
| `data/meet_repository.dart` | `partnerDirectoryProvider` (FutureProvider over the directory endpoint). |
| `data/partner_directory_models.dart` | `PartnerDirectoryEntry` + `PartnerDirectoryResponse` + the `localizedName` / `localizedSubtitle` / `logoUrl` helpers and the `isSpeaker` / `isSponsor` / `isBooth` kind predicates. |

## Behavior

- **List:** a lazy `ListView.separated` of `SimfIdentityCell` rows (title = name,
  subtitle = role/tagline/sector, image = per-kind logo, country tag = `countryId`).
- **Empty state:** `SimfEmptyState` (people icon) - "No one to show yet" /
  "لا يوجد أشخاص لعرضهم بعد" (`meetPeopleEmpty`).
- **Error state:** `SimfErrorState` with retry - "Could not load the directory." /
  "تعذّر تحميل الدليل." (`meetPeopleError`).
- **Pull-to-refresh** preserved (`SimfPullToRefresh` + `SimfPullableHost`), on the
  empty and error states too.

## Tests

`test/features/meet/partner_directory_models_test.dart` (model decode + the kind
predicates + logo URL) + `test/features/meet/meet_people_screen_test.dart`
(list renders the four kinds, empty state, error state, per-kind tap) +
`test/golden/meet_people_golden_test.dart`.
E2E: [`../../../tests/e2e/mobile-meet-people.md`](../../../tests/e2e/mobile-meet-people.md).

## Related decisions

- **Build #13** - the partner-directory rework (this doc). CP toggle
  `OrganizationProfile.PartnerDirectoryEnabled` (migration `AddPartnerDirectoryEnabled`);
  opt-in surfaced on My-interests via `UserProfileResponse.isForVisitor` (append-only).
- **D-736** - `UserProfile.ShowInMeetLikeYou` (the opt-in flag) added.
- **Superseded:** D-313 (recommender screen built), D-448 (Figma `1072:13409`
  parity), D-451 (backend match reason), D-632 (recommender clean-code freeze) -
  all describe the old "% match" recommender this build replaced.
