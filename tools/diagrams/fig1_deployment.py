"""Figure 1. SIMF production deployment and network architecture.

UML deployment diagram. Every node specification is reproduced from the customer
server requirements workbook, sheet `List`, without interpretation. The zone
model, the two firewalls and the WAF come from SIMF-HLD-004 as delivered.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from svgkit import Sheet  # noqa: E402

OUT = r"d:\SIMF\System\V1.0.0\docs\diagrams\SIMF-Fig1-Deployment-Network"

s = Sheet(1660, 1020,
          "SIMF production deployment and network architecture",
          "UML deployment diagram.  Node specifications are reproduced from the "
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

# ------------------------------------------------------- application zone
s.band(LEFT, 458, LW, 222, "Application zone", "Windows Server 2022")

COMMON = ["Windows Server 2022", "32 GB RAM, 16 vCPU", "300 GB storage",
          "Server + DR"]
s.node(45, 500, 300, 164, "WEB   x 2", "server", COMMON, ["SIMF.Web"])
s.node(430, 500, 300, 164, "API   x 4", "server", COMMON, ["SIMF.Api"])
s.node(815, 500, 300, 164, "CP   x 2", "server", COMMON, ["SIMF.ControlPanel"])

s.firewall(LEFT, 700, LW, "Internal firewall",
           "permits TCP 1433 and SMB 445 from the application zone only")

# -------------------------------------------------------------- data zone
s.band(LEFT, 754, LW, 205, "Data zone", "Windows Server 2022")
s.node(430, 790, 300, 155, "Database   x 2", "server",
       ["Windows Server 2022", "64 GB RAM, 8 vCPU", "2 TB storage", "Server + DR"],
       ["SIMF_Identity + SIMF_App", "File share (SMB)"])

# ---------------------------------------------------------------- paths
s.path([(195, 216), (195, 326), (480, 326), (480, 342)], "HTTPS 443",
       label_at=(195, 250))
s.path([(580, 216), (580, 342)], "HTTPS 443", label_at=(580, 250))
s.path([(965, 216), (965, 326), (680, 326), (680, 342)], "HTTPS 443",
       label_at=(965, 250))

s.path([(480, 414), (480, 452), (195, 452), (195, 500)], "HTTPS 443",
       label_at=(320, 452))
s.path([(580, 414), (580, 500)], "HTTPS 443", label_at=(580, 470))
s.path([(680, 414), (680, 452), (965, 452), (965, 500)], "HTTPS 443",
       label_at=(840, 452))

s.path([(358, 590), (430, 590)], "HTTPS 443", label_at=(394, 583))
s.path([(815, 590), (743, 590)], "HTTPS 443", label_at=(779, 583))

s.path([(545, 664), (545, 790)], "TCP 1433", label_at=(545, 692))
s.path([(615, 664), (615, 790)], "SMB 445", label_at=(615, 776))

# ------------------------------------------------- pre-production column
RX, RW = 1200, 420
s.band(RX, 110, RW, 430, "Pre-production environment", "single node per tier")
s.node(RX + 30, 160, 360, 150, "Front end   x 1", "server",
       ["Windows Server 2022", "32 GB RAM, 16 vCPU", "300 GB storage",
        "WEB + API + CP"],
       ["SIMF.Web, SIMF.Api, SIMF.ControlPanel"])
s.node(RX + 30, 360, 360, 165, "Database   x 1", "server",
       ["Windows Server 2022", "32 GB RAM, 8 vCPU", "1000 GB storage",
        "DB + File"],
       ["SIMF_Identity + SIMF_App", "File share (SMB)"])
s.path([(RX + 210, 310), (RX + 210, 360)], "TCP 1433, SMB 445",
       label_at=(RX + 210, 342))

s.legend(RX, 580, RW, [
    ("node", "Server node, with its specification"),
    ("box", "Client device or published entry point"),
    ("wall", "Firewall, with the traffic it permits"),
    ("band", "Network zone"),
    ("line", "Communication path, labelled with protocol and port"),
])

s.note(RX, 755, RW, [
    "Node counts and specifications: customer server",
    "requirements workbook, sheet List.",
    "Mobile distribution channels: same workbook, sheet NEW1.",
    "Deployed artifact names: SIMF solution source tree.",
    "Zone model, firewalls and WAF: SIMF-HLD-004 as delivered.",
], "Sources")

s.save(OUT)
