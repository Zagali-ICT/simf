# Page 009 — API (الشروط والأحكام · Terms & conditions)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. The page's
rules are in [Page_009_Logic.md](Page_009_Logic.md).

> **Status:** the content read **exists** and is the only call the page makes. The
> auditable acceptance record is **deferred (D8)** — acceptance is **client-side only**,
> so there is **no accept endpoint** in this version.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split shipped,
> D-247) — so the route below is `GET /api/v1/app/content/terms`.

## E1 — `GET /content/{key}`  (existing — the terms content)
| | |
|---|---|
| Full route | `GET /api/v1/app/content/terms` (the `terms` content-key convention — L-1) |
| Access | **Anonymous allowed** (public content read; Guest and above). No permission code. |
| App privilege | Guest and above |
| Returns | `ApiResult<AppContent>` |

```jsonc
// AppContent  (shape served by the existing GET /app/content/{key})
{
  "key":        "terms",        // the requested content key
  "titleAr":    "string",       // "الشروط والأحكام"
  "titleEn":    "string",       // "Terms & conditions"
  "bodyAr":     "string",       // rendered terms body (HTML/markdown) — Arabic
  "bodyEn":     "string",       // rendered terms body (HTML/markdown) — English
  "updatedUtc": "2026-09-01T00:00:00Z" // last-updated; null when not tracked
}
```

> If the live payload differs, the field used by this page is the **localized body**
> (Arabic primary, English secondary) plus the optional **last-updated** timestamp;
> the page only reads, never writes.

### Errors (E1)
| Code | When | App behaviour |
|------|------|---------------|
| `404` / empty body | no content stored for key `terms` | **Empty** state + retry (L-6) |
| `5xx` / network | server/transport failure | **Error** state + single retry (L-6) |

## E2 — Accept the terms  (TO BUILD — DEFERRED, D8)
| | |
|---|---|
| Intended route | `POST /api/v1/app/content/terms/accept` **(TO BUILD)** |
| Access | Signed-in account (own `sub`) |
| Status | **Deferred (D8).** The auditable acceptance **record** needs a persisted
  row (who / which version / when) on the **frozen Identity schema**, so it is **not
  built**. For now acceptance is **client-side only** (Page_009_Logic L-3) — no call. |
| Intended returns | `ApiResult<TermsAcceptanceReceipt>` (acceptedUtc, termsVersion, userId) — once the freeze is lifted |

This endpoint is **not implemented**. The app **does not** call any accept route in this
version; ticking Accept sets a local flag only. When the Identity-schema freeze is lifted,
adding this write does **not** change the E1 content read above.

## Summary
- **E1 exists** — the page's only live call: the `terms` content read.
- **E2 is TO BUILD / deferred (D8)** — no acceptance write; accept is client-side only.

## Build dependencies
- **Auditable acceptance record (E2)** is blocked on the **Identity-schema freeze lift**
  (D8 / D-110). Until then, consent is recorded **client-side only** and is not auditable
  server-side.
