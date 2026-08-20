"""Figure. SIMF target three-tier deployment and network architecture.

UML deployment diagram, drawn to the same notation as fig1_deployment.py. It
differs from that sheet in three respects, each an owner decision of 2026-08-10:
the application zone holds the API alone and is reachable only from the
presentation zone; the mobile clients address a presentation-tier edge rather
than the API itself; and stored files live on their own server, separate from
the database.

Four further elements are owner decisions of 2026-08-20: the two security areas
that group the zones, HSA over the data zone and SSA over everything else; a
load balancer in front of the API nodes; the internet zone; and the API's two
outbound calls to it, which cross both firewalls on their way out.

Provenance for every fact on this sheet is printed on the sheet itself, in the
Sources note at the foot of the right column. Change the two together.

Layout note. The two egress paths run in channels OUTSIDE the node columns, at
RISER_L and RISER_R, not in the gaps between them. Every cross-route on the
sheet runs between a column centre and an API entry point, so those inner gaps
are already spoken for and a riser placed in one would be cut six times. The
sheet's `zone_pad` then holds the zone names and notes off the two channels.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from svgkit import BAR_H, CUBE_D, NODE_FILL, Sheet  # noqa: E402

OUT = r"d:\SIMF\System\V1.0.0\docs\diagrams\SIMF-Fig4-Target-Tier-Separation"

# ------------------------------------------------------------------ across
FRAME_X, FRAME_W = 40, 1363          # security areas, 40 to 1403
LEFT, LW = 58, 1327                  # network zones, 58 to 1385
PAD = 88                             # zone name and note inset
C1, C2, C3 = 176, 565, 954           # node columns
NW = 300                             # every node and box is this wide
MID = NW // 2                        # a column's centre line
RISER_L, RISER_R = 108, 1335         # the two egress channels
RX, RW = 1473, 420                   # right column

COL1, COL2, COL3 = C1 + MID, C2 + MID, C3 + MID     # column centres
PORT_L, PORT_C, PORT_R = C2 + 50, COL2, C2 + 250    # where the API tier is addressed
DB_LINK, FILE_LINK = COL2 - 35, COL2 + 35           # the two links leaving the API
API_RIGHT = C2 + NW + CUBE_D                        # the API cube's right face
I1, I2 = 379, 764                                   # internet boxes, centred as a pair

# ------------------------------------------------------------------- down
Y_INTERNET = 110
Y_SSA = 252
Y_ACCESS = 286
Y_FW_PERIMETER = 430
Y_PERIMETER = 484
Y_PRESENTATION = 634
Y_FW_APP = 876
Y_APPLICATION = 930
Y_FW_DATA = 1314
Y_HSA = 1368
Y_DATA = 1402
H_SSA, H_HSA = 1028, 274

# A cross-route runs in the clear gap below a firewall bar; an egress riser
# turns in the gap above the SSA frame.
Y_CROSS_APP = Y_FW_APP + BAR_H + 15
Y_TURN = Y_SSA - 12

s = Sheet(1940, 1682,
          "SIMF target three-tier deployment and network architecture",
          "UML deployment diagram.  Server specifications are reproduced from the "
          "customer server requirements workbook.",
          zone_pad=PAD)

COMMON = ["Windows Server 2022", "32 GB RAM, 16 vCPU", "300 GB storage",
          "Server + DR"]
TBC = ["Windows Server 2022", "specification to be", "confirmed with the site"]

# ---------------------------------------------------------------- internet
s.band(LEFT, Y_INTERNET, LW, 118, "Internet", "third party services")
s.box(I1, 142, NW, 74, "YouTube", "external system", "youtubei.googleapis.com")
s.box(I2, 142, NW, 74, "Google Gemini API", "external system",
      "generativelanguage.googleapis.com")

# --------------------------------------------------------- security areas
s.group(FRAME_X, Y_SSA, FRAME_W, H_SSA, "SSA", "has access to the internet")

# ----------------------------------------------------------- access zone
s.band(LEFT, Y_ACCESS, LW, 120, "Access zone",
       "client devices, outside the data centre")
s.box(C1, 318, NW, 74, "Attendee mobile device", "device",
      "SIMF app, Google Play and App Store")
s.box(C2, 318, NW, 74, "Public website browser", "device", "public visitors")
s.box(C3, 318, NW, 74, "Administrator browser", "device", "Control Panel users")

s.firewall(LEFT, Y_FW_PERIMETER, LW, "Perimeter firewall",
           "permits inbound HTTPS 443, and outbound HTTPS 443 to the internet zone")

# --------------------------------------------------------- perimeter zone
s.band(LEFT, Y_PERIMETER, LW, 118, "Perimeter zone", "published entry point")
s.box(C2, 518, NW, 72, "WAF and load balancer", "device", "TLS terminates here",
      fill=NODE_FILL)

# ------------------------------------------------------ presentation zone
s.band(LEFT, Y_PRESENTATION, LW, 222, "Presentation zone", "Windows Server 2022")
s.node(C1, 680, NW, 164, "MOBILE EDGE", "server", TBC, ["SIMF.MobileEdge"])
s.node(C2, 680, NW, 164, "WEB   x 2", "server", COMMON, ["SIMF.Web"])
s.node(C3, 680, NW, 164, "CP   x 2", "server", COMMON, ["SIMF.ControlPanel"])

s.firewall(LEFT, Y_FW_APP, LW, "Internal firewall",
           "permits TCP 443 from the presentation zone, and outbound TCP 443 "
           "from the application zone")

# ------------------------------------------------------- application zone
s.band(LEFT, Y_APPLICATION, LW, 332, "Application zone", "Windows Server 2022")
s.box(C2, 964, NW, 72, "API load balancer", "device",
      "distributes to the API nodes", fill=NODE_FILL)
s.node(C2, 1082, NW, 164, "API   x 4", "server", COMMON, ["SIMF.Api"])

s.firewall(LEFT, Y_FW_DATA, LW, "Internal firewall",
           "permits TCP 1433 and SMB 445 from the application zone only")

s.group(FRAME_X, Y_HSA, FRAME_W, H_HSA, "HSA", "internal traffic only")

# -------------------------------------------------------------- data zone
s.band(LEFT, Y_DATA, LW, 222, "Data zone", "Windows Server 2022")
s.node(C2, 1448, NW, 155, "DATABASE   x 2", "server",
       ["Windows Server 2022", "64 GB RAM, 8 vCPU", "2 TB storage", "Server + DR"],
       ["SIMF_Identity + SIMF_App"])
s.node(C3, 1448, NW, 155, "FILE SERVER", "server", TBC,
       ["Stored files", "Data Protection key ring"])

# ---------------------------------------------------------------- paths
# Clients reach the published entry point.
s.path([(COL1, 392), (COL1, 502), (PORT_L, 502), (PORT_L, 518)], "HTTPS 443",
       label_at=(COL1, Y_FW_PERIMETER - 4))
s.path([(COL2, 392), (COL2, 518)], "HTTPS 443",
       label_at=(COL2, Y_FW_PERIMETER - 4))
s.path([(COL3, 392), (COL3, 502), (PORT_R, 502), (PORT_R, 518)], "HTTPS 443",
       label_at=(COL3, Y_FW_PERIMETER - 4))

# The entry point reaches the presentation tier, and nothing else.
s.path([(PORT_L, 590), (PORT_L, 628), (COL1, 628), (COL1, 680)], "HTTPS 443",
       label_at=((COL1 + PORT_L) // 2, 628))
s.path([(PORT_C, 590), (PORT_C, 680)], "HTTPS 443", label_at=(PORT_C, 620))
s.path([(PORT_R, 590), (PORT_R, 628), (COL3, 628), (COL3, 680)], "HTTPS 443",
       label_at=((COL3 + PORT_R) // 2, 628))

# The presentation tier is the only caller of the application tier. The
# cross-routing runs in the clear gap below the firewall bar, and label_dy=3
# centres each label on its own line rather than seating it above the line.
s.path([(COL1, 844), (COL1, Y_CROSS_APP), (PORT_L, Y_CROSS_APP), (PORT_L, 964)],
       "HTTPS 443", label_at=((COL1 + PORT_L) // 2, Y_CROSS_APP), label_dy=3)
s.path([(COL2, 844), (COL2, 964)], "HTTPS 443",
       label_at=(COL2, Y_CROSS_APP), label_dy=3)
s.path([(COL3, 844), (COL3, Y_CROSS_APP), (PORT_R, Y_CROSS_APP), (PORT_R, 964)],
       "HTTPS 443", label_at=((COL3 + PORT_R) // 2, Y_CROSS_APP), label_dy=3)

# The load balancer fronts the four API nodes.
s.path([(PORT_C, 1036), (PORT_C, 1082)], "HTTPS 443", label_at=(PORT_C, 1062))

# The API reaches out to the two internet services, crossing both firewalls.
# Each riser leaves one of the API node's own faces.
s.path([(C2, 1164), (RISER_L, 1164), (RISER_L, Y_TURN), (I1 + MID, Y_TURN),
        (I1 + MID, 216)], "HTTPS 443", label_at=(RISER_L, 550))
s.path([(API_RIGHT, 1164), (RISER_R, 1164), (RISER_R, Y_TURN),
        (I2 + MID, Y_TURN), (I2 + MID, 216)], "HTTPS 443",
       label_at=(RISER_R, 550))

# The application tier is the only caller of the data tier.
s.path([(DB_LINK, 1246), (DB_LINK, 1448)], "TCP 1433",
       label_at=(DB_LINK, Y_FW_DATA - 14))
s.path([(FILE_LINK, 1246), (FILE_LINK, 1392), (COL3, 1392), (COL3, 1448)],
       "SMB 445", label_at=((FILE_LINK + COL3) // 2, 1392))

# ------------------------------------------------------------ right column
GAP = 24
ry = 110
ry += s.legend(RX, ry, RW, [
    ("group", "Security area, HSA or SSA"),
    ("band", "Network zone"),
    ("node", "Server node, with its specification"),
    ("box", "Client device, entry point or external system"),
    ("wall", "Firewall, with the traffic it permits"),
    ("line", "Communication path, labelled with protocol and port"),
]) + GAP

ry += s.note(RX, ry, RW, [
    "SSA holds the access, perimeter, presentation",
    "and application zones, and is the area that",
    "reaches the internet.",
    "HSA holds the database and the file server,",
    "and carries internal traffic only.",
], "Security areas") + GAP

ry += s.note(RX, ry, RW, [
    "The API nodes call two internet services over",
    "HTTPS 443, outbound only:",
    "youtubei.googleapis.com and www.youtube.com,",
    "with *.googlevideo.com, for video captions.",
    "generativelanguage.googleapis.com, for the AI",
    "assistant, summaries and subtitles.",
], "Outbound egress") + GAP

# Sources is anchored to the bottom of the artwork, not stacked from the top,
# so the right column ends level with the HSA frame.
SOURCES = [
    "Node counts and specifications: customer server",
    "requirements workbook, sheet List.",
    "Mobile distribution channels: same workbook,",
    "sheet NEW1.",
    "Deployed artifact names: SIMF solution source tree.",
    "Zone model, firewalls and WAF: SIMF-HLD-004 as",
    "delivered.",
    "The mobile edge, the file server and the application",
    "zone holding the API alone are owner decisions of",
    "2026-08-10.",
    "The security areas, the API load balancer, the",
    "internet zone and the two outbound calls are owner",
    "decisions of 2026-08-20.",
    "Outbound endpoints: YoutubeTranscriptService.cs",
    "and AiOptions.cs in SIMF.Infrastructure.",
    "The workbook lists neither the mobile edge, the file",
    "server nor the load balancer, so no count or",
    "specification is stated for them.",
]
s.note(RX, Y_HSA + H_HSA - Sheet.note_height(SOURCES), RW, SOURCES, "Sources")

s.save(OUT)
