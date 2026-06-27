# Delegations (الوفود) — `/delegations`

| | |
|--|--|
| **Route** | `/delegations` (route name `delegations`) — mockup screen **#21** (restored; removed in D-277) |
| **Layout** | KSA app shell (`KsaPage`), back chevron + centred title |
| **Surface** | Mobile App (Flutter) |
| **Audience** | Guest+ (public) |
| **Auth** | **None** — `GET /app/delegations` is `AllowAnonymous`; reached from a home tile + the direct `/delegations` route |
| **Pattern** | Wave 4 (D-499) public read-only list screen; data-driven from one anonymous GET, mirrors the public booths / sponsors / speakers screens |
| **Status** | ✅ Real (D-499, Figma `1426:10771`) |
| **Implements use case(s)** | Invited-country delegation directory (event "delegations" / الوفود) |
| **Backend endpoints** | `GET /api/v1/app/delegations` (public). CP-side management: `GET /api/v1/admin/countries/{id}/delegates` (`Countries.View`) feeds the head-of-delegation picker; the dates + head are saved through the existing `PUT /admin/countries/{id}`. |
| **Source file** | Flutter `features/delegations/` screen + repository/model; API `app` delegations endpoint + `Country` entity (`SimfAppDbContext`). |
| **Tests** | [`docs/tests/e2e/mobile-delegations.md`](../../tests/e2e/mobile-delegations.md) (`E2E-DEL-001..009`) |
| **Last reviewed** | 2026-06-26 |

---

## 1. Purpose

Delegations (الوفود) is the public directory of the **invited countries'**
delegations attending the forum. A visitor — signed-in or guest — opens it to see
which countries are participating, how many participants there are in total, and,
per country, who leads the delegation, when they arrive and leave, and how many
members the delegation has. It is a read-only browse surface: there is no action
on the screen beyond searching and scrolling. The data is owned and curated in the
Control Panel on the Country record (an admin marks a country invited, sets the
delegation dates, and picks the head of delegation); the screen simply renders the
current state of those records.

## 2. Audience + permissions

- **Who can reach it:** anyone — the screen is public. It is opened from a home
  tile and from the direct `/delegations` route.
- **Who can edit/write on it:** no one from the app — the screen is read-only. The
  underlying data is edited only in the Control Panel (see §5).
- **Authorisation gates:** the app endpoint `GET /app/delegations` is
  `AllowAnonymous` — consistent with the other public event-content endpoints
  (speakers, booths, sponsors). The CP sub-endpoint that feeds the head picker
  (`GET /admin/countries/{id}/delegates`) is gated `Countries.View`.
- **What an unauthenticated user sees:** the full screen — a guest is a first-class
  audience here (no token is required to load the feed).

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with cards) | `docs/screenshots/delegations-default.png` | _pending on-device capture_ |
| Empty state | `docs/screenshots/delegations-empty.png` | _pending_ |
| RTL (Arabic) | `docs/screenshots/delegations-rtl.png` | _pending_ |
| Error state | `docs/screenshots/delegations-error.png` | _pending_ |

> Figma reference frame: `1426:10771`.

## 4. UI affordances

### 4.1 Header

Back chevron + centred title **الوفود** ("Delegations").

### 4.2 Stats strip

Two figures rendered from the response header:

| Figure | Source field | Label (AR) | Label (EN) |
|--------|--------------|------------|------------|
| Participating countries | `countryCount` | دولة مشاركة | Participating countries |
| Total participants | `totalParticipants` | إجمالي المشاركين | Total participants |

### 4.3 Search box

A single filter input. Hint: "ابحث عن دولة أو وفد..." / "Search for a country or
delegation...". It filters the cards client-side by country name (ar/en) and head
name (ar/en).

### 4.4 Country card (one per `items[]` entry)

| Element | Source field(s) | Notes |
|---------|-----------------|-------|
| Flag | `countryCode` | country flag |
| Country name | `countryName` / `countryNameArabic` | localized |
| Head of delegation row | `headName` / `headNameArabic`, `headTitle` | label "رئيس الوفد" / "Head of delegation" over name + job title, with an **initial avatar** (first letter). Shown **only** when a head is set. |
| Date range | `arrivalDate` → `departureDate` | the delegation's arrival/departure dates |
| Member count | `memberCount` | active delegate count for the country |

## 5. CP-side management (where the data comes from)

The delegations screen has no editor of its own. Each card is a projection of a
`Country` record curated on the existing Country Add/Edit form
(`/admin/countries`, page `CountriesList` → `CountryAddEdit.razor`). An admin:

1. **Marks the country invited** — `Country.IsInvited` (only invited + active
   countries appear in the delegations feed).
2. **Sets the delegation dates** — `Country.DelegationArrivalDate` /
   `DelegationDepartureDate` (additive nullable date columns, migration D499).
