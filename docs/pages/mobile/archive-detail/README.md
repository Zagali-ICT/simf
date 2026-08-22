# Past-edition detail (تفاصيل النسخة) — mobile, inside `/archive`

| Field | Value |
|---|---|
| Route | **None of its own.** There is no `RouteNames.archiveDetail` and no `/archive/:editionId` path — grep `route_names.dart` and it is not there. The edition detail is a **state of the Archive screen** (`/archive`, `RouteNames.archive`, page #24), reached by selecting an edition pill. It is indexed as page **#24-01** because it is a distinct documented surface, not because it is a distinct screen. |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/archive/archive_screen.dart` (`ArchiveScreen`, 76 lines) holds the selection; `features/archive/widgets/archive_body.dart` (`ArchiveBody`) renders the detail column |
| Widgets | `archive_edition_pills.dart` / `edition_pill.dart` · `archive_bullet.dart` · `archive_place_time_row.dart` / `labelled_bullet.dart` · `archive_stat_row.dart` / `stat_tile.dart` · `archive_gallery_row.dart` / `archive_gallery_tile.dart` · `archive_session_title_card.dart` · `archive_past_speakers_row.dart` / `archive_past_speaker_card.dart` / `past_speaker_overflow.dart` · `archive_notice_banner.dart` |
| Figma node | `925:3079` — the list and the edition detail are **one frame** |
| Shell | `SimfPageShell` (title الأرشيف), owned by `ArchiveScreen` |
| API | `GET /app/archive/{id}` → `PublicArchiveEditionDetail` (`ArchiveEndpoints.byId`), **`AllowAnonymous`**. The pill row itself comes from `GET /app/archive`. |
| Providers | `archiveEditionDetailProvider(editionId)` and `archiveEditionsProvider` (`features/archive/data/archive_repository.dart`) |
| Tests | The archive model tests + golden `test/golden/archive_golden_test.dart` (`goldens/archive_925-3079.png`, render-lock @375×1293). E2E [`mobile-archive-detail.md`](../../../tests/e2e/mobile-archive-detail.md) |
| Parent doc | [`../archive/README.md`](../archive/README.md) — the list, the pills, the pull-to-refresh and the D-617 clean-code freeze |
| Legacy detail | `docs/App/Page_024-01/` — the five-file spec this file replaces. See §9 for what had gone stale in it. |
| Status | ✅ Real — endpoint built D-273; the rich lists built **D-432**; country flag on past speakers **D-456** |

## 1. Purpose

One past edition of the forum: its title, نبذة summary, المكان / الزمن boxes, the
three counters, and — since D-432 — the gallery, the session titles and the past
speakers.

## 2. Audience & access

Anonymous, exactly like the list. The only gate is the archive-visibility
operations toggle (D-166): with the toggle off the whole archive surface is
hidden, so the list is empty and there is no pill to select.

## 3. How it is reached

`ArchiveScreen` loads `archiveEditionsProvider` once. On first data it **defaults
the selection to the most recent edition** (the API returns newest-first, so
`items.first`) rather than showing a chooser; tapping another pill sets
`_selectedId`. `ArchiveBody` then watches
`archiveEditionDetailProvider(selected.id)`, so selecting a pill is what fires
the per-edition read — the detail is **lazily loaded, one edition at a time**.

## 4. What the detail contributes

`ArchiveBody` renders one `ListView` in which the *summary* row (`ArchiveEdition`,
from the list read) and the *detail* row (`ArchiveEditionDetail`) are interleaved:

| Block | Source | Behaviour |
|---|---|---|
| Notice banner (`925:3222`) | `l10n.archiveNotice` | Always |
| "اختار ملتقى" + pill row (`927:3352`) | list read | Always |
| عنوان الملتقى — gold bulleted title (`926:3277`) | **list** row | Always |
| نبذة (`926:3276`) | **detail** row, falling back to the list row's summary | Hidden when both are null |
| المكان / الزمن two-column row (`926:3284`) | **detail** row only | Whole row hidden when both are null; each box hides individually |
| Stat tiles المتحدثون / الحضور / الفعاليات (`926:3285`) | **list** row | Always |
| الصور والفيديو gallery | **detail** row | Hidden when the list is empty |
| عناوين الجلسات cards | **detail** row | Hidden when the list is empty |
| المتحدثون السابقون row | **detail** row | Hidden when the list is empty, with a "+N آخرون" overflow |

Section labels use the shared `SimfSectionHeader`. The title and counters come
from the **list** payload on purpose: they are already on screen before the
detail read resolves, so switching pills never blanks the page.

## 5. Failure behaviour — a 404 is invisible

`archiveEditionDetailProvider` catches **every** `ApiFailure` and returns `null`:

```dart
try {
  return await client.get<ArchiveEditionDetail>(...);
} on ApiFailure {
  return null;
}
```

`ArchiveBody` reads `detail.asData?.value` and treats null as "no extra fields",
so a failed or 404 detail read renders as an edition with **no place, no time, no
gallery, no session titles and no past speakers** — visually identical to an
edition the CP has not filled in. There is no error state and no retry for the
detail read specifically; the screen-level error branch only covers the list.

## 6. Data contract (`PublicArchiveEditionDetail`, `GET /app/archive/{id}`)

Wire keys the app decodes (D-219 frozen):

| Key | Notes |
|---|---|
| `id`, `year` | |
| `titleEn` / `titleAr` | |
| `summaryEn` / `summaryAr` | nullable |
| `locationEn` / `locationAr` | nullable — added D-273; **detail-only**, absent from the list payload |
| `dateLabelEn` / `dateLabelAr` | nullable — added D-273; detail-only |
| `attendees`, `sessions`, `speakers` | the three counters |
| `gallery[]` | `ArchiveMediaItem`: `kind` (0 image / 1 video), `url`, `captionEn` / `captionAr` |
| `sessionTitles[]` | `ArchiveSessionTitle`: `titleEn` / `titleAr` |
| `pastSpeakers[]` | `ArchivePastSpeaker`: `nameEn` / `nameAr`, `photoRelativePath`, `countryId` (ISO 3166-1 numeric, D-456) |

The list payload (`PublicArchiveEdition`) carries the same scalars **minus**
location and date label, **plus** `coverImageRelativePath`.

Error surface: a hidden archive (toggle off) and an unknown / soft-deleted
edition are a **single 404**, so one id cannot be probed to learn that the
archive exists. There is no 401 / 403 — the read is anonymous.

## 7. Authoring (context, not this page)

The edition is authored in the CP at `/admin/archive` (D-199), gated on the
`Archive.*` permissions, with `PUT /admin/archive/visibility` driving the D-166
toggle.

## 8. i18n / RTL

`AppL10n`: `archiveTitle` · `archiveNotice` · `archivePickEdition` ·
`archiveTitleLabel` (عنوان الملتقى) · `archiveSummaryLabel` (نبذة) ·
`archivePlaceLabel` (المكان) · `archiveTimeLabel` (الزمن) ·
`archiveGalleryLabel` · `archiveSessionsLabel` · `archivePastSpeakersLabel` ·
`archiveEmpty` · `archiveError` · `retryLabel`. Every bilingual pair is picked by
`l10n.isArabic` through `pickLocalized` / `pickLocalizedOrNull`, which falls back
to the other language rather than rendering blank.

## 9. What the legacy `Page_024-01` doc got wrong (corrected here)

The five-file set under `docs/App/Page_024-01/` was written when the endpoint had
just landed and the Flutter work had not. Four of its claims are now false:

1. **"Route `RouteNames.archiveDetail` *(planned)* → `/archive/:editionId`
   *(planned)*; Flutter wiring deferred, todo #9."** No such route was ever
   added, and none is needed — the detail became a state of `/archive`.
2. **"The rich lists — الصور والفيديو, عناوين الجلسات, المتحدثون السابقون — are
   deferred; the screen renders them as 'coming soon' placeholders."** They were
   modelled and built in **D-432**. There are no placeholders; each section is
   present when its list is non-empty and absent otherwise.
3. **"Cover banner with the year overlaid; gradient fallback when the cover is
   null."** `ArchiveBody` renders no cover banner at all. The list model still
   decodes `coverImageRelativePath`, and nothing reads it.
4. **"Not found → a 'not found' empty state with a back action."** The detail read
   folds every failure to null (§5), so a 404 shows a thinner edition, never a
   not-found state.

Two more of its statements are still true and worth keeping: the single-404
visibility surface (§6) and the "never render an empty labelled box" rule, which
`ArchiveBody` honours by hiding each block rather than emitting a bare label.

## 10. Findings (recorded, not changed)

1. **Gallery video tiles show a play glyph but have no tap handler** — no lightbox
   and no player.
2. **Past-speaker cards are display-only** — past speakers have no profile route.
3. **`ArchivePastSpeaker.countryId` is decoded but not drawn.** The D-456 corner
   flag exists in the model and not on the card.
4. **`ArchiveEdition.coverImageRelativePath` is decoded and never used** (§9.3).
