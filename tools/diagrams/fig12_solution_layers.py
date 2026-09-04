"""Sheet 12: the SIMF layered solution structure.

Section 7.1 of the Solution Design Document states the layering as a dependency
rule in one line, Api to Infrastructure to Application to Domain, and then lists
the layers in a table. A table cannot show the property that rule exists to
protect: dependencies point in ONE direction only, so the domain compiles with
no knowledge of the database, the web host or any client.

The sheet draws that as nesting rather than as a stack of arrows. Domain sits
innermost; each outer layer encloses the one it depends on. Nesting makes the
illegal direction undrawable, which a row of arrows does not.

Two things sit beside the layers rather than inside them, because they belong to
every layer at once: the shared libraries, which all four layers and all four
clients reference, and the cross-cutting concerns, which are implemented once
and applied across every feature context.

Regenerate with:  python tools/diagrams/fig12_solution_layers.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from svgkit import INK, RULE, ACCENT, PAPER, NODE_FILL, EXTERNAL_FILL, Sheet, _grey  # noqa: E402

W, H = 1720, 850

# Nested layers, outermost first. Each is (title, project, duty).
LAYERS = [
    ("API host", "SIMF.Api",
     "FastEndpoints host, middleware, authentication, policies, "
     "in-process scheduled jobs"),
    ("Infrastructure", "SIMF.Infrastructure",
     "EF Core contexts, storage, e-mail, identity, JWT, audit interceptors"),
    ("Application", "SIMF.Application",
     "Use cases and service abstractions. No ASP.NET and no EF Core"),
    ("Domain", "SIMF.Domain",
     "Entities, aggregates, enums and domain rules"),
]

CLIENTS = [
    ("Mobile application", "Flutter",
     "lib/app, lib/core, lib/features, over simf_data_pkg and simf_auth_pkg"),
    ("Control Panel", "Blazor Server",
     "Interactive server render, cookie auth simf.cp.auth, "
     "tokens held per circuit"),
    ("Public website", "Blazor SSR",
     "Static public content. No sign-in and no personal data"),
]

CONCERNS = [
    ("Authentication", "JWT bearer (RS256), plus a StreamToken scheme"),
    ("Authorisation", "Named policies and a dynamic permission provider"),
    ("Validation", "FluentValidation, a failure becomes HTTP 400"),
    ("Correlation", "CorrelationIdMiddleware enriches every log"),
    ("Error handling", "ErrorHandlingMiddleware returns ApiResult.Fail"),
    ("Logging", "Serilog, correlation enriched"),
    ("Auditing", "Two save-changes interceptors, plus OperationLog"),
    ("Soft delete", "Deactivate flips IsActive; list queries filter on it"),
    ("Rate limiting", "A global per-IP cap plus six named policies"),
]

SHARED = ["SIMF.Common", "SIMF.Contracts", "SIMF.ApiClient", "SIMF.Components"]

LX, LY, LW = 170, 314, 780      # outermost layer frame
INSET_X, INSET_TOP = 34, 62    # how far each layer is inset from its parent
LAYER_BOTTOM = 26


def build():
    sheet = Sheet(
        W, H,
        "SIMF layered solution structure",
        "A modular monolith with domain-driven layering. Each layer encloses "
        "the layer it depends on, so a dependency can only point inward.")

    # ------------------------------------------------------------- clients
    sheet.text(LX, 150, "Clients", 16.5, INK, weight=700)
    cw = (LW - 2 * 18) / 3
    for i, (name, tech, note) in enumerate(CLIENTS):
        x = LX + i * (cw + 18)
        sheet.parts.append(
            f'<rect x="{x}" y="168" width="{cw}" height="86" rx="3" '
            f'fill="{_grey(EXTERNAL_FILL)}" stroke="{_grey(INK)}" '
            f'stroke-width="1.4"/>')
        sheet.text(x + cw / 2, 190, name, 13.5, INK, weight=600,
                   anchor="middle")
        sheet.text(x + cw / 2, 208, tech, 11.5, RULE, anchor="middle")
        words, line, rows = note.split(), "", []
        for word in words:
            trial = (line + " " + word).strip()
            if len(trial) > 40:
                rows.append(line)
                line = word
            else:
                line = trial
        rows.append(line)
        for r, row in enumerate(rows[:2]):
            sheet.text(x + cw / 2, 228 + r * 15, row, 10.5, INK,
                       anchor="middle")

    # Every client reaches the API over the same typed client.
    sheet.path([(LX + LW / 2, 254), (LX + LW / 2, LY - 4)],
               "HTTPS, through the typed client SIMF.ApiClient",
               label_at=(LX + LW / 2, 286), label_dy=0)

    # -------------------------------------------------------------- layers
    x, y, w = LX, LY, LW
    height = 400
    for i, (title, project, duty) in enumerate(LAYERS):
        fill = NODE_FILL if i else PAPER
        sheet.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{height}" rx="5" '
            f'fill="{_grey(fill)}" stroke="{_grey(INK)}" stroke-width="1.6"/>')
        sheet.text(x + 16, y + 26, title, 15, INK, weight=700)
        sheet.text(x + w - 16, y + 26, project, 12.5, RULE, anchor="end")
        sheet.text(x + 16, y + 45, duty, 11.5, INK)
        x += INSET_X
        y += INSET_TOP
        w -= 2 * INSET_X
        height -= INSET_TOP + LAYER_BOTTOM

    # The rule, drawn down the left margin.
    sheet.path([(LX - 34, LY + 384), (LX - 34, LY + 40)], "")
    for r, row in enumerate(("dependencies", "point inward", "only")):
        sheet.text(LX - 46, LY + 176 + r * 16, row, 11.5, INK, anchor="end")

    # ----------------------------------------------------- cross-cutting bar
    bx, by, bw = 990, 150, 372
    sheet.parts.append(
        f'<rect x="{bx}" y="{by}" width="{bw}" height="{78 + len(CONCERNS) * 40 + 12}" '
        f'rx="5" fill="{_grey(PAPER)}" stroke="{_grey(INK)}" '
        f'stroke-width="1.6"/>')
    sheet.text(bx + 16, by + 28, "Cross-cutting concerns", 15, INK, weight=700)
    sheet.text(bx + 16, by + 48,
               "Implemented once, applied across every context.", 11.5, RULE)
    cy = by + 78
    for name, how in CONCERNS:
        sheet.text(bx + 16, cy, name, 12.5, INK, weight=600)
        sheet.text(bx + 16, cy + 16, how, 10.5, INK)
        cy += 40

    # ---------------------------------------------------------- shared libs
    sx, sw = 1394, 268
    sheet.parts.append(
        f'<rect x="{sx}" y="{by}" width="{sw}" height="228" rx="5" '
        f'fill="{_grey(PAPER)}" stroke="{_grey(INK)}" stroke-width="1.6"/>')
    sheet.text(sx + 16, by + 28, "Shared libraries", 15, INK, weight=700)
    sheet.text(sx + 16, by + 48, "Referenced by every layer", 11.5, RULE)
    for i, lib in enumerate(SHARED):
        sheet.parts.append(
            f'<rect x="{sx + 16}" y="{by + 64 + i * 38}" width="{sw - 32}" '
            f'height="30" rx="3" fill="{_grey(NODE_FILL)}" '
            f'stroke="{_grey(INK)}" stroke-width="1.2"/>')
        sheet.text(sx + sw / 2, by + 84 + i * 38, lib, 12.5, INK,
                   anchor="middle")

    # ------------------------------------------------------------ real time
    sheet.note(sx, by + 250, sw, [
        "Live updates are polled over REST",
        "on a bounded 30-second interval,",
        "conditional and cache-answered.",
        "",
        "No server-push transport ships in",
        "this build.",
    ], "Real time")

    sheet.note(sx, by + 414, sw, [
        "Two physically separate SQL Server",
        "2022 databases, reached only through",
        "Infrastructure:",
        "",
        "SIMF_Identity, identity and access.",
        "SIMF_App, everything else.",
        "",
        "No cross-database foreign key, join",
        "or transaction.",
    ], "Persistence")

    out = os.path.join(
        os.path.dirname(os.path.dirname(os.path.dirname(
            os.path.abspath(__file__)))),
        "docs", "diagrams", "SIMF-Fig12-Solution-Layers")
    sheet.save(out)


if __name__ == "__main__":
    build()
