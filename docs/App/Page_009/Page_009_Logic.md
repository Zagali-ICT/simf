# Page 009 — Logic (الشروط والأحكام · Terms & conditions)

Business rules behind the page. The page is **content + an accept gate**; the
auditable acceptance record is **deferred (D8)** while the Identity schema is frozen.

## L-1 Content source
The terms body is the published content returned by **`GET /app/content/{key}`** with
the well-known key **`terms`** (the content-key convention). The payload carries the
bilingual body (AR/EN) and an optional last-updated timestamp; the app renders the
field for the active locale. There is **no terms-specific endpoint** — terms reuses the
shared content read.

## L-2 The accept gate
- The page has two modes:
  - **Standalone read** — opened from a link (footer / "More"); content only, no gate.
  - **In-flow consent** — opened as a step of a flow that requires acceptance (e.g. a
    sign-up step). The accept checkbox + Accept button are rendered.
- The mode is decided by the **caller** (a navigation argument), not by privilege.

## L-3 Acceptance record — DEFERRED (D8)
Per **D8** the auditable acceptance **record is deferred**: the Identity schema is
**frozen**, so there is **no** `TermsAcceptance` table, column, or write endpoint in this
version. Acceptance is therefore **client-side only**:
- Ticking "أوافق" + tapping Accept sets a **local** consent flag (in-memory / local
  store) and hands control back to the calling flow.
- **No backend call** is made on accept. Nothing is persisted server-side, and no
  version/timestamp/actor is recorded on the server.
- When the Identity-schema freeze is lifted, an auditable accept record + write endpoint
  can be added without changing this page's content read (see [Page_009_API.md](Page_009_API.md)).

## L-4 Validation
- The **Accept** button is disabled until the consent checkbox is ticked (in-flow mode).
- No free-text input on this page — nothing else to validate.
- The content key is fixed to `terms`; the app does not accept an arbitrary key here.

## L-5 State transitions
| State | Enters when | Leaves to |
|-------|-------------|-----------|
| **Loading** | page opened; content fetch in flight | Loaded (success) / Error (failure) |
| **Loaded — read only** | standalone mode, content rendered | back |
| **Loaded — gate idle** | in-flow mode, checkbox unticked → Accept disabled | gate armed (tick) / back (decline) |
| **Gate armed** | checkbox ticked → Accept enabled | Accepted (tap Accept) / gate idle (untick) |
| **Accepted (client)** | Accept tapped → local consent flag set | returns to calling flow |
| **Empty** | content fetch returns no `terms` body | retry → Loading |
| **Error** | content fetch fails | retry → Loading |

## L-6 Edge cases
- Missing `terms` key / empty body → **Empty** state with a retry, never a blank page.
- Fetch failure (network/5xx) → **Error** state with a single retry surface.
- Decline / back in-flow → no consent flag set; the calling flow remains blocked.
- Re-entering after a prior client accept → the gate is shown again (no server memory of consent — D8).

## L-7 Role gating
- Reading is open to **Guest and above** — no sign-in needed to view the terms.
- The accept gate's appearance depends on the **flow mode** (L-2), not on app privilege.
- App authorization is expressed in the four app roles only (Guest/Visitor/Moderator/Staff),
  never the CP `UserType` or the permission catalogue.

## L-8 Localization & direction
Arabic primary (RTL), English secondary. The content payload carries the body paired
(AR/EN); the app picks per active locale and renders RTL for Arabic. Any last-updated
date is formatted in the device locale.

## L-9 Dependencies
- **`GET /app/content/{key}`** (exists) with the `terms` key — the only backend call.
- **Auditable acceptance record + write endpoint** — **not built (D8 deferred)**, pending
  the Identity-schema freeze lift.
