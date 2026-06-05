# Website marketing landing — `/`

| | |
|--|--|
| **Route** | `/` (static `wwwroot/index.html` + `content.js`) |
| **Surface** | Website (public, anonymous) |
| **Audience** | Anyone (public marketing site) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real (D-294 — dynamic content) |
| **Source** | [`index.html`](../../../src/Website/SIMF.Web/wwwroot/index.html) · [`content.js`](../../../src/Website/SIMF.Web/wwwroot/content.js) · [`SiteContentEndpoints.cs`](../../../src/Website/SIMF.Web/Endpoints/SiteContentEndpoints.cs) |
| **Last reviewed** | 2026-06-05 |

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

The API has **no CORS policy**, so the browser cannot call it directly — the
Website proxy is the same-origin bridge (mirrors the BFF proxy in
`AccountEndpoints`). The access path is anonymous throughout; no token is used.

## 3. Bilingual model

Every text field is emitted twice — `field` (Arabic-preferred display) and
`field_en` (English) — matching `content.js`'s `pickLang` / `getCmsValue`. The
hero CMS blocks store `ContentArabic` (→ base) and `Content` (→ `_en`).

## 7. Edge cases

- **API offline / 503** → `loadSiteContentRemote()` returns `null` → the landing
  keeps its `SITE_DEFAULTS` (no blanking, no error surfaced to the visitor).
- **A section has no rows** (e.g. archive hidden) → that key is omitted from
  `/content/site` → the section keeps its default content.
- **Hero is all-or-nothing** → the proxy emits `hero` only when **every** hero
  key resolved, so the hero never renders half-populated.
- **No public image** for sessions/news → a neutral branded SVG placeholder.
  Partner logos are not publicly servable, so the partner card falls back to the
  partner name text. Archive + speaker cards are image-free by design.

## 11. E2E

| Scenario | ID |
|----------|----|
| Landing loads live sections from `/content/site` | E2E-WLD-001 |
| Offline API falls back to `SITE_DEFAULTS` | E2E-WLD-004 |
| Hero CMS text + bilingual switch | E2E-WLD-007 |

Full catalogue: [`e2e/web-landing.md`](../../tests/e2e/web-landing.md).

_Last reviewed:_ 2026-06-05 by Claude (D-294 — landing dynamic content).
