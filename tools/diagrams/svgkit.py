"""Shared SVG primitives for the SIMF engineering diagrams.

Three sheets are built on top of this module:

    fig1_deployment.py   UML deployment diagram (servers, zones, firewalls)
    fig2_component.py    UML component diagram (C4 container discipline)
    fig3_dataflow.py     Gane and Sarson data flow diagram

Notation rules this module enforces, so a sheet cannot drift from them:

  * every element carries a type (a stereotype or a shape) and a technology line
  * every communication line is unidirectional and carries a label
  * every sheet has a title and a legend
  * colour is one value per element type, never per box, and the palette is
    printer friendly; set MONO = True for a pure greyscale render

Regenerate with:  python tools/diagrams/fig1_deployment.py   (and fig2, fig3)
"""

# Flip to True for a pure greyscale render of all three sheets.
MONO = False

FONT = "Segoe UI, Calibri, Arial, sans-serif"

INK = "#1A1A1A"          # text and primary strokes
RULE = "#6B7280"         # secondary strokes, zone frames
FAINT = "#9CA3AF"        # dividers inside a box
ACCENT = "#2F5D8C"       # communication lines, one accent only
PAPER = "#FFFFFF"

EXTERNAL_FILL = "#FFFFFF"   # actors and external systems
NODE_FILL = "#E7EDF4"       # server nodes and components
NODE_TOP = "#D3DDE8"        # the two visible cube faces
STORE_FILL = "#D8D2C8"      # data stores, a clearly different grey when printed
BAND_FILL = "#F7F8FA"       # zone bands
BAR_FILL = "#E3E6EA"        # firewall bars


def _grey(value):
    """Collapse a fill to its greyscale equivalent when MONO is on."""
    if not MONO or not value.startswith("#") or len(value) != 7:
        return value
    r, g, b = (int(value[i:i + 2], 16) for i in (1, 3, 5))
    lum = round(0.299 * r + 0.587 * g + 0.114 * b)
    return "#%02X%02X%02X" % (lum, lum, lum)


def esc(text):
    return (str(text).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))


def text_width(text, size):
    """Rough advance width, good enough to back a line label with paper."""
    return len(str(text)) * size * 0.53


