# Page 009 — Logic (الشروط والأحكام · Terms & conditions)

Business rules behind the page. The page is **content + a consent action** (the
single موافق button — D-367); the auditable acceptance record is **deferred (D8)**
while the Identity schema is frozen.

Last updated: **2026-06-13** (conformance pass to the as-built KSA-Project redesign — D-367/D-375).

## L-1 Content source
The terms body is the published content returned by **`GET /app/content/{key}`** with
the well-known key **`terms`** (the content-key convention). The payload carries the
bilingual body (AR/EN) and a last-updated timestamp; the app renders the field for
the active locale (with cross-language fallback) and splits it on newlines — each
non-empty trimmed line is one bullet card. The timestamp is decoded but **not
rendered** (D-375). There is **no terms-specific endpoint** — terms reuses the shared
content read.

## L-2 The consent action
- The page has two modes:
  - **Standalone read** — opened from a plain link (the More tile, the sign-up
    form's terms link); موافق simply leaves the page.
  - **In-flow consent** — opened with the **`?consent=1`** query flag by a flow that
    requires acceptance. The explicit **موافق tap IS the consent** (returns `true`
    via `pop`); the **back chevron declines** (returns `false`). There is no
    checkbox and no separate Decline link (D-367).
- The mode is decided by the **caller** (the `?consent=1` query parameter on the
  `/terms` route), not by privilege. The موافق button itself renders in **both**
  modes (D-375) — only the pop result differs.
- **No caller passes `?consent=1` today** — the consent mode is implemented and
  dormant until a flow needs it.

## L-3 Acceptance record — DEFERRED (D8)
Per **D8** the auditable acceptance **record is deferred**: the Identity schema is
**frozen**, so there is **no** `TermsAcceptance` table, column, or write endpoint in this
version. Acceptance is therefore **client-side only**:
- Tapping **موافق** in consent mode hands control back to the calling flow with a
  `true` result (`pop(true)`); the chevron hands back `false`. The boolean pop
  result is the **only** consent signal — nothing is persisted, not even locally.
- **No backend call** is made on accept. Nothing is recorded server-side — no
  version/timestamp/actor.
- When the Identity-schema freeze is lifted, an auditable accept record + write endpoint
  can be added without changing this page's content read (see [Page_009_API.md](Page_009_API.md)).

## L-4 Validation
- The **موافق** button is **always enabled** — consent is the explicit tap itself,
  not a checkbox state (D-367). Nothing gates it.
- No free-text input on this page — nothing else to validate.
- The content key is fixed to `terms`; the app does not accept an arbitrary key here.

## L-5 State transitions
| State | Enters when | Leaves to |
|-------|-------------|-----------|
| **Loading** | page opened or retry tapped; content fetch in flight | Loaded (success with body) / Empty (404 or blank body) / Error (failure) |
| **Loaded** | content rendered (one layout for both modes; موافق always shown) | موافق or chevron → pop back to the caller (consent mode carries `true`/`false`) |
| **Empty** | fetch returns 404 or a body with no text | retry → Loading |
| **Error** | fetch fails (network/5xx) | retry → Loading |

There are no checkbox "gate idle / gate armed" states and no separate "accepted"
state — موافق pops immediately.

## L-6 Edge cases
- Missing `terms` key (**404**, `ContentBlockNotFound`) → **Empty** state with retry,
  never a blank page. A `200` whose AR **and** EN bodies are blank is also Empty.
- Fetch failure (network/5xx) → **Error** state showing the failure message + retry.
- Chevron in consent mode → `false` is returned; the calling flow remains blocked.
- Nothing to pop (deep-linked `/terms` as the first route) → موافق and the chevron
  both fall back to `go('/')`.
- Re-entering after a prior client accept → the page behaves identically (no
  memory of consent anywhere — D8).

## L-7 Role gating
- Reading is open to **Guest and above** — no sign-in needed to view the terms.
- The consent meaning of موافق depends on the **flow mode** (L-2), not on app privilege.
- App authorization is expressed in the four app roles only (Guest/Visitor/Moderator/Staff),
  never the CP `UserType` or the permission catalogue.

## L-8 Localization & direction
Arabic primary (RTL), English secondary. The content payload carries the body paired
(AR/EN); the app picks per active locale and **falls back to the other language when
the picked one is empty** (both directions). Cards use directional padding so the
gold bullet sits at the inline start under RTL and LTR. Fixed strings come from
`app_l10n.dart` (`termsTitle`, `termsImportantInfoTitle`, `termsAcceptButton`
موافق/Agree, `termsEmpty`, `retryLabel`). No date is rendered on this page (D-375).

## L-9 Dependencies
- **`GET /app/content/{key}`** (exists, anonymous) with the `terms` key — the only
  backend call. The endpoint also supports `If-Modified-Since`/`304` + a
  `Last-Modified` header (D-173), which this app does **not** use — every load is a
  full fetch.
- **Auditable acceptance record + write endpoint** — **not built (D8 deferred)**, pending
  the Identity-schema freeze lift.
