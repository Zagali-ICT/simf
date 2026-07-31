# PAR-P1a — media-partners active tab label renders 1 line vs Figma's 2

Item ref: `PAR-P1a` (Track D-a, fix-all run 2026-07-30).
Files touched: `docs/tests/e2e/mobile-media-partners.md` ·
`docs/pages/mobile/media-partners/README.md`.
**No code changed** — see below.

## DECISIONS_LOG

### D-NEXT — PAR-P1a closed as not-a-defect: Figma 1049:12629 shows a ONE-line tab label

The item asked to raise the media-coverage tab label to `maxLines: 2` "with a
slightly smaller line-height", on the reading that the Figma pill carries two
lines. It also allowed re-confirming against the frame first. **The frame was
re-read on 2026-07-30 and it shows a single line, so nothing was changed.**

Measured from `1049:12629` ("Media coverage"):

| Node | Name | Size | Position |
|---|---|---|---|
| `1049:12639` | Frame 427322018 (the strip) | 343 × 48 | x 16, y 126 |
| `1049:12640` / `1049:12642` | Button (each pill) | 163.5 × 48 | — |
| `1049:12641` | `احدث المستجدات` | **92 × 15** | y 16.5 |
| `1049:12643` | `الشركاء الإعلاميون` | **94 × 15** | y 16.5 |

A 15px-high text node is one line at this type size (two would be ~30), and
`16.5 + 15 + 16.5 = 48` — the label is vertically centred in the pill with equal
space above and below, which is only possible for a single line. Each label is
also ~70px narrower than its 163.5-wide button, so it neither wraps nor
ellipsises at the frame's own type size.

`lib/app/widgets/media_coverage_tabs.dart`'s existing `maxLines: 1` +
`TextOverflow.ellipsis` is therefore **frame-accurate already**, and raising it
to 2 would have introduced the deviation the item was trying to remove.

The register's own caveat called this: the strip was rebuilt against the newer
`1049` frame — two tabs, the معرض الصور tab dropped (see the class doc at
`media_coverage_tabs.dart:11-16`) — **after** `FIGMA-PARITY-DEFECTS.md` was
written, so the two-line expectation came from the superseded frame. Recorded as
`E2E-MOB031-009` so the next parity sweep does not re-raise it.

## PAGE-INDEX

No row change. Nothing about the route, access, status or docs of
`#31 mediaPartners` changed — this is a recorded closure.

## E2E-README

Replace the `#31 mediaPartners` registry row with:

| #31 `mediaPartners` (`GET /app/media-partners`) | [`mobile-media-partners.md`](mobile-media-partners.md) | E2E-MOB031-001..009 |

(The range widens from `001..003` to `001..009`: the file already carried
scenarios through `-008`, and this run adds `-009` for PAR-P1a.)
