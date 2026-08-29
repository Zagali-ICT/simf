"""Build the manual's page model straight out of the Control Panel source.

Everything the operations manual says about a page - its route, the file that
implements it, the permission that gates it, where it sits in the navigation and
what its menu entry is called in each language - is read from the code and the
resource files here, never typed by hand. That is the whole point: a manual
transcribed by a person goes stale silently, and the existing one did exactly
that (it still documents a page gate as [Authorize(Roles = "Administrator")]
when the code has used [RequirePermission] for months).

Outputs
  docs/manuals/source/page-model.json   every page, with its facts
  tools/manual/routes.tsv               "slug<TAB>route", in navigation order,
                                        which is what the capture runner walks

Run:  python tools/manual/build_page_model.py
"""

import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
CP = REPO / "src/ControlPanel/SIMF.ControlPanel"
COMPONENTS = CP / "Components"
NAV_FILE = CP / "CpNavigation.cs"
RESX_EN = CP / "Resources/Strings.resx"
RESX_AR = CP / "Resources/Strings.ar.resx"

# Routes carrying a parameter cannot be visited as written. Each is mapped to a
# concrete, seeded instance so the sweep photographs a real page rather than a
# 404. The value is resolved at capture time where it is a database id.
PARAMETERISED = {
    "/admin/ai/services/{Feature}": "/admin/ai/services/Assistant",
    "/m/{Module}": "/m/live-sessions",
    # These two need a real record id, which is specific to whichever database
    # the capture runs against, so they are supplied by ROUTE_IDS below rather
    # than committed. Leaving them unresolved would drop two pages out of the
    # sweep and the manual would be built with two holes in it - which is
    # exactly what the missing-screenshot guard is there to catch.
    "/admin/roles/{RoleId:guid}/permissions": None,
    "/sessions/{SessionId:guid}/moderate": None,
}

# Optional, gitignored: one "route<TAB>concrete-route" per line, written by
# whoever prepares the capture database.
ROUTE_IDS = REPO / ".tmp/manual-env/route-ids.tsv"


def load_route_ids():
    if not ROUTE_IDS.exists():
        return {}
    resolved = {}
    for line in ROUTE_IDS.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = line.split("\t", 1)
        if len(parts) == 2:
            resolved[parts[0].strip()] = parts[1].strip()
    return resolved


def read_resx(path):
    """name -> value for one .resx, ignoring the schema preamble."""
    values = {}
    for data in ET.parse(path).getroot().findall("data"):
        name = data.get("name")
        value = data.find("value")
        if name and value is not None and value.text is not None:
            values[name] = value.text
    return values


def global_access():
    """The access attribute _Imports.razor applies to every component.

    A page with no attribute of its own is NOT ungated - it inherits this. The
    manual previously printed "Page permission: -" for two account pages that in
    fact require a signed-in user, which is the same staleness this tool exists
    to remove, only pointing the other way.
    """
    imports = COMPONENTS / "_Imports.razor"
    if not imports.exists():
        return None
    text = imports.read_text(encoding="utf-8", errors="replace")
    if re.search(r"@attribute\s*\[AllowAnonymous\]", text):
        return "anonymous"
    if re.search(r"@attribute\s*\[Authorize\b", text):
        return "authenticated"
    return None



# The grid callbacks a page wires, in the order a toolbar shows them. The
# callback is what proves the action EXISTS on that page - a label alone would
# also match a page that only mentions it.
# (callback, fallback English name, the parameter naming the button). The label
# parameter is what the toolbar actually shows, so the manual can print the
# page's own word in the reader's own language instead of a method name.
GRID_ACTIONS = [
    ("OnAdd", "Add", "AddLabel"),
    ("OnEditOne", "Edit", "EditLabel"),
    ("OnDetailsOne", "Details", "DetailsLabel"),
    ("OnDuplicateOne", "Duplicate", "DuplicateLabel"),
    ("OnCopyOne", "Copy", "CopyLabel"),
    ("OnCopySelected", "Copy selected", "CopyLabel"),
    ("OnPaste", "Paste", "PasteLabel"),
    ("OnApproveSelected", "Approve selected", "ApproveSelectedLabel"),
    ("OnRejectSelected", "Reject selected", "RejectSelectedLabel"),
    ("OnDeleteOne", "Delete", "DeleteLabel"),
    ("OnDeleteSelected", "Delete selected", "DeleteLabel"),
    ("OnImport", "Import", "ImportLabel"),
    ("OnExport", "Export", "ExportLabel"),
]

# The grid's own permission parameters. A null means the action is ungated,
# which the manual should say rather than leave blank.
GRID_PERMISSIONS = [
    ("AddPermission", "Add, Duplicate and Paste"),
    ("EditPermission", "Edit"),
    ("DeletePermission", "Delete"),
    ("ApprovePermission", "Approve"),
    ("RejectPermission", "Reject"),
    ("ImportPermission", "Import"),
    ("ExportPermission", "Export"),
]


