"""Figure. SIMF phase one, end-to-end sequence.

UML sequence diagram: nine lifelines, ordered numbered messages, each labelled
with its protocol and port. It is the sheet the technical review's point 7 asked
for and the one notation the other sheets cannot supply: sheet 7 is a component
diagram and sheet 8 a data flow diagram, and neither carries a lifeline or a
time ordering, so neither can show WHEN a thing happens or in WHAT ORDER.

A SEPARATE SHEET, not a redraw. It adds the time axis to the estate sheets 6, 7
and 8 already draw, and it changes nothing about them.

Two eliding conventions, both stated on the sheet so a reader cannot mistake
them for a claim about the architecture:

  * every attendee message crosses the WAF and load balancer and then
    SIMF.MobileEdge. That chain is drawn in full once, at steps 1 to 3, and
    elided afterwards. Eliding a repeated hop is ordinary sequence-diagram
    practice; hiding one would not be, which is why the elision is named on
    each arrow that uses it.
  * the API load balancer is folded into the SIMF.Api lifeline rather than
    given one of its own. It forwards and does not participate, so a lifeline
    for it would add a column and no ordering.

Participants, their zones and every protocol and port come from the
Communication Requirements Matrix in SIMF-HLD-004 section 2.8. The workflows are
section 2.3's own three core journeys. The endpoint paths are read from the
solution source tree, not invented: see the FastEndpoints route declarations
under src/Backend/SIMF.Api/Endpoints.

Regenerate with:  python tools/diagrams/fig9_phase_one_sequence.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from svgkit import (Sheet, ACCENT, EXTERNAL_FILL, FAINT, INK,  # noqa: E402
                    NODE_FILL, PAPER, RULE, STORE_FILL, _grey, esc)

OUT = r"d:\SIMF\System\V1.0.0\docs\diagrams\SIMF-Fig9-Phase-One-Sequence"

W, H = 1820, 1560
HEAD_Y, HEAD_H = 112, 62          # lifeline header boxes
FIRST_ROW = 228                   # first message row
STEP = 44                         # one message per row
# A journey band needs air above it, or the previous message's protocol line
# runs into the band rule.
GROUP_LEAD = 20

# Lifelines, left to right. `fill` follows the sheet-wide rule of one value per
# element type: actors and external clients white, server components blue-grey,
# data stores the printer-distinct warm grey.
LANES = [
    ("Attendee",          "mobile app, Flutter",              EXTERNAL_FILL),
    ("WAF + Load Balancer", "SSA perimeter",                  NODE_FILL),
    ("SIMF.MobileEdge",   "SSA presentation",                 NODE_FILL),
    ("SIMF.Api",          "HSA, four nodes behind the API load balancer", NODE_FILL),
    ("SQL Server",        "SIMF_Identity + SIMF_App, HSA",    STORE_FILL),
    ("MinIO",             "object storage, HSA",              STORE_FILL),
    ("Mail server",       "on-site SMTP relay, HSA",          NODE_FILL),
    ("Administrator",     "Control Panel, SSA",               EXTERNAL_FILL),
    ("Gate operator",     "mobile app staff screens",         EXTERNAL_FILL),
]

A, WAF, EDGE, API, SQL, MINIO, MAIL, ADMIN, GATE = range(9)

VIA = "HTTPS 443, via steps 1 to 3"

# (kind, source, target, label, protocol)
#   kind "g" is a journey band, "m" a call, "r" a reply.
ROWS = [
    ("g", None, None, "Registration and approval", ""),
    ("m", A, WAF, "POST /app/auth/sign-up, e-mail and password", "HTTPS 443"),
    ("m", WAF, EDGE, "forward to the presentation tier", "HTTPS 443, internal SSA"),
    ("m", EDGE, API, "forward through the API load balancer",
     "HTTPS 443, SSA to HSA, across the internal firewall"),
    ("m", API, SQL, "create the account, state Registered, PBKDF2 password hash",
     "TCP 1433"),
    ("m", API, MAIL, "queue the six-digit e-mail verification code, stored hashed",
     "SMTP with STARTTLS, 587"),
    ("m", A, API, "POST /app/auth/verify-email, the six-digit code", VIA),
    ("m", A, API, "POST /app/account/user-profile and /id-image, "
     "profile, identity image and face photo", VIA),
    ("m", API, MINIO, "store both, AES-GCM encrypted at rest", "S3 over HTTPS 443"),
    ("m", API, SQL, "profile saved, state Pending approval", "TCP 1433"),
    ("m", ADMIN, API, "POST /admin/visitors/{id}/approve",
     "HTTPS 443, SSA to HSA"),
    ("m", API, SQL, "state Approved, QR badge issued, OperationLog entry",
     "TCP 1433"),
    ("m", API, MAIL, "queue the approval notice", "SMTP with STARTTLS, 587"),
    ("r", API, A, "notification: approved, digital badge available",
     "HTTPS 443, back along steps 3 to 1"),

    ("g", None, None, "Sign-in with the second factor", ""),
    ("m", A, API, "POST /app/auth/sign-in, e-mail and password", VIA),
    ("m", API, SQL, "verify the stored hash, write the hashed one-time code",
     "TCP 1433"),
    ("m", API, MAIL, "queue the one-time code, single use and time limited",
     "SMTP with STARTTLS, 587"),
    ("m", A, API, "POST /app/auth/verify-otp, the one-time code", VIA),
    ("r", API, A, "access token, 5-minute cap, and a rotating refresh token",
     "HTTPS 443, back along steps 3 to 1"),

    ("g", None, None, "Gate scan on the forum days", ""),
    ("m", GATE, API, "POST /app/gates/{gateId}/scans, the attendee badge QR",
     VIA),
    ("m", API, SQL, "validate the badge, the approval state and the gate's "
     "allowed profile types; record the scan idempotently", "TCP 1433"),
    ("r", API, GATE, "entry or exit outcome", "HTTPS 443, back along steps 3 to 1"),
]

s = Sheet(W, H, "SIMF phase one, end-to-end sequence",
          "UML sequence diagram. Ordered messages between the participants of "
          "section 2.3, each labelled with its protocol and port.")

# ------------------------------------------------------------------ lanes
span = (W - 80) / len(LANES)
CX = [40 + span / 2 + i * span for i in range(len(LANES))]
BOX_W = span - 18

# Row centres, resolved before anything is drawn so the lifelines know how far
# down to run.
ROW_Y, _y = [], FIRST_ROW
for _kind, *_ in ROWS:
    if _kind == "g":
        _y += GROUP_LEAD
    ROW_Y.append(_y)
    _y += STEP
foot_y = _y - 14

for i, (name, tech, fill) in enumerate(LANES):
    x = CX[i] - BOX_W / 2
    s.parts.append(
        f'<rect x="{x:.1f}" y="{HEAD_Y}" width="{BOX_W:.1f}" height="{HEAD_H}" '
        f'rx="3" fill="{_grey(fill)}" stroke="{_grey(INK)}" stroke-width="1.4"/>')
    s.text(CX[i], HEAD_Y + 25, name, 13.5, INK, weight=600, anchor="middle")
    # The technology line wraps rather than overrunning its box.
    words, line, wrapped = tech.split(), "", []
    for word in words:
        trial = (line + " " + word).strip()
        if len(trial) * 5.5 > BOX_W - 14 and line:
            wrapped.append(line)
            line = word
        else:
            line = trial
    wrapped.append(line)
    for k, row in enumerate(wrapped[:2]):
        s.text(CX[i], HEAD_Y + 42 + k * 13, row, 10.5, INK, anchor="middle")
    # The lifeline itself.
    s.parts.append(
        f'<line x1="{CX[i]:.1f}" y1="{HEAD_Y + HEAD_H}" x2="{CX[i]:.1f}" '
        f'y2="{foot_y}" stroke="{_grey(FAINT)}" stroke-width="1.2" '
        f'stroke-dasharray="6 5"/>')

# --------------------------------------------------------------- messages
number = 0
for r, (kind, src, dst, label, proto) in enumerate(ROWS):
    y = ROW_Y[r]

    if kind == "g":
        s.parts.append(
            f'<line x1="40" y1="{y - 12}" x2="{W - 40}" y2="{y - 12}" '
            f'stroke="{_grey(RULE)}" stroke-width="1" stroke-dasharray="3 4"/>')
        s.text(46, y - 17, label, 12.5, RULE, weight=600)
        continue

    number += 1
    x1, x2 = CX[src], CX[dst]
    dash = ' stroke-dasharray="7 4"' if kind == "r" else ""
    s.parts.append(
        f'<path d="M {x1:.1f} {y:.1f} L {x2:.1f} {y:.1f}" fill="none" '
        f'stroke="{_grey(ACCENT)}" stroke-width="1.6"{dash} '
        f'marker-end="url(#ar)"/>')

    # The step number sits on the source lifeline, so the reading order is
    # unambiguous even where two arrows share a row band.
    s.parts.append(
        f'<circle cx="{x1:.1f}" cy="{y:.1f}" r="9" fill="{PAPER}" '
        f'stroke="{_grey(ACCENT)}" stroke-width="1.4"/>')
    s.text(x1, y + 4, str(number), 11, ACCENT, weight=600, anchor="middle")

    mid = (x1 + x2) / 2
    s.text(mid, y - 9, label, 12, INK, anchor="middle")
    if proto:
        s.text(mid, y + 15, proto, 10.5, RULE, anchor="middle")

# -------------------------------------------------------------- apparatus
LEG_W = 430
s.legend(W - 40 - LEG_W, foot_y + 26, LEG_W, [
    ("box", "Actor or client application"),
    ("comp", "Server component"),
    ("store", "Data store"),
    ("line", "Message, numbered, with its protocol and port"),
])

s.note(40, foot_y + 26, W - 40 - LEG_W - 60, [
    "Workflows: SIMF-HLD-004 section 2.3.",
    "Participants, zones, protocols and ports: the Communication Requirements "
    "Matrix, SIMF-HLD-004 section 2.8.",
    "Endpoint paths: the FastEndpoints route declarations in the SIMF solution "
    "source tree.",
    "Every attendee message crosses the WAF and load balancer and then "
    "SIMF.MobileEdge. That chain is drawn in full at steps 1 to 3 and elided "
    "afterwards, which each arrow states.",
    "The API load balancer forwards and does not participate, so it is folded "
    "into the SIMF.Api lifeline rather than given one of its own.",
    "A dashed arrow is a reply, a solid arrow a call.",
], "Sources and conventions")

s.save(OUT)
