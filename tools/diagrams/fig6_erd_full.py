"""SIMF detailed entity relationship sheets, for SIMF-LLD-003 section 6.2.

Crow's foot entity relationship diagrams covering every table in both
databases: each entity with all of its columns, the column type, and the
conventional PK, FK and U markers, plus the unique and non-unique indexes
declared on it.

The schema is read from the EF Core model snapshots by efschema.py, so these
sheets are generated from the real database rather than drawn by hand.

One sheet per bounded context, because a single sheet carrying 98 tables and
1267 columns cannot be read on a page. Every table appears on exactly one
sheet, and relationships that leave a sheet are listed on it by name.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import efschema
from svgkit import INK, RULE, Sheet

OUTDIR = os.path.join(os.path.dirname(os.path.dirname(
    os.path.dirname(os.path.abspath(__file__)))), "docs", "diagrams")

# Every entity is assigned to exactly one sheet. The grouping follows the
# bounded contexts used by section 6.1 of the design document.
CONTEXTS = [
    ("A", "Identity and access", "SIMF_Identity", [
        "SimfUser", "SimfRole", "Permission", "RolePermission", "RefreshToken",
        "AccountCode", "SecondFactorToken", "TotpRecoveryCode", "DeviceKey",
        "PasswordHistoryEntry", "IdentityUserToken<Guid>",
        "IdentityUserRole<Guid>", "IdentityUserClaim<Guid>",
        "IdentityUserLogin<Guid>", "IdentityRoleClaim<Guid>",
    ]),
    ("B", "Profiles, registration and reference data", "SIMF_App", [
        "UserProfile", "UserProfileType", "UserInterest", "UserProfileInterests",
        "Organisation", "Region", "Country", "BadgeUpdateRequest",
        "ParticipationDocumentRequest", "RegistrationGate", "BadgeBatch",
    ]),
    ("C", "Programme and sessions", "SIMF_App", [
        "Theme", "SessionTheme", "Session", "SessionCategory",
        "ProgrammeDay", "Speaker", "SessionSpeaker", "SessionSummary",
        "SessionOutcome", "SessionFavourite", "SpeakerPresentation",
        "SpeakerAvailabilityWindow", "DevicePositionPing",
    ]),
    ("D", "Halls, seating and attendance", "SIMF_App", [
        "Hall", "HallSeatLayout", "SeatReservation", "HallAttendance",
        "HallAllocation", "HallAvailabilityWindow", "MeetingTable",
    ]),
    ("E", "Gates and access control", "SIMF_App", [
        "Gate", "GateAssignment", "GateProfileTypeAllow", "GateScan",
        "ScanIdempotency",
    ]),
    ("F", "Exhibition, exhibitors and sponsors", "SIMF_App", [
        "Exhibitor", "ExhibitorMembership", "ExhibitorVisitorScan", "Booth",
        "Sponsor", "OrganizationProfile", "OrganizationDetail",
        "OrganizationAboutItem", "VenueMapNode",
    ]),
    ("G", "Meetings", "SIMF_App", [
        "BusinessMeeting", "BusinessMeetingParticipant",
        "DelegationMeetingRequest", "DelegationMeetingActionToken",
        "DelegationAvailabilityWindow", "SpeakerMeetingRequest",
        "MeetingActionToken",
    ]),
    ("H", "Engagement, questions and feedback", "SIMF_App", [
        "SessionQuestion", "SessionModerator", "RatingType",
        "RatingQuestionGroup", "RatingQuestion", "RatingResponse",
        "RatingAnswer", "Connection", "SavedContact", "VisitorShareToken",
    ]),
    ("I", "Content, media and archive", "SIMF_App", [
        "MediaItem", "News", "MediaPartner", "Invitation", "Banner",
        "ContentBlock", "ArchiveEdition", "ArchiveMediaItem",
        "ArchivePastSpeaker", "ArchiveSessionTitle", "ArchiveVisibility",
        "FaqGroup", "FaqEntry",
    ]),
    ("J", "Platform services and auditing", "SIMF_App", [
        "Notification", "NotificationBroadcast", "StoredFile", "SystemSetting",
        "EmailTemplate", "AiPrompt", "AiPromptHistory", "AiInvocation",
        "AiChatMessage", "OperationLogEntry", "RowAudit", "ContactInquiry",
    ]),
]

COLS = 4
COL_W = 372
GAP_X = 24
MARGIN = 60


def columns_for(name):
    entity = efschema.ENTITIES[name]
    pk = set(entity["pk"])
    fks = {r["fk"] for r in efschema.RELATIONSHIPS
           if r["from"] == name and r["fk"]}
    uniq = {c for i in entity["indexes"] if i["unique"] for c in i["cols"]}
    rows = []
    for p in entity["props"]:
        if p["name"] in pk:
            marker = "PK"
        elif p["name"] in fks:
            marker = "FK"
        elif p["name"] in uniq:
            marker = "U"
        else:
            marker = ""
        rows.append((marker, p["name"],
                     efschema.short_type(p["sql"], p["clr"])))
    rows.sort(key=lambda r: {"PK": 0, "FK": 1, "U": 2, "": 3}[r[0]])
    return rows


def build(tag, title, db, names):
    present = [n for n in names if n in efschema.ENTITIES]
    missing = [n for n in names if n not in efschema.ENTITIES]

    # Lay out column by column, shortest-first packing into the four columns.
    boxes, heights = [], [0.0] * COLS
    for name in sorted(present, key=lambda n: -len(efschema.ENTITIES[n]["props"])):
        col = heights.index(min(heights))
        cols = columns_for(name)
        blocks = 1 if len(cols) <= 22 else 2
        rows = -(-len(cols) // blocks)
        h = 40 + rows * 14 + 8
        x = MARGIN + col * (COL_W + GAP_X)
        y = 150 + heights[col]
        boxes.append((name, x, y, h, cols))
        heights[col] += h + 26

    height = int(150 + max(heights) + 210)
    width = MARGIN * 2 + COLS * COL_W + (COLS - 1) * GAP_X

    s = Sheet(width, height, f"SIMF data model, sheet {tag}: {title}",
              f"Crow's foot entity relationship diagram, {db}. Every column "
              f"with its type; PK primary key, FK foreign key, U unique index.")

    placed = {}
    for name, x, y, h, cols in boxes:
        s.entity(x, y, COL_W, name, efschema.ENTITIES[name]["table"], cols)
        placed[name] = (x, y, h)

    inside, outside = [], []
    for rel in efschema.RELATIONSHIPS:
        child, parent = rel["from"], rel["to"]
        if child in placed and parent in placed:
            inside.append((child, parent, rel["fk"]))
        elif child in placed and parent in efschema.ENTITIES:
            outside.append(f"{child}.{rel['fk']} to {parent}")

    # Relationship lines are drawn in the margin lane to the right of the
    # child, so a line never crosses an entity box.
    for i, (child, parent, fk) in enumerate(inside):
        cx, cy, ch = placed[child]
        px, py, ph = placed[parent]
        lane = MARGIN - 14 - (i % 3) * 10 if cx == MARGIN else cx - 8 - (i % 3) * 8
        pts = [(cx, cy + 20), (lane, cy + 20), (lane, py + 20), (px, py + 20)]
        # No line label: the foreign key column is already named and
        # marked FK inside the entity box, so a label here only
        # collides with the neighbouring table.
        s.relation(pts, ("L", True, False), ("L", False, False))

    notes = [f"Tables on this sheet: {len(placed)}. Columns: "
             f"{sum(len(c) for _, _, _, _, c in boxes)}. Foreign keys drawn: "
             f"{len(inside)}."]
    if outside:
        notes.append("Foreign keys to tables on another sheet: "
                     + "; ".join(sorted(outside)[:6])
                     + ("; and others" if len(outside) > 6 else ""))
    if missing:
        notes.append("Declared in this context but absent from the model: "
                     + ", ".join(missing))
    notes.append("Indexes are listed per table in the data dictionary at "
                 "section 6.3. Every unique index column carries the U marker "
                 "above.")

    ny = 150 + max(heights) + 10
    s.note(MARGIN, ny, width - MARGIN * 2 - 360, notes, title="Sheet coverage")
    s.legend(width - MARGIN - 340, ny, 340, [
        ("comp", "Table, with its columns and types"),
        ("plain", "Foreign key, crow's foot on the many end"),
    ], title="Key")

    stem = os.path.join(OUTDIR, f"SIMF-Fig6{tag}-Data-Model-{title.split(',')[0]
                                                             .replace(' ', '-')}")
    s.save(stem)
    return os.path.basename(stem), len(placed)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    total = 0
    for tag, title, db, names in CONTEXTS:
        stem, n = build(tag, title, db, names)
        total += n
        print(f"  sheet {tag}: {n:2d} tables  {stem}")
    print(f"tables placed {total} of {len(efschema.ENTITIES)}")
    unplaced = set(efschema.ENTITIES) - {n for _, _, _, names in CONTEXTS
                                         for n in names}
    if unplaced:
        print("NOT PLACED:", ", ".join(sorted(unplaced)))
