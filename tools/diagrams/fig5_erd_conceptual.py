"""SIMF conceptual data model, for SIMF-LLD-003 section 6.1.

A conceptual entity relationship diagram: the major business entities and the
named relationships between them, with cardinality shown in crow's foot
notation. No columns and no data types, which is what separates this sheet
from the detailed entity relationship sheets at section 6.2.

Entity names and every relationship drawn here are read from the EF Core model
snapshots by efschema.py, so the sheet cannot drift from the database.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import efschema
from svgkit import INK, NODE_FILL, PAPER, RULE, Sheet, _grey

W, H = 1660, 1180
OUT = os.path.join(os.path.dirname(os.path.dirname(
    os.path.dirname(os.path.abspath(__file__)))), "docs", "diagrams",
    "SIMF-Fig5-Conceptual-Data-Model")

s = Sheet(W, H, "SIMF conceptual data model",
          "Conceptual entity relationship diagram, crow's foot notation. Major "
          "business entities and their named relationships. Entity names and "
          "relationships are read from the EF Core model.")

EW, EH = 196, 50

# Entity placement. Grouped by bounded context so the bands carry the meaning
# and the relationship lines stay short.
PLACE = {
    # Identity and access
    "SimfUser": (80, 190), "SimfRole": (80, 280), "Permission": (80, 370),
    "RolePermission": (80, 460),
    # Profiles and reference data
    "UserProfile": (80, 620), "UserProfileType": (80, 710),
    "Organisation": (80, 800), "Region": (80, 890), "Country": (80, 980),
    # Programme
    "Theme": (500, 190), "SessionTheme": (500, 280), "Session": (500, 370),
    "SessionCategory": (500, 460), "Hall": (500, 550),
    "ProgrammeDay": (500, 640), "Speaker": (500, 730),
    "SessionSpeaker": (500, 820), "SessionSummary": (500, 910),
    # Attendance, bookings and access control
    "SeatReservation": (900, 190), "HallAttendance": (900, 280),
    "HallSeatLayout": (900, 370), "Gate": (900, 460), "GateScan": (900, 550),
    "BadgeBatch": (900, 640), "SessionQuestion": (900, 730),
    "SessionFavourite": (900, 820), "SpeakerPresentation": (900, 910),
    # Exhibition, engagement and content
    "Exhibitor": (1320, 190), "Booth": (1320, 280), "Sponsor": (1320, 370),
    "MediaPartner": (1320, 460), "ArchiveEdition": (1320, 550),
    "RatingType": (1320, 640), "RatingResponse": (1320, 730),
    "Notification": (1320, 820), "VenueMapNode": (1320, 910),
}

BANDS = [
    (60, 150, 236, 470, "Identity and access", ""),
    (60, 580, 236, 470, "Profiles and reference data", ""),
    (480, 150, 236, 830, "Programme", ""),
    (880, 150, 236, 830, "Attendance and access", ""),
    (1300, 150, 236, 830, "Exhibition and content", ""),
]
for x, y, w, h, label, note in BANDS:
    s.band(x, y, w, h, label, note)

for name, (x, y) in PLACE.items():
    s.parts.append(
        f'<rect x="{x}" y="{y}" width="{EW}" height="{EH}" rx="3" '
        f'fill="{_grey(NODE_FILL)}" stroke="{_grey(INK)}" stroke-width="1.4"/>')
    s.text(x + EW / 2, y + 24, name, 12.5, INK, bold=True, anchor="middle")
    s.text(x + EW / 2, y + 39, efschema.ENTITIES[name]["table"], 9.5, RULE,
           anchor="middle")

# Relationships actually declared in the model, restricted to the entities on
# this sheet. Drawn parent to child, so the crow's foot sits on the child.
LABELS = {
    ("RolePermission", "SimfRole"): "granted to",
    ("RolePermission", "Permission"): "grants",
    ("UserProfile", "UserProfileType"): "typed by",
    ("UserProfile", "Organisation"): "belongs to",
    ("UserProfile", "Region"): "located in",
    ("UserProfile", "BadgeBatch"): "printed in",
    ("SessionTheme", "Theme"): "groups",
    ("SessionTheme", "Session"): "covers",
    ("Session", "SessionCategory"): "categorised as",
    ("Session", "Hall"): "held in",
    ("SessionSpeaker", "Session"): "features",
    ("SessionSpeaker", "Speaker"): "appears in",
    ("SessionSummary", "Session"): "summarises",
    ("SeatReservation", "Session"): "reserves for",
    ("HallAttendance", "Session"): "records attendance",
    ("HallAttendance", "Hall"): "recorded in",
    ("HallSeatLayout", "Hall"): "lays out",
    ("GateScan", "Gate"): "scanned at",
    ("GateScan", "UserProfile"): "scans",
    ("SessionQuestion", "Session"): "asked in",
    ("SessionFavourite", "Session"): "marks",
    ("SpeakerPresentation", "Session"): "presented in",
    ("SpeakerPresentation", "Speaker"): "given by",
    ("Booth", "Exhibitor"): "run by",
    ("Booth", "Hall"): "stands in",
    ("Exhibitor", "Country"): "registered in",
    ("Speaker", "Country"): "from",
    ("Sponsor", "Country"): "from",
    ("MediaPartner", "Country"): "from",
    ("RatingResponse", "RatingType"): "answers",
    ("VenueMapNode", "Booth"): "locates",
    ("VenueMapNode", "Hall"): "locates",
}

COLUMNS = [80, 500, 900, 1320]
GUTTERS = {(0, 1): 300, (1, 2): 730, (2, 3): 1130}   # left edge of each gutter


def column_of(x):
    return COLUMNS.index(x)


# Vertical links inside a column hug the right edge; each column keeps its own
# lane counter so two links never sit on the same x.
lane = {0: 0, 1: 0, 2: 0, 3: 0}
gutter_lane = {k: 0 for k in GUTTERS}

drawn, deferred = 0, []
pairs = [(r["from"], r["to"]) for r in efschema.RELATIONSHIPS]
for child, parent in pairs:
    if child not in PLACE or parent not in PLACE:
        continue
    label = LABELS.get((child, parent))
    if label is None:
        continue
    cx, cy = PLACE[child]
    px, py = PLACE[parent]
    ci, pi = column_of(cx), column_of(px)
    if abs(ci - pi) > 1:
        deferred.append(f"{child} to {parent}")
        continue
    if ci == pi:
        # Bracket the link outside the column so it never crosses a box. The
        # last column brackets to its left, the others to their right.
        step = (lane[ci] % 3) * 10
        if ci == len(COLUMNS) - 1:
            x = cx - 8 - step
            side = "L"
            edge_c, edge_p = cx, px
        else:
            x = cx + EW + 8 + step
            side = "R"
            edge_c, edge_p = cx + EW, px + EW
        lane[ci] += 1
        pts = [(edge_c, cy + EH / 2), (x, cy + EH / 2),
               (x, py + EH / 2), (edge_p, py + EH / 2)]
        ends = ((side, True, False), (side, False, False))
    else:
        key = (min(ci, pi), max(ci, pi))
        corridor = GUTTERS[key] + 40 + (gutter_lane[key] % 6) * 18
        gutter_lane[key] += 1
        if ci < pi:
            pts = [(cx + EW, cy + EH / 2), (corridor, cy + EH / 2),
                   (corridor, py + EH / 2), (px, py + EH / 2)]
            ends = (("R", True, False), ("L", False, False))
        else:
            pts = [(cx, cy + EH / 2), (corridor, cy + EH / 2),
                   (corridor, py + EH / 2), (px + EW, py + EH / 2)]
            ends = (("L", True, False), ("R", False, False))
    s.relation(pts, ends[0], ends[1], label)
    drawn += 1

s.note(60, 1064, 900, [
    f"Entities shown: {len(PLACE)} of {len(efschema.ENTITIES)}. Relationships "
    f"drawn: {drawn} of {len(efschema.RELATIONSHIPS)}.",
    "Links to the shared lookup tables, and every relationship between "
    "non-adjacent contexts, are drawn on the detailed",
    "sheets at section 6.2 rather than here, where they would cross the sheet. "
    "The full column detail is the data dictionary at 6.3.",
    "No reference crosses the two databases: a link from business data to a "
    "user is a plain identifier, resolved on read.",
], title="Coverage")

s.legend(1000, 1064, 536, [
    ("comp", "Business entity, with its table name"),
    ("band", "Bounded context"),
    ("plain", "Relationship, crow's foot on the many end"),
], title="Key")

s.save(OUT)
