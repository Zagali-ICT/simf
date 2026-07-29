#!/usr/bin/env python3
"""Add the two element-sweep scenarios to every per-page E2E catalogue file.

WS1.3 of the QA programme. The element sweep proves a page's *controls* work —
that every button, link, field and image the page wires is actually present,
accessibly named, correctly gated, and not pointing at a 404. That is a real
contract, and until now it lived only in a scratchpad script and a one-off
report: nothing in the catalogue said a page was supposed to satisfy it, so
nothing tracked whether it still did.

This writes two rows per page, in the catalogue's own format, so the sweep
becomes ordinary tracked coverage that shows up in the testbook alongside
everything else:

    E2E-{NS}-ELS-001  the inventory contract (present / named / gated, LTR + RTL)
    E2E-{NS}-ELS-002  the health contract    (no dead control, no broken image,
                                              every same-origin link/asset < 400)

Two rows rather than one because they fail for different reasons and get fixed
by different people: -001 is a markup/wiring defect, -002 is usually a data or
routing defect (the seeded-asset-without-bytes class, D-687 / BUG-001).

The ids are two-segment on purpose (`E2E-HAL-ELS-001`, not `E2E-HAL-031`) so a
generated row can never collide with a hand-authored one, and so a whole page's
sweep coverage can be selected with one glob. Both the testbook projector's
`ID_RE` and `E2eCatalogueIntegrityTests` accept a hyphenated namespace.

Idempotent: a file that already has its ELS rows is left alone, so this can be
re-run after new pages are catalogued.

Usage:
    python tools/qa/generate_els_rows.py --dry-run
    python tools/qa/generate_els_rows.py
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

CATALOGUE = pathlib.Path("docs/tests/e2e")
SKIP_FILES = {"README.md", "_TEMPLATE.md", "E2E-TEST-PLAN.md"}

# Retired pages describe a route that no longer exists — giving them a live
# element contract would be asserting coverage of a 404.
RETIRED = {"cp-admin-companies.md"}

MATRIX_ROW = re.compile(r"^\|\s*(E2E-([A-Z0-9][A-Z0-9-]*)-\d{3,4})\s*\|")
HEADING = re.compile(r"(?m)^#{2,4}\s+E2E-([A-Z0-9][A-Z0-9-]*)-\d{3,4}")

ROW_001 = (
    "| E2E-{ns}-ELS-001 | Element inventory — every control the page wires is present, "
    "accessibly named, and correctly gated (no selection: selection-gated buttons "
    "present **and disabled**; one row selected: they enable). Asserted in **LTR and "
    "RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | "
    "P1 | _to author_ |"
)
ROW_002 = (
    "| E2E-{ns}-ELS-002 | Element health — no dead control, no broken image, and every "
    "same-origin link and asset returns < 400. Console reports zero errors and "
    "`scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | "
    "_to author_ |"
)

SECTION = """
## Element sweep (WS1)

Generated contract — see `tools/qa/element-sweep.js` and
`docs/tests/element-sweeps/`.

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
{rows}
"""


def namespace_of(text: str) -> tuple[str | None, str]:
    """The page's scenario namespace, and how it was found."""
    rows = collections.Counter(
        m.group(2) for m in (MATRIX_ROW.match(l) for l in text.splitlines()) if m)
    if len(rows) == 1:
        return next(iter(rows)), "matrix"
    if len(rows) > 1:
        return None, f"ambiguous: {dict(rows)}"
    headings = collections.Counter(HEADING.findall(text))
    if len(headings) == 1:
        return next(iter(headings)), "headings"
    if len(headings) > 1:
        return None, f"ambiguous headings: {dict(headings)}"
    return None, "no scenario ids at all"


def last_matrix_row_index(lines: list[str]) -> int | None:
    last = None
    for i, line in enumerate(lines):
        if MATRIX_ROW.match(line):
            last = i
    return last


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--root", default=".")
    args = parser.parse_args()

    directory = pathlib.Path(args.root) / CATALOGUE
    if not directory.is_dir():
        print(f"error: {directory} not found — run from the repo root", file=sys.stderr)
        return 2

    added, already, skipped = 0, 0, []
    for path in sorted(directory.glob("*.md")):
        if path.name in SKIP_FILES:
            continue
        if path.name in RETIRED:
            skipped.append((path.name, "retired page"))
            continue

        text = path.read_text(encoding="utf-8")
        if "-ELS-001" in text:
            already += 1
            continue

        ns, how = namespace_of(text)
        if ns is None:
            skipped.append((path.name, how))
            continue

        lines = text.splitlines(keepends=True)
        rows = ROW_001.format(ns=ns) + "\n" + ROW_002.format(ns=ns) + "\n"

        if how == "matrix":
            at = last_matrix_row_index(lines)
            lines.insert(at + 1, rows)
            new = "".join(lines)
        else:
            # No matrix to extend — give the page its own small section rather
            # than inventing a table shape the rest of the file does not use.
            new = text.rstrip("\n") + "\n" + SECTION.format(
                rows=ROW_001.format(ns=ns) + "\n" + ROW_002.format(ns=ns)) + "\n"

        if not args.dry_run:
            path.write_text(new, encoding="utf-8")
        added += 1

    print(f"files given ELS rows : {added}")
    print(f"already had them     : {already}")
    print(f"skipped              : {len(skipped)}")
    for name, why in skipped:
        print(f"    {name}: {why}")
    if args.dry_run:
        print("\n(dry run — nothing written)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
