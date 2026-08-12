"""SIMF high level use case diagram, for SIMF-LLD-003 section 3.1.1.

A UML use case diagram: all primary actors, the major use cases at the system
boundary, and the association between them. Use case identifiers are the
UC numbers already carried by the low level use case tables in section 3.1.2,
so the two sections address the same set.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from svgkit import Sheet

W, H = 1660, 1330
OUT = os.path.join(os.path.dirname(os.path.dirname(
    os.path.dirname(os.path.abspath(__file__)))), "docs", "diagrams",
    "SIMF-Fig8-Use-Case")

s = Sheet(W, H, "SIMF high level use cases",
          "UML use case diagram. Primary actors and the major use cases at the "
          "system boundary. Identifiers match the low level use case tables.")

BX, BY, BW, BH = 380, 130, 900, 990
s.boundary(BX, BY, BW, BH, "SIMF platform")

# Actors, keyed by name so a use case never refers to a bare coordinate.
LEFT = {
    "public": (200, "Anonymous public", "website reader"),
    "visitor": (430, "Visitor / attendee", "mobile application"),
    "exhibitor": (680, "Exhibitor", "company account"),
    "speaker": (930, "Speaker", "invited participant"),
}
RIGHT = {
    "admin": (190, "Administrator", "Control Panel"),
    "committee": (400, "Scientific committee", "programme and Q and A"),
    "pr": (620, "Public relations", "exhibitors and booths"),
    "gate": (830, "Gate operator", "mobile staff screens"),
    "moderator": (1030, "Session moderator", "moderator desk"),
}
for y, name, role in LEFT.values():
    s.actor(150, y, name, role)
for y, name, role in RIGHT.values():
    s.actor(1500, y, name, role)

COL_A, COL_B, COL_C = 545, 830, 1115
RX, RY = 122, 40
ROWS = [220 + i * 92 for i in range(10)]

# (column, row index, identifier, name, left actors, right actors)
CASES = [
    (COL_A, 0, "UC-01", "Register and verify", ["public", "visitor"], []),
    (COL_A, 1, "UC-04", "Sign in, second factor",
     ["visitor", "exhibitor", "speaker"], ["admin"]),
    (COL_A, 2, "UC-06", "Reset password", ["visitor"], []),
    (COL_A, 3, "UC-08", "Browse programme", ["public", "visitor"], []),
    (COL_A, 4, "UC-09", "Reserve a seat", ["visitor"], []),
    (COL_A, 5, "UC-13", "Watch a live session", ["visitor"], []),
    (COL_A, 6, "UC-14", "Ask a question", ["visitor"], []),
    (COL_A, 7, "UC-15", "Comment and rate", ["visitor"], []),
    (COL_A, 8, "UC-17", "Use the assistant", ["public", "visitor"], []),
    (COL_A, 9, "UC-19", "Share a contact", ["visitor", "exhibitor"], []),

    (COL_B, 0, "UC-20", "Approve registration", [], ["admin"]),
    (COL_B, 1, "UC-22", "Manage the programme", [], ["admin", "committee"]),
    (COL_B, 2, "UC-24", "Manage speakers", [], ["admin"]),
    (COL_B, 3, "UC-26", "Moderate comments", [], ["committee"]),
    (COL_B, 4, "UC-28", "Manage the exhibition", [], ["pr"]),
    (COL_B, 5, "UC-30", "Assign booths", ["exhibitor"], ["pr"]),
    (COL_B, 6, "UC-32", "Publish news and media", [], ["admin"]),
    (COL_B, 7, "UC-34", "Register at the desk", [], ["admin"]),
    (COL_B, 8, "UC-36", "Moderate questions", [], ["committee", "moderator"]),
    (COL_B, 9, "UC-38", "Run the live broadcast", [], ["committee"]),

    (COL_C, 0, "UC-40", "Scan a badge at a gate", [], ["gate"]),
    (COL_C, 1, "UC-41", "Confirm hall arrival", ["visitor"], ["gate"]),
    (COL_C, 2, "UC-43", "Approve session minutes", [], ["committee"]),
    (COL_C, 3, "UC-45", "Request a meeting", ["visitor", "exhibitor"], ["pr"]),
    (COL_C, 4, "UC-47", "Confirm a meeting", ["speaker"], []),
    (COL_C, 5, "UC-49", "Send a notification", [], ["admin"]),
    (COL_C, 6, "UC-51", "View statistics", [], ["admin"]),
    (COL_C, 7, "UC-53", "Configure the system", [], ["admin"]),
    (COL_C, 8, "UC-55", "Review the audit trail", [], ["admin"]),
    (COL_C, 9, "UC-57", "Manage reference data", [], ["admin"]),
]

for cx, ri, ident, name, lefts, rights in CASES:
    s.usecase(cx, ROWS[ri], RX, RY, name, ident)

for cx, ri, ident, name, lefts, rights in CASES:
    cy = ROWS[ri]
    for key in lefts:
        s.assoc([(178, LEFT[key][0] + 20), (cx - RX, cy)])
    for key in rights:
        s.assoc([(1472, RIGHT[key][0] + 20), (cx + RX, cy)])

s.note(BX + 24, BY + BH + 30, BW - 48, [
    "Identifiers are the UC numbers used by the low level use case tables in "
    "section 3.1.2, where each case carries its",
    "preconditions, main flow, alternative flows, exception flows and "
    "postconditions. Cases reached only by redirect,",
    "and administrative variants of the cases above, are held in those tables "
    "rather than drawn here.",
], title="Scope of this diagram")

s.legend(60, 1150, 300, [
    ("box", "System boundary"),
    ("process", "Use case"),
    ("plain", "Association"),
], title="Key")

s.save(OUT)
