# E2E test catalogue — `Archive` (`archive`)

> **Authority:** SIMF E2E template (D-133). Archive reads built + anonymous (D-273;
> API `tests/SIMF.Api.Tests/ArchiveTests.cs`). **Flutter screen built (D-307)** —
> widget tests in `src/Mobile/simf_app/test/features/archive/archive_screen_test.dart`.
> The detail-endpoint catalogue is [`mobile-archive-detail.md`](mobile-archive-detail.md).
>
> **D-453 frame re-verify (925:3079):** re-diffed against the current frame and
> tightened to exact parity — the stat row now shows **two** tiles (الفعاليات /
> المتحدثون; the الحضور/attendees tile was dropped to match the frame), the
> edition pills are **equal-width** (fill the row, no scroll), عناوين الجلسات are
> **bordered navy cards** (h48/r4, not bare bullets), the الصور والفيديو gallery
> uses **104×104 scrim tiles**, and المتحدثون السابقون is a **72×72 photo-card
> grid** with a bordered "+N / آخرون" overflow card. Data binding unchanged.

| | |
|--|--|
| **Page** | [`Page_024`](../../App/Page_024/README.md) |
| **Route** | `GET /api/v1/app/archive` (+ `/{id}`) · app screen #24 `/archive` |
| **Auth setup** | **None** — `AllowAnonymous`. |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB024-001 | Guest loads past editions (year · title · attendees/sessions/speakers) | happy | P0 | authored ✓ (screen `lists the editions with stats`) |
| E2E-MOB024-002 | Tap an edition → sheet lazily loads the detail (date · location · summary) | happy | P1 | authored (screen sheet; detail read in `mobile-archive-detail.md`) |
| E2E-MOB024-003 | Empty → empty state | edge | P1 | authored ✓ (screen `empty shows the empty state`) |
| E2E-MOB024-004 | Read failure → error state | resilience | P0 | authored ✓ (screen `error shows the error state`) |
| E2E-MOB024-005 | Edition binds the En/Ar wire names | contract | P0 | authored ✓ (model `ArchiveEdition.fromJson`) |

## Scenarios

```gherkin
Scenario: Editions render without a token
  When the app calls GET /api/v1/app/archive
  Then it returns 200 with items[] (year, titleEn/titleAr, stats)
  And the screen lists each edition with its attendees/sessions/speakers

Scenario: Tapping an edition loads its detail
  When the visitor taps an edition
  Then a sheet shows year · title + stats
  And GET /api/v1/app/archive/{id} fills the date label, location and summary

Scenario: Empty → placeholder; failed read → error
  Given no editions (or a failed read)
  Then the screen shows "No past editions" / the error message
```

**Evidence:** `archive_screen_test.dart` (4) + `ArchiveTests` (API).

---

_Last reviewed:_ `2026-06-05` by `SIMF Team`.
