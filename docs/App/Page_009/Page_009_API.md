# Page 009 — API (الشروط والأحكام · Terms & conditions)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. The page's
rules are in [Page_009_Logic.md](Page_009_Logic.md).

Last updated: **2026-06-13** (conformance pass to the as-built KSA-Project redesign — D-367/D-375; contract unchanged by the redesign).

> **Status:** the content read **exists** and is the only call the page makes. The
> auditable acceptance record is **deferred (D8)** — acceptance is **client-side only**
> (the موافق tap returns control to the caller, D-367), so there is **no accept
> endpoint** in this version.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split shipped,
> D-247) — so the route below is `GET /api/v1/app/content/terms`.

## E1 — `GET /app/content/{key}`  (existing — the terms content)
| | |
|---|---|
| Full route | `GET /api/v1/app/content/terms` (the `terms` content-key convention — L-1) |
| Access | **AllowAnonymous** (public content read; Guest and above). No permission code. |
| App privilege | Guest and above |
| Returns | `ApiResult<PublicContentBlock>` |

```jsonc
// PublicContentBlock  (SIMF.Contracts.Cms — served by GET /app/content/{key})
{
  "key":           "terms",     // the requested content key
  "content":       "string",    // terms body — English (plain text; the app splits it into lines)
  "contentArabic": "string",    // terms body — Arabic (plain text; the app splits it into lines)
  "lastUpdatedAt": "2026-09-01T00:00:00Z" // last-updated; always present (non-nullable DateTimeOffset)
}
```

> The field used by this page is the **localized body** (Arabic primary, English
> fallback — and vice versa when the requested language is empty). `lastUpdatedAt`
> is decoded by the app model but **not rendered** since D-375 (the KSA frame has no
> last-updated line). The page only reads, never writes.

### Conditional GET (server-side, D-173)
The endpoint supports an **`If-Modified-Since` handshake**: it truncates
`lastUpdatedAt` to the second, emits it as a **`Last-Modified`** response header, and
answers **`304 Not Modified`** (no body) when the request's `If-Modified-Since` is at
or after that instant. The Flutter app does **not** send `If-Modified-Since` — every
load is a full `200` fetch; the 304 path is unused by this page.

### Errors (E1)
| Code | When | App behaviour |
|------|------|---------------|
| `404` (`ContentBlockNotFound` — "Content block not found." / "لم يتم العثور على المحتوى.") | no content stored for key `terms` | **Empty** state + retry (L-6) |
| `200` with both bodies blank | block exists but has no text | **Empty** state + retry (L-6, client-side `hasBody` check) |
| `5xx` / network | server/transport failure | **Error** state + retry (L-6) |

## E2 — Accept the terms  (TO BUILD — DEFERRED, D8)
| | |
|---|---|
| Intended route | `POST /api/v1/app/content/terms/accept` **(TO BUILD)** |
| Access | Signed-in account (own `sub`) |
| Status | **Deferred (D8).** The auditable acceptance **record** needs a persisted
  row (who / which version / when) on the **frozen Identity schema**, so it is **not
  built**. For now acceptance is **client-side only** (Page_009_Logic L-3) — no call. |
| Intended returns | `ApiResult<TermsAcceptanceReceipt>` (acceptedUtc, termsVersion, userId) — once the freeze is lifted |

This endpoint is **not implemented**. The app **does not** call any accept route in
this version; tapping **موافق** only hands a `true` result back to the calling flow
(`pop(true)` in consent mode — D-367). Nothing is persisted, locally or server-side.
When the Identity-schema freeze is lifted, adding this write does **not** change the
E1 content read above.

## Summary
- **E1 exists** — the page's only live call: the `terms` content read (anonymous,
  with server-side `Last-Modified`/`304` support the app does not use).
- **E2 is TO BUILD / deferred (D8)** — no acceptance write; the موافق tap is
  client-side only.

## Build dependencies
- **Auditable acceptance record (E2)** is blocked on the **Identity-schema freeze lift**
  (D8 / D-110). Until then, consent is recorded **client-side only** and is not auditable
  server-side.
