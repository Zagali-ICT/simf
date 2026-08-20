"""Figure. SIMF target three-tier deployment and network architecture.

UML deployment diagram, drawn to the same notation as fig1_deployment.py. It
differs from that sheet in three respects, each an owner decision of 2026-08-10:
the application zone holds the API alone and is reachable only from the
presentation zone; the mobile clients address a presentation-tier edge rather
than the API itself; and stored files live on their own server, separate from
the database.

Server specifications are reproduced from the customer server requirements
workbook, sheet `List`, without interpretation. The zone model and the firewalls
follow SIMF-HLD-004 as delivered. The mobile edge and the file server are not in
that workbook, so neither carries a node count or a specification here.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from svgkit import Sheet  # noqa: E402

OUT = r"d:\SIMF\System\V1.0.0\docs\diagrams\SIMF-Fig4-Target-Tier-Separation"

s = Sheet(1660, 1310,
          "SIMF target three-tier deployment and network architecture",
          "UML deployment diagram.  Server specifications are reproduced from the "
          "customer server requirements workbook.")

LEFT, LW = 40, 1140

# ----------------------------------------------------------- access zone
s.band(LEFT, 110, LW, 120, "Access zone", "client devices, outside the data centre")
s.box(45, 142, 300, 74, "Attendee mobile device", "device",
      "SIMF app, Google Play and App Store")
s.box(430, 142, 300, 74, "Public website browser", "device", "public visitors")
s.box(815, 142, 300, 74, "Administrator browser", "device", "Control Panel users")

s.firewall(LEFT, 254, LW, "Perimeter firewall", "permits inbound HTTPS 443")

# --------------------------------------------------------- perimeter zone
s.band(LEFT, 308, LW, 118, "Perimeter zone", "published entry point")
s.box(430, 342, 300, 72, "WAF and load balancer", "device", "TLS terminates here",
      fill="#E7EDF4")

# ------------------------------------------------------ presentation zone
s.band(LEFT, 458, LW, 222, "Presentation zone", "Windows Server 2022")

COMMON = ["Windows Server 2022", "32 GB RAM, 16 vCPU", "300 GB storage",
          "Server + DR"]
s.node(45, 500, 300, 164, "MOBILE EDGE", "server",
       ["Windows Server 2022", "specification to be", "confirmed with the site"],
       ["SIMF.MobileEdge"])
s.node(430, 500, 300, 164, "WEB   x 2", "server", COMMON, ["SIMF.Web"])
s.node(815, 500, 300, 164, "CP   x 2", "server", COMMON, ["SIMF.ControlPanel"])

s.firewall(LEFT, 700, LW, "Internal firewall",
           "permits TCP 443 from the presentation zone only")

# ------------------------------------------------------- application zone
s.band(LEFT, 754, LW, 222, "Application zone", "Windows Server 2022")
s.node(430, 796, 300, 164, "API   x 4", "server", COMMON, ["SIMF.Api"])

s.firewall(LEFT, 996, LW, "Internal firewall",
           "permits TCP 1433 and SMB 445 from the application zone only")

# -------------------------------------------------------------- data zone
s.band(LEFT, 1050, LW, 222, "Data zone", "Windows Server 2022")
s.node(430, 1092, 300, 155, "DATABASE   x 2", "server",
       ["Windows Server 2022", "64 GB RAM, 8 vCPU", "2 TB storage", "Server + DR"],
       ["SIMF_Identity + SIMF_App"])
s.node(815, 1092, 300, 155, "FILE SERVER", "server",
       ["Windows Server 2022", "specification to be", "confirmed with the site"],
       ["Stored files", "Data Protection key ring"])

# ---------------------------------------------------------------- paths
# Clients reach the published entry point.
s.path([(195, 216), (195, 326), (480, 326), (480, 342)], "HTTPS 443",
       label_at=(195, 250))
s.path([(580, 216), (580, 342)], "HTTPS 443", label_at=(580, 250))
s.path([(965, 216), (965, 326), (680, 326), (680, 342)], "HTTPS 443",
       label_at=(965, 250))

# The entry point reaches the presentation tier, and nothing else.
s.path([(480, 414), (480, 452), (195, 452), (195, 500)], "HTTPS 443",
       label_at=(320, 452))
s.path([(580, 414), (580, 500)], "HTTPS 443", label_at=(580, 470))
s.path([(680, 414), (680, 452), (965, 452), (965, 500)], "HTTPS 443",
       label_at=(840, 452))

# The presentation tier is the only caller of the application tier. The
# cross-routing runs in the gap between the firewall bar and the zone band, so a
# label never lands on the bar's own rule text.
s.path([(195, 664), (195, 745), (480, 745), (480, 796)], "HTTPS 443",
       label_at=(320, 745))
s.path([(580, 664), (580, 796)], "HTTPS 443", label_at=(580, 692))
s.path([(965, 664), (965, 745), (680, 745), (680, 796)], "HTTPS 443",
       label_at=(840, 745))

# The application tier is the only caller of the data tier.
s.path([(545, 960), (545, 1092)], "TCP 1433", label_at=(545, 988))
s.path([(615, 960), (615, 1040), (965, 1040), (965, 1092)], "SMB 445",
       label_at=(800, 1040))

# ------------------------------------------------------------ right column
RX, RW = 1200, 420

s.legend(RX, 110, RW, [
    ("node", "Server node, with its specification"),
    ("box", "Client device or published entry point"),
    ("wall", "Firewall, with the traffic it permits"),
    ("band", "Network zone"),
    ("line", "Communication path, labelled with protocol and port"),
])

s.note(RX, 290, RW, [
    "Node counts and specifications: customer server",
    "requirements workbook, sheet List.",
    "Mobile distribution channels: same workbook, sheet NEW1.",
    "Deployed artifact names: SIMF solution source tree.",
    "Zone model, firewalls and WAF: SIMF-HLD-004 as delivered.",
    "The mobile edge, the file server and the application zone",
    "holding the API alone are owner decisions of 2026-08-10.",
    "The workbook lists neither the mobile edge nor the file",
    "server, so no count or specification is stated for them.",
], "Sources")

s.save(OUT)
