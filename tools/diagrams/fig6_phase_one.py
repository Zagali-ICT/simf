"""Figure. SIMF phase one deployment, security areas and on-site services.

UML deployment diagram. A SEPARATE SHEET, not a redraw of sheet 5. Sheet 5 is
the deployment figure published in SIMF-LLD-003 v1.3 and SIMF-HLD-004 v1.2 and
stays exactly as issued; this sheet is what the phase one reissues carry in its
place. The same rule was applied when sheet 4 was added beside sheet 1, and
sheet 5 beside sheet 4.

Phase one is a customer requirement of 2026-08-30 and moves five things:

  * the API server moves OUT of SSA and into HSA, so it has no internet path;
  * the AI provider stops being Gemini and becomes the SITE-hosted GPT OSS 120B
    model on an on-site LLM server in HSA, over an OpenAI-compatible API;
  * mail stops going to an external relay and goes to an on-site mail server
    in HSA;
  * the file store stops writing into a directory over SMB and becomes MinIO
    object storage in HSA, reached over the S3 API;
  * the only remaining internet call, to YouTube, is made by the Control Panel
    in SSA, which is the tier that has internet access.

What that leaves is a clean statement of the estate: everything that holds or
processes SIMF data sits in HSA and never reaches the internet, and the single
egress belongs to the one tier that is allowed one.

CP, WEB and the mobile edge stay in the presentation zone. The zone model is
otherwise sheet 5's, and the server specifications are still reproduced from the
customer server requirements workbook, sheet `List`, without interpretation.

Layout note. The egress riser runs in a channel OUTSIDE the node columns, at
RISER_R. Every cross-route on the sheet runs between a column centre and an API
entry point, so the inner gaps are already spoken for and a riser placed in one
would be cut. The sheet's `zone_pad` holds the zone names and notes off it.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from svgkit import BAR_H, CUBE_D, NODE_FILL, Sheet  # noqa: E402

OUT = r"d:\SIMF\System\V1.0.0\docs\diagrams\SIMF-Fig6-Phase-One-Security-Areas"

# ------------------------------------------------------------------ across
FRAME_X, FRAME_W = 40, 1363          # security areas, 40 to 1403
LEFT, LW = 58, 1327                  # network zones, 58 to 1385
PAD = 88                             # zone name and note inset
C1, C2, C3 = 176, 565, 954           # node columns
NW = 300                             # every node and box is this wide
MID = NW // 2                        # a column's centre line
RISER_R = 1335                       # the single egress channel

COL1, COL2, COL3 = C1 + MID, C2 + MID, C3 + MID     # column centres
PORT_L, PORT_C, PORT_R = C2 + 50, COL2, C2 + 250    # where the API tier is addressed
DB_LINK, FILE_LINK = COL2 - 35, COL2 + 35           # the two links leaving the API
C1_RIGHT = C1 + NW + CUBE_D                         # the LLM cube's right face
C2_LEFT, C2_RIGHT = C2, C2 + NW + CUBE_D            # the API cube's two faces
C3_RIGHT = C3 + NW + CUBE_D                         # the CP cube's right face
YT = 572                                            # the one internet box

# ------------------------------------------------------------------- down
Y_INTERNET = 110
Y_SSA = 252
Y_ACCESS = 286
Y_FW_PERIMETER = 430
Y_PERIMETER = 484
Y_PRESENTATION = 634
Y_FW_HSA = 908
Y_HSA = 962
Y_APPLICATION = 996
Y_FW_DATA = 1362
Y_DATA = 1426
H_SSA, H_HSA = 622, 704

# Cross-routes run in the clear gap below a firewall bar; the egress riser turns
# in the gap above the SSA frame.
Y_CROSS_APP = Y_FW_HSA + BAR_H + 29
Y_TURN = Y_SSA - 12
Y_ROW = 1148                          # the application zone's second row
Y_ROW_MID = Y_ROW + 82                # where the API's side links leave it

s = Sheet(1940, 1706,
          "SIMF phase one deployment, security areas and on-site services",
          "UML deployment diagram.  Server specifications are reproduced from the "
          "customer server requirements workbook.",
          zone_pad=PAD)

COMMON = ["Windows Server 2022", "32 GB RAM, 16 vCPU", "300 GB storage",
          "Server + DR"]
TBC = ["Windows Server 2022", "specification to be", "confirmed with the site"]

# ---------------------------------------------------------------- internet
s.band(LEFT, Y_INTERNET, LW, 118, "Internet", "one third party service")
s.box(YT, 142, NW, 74, "YouTube", "external system", "youtubei.googleapis.com")

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
           "permits inbound HTTPS 443, and outbound HTTPS 443 from the presentation zone")

# --------------------------------------------------------- perimeter zone
s.band(LEFT, Y_PERIMETER, LW, 118, "Perimeter zone", "published entry point")
s.box(C2, 518, NW, 72, "WAF and load balancer", "device", "TLS terminates here",
      fill=NODE_FILL)

# ------------------------------------------------------ presentation zone
s.band(LEFT, Y_PRESENTATION, LW, 222, "Presentation zone", "Windows Server 2022")
s.node(C1, 680, NW, 164, "EDGE API", "server", TBC, ["SIMF.MobileEdge"])
s.node(C2, 680, NW, 164, "WEB   x 2", "server", COMMON, ["SIMF.Web"])
s.node(C3, 680, NW, 164, "CP   x 2", "server", COMMON, ["SIMF.ControlPanel"])

s.firewall(LEFT, Y_FW_HSA, LW, "Internal firewall",
           "permits TCP 443 from the presentation zone only")

s.group(FRAME_X, Y_HSA, FRAME_W, H_HSA, "HSA", "internal traffic only")

# ------------------------------------------------------- application zone
s.band(LEFT, Y_APPLICATION, LW, 332, "Application zone", "Windows Server 2022")
s.box(C2, 1030, NW, 72, "API load balancer", "device",
      "distributes to the API nodes", fill=NODE_FILL)
s.node(C1, Y_ROW, NW, 164, "LLM SERVER", "server", TBC,
       ["GPT OSS 120B", "OpenAI-compatible API"])
s.node(C2, Y_ROW, NW, 164, "API   x 4", "server", COMMON, ["SIMF.Api"])
s.node(C3, Y_ROW, NW, 164, "MAIL SERVER", "server", TBC, ["On-site SMTP relay"])

s.firewall(LEFT, Y_FW_DATA, LW, "Internal firewall",
           "permits TCP 1433 and HTTPS 443 from the application zone only")

# -------------------------------------------------------------- data zone
s.band(LEFT, Y_DATA, LW, 222, "Data zone", "Windows Server 2022")
s.node(C2, 1472, NW, 155, "DATABASE   x 2", "server",
       ["Windows Server 2022", "64 GB RAM, 8 vCPU", "2 TB storage", "Server + DR"],
       ["SIMF_Identity + SIMF_App"])
s.node(C3, 1472, NW, 155, "MinIO", "server", TBC,
       ["Object storage, S3 API", "Stored files"])

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

# The one internet call on the sheet, and it belongs to the Control Panel.
s.path([(C3_RIGHT, 762), (RISER_R, 762), (RISER_R, Y_TURN), (YT + MID, Y_TURN),
        (YT + MID, 216)], "HTTPS 443", label_at=(RISER_R, 550))

# The presentation tier is the only caller of the application tier, and the
# call now crosses the boundary between the two security areas.
s.path([(COL1, 844), (COL1, Y_CROSS_APP), (PORT_L, Y_CROSS_APP), (PORT_L, 1030)],
       "HTTPS 443", label_at=((COL1 + PORT_L) // 2, Y_CROSS_APP), label_dy=3)
s.path([(COL2, 844), (COL2, 1030)], "HTTPS 443",
       label_at=(COL2, Y_CROSS_APP), label_dy=3)
s.path([(COL3, 844), (COL3, Y_CROSS_APP), (PORT_R, Y_CROSS_APP), (PORT_R, 1030)],
       "HTTPS 443", label_at=((COL3 + PORT_R) // 2, Y_CROSS_APP), label_dy=3)

# The load balancer fronts the four API nodes.
s.path([(PORT_C, 1102), (PORT_C, Y_ROW)], "HTTPS 443", label_at=(PORT_C, 1121))

# The API reaches its two on-site services without leaving HSA.
s.path([(C2_LEFT, Y_ROW_MID), (C1_RIGHT, Y_ROW_MID)], "HTTPS 443",
       label_at=((C2_LEFT + C1_RIGHT) // 2, Y_ROW_MID), label_dy=3)
s.path([(C2_RIGHT, Y_ROW_MID), (C3, Y_ROW_MID)], "SMTP 587",
       label_at=((C2_RIGHT + C3) // 2, Y_ROW_MID), label_dy=3)

# The application tier is the only caller of the data tier.
s.path([(DB_LINK, 1312), (DB_LINK, 1472)], "TCP 1433",
       label_at=(DB_LINK, Y_FW_DATA - 14))
s.path([(FILE_LINK, 1312), (FILE_LINK, 1406), (COL3, 1406), (COL3, 1472)],
       "S3 HTTPS 443", label_at=((FILE_LINK + COL3) // 2, 1406))

# ------------------------------------------------------------ right column
RX, RW = 1473, 420
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
    "SSA holds the access, perimeter and presentation",
    "zones, and is the area that reaches the internet.",
    "HSA holds the application and data zones, and",
    "carries internal traffic only.",
], "Security areas") + GAP

ry += s.note(RX, ry, RW, [
    "Phase one puts every service that holds or",
    "processes SIMF data inside HSA: the API, the AI",
    "model, the mail relay and the file store.",
    "The AI runs on the SITE-hosted GPT OSS 120B model on an",
    "on-site LLM server, reached over an OpenAI-compatible",
    "API. Mail goes to an on-site SMTP relay.",
    "Files are written to MinIO object storage over the",
    "S3 API, in place of a directory on a file share.",
    "One internet call remains, and the Control Panel",
    "makes it: the caption fetch to YouTube.",
], "Phase one, on site") + GAP

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
    "The security areas, the API load balancer and the",
    "internet zone are owner decisions of 2026-08-20.",
    "The API in HSA, the on-site LLM server, the on-site",
    "mail server, MinIO in place of a file share, and the",
    "YouTube call moving to the Control Panel are a",
    "customer requirement of 2026-08-30.",
    "SMTP port 587: the EmailOptions.Port default.",
    "YouTube caption host: YoutubeTranscriptService.cs.",
    "The workbook lists none of the edge, LLM, mail,",
    "MinIO or load balancer servers, so no count or",
    "specification is stated for them.",
]
s.note(RX, Y_HSA + H_HSA - Sheet.note_height(SOURCES), RW, SOURCES, "Sources")

s.save(OUT)
