# Pending global-doc merges — `FR-1203-markdown-render` (Track E, item E4)

`ContentBlock`'s XML doc claimed "markdown allowed"; nothing rendered markdown on
any surface.

## DECISIONS_LOG

| D-NEXT | 2026-07-31 | **Content blocks are plain text — the contract was corrected, no renderer was built (`FR-1203-markdown-render`, option (b)).** `src/Backend/SIMF.Domain/Cms/ContentBlock.cs` documented `Content` and `ContentArabic` as "markdown allowed" while no surface rendered markdown: the CP prints them through Razor's auto-encoder, the Website hydrator (`wwwroot/js/site-content.js`) writes them with `textContent` and HTML-escapes every value it concatenates into an `innerHTML` sink through `esc()`, and the Flutter app decodes them into `Text` widgets. **Option (a) — a sanitizing render pipeline — was rejected on three grounds.** (1) Every key in use is a short plain-text field: an eyebrow, an `h2`, a one-line paragraph, a numeric counter (`stats.count1`), a button label — see `LandingSectionContentKeys` and `LandingHeroContentKeys`. None is a long-form document, so markdown buys nothing. (2) It changes how already-seeded production copy looks: a `#` or `*` inside an Arabic paragraph would silently become a heading. (3) It requires injecting HTML built from an **admin-editable** field into a public page that today has no such path at all, which is precisely what the item's own absolute rule forbids — and it could not make the claim true anyway, because no app-side renderer is in scope this round, so the contract would stay false for one of the two consumers. **Fix:** delete "(markdown allowed)" from the two XML docs (see *Outside Track E* below) and pin the behaviour the corrected contract describes with `tests/SIMF.ControlPanel.Tests/ContentBlockPlainTextContractTests.cs`: a `<script>alert('xss')</script>` payload rendered through `ContentBlockViewDelete` produces **no** `script` element, shows `&lt;script&gt;` in the markup and the exact typed text in `textContent`; no `(MarkupString)` cast anywhere in the Control Panel, the Website or `SIMF.Components` wraps anything but a server-generated SVG (the TOTP pairing QR, the badge QR, the icon body); and `site-content.js` keeps its five-replacement `esc()`, its `textContent` single-value path and **exactly four** `innerHTML` sinks, so a fifth cannot be added without being read. `ErrorCodes.ContentMarkdownUnsafe` was pre-allocated for option (a)'s write path and is **unused** under this ruling — drop it or leave it reserved. No schema, no permission, no behaviour change. | An XML doc is a contract: leaving it claiming a feature that does not exist invites the next developer to "finish" it by piping an admin-editable field into `MarkupString`, which is stored XSS reachable from the CMS desk. The honest correction is one line; building the renderer would have been a behaviour change on live content plus a brand-new HTML-injection surface, for fields that are one line of copy each. The tests are the durable half — they make the corrected contract enforced rather than merely written down. |

## PAGE-INDEX

Row 107 — no route or status change; the doc column already points at the page
reference that now carries the plain-text ruling (§7). **No edit required.**
Included here so the merge does not assume an omission:

```
| `/admin/content-blocks` | ✅ Real | Administrator  | [cp/admin-content-blocks.md](cp/admin-content-blocks.md) | [e2e/cp-admin-content-blocks.md](../tests/e2e/cp-admin-content-blocks.md) |
```

## E2E-README

Row 134 — the CNT range extends by one. Replace:

```
| `/admin/content-blocks` | [`cp-admin-content-blocks.md`](cp-admin-content-blocks.md) | E2E-CNT-001..020 |
```

with:

```
| `/admin/content-blocks` | [`cp-admin-content-blocks.md`](cp-admin-content-blocks.md) | E2E-CNT-001..021 |
```

**Roll-up:** adds **1** Coverage-matrix row (`E2E-CNT-021`); no new catalogue
file. See the note in `QA-LIVE-001.md` — Track E contributes **+3** in total.

## Outside Track E — must land in the same changeset

Track E does not own `src/Backend/SIMF.Domain/`, so the one-line contract
correction is listed rather than applied. Both edits are mechanical.

`src/Backend/SIMF.Domain/Cms/ContentBlock.cs`, lines 24 and 29:

```csharp
    /// <summary>English content (markdown allowed). Up to 8000 chars
```
becomes
```csharp
    /// <summary>English content — PLAIN TEXT. No surface renders markdown or
    /// HTML (FR-1203, 2026-07-31): the CP encodes it, the Website writes it with
    /// textContent, the app puts it in a Text widget. Up to 8000 chars
```

```csharp
    /// <summary>Arabic content (markdown allowed). Same shape as
```
becomes
```csharp
    /// <summary>Arabic content — PLAIN TEXT, same rule. Same shape as
```

Two further copies of the stale claim, both outside this track:

- `docs/CP/admin-content-blocks/admin-content-blocks_Logic.md:13` — "each,
  markdown allowed" → "each, plain text".
- `src/Mobile/simf_app/lib/features/content/data/content_models.dart:17-18` —
  the `// English body (HTML/markdown)` comments are wrong in the same way and
  belong to Track D.
