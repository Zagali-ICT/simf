"""Figure. SIMF phase one components and their interaction.

UML component diagram, drawn to C4 container-diagram discipline: every element
states its type and its technology, and every line is unidirectional and carries
its protocol. Component names come from the solution source tree; the inward
dependency rule comes from SIMF-LLD-003 section 7.1.

A SEPARATE SHEET, not a redraw of sheet 2. Sheet 2 is the component figure
published up to SIMF-HLD-004 v1.4 and stays exactly as issued; this sheet is
what the phase one reissues carry in its place, on the same rule that put
sheet 4 beside sheet 1, sheet 5 beside sheet 4 and sheet 6 beside sheet 5.

Phase one moves two things on this sheet, both customer requirements of
2026-08-30 already drawn on sheet 6:

  * the file store stops being a directory on a share reached over SMB and
    becomes MinIO object storage reached over the S3 API;
  * the background-worker box goes. The API host runs its scheduled jobs
    in-process as IHostedService registrations, so there is no second
    deployable and nothing for a box to stand for.

The video platform, the AI service and the e-mail relay are still NOT drawn
here. Two of the three now run on site, but none of them is a component
designed as part of this solution, and what matters about each is the data that
crosses to it, which is a data-flow question. They are on the data-flow sheet
instead. The log collector stays: it is infrastructure the estate depends on
and it receives from this host continuously.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from svgkit import Sheet, NODE_FILL  # noqa: E402

OUT = r"d:\SIMF\System\V1.0.0\docs\diagrams\SIMF-Fig7-Phase-One-Components"

s = Sheet(1660, 1220,
          "SIMF phase one components and their interaction",
          "UML component diagram.  Every component carries its technology and "
          "every call carries its protocol.")

# --------------------------------------------------- client applications
s.band(40, 140, 1000, 150, "Client applications")
s.box(65, 180, 230, 96, "Mobile application", "client", "Flutter, Android and iOS")
s.box(310, 180, 230, 96, "Public website", "client", "SIMF.Web, Blazor SSR")
s.box(555, 180, 230, 96, "Control Panel", "client", "SIMF.ControlPanel, Blazor Server")
s.box(800, 180, 230, 96, "Badge desk", "client", "SIMF.BadgeDesk, WinForms")

# ------------------------------------------------------ API host process
s.band(40, 340, 1000, 520, "SIMF.Api host", "single deployable process")
s.box(70, 380, 580, 80, "SIMF.Api", "component",
      "FastEndpoints, middleware, authorisation", fill=NODE_FILL, component=True)
s.box(70, 500, 580, 110, "SIMF.Infrastructure", "component",
      "EF Core, storage, e-mail, identity, audit", fill=NODE_FILL, component=True)
s.box(70, 650, 580, 72, "SIMF.Application", "component",
      "use cases and service abstractions", fill=NODE_FILL, component=True)
s.box(70, 752, 580, 70, "SIMF.Domain", "component",
      "entities, aggregates, rules", fill=NODE_FILL, component=True)

# ------------------------------------------------------ external systems
# The video platform, the AI service and the e-mail relay are NOT drawn here.
# Two of the three now run on site, but none is a component designed as part of
# this solution, and an architecture sheet that boxes them implies a design
# commitment the system does not make. What matters about each is the data that
# crosses to it, which is a data-flow concern, so they are on the data-flow
# sheet instead (owner decision 2026-08-10, kept through phase one). The log
# collector stays: it is infrastructure the estate depends on and it receives
# from this host continuously.
s.band(1180, 140, 440, 140, "External services")
s.box(1205, 180, 390, 80, "Log collector", "external", "syslog over TLS")

s.band(1180, 320, 440, 140, "Shared libraries",
       "referenced by every client and by the API host")
s.box(1205, 355, 390, 86, "Shared libraries", "library",
      "SIMF.Common, .Contracts, .ApiClient, .Components")

# ------------------------------------------------------------- data tier
s.band(40, 900, 1000, 150, "Data tier")
s.store(70, 945, 300, 60, "DB", "SIMF_Identity", "SimfIdentityDbContext")
s.store(400, 945, 300, 60, "DB", "SIMF_App", "SimfAppDbContext")
s.store(730, 945, 300, 60, "OS", "MinIO", "object storage, S3 API")

# --------------------------------------------------------- client to API
s.path([(180, 276), (180, 315), (150, 315), (150, 380)], "HTTPS 443",
       label_at=(180, 304))
s.path([(425, 276), (425, 325), (300, 325), (300, 380)], "HTTPS 443",
       label_at=(425, 304))
s.path([(670, 276), (670, 335), (450, 335), (450, 380)], "HTTPS 443",
       label_at=(670, 304))
s.path([(915, 276), (915, 345), (580, 345), (580, 380)], "HTTPS 443",
       label_at=(915, 304))

# ------------------------------------------------------- inward layering
s.path([(360, 460), (360, 500)], "in-process", label_at=(360, 488))
s.path([(360, 610), (360, 650)], "in-process", label_at=(360, 638))
s.path([(360, 722), (360, 752)], "in-process", label_at=(360, 742))

# ------------------------------------------- infrastructure to the outside
s.path([(650, 545), (1130, 545), (1130, 220), (1205, 220)], "syslog 6514",
       label_at=(1167, 213))

s.path([(650, 588), (700, 588), (700, 872), (220, 872), (220, 945)], "TCP 1433",
       label_at=(460, 865))
s.path([(650, 598), (720, 598), (720, 882), (550, 882), (550, 945)], "TCP 1433",
       label_at=(645, 875))
s.path([(650, 606), (740, 606), (740, 892), (880, 892), (880, 945)], "S3 HTTPS 443",
       label_at=(815, 885))

# ------------------------------------------------------------- apparatus
s.legend(1180, 500, 440, [
    ("comp", "Component, with its technology"),
    ("box", "Client application or external service"),
    ("store", "Data store"),
    ("band", "Grouping or process boundary"),
    ("line", "Call, labelled with protocol and port"),
])

s.note(40, 1080, 1000, [
    "Component names and layering: SIMF solution source tree.",
    "Inward dependency rule Api, Infrastructure, Application, Domain: "
    "SIMF-LLD-003 section 7.1.",
    "The API host runs its scheduled jobs in-process, as IHostedService "
    "registrations. There is no separate worker deployable.",
    "Protocols and component roles: SIMF-LLD-003 section 2.1.",
    "MinIO over the S3 API in place of a directory on a share: customer "
    "requirement of 2026-08-30, drawn on the phase one deployment sheet.",
], "Sources")

s.save(OUT)