def scan_depth(razor_path, code_behind_path):
    """What one page offers: its actions, their gates, its columns, its calls."""
    text = razor_path.read_text(encoding="utf-8", errors="replace")
    body = re.sub(r"@\*.*?\*@", "", text, flags=re.S)

    actions = []
    for attribute, fallback, label_parameter in GRID_ACTIONS:
        if not re.search(attribute + r'\s*=\s*"', body):
            continue
        found = re.search(label_parameter + r'\s*=\s*"@L\["([^"]+)"\]"', body)
        actions.append({"name": fallback, "labelKey": found.group(1) if found else None})

    gates = []
    for attribute, covers in GRID_PERMISSIONS:
        found = re.search(attribute + r'\s*=\s*"@PermissionCatalog\.([A-Za-z0-9_.]+)"', body)
        if found:
            gates.append((covers, found.group(1)))

    # Buttons the page writes itself are wrapped rather than parameterised.
    for found in re.finditer(
            r'<AuthorizedAction\s+Permission="@PermissionCatalog\.([A-Za-z0-9_.]+)"', body):
        code = found.group(1)
        if all(code != existing for _, existing in gates):
            gates.append(("An action on the page", code))

    columns = [
        {"key": key, "headerKey": header}
        for key, header in re.findall(
            r'<SimfDataGridColumn[^>]*?Key="([^"]+)"[^>]*?Header="@L\["([^"]+)"\]',
            body, re.S)
    ]

    calls = set()
    for source in (body, (code_behind_path.read_text(encoding="utf-8", errors="replace")
                          if code_behind_path and code_behind_path.exists() else "")):
        calls.update(re.findall(r'"(/account/api/[A-Za-z0-9/_{}.-]+)"', source))

    return {
        "actions": actions,
        "actionGates": [{"covers": covers, "permission": code} for covers, code in gates],
        "columns": columns,
        "calls": sorted(calls),
    }


def scan_pages():
    """Every @page route, with the file, layout, permission and title key."""
    pages = {}
    inherited = global_access()
    for razor in sorted(COMPONENTS.rglob("*.razor")):
        text = razor.read_text(encoding="utf-8", errors="replace")
        routes = re.findall(r'^@page\s+"([^"]+)"', text, re.M)
        if not routes:
            continue

        # Ignore an attribute that only appears inside a @* ... *@ header
        # comment: these files carry long comment blocks, and re.search returns
        # the EARLIEST match, so a commented-out old gate would beat the real one.
        body = re.sub(r"@\*.*?\*@", "", text, flags=re.S)

        permission, access = None, inherited
        found = re.search(r"@attribute\s*\[RequirePermission\(([^)]+)\)\]", body)
        if found:
            permission = found.group(1).replace("PermissionCatalog.", "").strip()
            access = "permission"
        elif re.search(r"@attribute\s*\[AllowAnonymous\]", body):
            access = "anonymous"
        elif re.search(r"@attribute\s*\[Authorize\b", body):
            access = "authenticated"

        layout = None
        found = re.search(r"^@layout\s+(\S+)", text, re.M)
        if found:
            layout = found.group(1)

        title_key = None
        found = re.search(r'<PageTitle>@L\["([^"]+)"\]', text)
        if not found:
            found = re.search(r'SimfPageHeader\s+Title="@L\["([^"]+)"\]', text)
        if not found:
            found = re.search(r'SimfBanner\s+Title="@L\["([^"]+)"\]', text)
        if found:
            title_key = found.group(1)

        relative = razor.relative_to(REPO).as_posix()
        code_behind = razor.with_suffix(".razor.cs")
        depth = scan_depth(razor, code_behind)
        for route in routes:
            pages[route] = {
                "route": route,
                "razor": relative,
                "codeBehind": (code_behind.relative_to(REPO).as_posix()
                               if code_behind.exists() else None),
                "layout": layout,
                "permission": permission,
                "access": access,
                "titleKey": title_key,
                **depth,
            }
    return pages


def scan_navigation():
    """The navigation groups and items, in the order they are declared.

    The file uses target-typed `new(...)`, so a group is recognised by its
    Nav.* label key and an item by its Module.* key plus an href.
    """
    text = NAV_FILE.read_text(encoding="utf-8")
    groups, group, unparsed = [], None, []
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped.startswith("new("):
            continue
        found = re.match(r'new\("(Nav\.[^"]+)"', stripped)
        if found:
            group = found.group(1)
            groups.append((group, []))
            continue
        if stripped.startswith('new("Module.') and not re.match(
                r'new\("(Module\.[^"]+)",\s*"([^"]+)"', stripped):
            # A navigation entry wrapped onto two lines, or one whose permission
            # is passed positionally, would otherwise be read as ungated - the
            # manual would then describe a gated page as open to anyone.
            unparsed.append(stripped[:90])
        found = re.match(r'new\("(Module\.[^"]+)",\s*"([^"]+)"(.*)$', stripped)
        if found and groups:
            key, href, rest = found.groups()
            permission = None
            perm = re.search(r"RequiredPermission:\s*PermissionCatalog\.([A-Za-z0-9_.]+)", rest)
            if perm:
                permission = perm.group(1)
            icon = re.search(r'Icon:\s*"([^"]+)"', rest)
            groups[-1][1].append({
                "labelKey": key,
                "route": href,
                "navPermission": permission,
                "icon": icon.group(1) if icon else None,
                "isStub": "IsStub: true" in rest,
            })
    if unparsed:
        raise SystemExit(
            "CpNavigation entries that could not be parsed:\n  "
            + "\n  ".join(unparsed))
    return groups


