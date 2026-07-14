# Website marketing landing — `/`

| | |
|--|--|
| **Route** | ~~`/`~~ — **retired** at the 2026-07-14 cutover (`index.html` deleted) |
| **Surface** | Website (public, anonymous) |
| **Audience** | Anyone (public marketing site) |
| **Auth** | None — anonymous |
| **Status** | 🗑️ **Retired & deleted** — superseded by the Bootstrap SSR rebuild now serving `/`; see [`landing-rebuild.md`](landing-rebuild.md). The whole static landing was removed at the 2026-07-14 cutover: `index.html`, the leftover `index.legacy.html`, their renderer `content.js`, and `assets/figma/themes/bg-*.jpg`. |
| **Source** | _(deleted at cutover)_ `wwwroot/index.html` · `wwwroot/index.legacy.html` · `wwwroot/content.js`. Still present: [`SiteContentEndpoints.cs`](../../../src/Website/SIMF.Web/Endpoints/SiteContentEndpoints.cs) (now unused) · [`LandingSectionContentKeys.cs`](../../../src/Shared/SIMF.Common/LandingSectionContentKeys.cs) |
| **Last reviewed** | 2026-06-07 |

## 1. Purpose

The public marketing landing for SIMF 2026 — hero, about, sessions, speakers,
partners, news, archive (past editions) and the "Saudi spirit" gallery. It is a
**static, client-rendered** page (not Blazor): `index.html` ships hardcoded
`SITE_DEFAULTS` (in `content.js`) and renders them client-side.

Per **D-294** the page also loads its content **live from the API**. The
designed-in `content.js` hook `loadSiteContentRemote()` (previously stubbed to
`null`) now `fetch()`es **`GET /content/site`** — a same-origin Website proxy
that server-side reads the API's anonymous public endpoints and reshapes them
into the `SITE_DEFAULTS` content model. Sections that come back are merged in and
re-rendered; anything missing (or an offline API) leaves the built-in defaults.

## 2. Data sources (server-side, via `SiteContentEndpoints`)

| Landing section | content key | API public read |
|-----------------|-------------|-----------------|
| Sessions | `sessions` | `GET /api/v1/app/programme/sessions` |
| Speakers | `speakers` | `GET /api/v1/app/speakers` |
| Partners | `partners` | `GET /api/v1/app/sponsors` + `GET /api/v1/app/media-partners` (merged) |
| News | `news` | `GET /api/v1/app/news` |
| Archive (history) | `archive` | `GET /api/v1/app/archive` (empty when the D-166 visibility toggle is off) |
| Saudi-spirit gallery | `spirit` | `GET /api/v1/app/media` (images re-streamed via `GET /content/media/{id}/image`) |
| Hero text | `hero.*` | `POST /api/v1/app/content/batch` (keys `hero.titlestart`…`hero.ctasecondary`, seeded) |
| About / stats strip / Pillars header / Goals (D-336) | `about.*` `stats.*` `pillars.*` `goals.*` | `POST /api/v1/app/content/batch` (32 keys incl. `stats.count1..4`, `goals.item1..5.t/.d`, seeded; editable from CP `/admin/content-blocks`) |

The five Pillar rows and the 12-item insights marquee remain page arrays
(list/animation data, not CMS) — a noted follow-up. The editorial CMS bindings
use `data-cms` **alongside** the existing `data-i18n`, so the binding order is
**API → seeded CMS → built-in dictionary**; an unseeded key keeps the page's own
copy (zero visual regression).

The API has **no CORS policy**, so the browser cannot call it directly — the
Website proxy is the same-origin bridge (mirrors the BFF proxy in
`AccountEndpoints`). The access path is anonymous throughout; no token is used.

## 3. Bilingual model

Every text field is emitted twice — `field` (Arabic-preferred display) and
`field_en` (English) — matching `content.js`'s `pickLang` / `getCmsValue`. The
hero CMS blocks store `ContentArabic` (→ base) and `Content` (→ `_en`).

## 7. Edge cases

- **Loading** → while `/content/site` is in flight the dynamic sections
  (sessions/speakers/partners/news) show a CSS **skeleton shimmer** (gated on
  `window.__contentReady`), not the sample defaults; the loader's `finish()`
  flips the flag and re-renders real rows. Each render step runs in its own
  try/catch so one section failing on edge data can't strand the others (D-337).
- **API offline / 503** → `loadSiteContentRemote()` returns `null` → the landing
  keeps its `SITE_DEFAULTS` (no blanking, no error surfaced to the visitor).
- **A section has no rows** (e.g. archive hidden) → that key is omitted from
  `/content/site` → the section keeps its default content.
- **Hero is all-or-nothing** → the proxy emits `hero` only when **every** hero
  key resolved, so the hero never renders half-populated.
- **No public image** for sessions/news → a neutral branded SVG placeholder.
  Partner logos are not publicly servable, so the partner card falls back to the
  partner name text. Archive cards are image-free by design. **Speaker cards
  render a portrait** (`photo` from the proxy = `Speaker.PhotoRelativePath`)
  when present, else the SVG silhouette (D-346 — demo speakers seed a test
  portrait URL).

## 11. E2E

| Scenario | ID |
|----------|----|
| Landing loads live sections from `/content/site` | E2E-WLD-001 |
| Offline API falls back to `SITE_DEFAULTS` | E2E-WLD-004 |
| Hero CMS text + bilingual switch | E2E-WLD-007 |
| Editorial sections (About/stats/Pillars/Goals) CMS-driven | E2E-WLD-009 |

Full catalogue: [`e2e/web-landing.md`](../../tests/e2e/web-landing.md).

_Last reviewed:_ 2026-06-07 by Claude (D-336 — About/stats/Pillars-header/Goals CMS-driven; was D-294 landing dynamic content).