3. **Picks the head of delegation** — `Country.HeadOfDelegationUserProfileId`
   (additive nullable `Guid` FK → `UserProfile`, `SetNull`). The picker offers the
   country's **active delegates**, fed by the new admin sub-endpoint
   `GET /admin/countries/{id}/delegates` (gated `Countries.View`).

The dates + head are persisted through the existing `PUT /admin/countries/{id}`
(`Countries.Edit`) — the CP permissions are unchanged (`Countries.View` /
`.Create` / `.Edit` / `.Delete`). See
[`docs/pages/cp/admin-countries.md`](../cp/admin-countries.md).

The **member count** is **not** stored — it is **derived on read** from the active
delegate `UserProfile`s (`IsDelegate && IsActive`) whose `NationalityId` is the
country (per the D-157 "no duplicated data — resolve on read" rule).

## 6. Data flow

```
Guest opens /delegations → screen calls GET /api/v1/app/delegations (anonymous)
  → app delegations service: select Country where IsInvited && IsActive
     → resolve head from Country.HeadOfDelegationUserProfileId (UserProfile)
     → count active delegates (IsDelegate && IsActive) by NationalityId
  → ApiResult<AppDelegations> → cards + stats render
```

| When | Method + path | Request | Response shape |
|------|---------------|---------|----------------|
| Screen open | `GET /api/v1/app/delegations` | — | `ApiResult<AppDelegations>` |

`AppDelegations { countryCount, totalParticipants, items[] }`, each
`AppDelegationItem { countryId, countryCode, countryName, countryNameArabic,
headName?, headNameArabic?, headTitle?, arrivalDate?, departureDate?,
memberCount }`.

## 7. States (loading / error / empty)

- **Loading:** a spinner while the GET is in flight.
- **Error:** an inline retry surface with "تعذر تحميل الوفود." / "Could not load
  delegations." on a network / 5xx failure; Retry re-runs the call.
- **Empty:** "لا توجد وفود بعد." / "No delegations yet." when no invited countries
  are returned (`items` empty, `countryCount` / `totalParticipants` zero).
- **Head omitted:** when a country has no `HeadOfDelegationUserProfileId`, the card
  renders without the head row and the initial avatar; the item's `headName` /
  `headNameArabic` / `headTitle` are null.

## 8. i18n + RTL

All visible strings are localized (AR / EN): title "الوفود" / "Delegations",
search hint "ابحث عن دولة أو وفد..." / "Search for a country or delegation...",
the stats labels "دولة مشاركة" / "Participating countries" and "إجمالي المشاركين"
/ "Total participants", the head label "رئيس الوفد" / "Head of delegation", the
error "تعذر تحميل الوفود." / "Could not load delegations.", and the empty
"لا توجد وفود بعد." / "No delegations yet.". Country and head names come from the
record's bilingual fields and switch with the locale. Under Arabic the header,
stats strip, search box and cards mirror right-to-left.

## 9. Edge cases + known limitations

- **Only invited + active countries appear** — a country with `IsInvited = false`
  (or deactivated) is excluded from the feed entirely.
- **Member count is derived, never stored** — it reflects the live count of active
  delegate profiles for the country at read time (D-157), so it changes as
  delegates are added / deactivated without any write to `Country`.
- **Head is optional** — a country can be invited and dated with no head picked;
  the card simply omits the head row.
- **Dates are optional** — `arrivalDate` / `departureDate` may be null until an
  admin sets them.
- **Read-only on the app** — there is no submit / join / contact action on this
  screen; all curation is CP-side.

## 10. Related E2E test scenarios

See [`docs/tests/e2e/mobile-delegations.md`](../../tests/e2e/mobile-delegations.md)
(`E2E-DEL-001..009`): golden path (invited cards + the two stats), search by
country name (ar/en) and head name (ar/en), empty state, head-omitted, member
count excluding inactive / non-delegate profiles, public/anonymous access, wire
error → retry, and RTL.

## 11. Related docs

- CP source-of-truth page: [`docs/pages/cp/admin-countries.md`](../cp/admin-countries.md)
  (where the country is marked invited + the dates + head are set).
- Decisions log: **D-499** (this screen + the public endpoint + the all-on-Country
  schema choice + the additive migration D499). Related: D-157 (Data ↔ Identity
  separation — member count derived on read), D-473 (delegate = visitor +
  `IsDelegate` + invited country), D-277 (the earlier removal of screen #21).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) — `app`
  endpoint group, `ApiResult<T>` envelope.

## 12. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-06-26 | D-499 | Wave 4 — new public delegations screen (الوفود, Figma `1426:10771`, screen #21 restored from D-277) + `GET /app/delegations`; `Country` gains `DelegationArrivalDate` / `DelegationDepartureDate` + `HeadOfDelegationUserProfileId` (additive migration D499); CP Country form sets the dates + head; member count derived from active delegate profiles. |

---

_Last reviewed:_ 2026-06-26 by SIMF Team (D-499 — delegations screen reference doc).
