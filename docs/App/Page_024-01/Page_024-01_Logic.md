# Page 024-01 — Logic (تفاصيل النسخة · Past-edition detail)

Business rules behind the per-edition detail. Verified against the `ArchiveEdition`
entity + `PublicArchiveService` (D-199 / D-273). This page is the per-edition twin
of the Archive list (screen 24): same source table, same visibility gate, one row.

## L-1 One anonymous call draws the screen
The detail renders from a **single** read:
`GET /app/archive/{id}` → `PublicArchiveEditionDetail`:

| Field | Drives |
|-------|--------|
| `year` | the cover overlay + app-bar year (`2024`) |
| `titleEn` / `titleAr` | the **عنوان الملتقى** heading |
| `summaryEn` / `summaryAr` | the **نبذة** paragraph (nullable → hide when empty) |
| `locationEn` / `locationAr` | the **المكان** box (nullable → hide when empty) |
| `dateLabelEn` / `dateLabelAr` | the **الزمن** box (nullable → hide when empty) |
| `attendees` / `sessions` / `speakers` | the three counters |
| `coverImageRelativePath` | the cover banner image (nullable → gradient fallback) |

No second fetch: the list (24) carries `id`, and the detail call returns everything
the screen shows.

## L-2 Visibility gate (D-166) — the same gate as the list
The detail is gated by the **archive-visibility operations toggle**, exactly like
the list:
- toggle **on** + edition **active** → 200 with the payload;
- toggle **off** → the service returns `null` → the endpoint returns **404**
  `archive_edition_not_found` (a hidden archive does not leak one edition by id);
- edition **missing / soft-deleted (`IsActive == false`)** → `null` → **404**.

So "archive hidden" and "edition not found" are a **single** 404 surface to the
client (Page_024-01_API E1). There is **no** auth dimension — the read is public.

## L-3 The rich lists are deferred (entity TODO — §9 / D-273)
The mockup sketches three more sections — **الصور والفيديو** (gallery / video),
**عناوين الجلسات** (session titles), **المتحدثون السابقون** (past speakers). The
`ArchiveEdition` entity does **not** model these yet (only the scalar
title/summary/place/date/counters/cover). They are an explicit deferred item
recorded on the entity and in D-273; the screen renders them as **"coming soon"**
placeholders. When they are modelled (a future additive migration), the detail DTO
gains the lists and this page binds them — no breaking change (append-only, D-219).

## L-4 List → detail hop
The Archive list (24) renders one card per edition with `اعرف المزيد ←`; the card
carries the edition `id`, so the hop to 24-01 passes that id and the detail screen
reads `GET /app/archive/{id}`. Back returns to the list. There is no edit / action
on this screen — it is read-only (admin authoring lives in the CP `/admin/archive`).

## L-5 Edge cases
- **Archive hidden (toggle off)** → 404 → the screen is not reachable (the list is
  empty, so there is nothing to tap); a deep-link to the id 404s → "not found".
- **Unknown / soft-deleted edition** → 404 → "not found" state.
- **Null optional scalars** (summary / location / date label / cover) → hide the
  box / use the gradient fallback; never render an empty labelled box.
- **Network error / 5xx** → retry state, the detail is not cached across launches.

## L-6 Localization
Arabic primary (RTL), English secondary — the DTO carries both `*En` and `*Ar`
scalars and the app picks by active locale. The **year** renders `dir="ltr"`. The
counters' numbers render `dir="ltr"` inside the Arabic labels (الفعاليات / الحضور /
المتحدثون). When an `*Ar` value is null the app may fall back to the `*En` value (or
hide the box), never showing a blank.
