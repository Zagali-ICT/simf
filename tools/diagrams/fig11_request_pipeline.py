"""Sheet 11: the SIMF API request and response pipeline.

Section 2 of the Solution Design Document describes a fixed middleware pipeline
that every API request passes through, then lists its classes in a table. A
table states membership; it does not show that a pipeline is a two-way channel,
which is the fact a reader most needs: the same stages are traversed on the way
out, and one of them, error handling, exists ONLY to act on the way back.

The sheet is therefore drawn as a descending request path and an ascending
response path over one shared stack of stages, rather than as a left to right
chain, so that the pair reads as one channel.

Stage order is the order section 2.1.4 lists it in. Host configuration that is
applied by the host rather than by a middleware class (the two authentication
schemes, the authorisation policies, the allow-list, the limiters and the health
endpoint) is drawn as a configuration panel beside the stack, because it is not
a stage a request passes through in sequence.

Regenerate with:  python tools/diagrams/fig11_request_pipeline.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from svgkit import INK, RULE, ACCENT, PAPER, NODE_FILL, STORE_FILL, Sheet, _grey  # noqa: E402

W, H = 1560, 1330

COL_X, COL_W = 430, 470       # the stage stack
TOP = 214
STAGE_H = 62
STEP = 78

REQ_X = COL_X - 52            # the descending request path
RES_X = COL_X + COL_W + 74    # the ascending response path

# (class or stage, what it does). Order as section 2.1.4 lists it.
STAGES = [
    ("CorrelationIdMiddleware",
     "Read or generate X-Correlation-Id, enrich logs"),
    ("SecurityHeadersMiddleware",
     "Apply the baseline security response headers"),
    ("ErrorHandlingMiddleware",
     "Convert an exception to ApiResult.Fail with its status"),
    ("EmailRateLimitKeyMiddleware",
     "Extract the e-mail from a credential body, to throttle per e-mail"),
    ("SwaggerBasicAuthMiddleware",
     "Gate the OpenAPI user interface in production"),
]


class Pipe(Sheet):

    def stage(self, y, name, duty, fill=NODE_FILL):
        self.parts.append(
            f'<rect x="{COL_X}" y="{y}" width="{COL_W}" height="{STAGE_H}" '
            f'rx="4" fill="{_grey(fill)}" stroke="{_grey(INK)}" '
            f'stroke-width="1.4"/>')
        self.text(COL_X + COL_W / 2, y + 25, name, 14, INK, weight=600,
                  anchor="middle")
        self.text(COL_X + COL_W / 2, y + 45, duty, 11.5, INK, anchor="middle")

    def arrow(self, x, y1, y2, label="", side="start"):
        self.parts.append(
            f'<path d="M {x} {y1} V {y2}" fill="none" '
            f'stroke="{_grey(ACCENT)}" stroke-width="1.8" '
            f'marker-end="url(#ar)"/>')
        if label:
            self.text(x + (-10 if side == "end" else 10), (y1 + y2) / 2 + 4,
                      label, 11.5, INK, anchor=side)

    def hook(self, x, y, to_x, label):
        """A short spur from a path into the stack, or back out of it."""
        self.parts.append(
            f'<path d="M {x} {y} H {to_x}" fill="none" '
            f'stroke="{_grey(ACCENT)}" stroke-width="1.6" '
            f'marker-end="url(#ar)"/>')
        if label:
            self._plate((x + to_x) / 2, y - 7, label, 11, INK, None, "middle",
                        PAPER)


def build():
    sheet = Pipe(
        W, H,
        "SIMF API request and response pipeline",
        "Every request descends the stack and every response ascends it. The "
        "stage order is the order section 2.1.4 lists.")

    # ---------------------------------------------------------------- client
    sheet.box(COL_X, 116, COL_W, 70, "Client", "caller",
              "Mobile application, mobile edge, Control Panel, badge desk")

    ys = [TOP + i * STEP for i in range(len(STAGES))]
    for y, (name, duty) in zip(ys, STAGES):
        sheet.stage(y, name, duty)

    # The three host-applied gates, then the endpoint.
    gate_y = ys[-1] + STEP
    sheet.stage(gate_y, "Rate limiting",
                "Global per-IP cap, plus six named policies", STORE_FILL)
    sheet.stage(gate_y + STEP, "Authentication",
                "JWT bearer (RS256), and a distinct StreamToken scheme",
                STORE_FILL)
    sheet.stage(gate_y + 2 * STEP, "Authorisation",
                "Named policies, and a dynamic permission policy provider",
                STORE_FILL)
    sheet.stage(gate_y + 3 * STEP, "Validation",
                "FluentValidation, a failure becomes HTTP 400 VALIDATION_FAILED",
                STORE_FILL)

    end_y = gate_y + 4 * STEP + 8
    sheet.box(COL_X, end_y, COL_W, 60, "Endpoint", "FastEndpoints",
              "The feature handler for the route", fill=NODE_FILL)

    app_y = end_y + 84
    sheet.box(COL_X, app_y, COL_W, 70, "SIMF.Application", "use case",
              "Service abstractions, no ASP.NET and no EF Core")
    inf_y = app_y + 92
    sheet.box(COL_X, inf_y, COL_W, 70, "SIMF.Infrastructure", "adapter",
              "EF Core contexts, storage, e-mail, identity, tokens")

    db_y = inf_y + 96
    sheet.store(COL_X, db_y, 224, 52, "D1", "SIMF_Identity", "SQL Server 2022")
    sheet.store(COL_X + 246, db_y, 224, 52, "D2", "SIMF_App",
                "SQL Server 2022")

    # ---------------------------------------------------------- request path
    sheet.arrow(REQ_X, 186, TOP + 22, "request")
    for i, y in enumerate(ys):
        sheet.hook(REQ_X, y + 22, COL_X - 4, "")
        if i + 1 < len(ys):
            sheet.arrow(REQ_X, y + 26, ys[i + 1] + 18)
    sheet.arrow(REQ_X, ys[-1] + 26, gate_y + 18)
    for k in range(4):
        y = gate_y + k * STEP
        sheet.hook(REQ_X, y + 22, COL_X - 4, "")
        sheet.arrow(REQ_X, y + 26, y + STEP + 18) if k < 3 else None
    sheet.arrow(REQ_X, gate_y + 3 * STEP + 26, end_y + 26)
    sheet.hook(REQ_X, end_y + 30, COL_X - 4, "")

    # inward calls, endpoint down to the data
    for a, b in ((end_y + 60, app_y), (app_y + 70, inf_y),
                 (inf_y + 70, db_y)):
        sheet.parts.append(
            f'<path d="M {COL_X + COL_W / 2} {a} V {b}" fill="none" '
            f'stroke="{_grey(INK)}" stroke-width="1.6" '
            f'marker-end="url(#ai)"/>')

    # --------------------------------------------------------- response path
    sheet.parts.append(
        f'<path d="M {COL_X + COL_W} {end_y + 30} H {RES_X} V {158}" '
        f'fill="none" stroke="{_grey(ACCENT)}" stroke-width="1.8" '
        f'stroke-dasharray="7 4" marker-end="url(#ar)"/>')
    sheet.parts.append(
        f'<path d="M {RES_X} {158} H {COL_X + COL_W / 2 + 90}" fill="none" '
        f'stroke="{_grey(ACCENT)}" stroke-width="1.8" stroke-dasharray="7 4" '
        f'marker-end="url(#ar)"/>')
    sheet.text(RES_X + 12, 700, "response, as an", 11.5, INK)
    sheet.text(RES_X + 12, 717, "ApiResult envelope", 11.5, INK)

    # The one stage that does its work on the way back.
    sheet.parts.append(
        f'<path d="M {RES_X} {ys[2] + 40} H {COL_X + COL_W + 4}" '
        f'fill="none" stroke="{_grey(ACCENT)}" stroke-width="1.6" '
        f'stroke-dasharray="7 4" marker-end="url(#ar)"/>')
    sheet._plate((RES_X + COL_X + COL_W) / 2, ys[2] + 33,
                 "an exception becomes ApiResult.Fail", 11, INK, None,
                 "middle", PAPER)

    # ---------------------------------------------------- configuration note
    sheet.note(58, 214, 300, [
        "Applied by the host rather than traversed",
        "as a stage in sequence:",
        "",
        "Two authentication schemes, JWT bearer",
        "as the default and StreamToken for",
        "recording playback.",
        "",
        "Named authorisation policies and a",
        "dynamic permission policy provider over",
        "the permission catalogue.",
        "",
        "An explicit CORS allow-list.",
        "",
        "Per-IP rate limiting and six named",
        "policies: auth, auth-email, ai-test,",
        "ai-assistant, operational and lookup.",
        "",
        "GET /health, which is unauthenticated",
        "and is the probe the load balancer uses.",
    ], "Host configuration")

    sheet.legend(58, 706, 300, [
        ("comp", "A middleware class in the pipeline"),
        ("store", "A gate applied from host configuration"),
        ("line", "Request, descending"),
        ("dash", "Response, ascending"),
    ], "Key")

    out = os.path.join(
        os.path.dirname(os.path.dirname(os.path.dirname(
            os.path.abspath(__file__)))),
        "docs", "diagrams", "SIMF-Fig11-Request-Pipeline")
    sheet.save(out)


if __name__ == "__main__":
    build()