class Sheet:
    """One diagram sheet: a titled canvas that collects SVG fragments."""

    def __init__(self, width, height, title, subtitle=""):
        self.w = width
        self.h = height
        self.parts = []
        self._defs()
        self.parts.append(
            f'<rect x="0" y="0" width="{width}" height="{height}" fill="{PAPER}"/>')
        self.text(40, 46, title, 25, INK, bold=True)
        if subtitle:
            self.text(40, 74, subtitle, 15, RULE)
        self.parts.append(
            f'<line x1="40" y1="90" x2="{width - 40}" y2="90" '
            f'stroke="{_grey(RULE)}" stroke-width="1.2"/>')

    # ---------------------------------------------------------------- defs
    def _defs(self):
        ink, accent = _grey(INK), _grey(ACCENT)
        self.parts.append(f'''<defs>
  <marker id="ar" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7"
          markerHeight="7" orient="auto-start-reverse">
    <path d="M 0 0 L 10 5 L 0 10 z" fill="{accent}"/>
  </marker>
  <marker id="ai" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7"
          markerHeight="7" orient="auto-start-reverse">
    <path d="M 0 0 L 10 5 L 0 10 z" fill="{ink}"/>
  </marker>
  <pattern id="brick" width="20" height="10" patternUnits="userSpaceOnUse">
    <rect width="20" height="10" fill="{_grey(BAR_FILL)}"/>
    <path d="M0 0 H20 M0 5 H20 M10 0 V5 M0 5 V10 M20 5 V10"
          stroke="{_grey(FAINT)}" stroke-width="0.9" fill="none"/>
  </pattern>
</defs>''')

    # ---------------------------------------------------------------- text
    def text(self, x, y, body, size=13, colour=INK, bold=False, anchor="start",
             italic=False):
        weight = ' font-weight="600"' if bold else ""
        style = ' font-style="italic"' if italic else ""
        self.parts.append(
            f'<text x="{x}" y="{y}" font-family="{FONT}" font-size="{size}" '
            f'fill="{_grey(colour)}"{weight}{style} text-anchor="{anchor}">'
            f'{esc(body)}</text>')

    def lines(self, x, y, rows, size=12, colour=INK, step=None, anchor="start"):
        step = step or size + 4
        for i, row in enumerate(rows):
            self.text(x, y + i * step, row, size, colour, anchor=anchor)

    # --------------------------------------------------------------- zones
    def band(self, x, y, w, h, label, note=""):
        """A dashed network or grouping band with its name on the top left."""
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="4" '
            f'fill="{_grey(BAND_FILL)}" stroke="{_grey(RULE)}" '
            f'stroke-width="1.2" stroke-dasharray="7 4"/>')
        self.text(x + 14, y + 22, label, 14, RULE, bold=True)
        if note:
            self.text(x + w - 14, y + 22, note, 12, RULE, anchor="end")

    def firewall(self, x, y, w, label, allowed):
        """A firewall drawn as the conventional brick bar, with its rule."""
        h = 30
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" '
            f'fill="url(#brick)" stroke="{_grey(INK)}" stroke-width="1.3"/>')
        self.text(x + 14, y + 20, label, 13.5, INK, bold=True)
        self.text(x + w - 14, y + 20, allowed, 12.5, INK, anchor="end")

    # --------------------------------------------------------------- boxes
    def node(self, x, y, w, h, name, stereotype="server", specs=(), artifacts=()):
        """A UML node: a 3D cube, a stereotype, and a specification compartment."""
        d = 13
        top = f"{x},{y} {x + d},{y - d} {x + w + d},{y - d} {x + w},{y}"
        side = (f"{x + w},{y} {x + w + d},{y - d} "
                f"{x + w + d},{y + h - d} {x + w},{y + h}")
        for pts in (top, side):
            self.parts.append(
                f'<polygon points="{pts}" fill="{_grey(NODE_TOP)}" '
                f'stroke="{_grey(INK)}" stroke-width="1.3"/>')
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" '
            f'fill="{_grey(NODE_FILL)}" stroke="{_grey(INK)}" stroke-width="1.4"/>')

        cy = y + 20
        self.text(x + w / 2, cy, f"«{stereotype}»", 11.5, RULE,
                  anchor="middle")
        cy += 20
        self.text(x + w / 2, cy, name, 15, INK, bold=True, anchor="middle")
        cy += 10
        if specs:
            self.parts.append(
                f'<line x1="{x + 10}" y1="{cy}" x2="{x + w - 10}" y2="{cy}" '
                f'stroke="{_grey(FAINT)}" stroke-width="1"/>')
            cy += 16
            for row in specs:
                self.text(x + w / 2, cy, row, 11.5, INK, anchor="middle")
                cy += 15
        if artifacts:
            cy += 1
            self.parts.append(
                f'<line x1="{x + 10}" y1="{cy - 12}" x2="{x + w - 10}" '
                f'y2="{cy - 12}" stroke="{_grey(FAINT)}" stroke-width="1"/>')
            for row in artifacts:
                self.text(x + w / 2, cy, f"«artifact»  {row}", 11.5,
                          INK, anchor="middle")
                cy += 15

    def box(self, x, y, w, h, name, kind="", tech="", fill=EXTERNAL_FILL,
            dashed=False, component=False):
        """A plain typed box: name, element type, technology."""
        dash = ' stroke-dasharray="6 4"' if dashed else ""
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="3" '
            f'fill="{_grey(fill)}" stroke="{_grey(INK)}" stroke-width="1.4"{dash}/>')
        if component:
            bx, by = x + w - 26, y + 9
            self.parts.append(
                f'<rect x="{bx}" y="{by}" width="17" height="13" '
                f'fill="{PAPER}" stroke="{_grey(INK)}" stroke-width="1.1"/>'
                f'<rect x="{bx - 5}" y="{by + 2}" width="8" height="3.5" '
                f'fill="{PAPER}" stroke="{_grey(INK)}" stroke-width="1.1"/>'
                f'<rect x="{bx - 5}" y="{by + 8}" width="8" height="3.5" '
                f'fill="{PAPER}" stroke="{_grey(INK)}" stroke-width="1.1"/>')
        cy = y + 21
        if kind:
            self.text(x + w / 2, cy, f"«{kind}»", 11.5, RULE,
                      anchor="middle")
            cy += 19
        self.text(x + w / 2, cy, name, 14, INK, bold=True, anchor="middle")
        if tech:
            self.text(x + w / 2, cy + 17, tech, 11.5, INK, anchor="middle")

    def store(self, x, y, w, h, tag, name, tech=""):
        """A Gane and Sarson data store: open ended, with its identifier cell."""
        self.parts.append(
            f'<path d="M {x} {y} H {x + w} M {x} {y + h} H {x + w}" '
            f'stroke="{_grey(INK)}" stroke-width="1.4" fill="none"/>')
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" '
            f'fill="{_grey(STORE_FILL)}" stroke="none"/>')
        self.parts.append(
            f'<path d="M {x} {y} H {x + w} M {x} {y + h} H {x + w} '
            f'M {x} {y} V {y + h} M {x + 42} {y} V {y + h}" '
            f'stroke="{_grey(INK)}" stroke-width="1.4" fill="none"/>')
        self.text(x + 21, y + h / 2 + 5, tag, 14, INK, bold=True, anchor="middle")
        self.text(x + 54, y + (h / 2 - 3 if tech else h / 2 + 5), name, 13.5, INK,
                  bold=True)
        if tech:
            self.text(x + 54, y + h / 2 + 15, tech, 11.5, INK)

    def process(self, x, y, w, h, number, name, tech=""):
        """A Gane and Sarson process: rounded, with a numbered header strip."""
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="9" '
            f'fill="{_grey(NODE_FILL)}" stroke="{_grey(INK)}" stroke-width="1.4"/>')
        self.parts.append(
            f'<path d="M {x} {y + 24} H {x + w}" stroke="{_grey(INK)}" '
            f'stroke-width="1.2"/>')
        self.text(x + 10, y + 17, str(number), 12.5, INK, bold=True)
        cy = y + 44 if tech else y + h / 2 + 12
        self.text(x + w / 2, cy, name, 13.5, INK, bold=True, anchor="middle")
        if tech:
            self.text(x + w / 2, cy + 16, tech, 11.5, INK, anchor="middle")

    # --------------------------------------------------------------- lines
    def path(self, points, label="", colour=ACCENT, dashed=False, label_at=None,
             label_dx=0, label_dy=-7, both=False):
        """A unidirectional communication path, always labelled."""
        d = " ".join(("M" if i == 0 else "L") + f" {px} {py}"
                     for i, (px, py) in enumerate(points))
        marker = "ar" if colour == ACCENT else "ai"
        dash = ' stroke-dasharray="7 4"' if dashed else ""
        start = f' marker-start="url(#{marker})"' if both else ""
        self.parts.append(
            f'<path d="{d}" fill="none" stroke="{_grey(colour)}" '
            f'stroke-width="1.6"{dash} marker-end="url(#{marker})"{start}/>')
        if not label:
            return
        if label_at is None:
            mid = len(points) // 2
            ax, ay = points[mid - 1], points[mid]
            lx, ly = (ax[0] + ay[0]) / 2, (ax[1] + ay[1]) / 2
        else:
            lx, ly = label_at
        lx += label_dx
        ly += label_dy
        tw = text_width(label, 11.5)
        self.parts.append(
            f'<rect x="{lx - tw / 2 - 4}" y="{ly - 11}" width="{tw + 8}" '
            f'height="15" fill="{PAPER}" stroke="none"/>')
        self.text(lx, ly, label, 11.5, INK, anchor="middle")

    # -------------------------------------------------------------- legend
    def legend(self, x, y, w, entries, title="Key"):
        rows = len(entries)
        h = 34 + rows * 21
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="3" '
            f'fill="{PAPER}" stroke="{_grey(RULE)}" stroke-width="1.2"/>')
        self.text(x + 14, y + 22, title, 13.5, INK, bold=True)
        cy = y + 44
        for glyph, meaning in entries:
            gx = x + 16
            if glyph == "node":
                self.parts.append(
                    f'<polygon points="{gx},{cy - 8} {gx + 5},{cy - 13} '
                    f'{gx + 31},{cy - 13} {gx + 26},{cy - 8}" '
                    f'fill="{_grey(NODE_TOP)}" stroke="{_grey(INK)}" stroke-width="1"/>'
                    f'<rect x="{gx}" y="{cy - 8}" width="26" height="12" '
                    f'fill="{_grey(NODE_FILL)}" stroke="{_grey(INK)}" stroke-width="1"/>')
            elif glyph == "box":
                self.parts.append(
                    f'<rect x="{gx}" y="{cy - 9}" width="30" height="13" rx="2" '
                    f'fill="{_grey(EXTERNAL_FILL)}" stroke="{_grey(INK)}" stroke-width="1"/>')
            elif glyph == "comp":
                self.parts.append(
                    f'<rect x="{gx}" y="{cy - 9}" width="30" height="13" rx="2" '
                    f'fill="{_grey(NODE_FILL)}" stroke="{_grey(INK)}" stroke-width="1"/>'
                    f'<rect x="{gx + 20}" y="{cy - 7}" width="7" height="6" '
                    f'fill="{PAPER}" stroke="{_grey(INK)}" stroke-width="0.9"/>')
            elif glyph == "store":
                self.parts.append(
                    f'<rect x="{gx}" y="{cy - 9}" width="30" height="13" '
                    f'fill="{_grey(STORE_FILL)}" stroke="none"/>'
                    f'<path d="M {gx} {cy - 9} H {gx + 30} M {gx} {cy + 4} '
                    f'H {gx + 30} M {gx} {cy - 9} V {cy + 4} M {gx + 11} '
                    f'{cy - 9} V {cy + 4}" stroke="{_grey(INK)}" '
                    f'stroke-width="1" fill="none"/>')
            elif glyph == "process":
                self.parts.append(
                    f'<rect x="{gx}" y="{cy - 9}" width="30" height="13" rx="5" '
                    f'fill="{_grey(NODE_FILL)}" stroke="{_grey(INK)}" stroke-width="1"/>')
            elif glyph == "wall":
                self.parts.append(
                    f'<rect x="{gx}" y="{cy - 9}" width="30" height="13" '
                    f'fill="url(#brick)" stroke="{_grey(INK)}" stroke-width="1"/>')
            elif glyph == "line":
                self.parts.append(
                    f'<path d="M {gx} {cy - 3} H {gx + 28}" stroke="{_grey(ACCENT)}" '
                    f'stroke-width="1.6" marker-end="url(#ar)"/>')
            elif glyph == "dash":
                self.parts.append(
                    f'<path d="M {gx} {cy - 3} H {gx + 28}" stroke="{_grey(ACCENT)}" '
                    f'stroke-width="1.6" stroke-dasharray="6 4" marker-end="url(#ar)"/>')
            elif glyph == "band":
                self.parts.append(
                    f'<rect x="{gx}" y="{cy - 9}" width="30" height="13" rx="2" '
                    f'fill="{_grey(BAND_FILL)}" stroke="{_grey(RULE)}" '
                    f'stroke-width="1" stroke-dasharray="4 3"/>')
            self.text(gx + 44, cy + 1, meaning, 12, INK)
            cy += 21
        return h

    def note(self, x, y, w, rows, title=""):
        h = 20 + len(rows) * 17 + (18 if title else 0)
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="3" '
            f'fill="{PAPER}" stroke="{_grey(RULE)}" stroke-width="1.2"/>')
        cy = y + 21
        if title:
            self.text(x + 14, cy, title, 13, INK, bold=True)
            cy += 20
        self.lines(x + 14, cy, rows, 11.5, INK, step=17)
        return h

    # ---------------------------------------------------------------- save
    def save(self, stem):
        body = "\n".join(self.parts)
        svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" '
               f'height="{self.h}" viewBox="0 0 {self.w} {self.h}">\n{body}\n</svg>\n')
        with open(stem + ".svg", "w", encoding="utf-8") as handle:
            handle.write(svg)
        print("wrote", stem + ".svg")
