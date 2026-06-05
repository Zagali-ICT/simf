# Page 019 — المتحدثون · Speakers list

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_019_Function.md](Page_019_Function.md) | What the user does — browse the speakers list, read avatar/rank/name, tap **More** through to a profile |
| Logic | [Page_019_Logic.md](Page_019_Logic.md) | The one-call list source, the `displayOrder` → name ordering, tap-through, empty state |
| API | [Page_019_API.md](Page_019_API.md) | The backend endpoint + DTO this page reads (authoritative contract) — **already built (reuse)** |
| Design | [Page_019_Design.md](Page_019_Design.md) | Flutter screen design — header, vertical `sp-card` list, avatar/rank/name/More, RTL, states |

## Identity
| | |
|---|---|
| Mockup page | **19** (`Mockup.html`, line ~1334) |
| Route | `RouteNames.speakers` → `/speakers` (**guest+, anonymous**) |
| Titles | AR **المتحدثون** · EN **Speakers list** |
| Section | 2 — Core screens |
| Nature | **Vertical list of speaker cards** — avatar, rank line, name, **More** link → Speaker profile |
| App privilege | **Guest+ (anonymous).** The list endpoint is `AllowAnonymous`; a guest can browse with no sign-in (D-199). |
| Status | API **BUILT** (reuse, no new API — D-199); **Flutter screen BUILT (D-302)** — speaker cards → profile |

## Sources of truth (read first)
`Mockup.html` screen 19 (the visual) · `SIMF_Screen_Guide_and_User_Journey`
SCREEN19 (the narrative) · SIMF-MOB-API-001 (shared API conventions + auth) ·
`DECISIONS_LOG` **D-199** (Speakers list + profile built as anonymous reads) +
**D-269** (the owner's meeting-request addition — login-only, on the **profile**
page 20, **not** here).

## Headline
> Screen 19 **المتحدثون** is the **anonymous** directory of forum speakers — a
> vertical list of cards, each showing an avatar (⚓/★), a rank line (e.g.
> `القبطان البحري`), the speaker's name and a **المزيد / More** link that taps
> through to the **Speaker profile (20, [Page_020](../Page_020/README.md))**.

The whole list comes from **one** anonymous call
(`GET /app/speakers` → `PublicSpeakers`), ordered by `displayOrder` then name.
There is **no new API** for this page — it reuses the read shipped under D-199.
The login-only "**طلب مقابلة / Request meeting**" affordance (D-269) lives on the
**profile** (20), not on this list. See [Page_019_Logic.md](Page_019_Logic.md) and
[Page_019_API.md](Page_019_API.md). E2E catalogue: [`docs/tests/e2e/mobile-speakers.md`](../../tests/e2e/mobile-speakers.md).
