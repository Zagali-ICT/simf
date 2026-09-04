"""Sheet 10: the SIMF core entity relationship diagram.

The Solution Design Document had a section titled "ER Diagram" that carried no
diagram, only a table of relationships introduced as coming "from the mermaid
ERD" -- an artefact the reader of the document does not have. This sheet is that
diagram, so the section becomes self-contained.

Notation is crow's foot, which is what an entity relationship diagram is read in:

    one and only one        a single bar across the line
    zero or one             a circle and a bar
    one or many             a bar and a splayed foot
    zero or many            a circle and a splayed foot

The sheet draws the two databases as separate frames because that separation is
a design rule rather than a deployment detail: SIMF_Identity and SIMF_App are
physically separate, so a relationship that crosses the frame CANNOT be a
foreign key. Those crossings are drawn dashed and labelled as logical, which is
the single thing a reader most needs to take from this diagram.

Layout rule, and the reason this sheet is legible: the nine entities that
reference a user are placed in ONE column immediately right of the identity
frame, so every cross-database reference is a short horizontal hop off a single
trunk instead of a long diagonal across the sheet. Everything downstream is then
placed so that its parent sits directly to its left on the same baseline, which
turns most relationships into one straight segment carrying its own label in
clear space. A label is never centred over a box.

Only core entities are drawn. Lookup, configuration and audit tables are
omitted, exactly as the document's own text says, because a diagram nobody can
read proves nothing.

Regenerate with:  python tools/diagrams/fig10_data_model.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from svgkit import INK, RULE, FAINT, ACCENT, PAPER, Sheet, _grey  # noqa: E402

W, H = 2080, 1180

IDENT_FILL = "#E4EAF2"      # an entity living in SIMF_Identity
APP_FILL = "#EFEFEA"        # an entity living in SIMF_App
JOIN_FILL = "#FFFFFF"       # a join table, hollow so it reads as a connector

BOX_W, BOX_H = 186, 40
GAP = 5                     # air between a line end and the box it points at

# Column x positions. Column B holds every entity that references a user, so
# that all nine cross-database references are short hops off one trunk.
COL_B, COL_C, COL_D, COL_E, COL_F = 566, 838, 1110, 1382, 1654
TRUNK = 438                 # the cross-database trunk, in the gap before B

# name -> (x, y, kind). kind: "e" ordinary entity, "j" join table.
E = {
    # --- SIMF_Identity -----------------------------------------------------
    "User":                 (78, 196, "e"),
    "UserRole":             (78, 288, "j"),
    "Role":                 (78, 374, "e"),
    "RolePermission":       (78, 460, "j"),
    "Permission":           (78, 546, "e"),

    # --- column B: everything that references a user ----------------------
    "RegistrationRequest":  (COL_B, 170, "e"),
    "AttendeeProfile":      (COL_B, 248, "e"),
    "ExhibitorProfile":     (COL_B, 326, "e"),
    "Badge":                (COL_B, 412, "e"),
    "Booking":              (COL_B, 506, "e"),
    "HallAttendance":       (COL_B, 584, "e"),
    "MeetingRequest":       (COL_B, 662, "e"),
    "UserInterest":         (COL_B, 740, "j"),
    "Notification":         (COL_B, 818, "e"),

    # --- column C: their immediate children -------------------------------
    "Attachment":           (COL_C, 170, "e"),
    "Booth":                (COL_C, 326, "e"),
    "VenueEntry":           (COL_C, 412, "e"),
    "Category":             (COL_C, 740, "e"),
    "NotificationDelivery": (COL_C, 818, "j"),

    # --- column D: programme and reference roots --------------------------
    "Theme":                (COL_D, 170, "e"),
    "Hall":                 (COL_D, 326, "e"),
    "Seat":                 (COL_D, 412, "e"),
    "Session":              (COL_D, 506, "e"),
    "Edition":              (COL_D, 662, "e"),
    "FaqGroup":             (COL_D, 818, "e"),

    # --- column E: their children -----------------------------------------
    "SubTopic":             (COL_E, 170, "e"),
    "SessionSpeaker":       (COL_E, 248, "j"),
    "SessionQuestion":      (COL_E, 506, "e"),
    "SessionSummary":       (COL_E, 584, "e"),
    "EditionStat":          (COL_E, 662, "e"),
    "EditionSpeaker":       (COL_E, 740, "e"),
    "FaqEntry":             (COL_E, 818, "e"),

    # --- column F ----------------------------------------------------------
    "Speaker":              (COL_F, 248, "e"),
}

# Identity facts the document states, shown on the entity that owns them.
KEYS = {
    "User": "Email unique",
    "Badge": "ReferenceNumber unique",
}

# The nine cross-database references, in column B order. Each is a bare Guid
# resolved on read, never a constraint. Labels are short verbs on purpose: the
# nuance belongs in the notes, not strung along a connector.
CROSS = [
    ("RegistrationRequest", "submits",  "one",  "crow"),
    ("AttendeeProfile",     "profile",  "zone", "zone"),
    ("ExhibitorProfile",    "profile",  "one",  "zone"),
    ("Badge",               "holds",    "one",  "zone"),
    ("Booking",             "makes",    "one",  "crow"),
    ("HallAttendance",      "records",  "one",  "crow"),
    ("MeetingRequest",      "requests", "one",  "crow"),
    ("UserInterest",        "picks",    "one",  "crow"),
    ("Notification",        "receives", "one",  "crow"),
]

# Parent, child, label: a single horizontal hop, parent's right face to child's
# left face on the same baseline.
HOPS = [
    ("RegistrationRequest", "Attachment",           "includes"),
    ("ExhibitorProfile",    "Booth",                "runs"),
    ("Badge",               "VenueEntry",           "records scans"),
    ("UserInterest",        "Category",             "typed"),
    ("Notification",        "NotificationDelivery", "sent on"),
    ("Theme",               "SubTopic",             "contains"),
    ("Session",             "SessionQuestion",      "receives"),
    ("Edition",             "EditionStat",          "reports"),
    ("FaqGroup",            "FaqEntry",             "contains"),
]


def width_of(name):
    return BOX_W


def right(name):
    return E[name][0] + BOX_W


def bottom(name):
    x, y, _ = E[name]
    return y + BOX_H + (15 if name in KEYS else 0)


def cx(name):
    return E[name][0] + BOX_W / 2


def cy(name):
    return E[name][1] + BOX_H / 2


class Erd(Sheet):
    """Sheet plus the two things an ERD needs that no other SIMF sheet does."""

    def erd_defs(self):
        ink = _grey(INK)
        self.parts.append(f'''<defs>
  <marker id="crow" viewBox="0 0 14 14" refX="13" refY="7" markerWidth="14"
          markerHeight="14" orient="auto">
    <path d="M 13 7 L 1 1 M 13 7 L 1 7 M 13 7 L 1 13" stroke="{ink}"
          stroke-width="1.5" fill="none"/>
  </marker>
  <marker id="one" viewBox="0 0 14 14" refX="13" refY="7" markerWidth="14"
          markerHeight="14" orient="auto">
    <path d="M 6 1 L 6 13" stroke="{ink}" stroke-width="1.7" fill="none"/>
  </marker>
  <marker id="zone" viewBox="0 0 20 14" refX="19" refY="7" markerWidth="20"
          markerHeight="14" orient="auto">
    <circle cx="5" cy="7" r="3.6" fill="{PAPER}" stroke="{ink}"
            stroke-width="1.5"/>
    <path d="M 14 1 L 14 13" stroke="{ink}" stroke-width="1.7" fill="none"/>
  </marker>
</defs>''')

    def entity(self, name):
        x, y, kind = E[name]
        fill = IDENT_FILL if x < 372 else APP_FILL
        note = KEYS.get(name)
        h = BOX_H + (15 if note else 0)
        dash = ' stroke-dasharray="5 3"' if kind == "j" else ""
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{BOX_W}" height="{h}" rx="3" '
            f'fill="{_grey(JOIN_FILL if kind == "j" else fill)}" '
            f'stroke="{_grey(INK)}" stroke-width="1.4"{dash}/>')
        self.text(x + BOX_W / 2, y + 25, name, 13.5, INK, weight=600,
                  anchor="middle")
        if note:
            self.parts.append(
                f'<line x1="{x + 8}" y1="{y + 34}" x2="{x + BOX_W - 8}" '
                f'y2="{y + 34}" stroke="{_grey(FAINT)}" stroke-width="1"/>')
            self.text(x + BOX_W / 2, y + 48, note, 10.5, RULE, anchor="middle")

    def rel(self, points, tail, head, label="", logical=False, label_at=None,
            label_dy=-7):
        """One relationship. tail and head are the cardinality glyphs.

        Pass tail=None for a branch off a shared trunk, where the trunk itself
        already carries the parent's cardinality and nine repeated bars stacked
        at one point would read as a smudge.
        """
        d = " ".join(("M" if i == 0 else "L") + f" {px} {py}"
                     for i, (px, py) in enumerate(points))
        dash = ' stroke-dasharray="6 4"' if logical else ""
        colour = _grey(ACCENT if logical else INK)
        start = f' marker-start="url(#{tail})"' if tail else ""
        self.parts.append(
            f'<path d="{d}" fill="none" stroke="{colour}" stroke-width="1.4"'
            f'{dash}{start} marker-end="url(#{head})"/>')
        if not label:
            return
        if label_at is None:
            mid = len(points) // 2
            a, b = points[mid - 1], points[mid]
            label_at = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        self._plate(label_at[0], label_at[1] + label_dy, label, 10.5, INK,
                    None, "middle", PAPER)


def build():
    sheet = Erd(
        W, H,
        "SIMF core entity relationship diagram",
        "Crow's foot notation. The two database frames are physically separate, "
        "so a relationship that crosses a frame is a logical reference held in "
        "application code, never a foreign key.")
    sheet.erd_defs()

    sheet.group(40, 130, 300, 500, "SIMF_Identity", "identity and access")
    sheet.group(372, 130, 1668, 780, "SIMF_App", "everything else")

    for name in E:
        sheet.entity(name)

    # ------------------------------------------------ identity, within frame
    for parent, child, label in (
            ("User", "UserRole", "user has roles"),
            ("Role", "UserRole", "role membership"),
            ("Role", "RolePermission", "role grants permissions"),
            ("Permission", "RolePermission", "permission in roles")):
        downward = cy(parent) < cy(child)
        y1 = bottom(parent) if downward else E[parent][1]
        y2 = E[child][1] - GAP if downward else bottom(child) + GAP
        sheet.rel([(cx(parent), y1), (cx(child), y2)], "one", "crow", label,
                  label_at=(cx(parent), (y1 + y2) / 2), label_dy=4)

    # --------------------------------------- the cross-database logical trunk
    uy = cy("User")
    lowest = cy(CROSS[-1][0])
    sheet.rel([(right("User"), uy), (TRUNK, uy)], None, "one", "",
              logical=True)
    sheet.parts.append(
        f'<path d="M {TRUNK} {uy} V {lowest}" fill="none" '
        f'stroke="{_grey(ACCENT)}" stroke-width="1.4" stroke-dasharray="6 4"/>')
    for target, label, _tail, head in CROSS:
        ty = cy(target)
        sheet.rel([(TRUNK, ty), (E[target][0] - GAP, ty)], None, head, label,
                  logical=True, label_at=((TRUNK + E[target][0]) / 2, ty),
                  label_dy=-6)

    # ------------------------------------------------ single horizontal hops
    for parent, child, label in HOPS:
        y1, y2 = cy(parent), cy(child)
        mid = (right(parent) + E[child][0]) / 2
        pts = ([(right(parent), y1), (E[child][0] - GAP, y1)] if y1 == y2 else
               [(right(parent), y1), (mid, y1), (mid, y2),
                (E[child][0] - GAP, y2)])
        sheet.rel(pts, "one", "crow", label, label_at=(mid, (y1 + y2) / 2),
                  label_dy=-6)

    # --------------------------------------------------- routed relationships
    # Speaker sits right of its join table, so this hop runs leftward.
    sheet.rel([(E["Speaker"][0], cy("Speaker")),
               (right("SessionSpeaker") + GAP, cy("SessionSpeaker"))],
              "one", "crow", "appears in",
              label_at=((E["Speaker"][0] + right("SessionSpeaker")) / 2,
                        cy("Speaker")), label_dy=-6)

    # Theme groups sessions: down its own channel left of column D.
    ch = COL_D - 24
    sheet.rel([(cx("Theme"), bottom("Theme")), (cx("Theme"), 232), (ch, 232),
               (ch, cy("Session")), (E["Session"][0] - GAP, cy("Session"))],
              "one", "crow", "groups", label_at=(ch, 226), label_dy=-4)

    # Hall contains seats, and hosts sessions.
    sheet.rel([(cx("Hall"), bottom("Hall")), (cx("Hall"), E["Seat"][1] - GAP)],
              "one", "crow", "contains",
              label_at=(cx("Hall") + 72, (bottom("Hall") + E["Seat"][1]) / 2),
              label_dy=4)
    hh = COL_D + BOX_W + 26
    sheet.rel([(right("Hall"), cy("Hall")), (hh, cy("Hall")),
               (hh, cy("Session")), (right("Session") + GAP, cy("Session"))],
              "one", "crow", "hosts", label_at=(hh, cy("Hall") - 6),
              label_dy=-6)

    # Session features speakers, and is summarised.
    # A separate lane from the one "groups" uses, so the two do not overlap.
    fl = COL_D - 56
    sheet.rel([(cx("Session"), E["Session"][1]), (cx("Session"), 472),
               (fl, 472), (fl, cy("SessionSpeaker") + 14),
               (E["SessionSpeaker"][0] - GAP, cy("SessionSpeaker") + 14)],
              "one", "crow", "features", label_at=(fl + 40, 466), label_dy=-4)
    sheet.rel([(right("Session"), cy("Session") + 12),
               (COL_E - 34, cy("Session") + 12),
               (COL_E - 34, cy("SessionSummary")),
               (E["SessionSummary"][0] - GAP, cy("SessionSummary"))],
              "one", "zone", "summarised",
              label_at=(COL_E - 34, cy("SessionSummary") - 26), label_dy=0)

    # Booking is reached from the session and the seat, both leftward.
    sheet.rel([(E["Session"][0], cy("Session")),
               (right("Booking") + GAP, cy("Booking"))],
              "one", "crow", "booked",
              label_at=((E["Session"][0] + right("Booking")) / 2,
                        cy("Booking")), label_dy=-6)
    sk = COL_C + BOX_W + 30
    sheet.rel([(E["Seat"][0], cy("Seat")), (sk, cy("Seat")),
               (sk, cy("Booking") + 14),
               (right("Booking") + GAP, cy("Booking") + 14)],
              "one", "crow", "reserved", label_at=(sk, cy("Seat") - 6),
              label_dy=-6)

    # Hall holds booths, leftward and down.
    bh = COL_C + BOX_W + 30
    sheet.rel([(E["Hall"][0], cy("Hall") + 12), (bh, cy("Hall") + 12),
               (bh, cy("Booth")), (right("Booth") + GAP, cy("Booth"))],
              "one", "crow", "holds", label_at=(bh, cy("Booth") - 6),
              label_dy=-6)

    # Edition lists its speakers.
    eh = COL_E - 34
    sheet.rel([(right("Edition"), cy("Edition") + 12), (eh, cy("Edition") + 12),
               (eh, cy("EditionSpeaker")),
               (E["EditionSpeaker"][0] - GAP, cy("EditionSpeaker"))],
              "one", "crow", "lists",
              label_at=(eh, cy("EditionSpeaker") - 26), label_dy=0)

    # ---------------------------------------------------------------- legend
    ly = 950
    sheet.legend(78, ly, 452, [
        ("box", "Entity"),
        ("band", "Join table, a composite link"),
        ("group", "Database frame, a physical boundary"),
        ("dash", "Logical reference across the two databases"),
    ], "Key")

    sheet.note(560, ly, 452, [
        "a bar  =  one and only one",
        "a circle and a bar  =  zero or one",
        "a bar and a foot  =  one or many",
        "a circle and a foot  =  zero or many",
    ], "Cardinality")

    sheet.note(1042, ly, 620, [
        "A relationship that crosses the two database frames is a logical",
        "reference: a bare Guid resolved on read by a second query against",
        "the other context. It is never a database constraint, no query joins",
        "across the two databases, and no transaction spans them.",
        "",
        "A user and an attendee profile are each optional to the other: a badge",
        "holder registered at the desk has a profile row and no user row.",
        "",
        "Every entity carries a GUID identifier. Lookup, configuration and",
        "audit tables are omitted here; section 6.3.2 lists the complete set.",
    ], "Rules this diagram carries")

    out = os.path.join(
        os.path.dirname(os.path.dirname(os.path.dirname(
            os.path.abspath(__file__)))),
        "docs", "diagrams", "SIMF-Fig10-Data-Model")
    sheet.save(out)


if __name__ == "__main__":
    build()
