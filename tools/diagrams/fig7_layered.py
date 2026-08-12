"""SIMF logical architecture, the layered view for SIMF-LLD-003 section 7.1.

A layered architecture diagram. Each tier states its responsibility, the
dependency direction is drawn explicitly, and the cross-cutting concerns are
placed beside the tiers they apply to rather than inside one of them.

Facts come from the solution source tree: the four backend projects and their
project references, the shared libraries, and the two DbContexts.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from svgkit import ACCENT, INK, NODE_FILL, RULE, STORE_FILL, Sheet, wrap

W, H = 1660, 1250
OUT = os.path.join(os.path.dirname(os.path.dirname(
    os.path.dirname(os.path.abspath(__file__)))), "docs", "diagrams",
    "SIMF-Fig7-Layered-Architecture")

s = Sheet(W, H, "SIMF logical architecture",
          "Layered view. The dependency rule points inward: Api to "
          "Infrastructure to Application to Domain. Project names are the "
          "solution source tree.")

LX, LW = 60, 1080          # layer band geometry
CX, CW = 1180, 420         # cross-cutting column

# ------------------------------------------------------------------ clients
s.band(LX, 120, LW, 118, "Client tier", "outside the backend solution")
for i, (name, tech) in enumerate([
        ("Mobile application", "Flutter, iOS and Android"),
        ("Public website", "Blazor SSR"),
        ("Control Panel", "Blazor Server")]):
    s.box(LX + 24 + i * 350, 152, 320, 70, name, "client", tech,
          component=True)

s.path([(600, 238), (600, 286)], "HTTPS 443, ApiResult envelope",
       label_dy=-8)

# ------------------------------------------------------------------- layers
LAYERS = [
    (286, "SIMF.Api", "HTTP boundary",
     ["FastEndpoints endpoint classes, one per use case",
      "Middleware pipeline, authentication and authorisation policies",
      "Background workers hosted in process"]),
    (430, "SIMF.Infrastructure", "Technical implementation",
     ["EF Core persistence over two DbContexts, audit interceptors",
      "File storage, SMTP dispatch, identity and token issue",
      "AI provider clients behind one abstraction"]),
    (574, "SIMF.Application", "Use case orchestration",
     ["Application services, one per bounded context",
      "Service abstractions the Api and Infrastructure implement",
      "Validation rules and permission policy definitions"]),
    (718, "SIMF.Domain", "Business model",
     ["Entities, aggregates, value objects and enums",
      "Domain rules and state transitions",
      "No framework dependency of any kind"]),
]
for y, name, role, rows in LAYERS:
    s.layer(LX, y, LW, 122, name, role)
    s.lines(LX + 24, y + 52, rows, 12, INK, step=20)

for y in (408, 552, 696):
    s.path([(LX + LW / 2, y), (LX + LW / 2, y + 22)], "depends on",
           colour=INK, label_dy=-6)

s.note(LX, 862, LW, [
    "A layer may reference only the layer beneath it. SIMF.Domain references "
    "nothing, which is what keeps the business",
    "rules testable without a database. SIMF.Api never reaches the database "
    "directly; it calls an application service,",
    "and the implementation of that service lives in SIMF.Infrastructure.",
], title="Dependency rule")

# --------------------------------------------------------------- data tier
s.band(LX, 966, LW, 118, "Data tier", "reached only from SIMF.Infrastructure")
s.store(LX + 24, 998, 320, 58, "D1", "SIMF_Identity", "SQL Server 2022")
s.store(LX + 374, 998, 320, 58, "D2", "SIMF_App", "SQL Server 2022")
s.store(LX + 724, 998, 320, 58, "D3", "File store", "SMB share, HSA zone")

# ------------------------------------------------------- cross cutting
s.band(CX, 120, CW, 502, "Shared libraries", "referenced by every tier")
SHARED = [
    ("SIMF.Common", "Response envelope, permission catalogue, roles, enums, "
                    "error codes"),
    ("SIMF.Contracts", "Data transfer objects for both API surfaces"),
    ("SIMF.ApiClient", "Typed HTTP client used by the website and Control Panel"),
    ("SIMF.Components", "Shared UI components over one design-token stylesheet"),
]
for i, (name, role) in enumerate(SHARED):
    y = 152 + i * 116
    s.box(CX + 20, y, CW - 40, 96, name, "library", "", NODE_FILL)
    s.lines(CX + 34, y + 62, wrap(role, CW - 76, 10.5), 10.5, RULE, step=14)

s.band(CX, 650, CW, 434, "Cross-cutting concerns", "applied in the pipeline")
CONCERNS = [
    ("Correlation", "Correlation id read or generated, enriched into logs"),
    ("Authentication", "JWT bearer, security stamp checked per request"),
    ("Authorization", "Permission codes from one catalogue"),
    ("Validation", "Per request shape, bilingual failures"),
    ("Error handling", "Exceptions mapped to the failure envelope"),
    ("Auditing", "Interceptors write both append-only trails"),
    ("Logging", "Structured events to file and to the SIEM"),
    ("Rate limiting", "Per address, per e-mail, per administrator"),
    ("Caching", "Reference and configuration data, per node"),
]
cy = 686
for name, role in CONCERNS:
    s.text(CX + 22, cy + 12, name, 12, INK, bold=True)
    s.text(CX + 168, cy + 12, role, 10, RULE)
    cy += 42

s.legend(LX, 1100, 520, [
    ("band", "Architecture tier"),
    ("comp", "Deployable client or shared library"),
    ("store", "Persistent data store"),
    ("line", "Dependency or call, labelled"),
], title="Key")

s.save(OUT)