def slug_for(route):
    cleaned = re.sub(r"\{[^}]*\}", "x", route.strip("/"))
    cleaned = re.sub(r"[^A-Za-z0-9]+", "-", cleaned).strip("-").lower()
    return cleaned or "dashboard"


def main():
    pages = scan_pages()
    navigation = scan_navigation()
    PARAMETERISED.update(load_route_ids())
    en, ar = read_resx(RESX_EN), read_resx(RESX_AR)

    model, seen, tsv = [], set(), []
    tsv.append("# slug<TAB>route - generated by tools/manual/build_page_model.py")
    tsv.append("# Section 1: navigation order, which is the manual's chapter order.")

    for group_key, items in navigation:
        tsv.append("#")
        tsv.append(f"# {group_key} - {en.get(group_key, group_key)} / {ar.get(group_key, '')}")
        for item in items:
            route = item["route"]
            seen.add(route)
            page = pages.get(route, {})
            entry = {
                **item,
                "navGroupKey": group_key,
                "navGroupEn": en.get(group_key),
                "navGroupAr": ar.get(group_key),
                "labelEn": en.get(item["labelKey"]),
                "labelAr": ar.get(item["labelKey"]),
                "inNavigation": True,
                **page,
                "route": route,
                "slug": slug_for(route),
                "titleEn": en.get(page.get("titleKey") or "", None),
                "titleAr": ar.get(page.get("titleKey") or "", None),
                "hasPage": route in pages,
            }
            model.append(entry)
            visit = PARAMETERISED.get(route, route)
            if visit:
                tsv.append(f"{entry['slug']}\t{visit}")

    tsv.append("#")
    tsv.append("# Section 2: routed pages reached from inside another page, not the menu.")
    for route in sorted(pages):
        if route in seen:
            continue
        page = pages[route]
        entry = {
            **page,
            "slug": slug_for(route),
            "inNavigation": False,
            "labelEn": None,
            "labelAr": None,
            "titleEn": en.get(page.get("titleKey") or "", None),
            "titleAr": ar.get(page.get("titleKey") or "", None),
            "hasPage": True,
        }
        model.append(entry)
        visit = PARAMETERISED.get(route, route)
        if visit:
            tsv.append(f"{entry['slug']}\t{visit}")

    out_model = REPO / "docs/manuals/source/page-model.json"
    out_model.parent.mkdir(parents=True, exist_ok=True)
    out_model.write_text(json.dumps(model, indent=2, ensure_ascii=False), encoding="utf-8")

    out_tsv = REPO / "tools/manual/routes.tsv"
    out_tsv.write_text("\n".join(tsv) + "\n", encoding="utf-8")

    nav_count = sum(1 for e in model if e["inNavigation"])
    missing_page = [e["route"] for e in model if not e["hasPage"]]
    no_permission = [e["route"] for e in model if e.get("permission") is None]
    no_label = [e["labelKey"] for e in model
                if e["inNavigation"] and not e.get("labelAr")]

    print(f"resx entries            : {len(en)} EN / {len(ar)} AR")
    print(f"pages with an @page     : {len(pages)}")
    print(f"navigation items        : {nav_count} in {len(navigation)} groups")
    print(f"model entries           : {len(model)}")
    print(f"nav items with no page  : {missing_page or 'none'}")
    print(f"pages with no permission: {len(no_permission)} -> {no_permission}")
    print(f"nav items missing Arabic: {no_label or 'none'}")
    if no_label:
        raise SystemExit(
            "These navigation labels have no Arabic value, so the Arabic volume "
            "would print a resource key as a heading: " + ", ".join(no_label))
    unresolved = [r for r, v in PARAMETERISED.items() if v is None]
    print(f"unresolved parameterised: {unresolved or 'none'}")
    with_actions = sum(1 for e in model if e.get("actions"))
    labelled = sum(1 for e in model for a in (e.get("actions") or [])
                   if a.get("labelKey"))
    with_columns = sum(1 for e in model if e.get("columns"))
    with_gates = sum(1 for e in model if e.get("actionGates"))
    with_calls = sum(1 for e in model if e.get("calls"))
    print(f"pages with actions      : {with_actions}")
    print(f"actions with a label    : {labelled}")
    print(f"pages with grid columns : {with_columns}")
    print(f"pages with action gates : {with_gates}")
    print(f"pages with API calls    : {with_calls}")
    print(f"capture lines           : {sum(1 for l in tsv if not l.startswith('#'))}")
    print(f"written                 : {out_model.relative_to(REPO)}, {out_tsv.relative_to(REPO)}")


if __name__ == "__main__":
    main()
