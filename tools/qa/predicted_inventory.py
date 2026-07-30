#!/usr/bin/env python3
"""Predict the control inventory of a Control Panel page from its source.

WS1 of the QA programme. The element sweep (tools/qa/element-sweep.js) can only
say "here is what I found". On its own that makes it a presence scan: it cannot
tell a page that is missing its Delete button from a page that never had one, and
it cannot tell a button correctly greyed out because nothing is selected from a
button that is dead. Both are the defects the sweep exists to catch.

This script supplies the other half — what the page is SUPPOSED to expose —
derived from the one place that cannot drift from the rendered DOM: the
callbacks the page wires onto <SimfDataGrid>. SimfDataGrid renders each toolbar
button inside `@if (OnX.HasDelegate)`, so a wired callback is a rendered button,
one for one. It applies the gating the same way, so the expected enabled/disabled
state at zero selection is derivable too.

The runner then asserts an expected-vs-actual DIFF rather than "nothing looked
obviously broken".

Usage:
    python tools/qa/predicted_inventory.py                 # every CP grid page
    python tools/qa/predicted_inventory.py --route /admin/halls
    python tools/qa/predicted_inventory.py --json out.json

Coverage: this understands the uniform SimfDataGrid pages (63 of ~93 CP pages).
The bespoke pages (AiDashboard, RolePermissionsEditor, ProgrammeTimeline,
GateOperatorConsole, SessionModerationDesk, the seat / venue editors) are listed
in the output under `bespoke` with `predicted: null` — they need a hand-authored
expectation, and are reported rather than silently omitted so the gap is visible.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

CP_PAGES = Path("src/ControlPanel/SIMF.ControlPanel/Components/Pages")

# Each SimfDataGrid callback -> the control it renders and how it is gated.
# Mirrors SimfDataGrid.razor: `@if (OnX.HasDelegate)` wraps the button, and the
# `Disabled=` expression on it is one of the three rules below.
#   always      - rendered enabled regardless of selection
#   one-row     - Disabled="@(SelectedCount != 1)"
#   any-rows    - Disabled="@(SelectedOnPageCount == 0)"
TOOLBAR = {
    "OnAdd":             ("Add",       "always"),
    "OnEditOne":         ("Edit",      "one-row"),
    "OnDeleteSelected":  ("Delete",    "any-rows"),
    "OnApproveSelected": ("Approve",   "any-rows"),
    "OnRejectSelected":  ("Reject",    "any-rows"),
    "OnCopySelected":    ("Copy",      "any-rows"),
    "OnPaste":           ("Paste",     "always"),
    "OnDuplicateOne":    ("Duplicate", "one-row"),
    "OnImport":          ("Import",    "always"),
    "OnExport":          ("Export",    "always"),
}

# Row-level actions render one quiet icon button per row (and the same set in the
# right-click context menu).
ROW_ACTIONS = {
    "OnDetailsOne":   "Details",
    "OnEditOne":      "Edit",
    "OnCopyOne":      "Copy",
    "OnDuplicateOne": "Duplicate",
    "OnDeleteOne":    "Delete",
}

GRID_OPEN = re.compile(r"<SimfDataGrid\b", re.IGNORECASE)
CALLBACK = re.compile(r'\b(On[A-Za-z]+)\s*=\s*"')
PAGE_ROUTE = re.compile(r'^@page\s+"([^"]+)"', re.MULTILINE)
COLUMN = re.compile(r"<SimfDataGridColumn\b(.*?)/?>", re.DOTALL | re.IGNORECASE)
ATTR_TRUE = re.compile(r'\b{}\s*=\s*"?@?true"?', re.IGNORECASE)


def grid_block(text: str) -> str | None:
    """The <SimfDataGrid ...> opening tag plus its body, or None.

    Attributes are routinely spread over many lines, and a page may hold more
    than one grid; taking from the first `<SimfDataGrid` to the matching close
    keeps nested columns in scope without needing a real parser.
    """
    match = GRID_OPEN.search(text)
    if not match:
        return None
    end = text.lower().rfind("</simfdatagrid>")
    return text[match.start(): end if end > match.start() else len(text)]


def predict(path: Path) -> dict | None:
    text = path.read_text(encoding="utf-8")
    route_match = PAGE_ROUTE.search(text)
    if not route_match:
        return None

    route = route_match.group(1)
    body = grid_block(text)

    # A page-level file with no grid is bespoke: report it, do not guess.
    if body is None:
        return {
            "route": route,
            "file": path.as_posix(),
            "kind": "bespoke",
            "predicted": None,
            "note": "no <SimfDataGrid> — needs a hand-authored expected inventory",
        }

    wired = set(CALLBACK.findall(body))

    toolbar = [
        {"action": name, "gating": gate, "enabled_at_zero_selection": gate == "always"}
        for callback, (name, gate) in TOOLBAR.items()
        if callback in wired
    ]
    row_actions = sorted({label for cb, label in ROW_ACTIONS.items() if cb in wired})

    columns = COLUMN.findall(body)
    sortable = sum(1 for c in columns if ATTR_TRUE.pattern and re.search(r'\bSortable\s*=\s*"?@?true"?', c, re.I))
    filterable = sum(1 for c in columns if re.search(r'\bFilterable\s*=\s*"?@?true"?', c, re.I))

    return {
        "route": route,
        "file": path.as_posix(),
        "kind": "grid",
        "predicted": {
            "toolbar_buttons": sorted(toolbar, key=lambda b: b["action"]),
            "toolbar_button_count": len(toolbar),
            # At zero selection these MUST be present and disabled. That is the
            # sweep's phase A; phase B selects one row and they must flip.
            "disabled_at_zero_selection": sorted(
                b["action"] for b in toolbar if not b["enabled_at_zero_selection"]),
            "row_actions": row_actions,
            "columns": len(columns),
            "sortable_columns": sortable,
            "filterable_columns": filterable,
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--route", help="only this @page route")
    parser.add_argument("--json", help="write the full report to this file")
    parser.add_argument("--root", default=".", help="repo root (default: cwd)")
    args = parser.parse_args()

    root = Path(args.root)
    pages = root / CP_PAGES
    if not pages.is_dir():
        print(f"error: {pages} not found — run from the repo root", file=sys.stderr)
        return 2

    results = []
    for razor in sorted(pages.rglob("*.razor")):
        entry = predict(razor)
        if entry and (not args.route or entry["route"] == args.route):
            results.append(entry)

    grids = [r for r in results if r["kind"] == "grid"]
    bespoke = [r for r in results if r["kind"] == "bespoke"]

    print(f"routed pages     : {len(results)}")
    print(f"  grid pages     : {len(grids)}  (mechanical prediction)")
    print(f"  bespoke pages  : {len(bespoke)}  (need a hand-authored expectation)")
    if grids:
        print(f"  toolbar buttons: {sum(g['predicted']['toolbar_button_count'] for g in grids)}")
        print(f"  gated buttons  : {sum(len(g['predicted']['disabled_at_zero_selection']) for g in grids)}")

    if args.route:
        print(json.dumps(results, indent=2, ensure_ascii=False))
    if args.json:
        Path(args.json).write_text(
            json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"\nwrote {args.json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
