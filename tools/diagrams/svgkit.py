"""Shared SVG primitives for the SIMF engineering diagrams.

Four sheets are built on top of this module:

    fig1_deployment.py      UML deployment diagram (servers, zones, firewalls)
    fig2_component.py       UML component diagram (C4 container discipline)
    fig3_dataflow.py        Gane and Sarson data flow diagram
    fig4_tier_separation.py UML deployment diagram, the target three tiers

Notation rules this module enforces, so a sheet cannot drift from them:

  * every element carries a type (a stereotype or a shape) and a technology line
  * every communication line is unidirectional and carries a label
  * every sheet has a title and a legend
  * colour is one value per element type, never per box, and the palette is
    printer friendly; set MONO = True for a pure greyscale render
  * a zone name is set in ink at weight 700, heavier than anything inside the
    zone, so the reader finds the layer before the boxes
  * a band's wording is moved out of a path's way (`zone_pad`); a firewall's and
    a security area's is drawn over it (`late`). The difference is deliberate:
    a band's interior is where paths run, so patching there would sever a line,
    while a bar or a frame spans the sheet and must be crossed.

Regenerate with:  python tools/diagrams/fig1_deployment.py   (and fig2, 3, 4)
"""

# Flip to True for a pure greyscale render of all four sheets.
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

CUBE_D = 13                 # depth of the two visible faces of a node cube
BAR_H = 30                  # height of a firewall bar

# A sheet that attaches a path to a node's right face, or routes one through the
# gap below a firewall, needs these two. Read them; do not retype the numbers.


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

    def __init__(self, width, height, title, subtitle="", zone_pad=14):
        self.w = width
        self.h = height
        # How far a zone's name and note are inset from its edges. One sheet-wide
        # typographic fact, not a per-call choice: a sheet that runs a path up
        # the inside of its zones raises this once to open the channel, and a
        # ragged inset would read as a mistake.
        self.zone_pad = zone_pad
        self.parts = []
        # Drawn after everything in `parts`. Firewall bar text lives here: a
        # communication path that has to cross a bar in the middle would
        # otherwise be drawn over the bar's own rule text and strike it through.
        self.late = []
        self._defs()
        self.parts.append(
            f'<rect x="0" y="0" width="{width}" height="{height}" fill="{PAPER}"/>')
        self.text(40, 46, title, 25, INK, weight=600)
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
    def text(self, x, y, body, size=13, colour=INK, anchor="start",
             italic=False, weight=None, into=None):
        weight_attr = f' font-weight="{weight}"' if weight else ""
        style = ' font-style="italic"' if italic else ""
        target = self.parts if into is None else into
        target.append(
            f'<text x="{x}" y="{y}" font-family="{FONT}" font-size="{size}" '
            f'fill="{_grey(colour)}"{weight_attr}{style} text-anchor="{anchor}">'
            f'{esc(body)}</text>')

    def lines(self, x, y, rows, size=12, colour=INK, step=None, anchor="start"):
        step = step or size + 4
        for i, row in enumerate(rows):
            self.text(x, y + i * step, row, size, colour, anchor=anchor)

    # --------------------------------------------------------------- zones
    def band(self, x, y, w, h, label, note=""):
        """A dashed network zone, with its name on the top left.

        A band's interior is where paths legitimately run, so its wording is
        moved out of their way by `self.zone_pad` rather than being drawn over
        them: a backing patch here would sever the line passing behind it.
        """
        pad = self.zone_pad
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="4" '
            f'fill="{_grey(BAND_FILL)}" stroke="{_grey(RULE)}" '
            f'stroke-width="1.2" stroke-dasharray="7 4"/>')
        self.text(x + pad, y + 23, label, 16.5, INK, weight=700)
        if note:
            self.text(x + w - pad, y + 23, note, 12, RULE, anchor="end")
        return h

    def firewall(self, x, y, w, label, allowed):
        """A firewall drawn as the conventional brick bar, with its rule.

        Unlike a band, a bar spans the whole zone and paths MUST cross it, so
        its wording cannot be moved aside and wins on z-order instead: both
        texts are plated into the late layer. Returns the bar's height.
        """
        pad = self.zone_pad
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{BAR_H}" '
            f'fill="url(#brick)" stroke="{_grey(INK)}" stroke-width="1.3"/>')
        # The two plates are appended rect, text, rect, text. That is safe only
        # while they cannot meet, which needs w > 2*pad + both widths + 10.
        self._plate(x + pad, y + 20, label, 14.5, INK, 700, "start", BAR_FILL,
                    self.late)
        self._plate(x + w - pad, y + 20, allowed, 12.5, INK, None, "end",
                    BAR_FILL, self.late)
        return BAR_H

    def _plate(self, x, y, body, size, colour, weight, anchor, fill, into=None):
        """Text on its own backing patch, so a line underneath cannot strike it.

        The patch is the text's ink box, not its line box: cap height above the
        baseline, descender below. A looser patch would reach a neighbouring
        zone border and rub a gap in it.
        """
        tw = text_width(body, size)
        offset = {"start": 0, "middle": tw / 2, "end": tw}[anchor]
        target = self.parts if into is None else into
        target.append(
            f'<rect x="{x - offset - 4}" y="{y - 0.75 * size:.1f}" '
            f'width="{tw + 8}" height="{0.98 * size:.1f}" '
            f'fill="{_grey(fill)}" stroke="none"/>')
        self.text(x, y, body, size, colour, anchor=anchor, weight=weight,
                  into=into)

    def group(self, x, y, w, h, label, note=""):
        """A security area: a solid frame enclosing one or more network zones.

        Plated like a firewall's wording, and for the same reason: a frame this
        wide is crossed by any path leaving the area it encloses.
        """
        pad = self.zone_pad
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="6" '
            f'fill="none" stroke="{_grey(INK)}" stroke-width="2"/>')
        self._plate(x + pad, y + 27, label, 19, INK, 700, "start", PAPER,
                    self.late)
        if note:
            self._plate(x + w - pad, y + 27, note, 12.5, RULE, None, "end",
                        PAPER, self.late)
        return h

    # --------------------------------------------------------------- boxes
    def node(self, x, y, w, h, name, stereotype="server", specs=(), artifacts=()):
        """A UML node: a 3D cube, a stereotype, and a specification compartment."""
        d = CUBE_D
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
        self.text(x + w / 2, cy, name, 15, INK, weight=600, anchor="middle")
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
        self.text(x + w / 2, cy, name, 14, INK, weight=600, anchor="middle")
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
        self.text(x + 21, y + h / 2 + 5, tag, 14, INK, weight=600, anchor="middle")
        self.text(x + 54, y + (h / 2 - 3 if tech else h / 2 + 5), name, 13.5, INK,
                  weight=600)
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
        self.text(x + 10, y + 17, str(number), 12.5, INK, weight=600)
        cy = y + 44 if tech else y + h / 2 + 12
        self.text(x + w / 2, cy, name, 13.5, INK, weight=600, anchor="middle")
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
        self._plate(lx, ly, label, 11.5, INK, None, "middle", PAPER)

    # -------------------------------------------------------------- legend
    def legend(self, x, y, w, entries, title="Key"):
        rows = len(entries)
        h = 34 + rows * 21
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="3" '
            f'fill="{PAPER}" stroke="{_grey(RULE)}" stroke-width="1.2"/>')
        self.text(x + 14, y + 22, title, 13.5, INK, weight=600)
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
            elif glyph == "group":
                self.parts.append(
                    f'<rect x="{gx}" y="{cy - 10}" width="30" height="15" rx="3" '
                    f'fill="none" stroke="{_grey(INK)}" stroke-width="1.6"/>')
            self.text(gx + 44, cy + 1, meaning, 12, INK)
            cy += 21
        return h

    @staticmethod
    def note_height(rows, title=True):
        """A note's height before it is drawn, for a sheet that anchors one to
        its own bottom edge rather than stacking it from the top."""
        return 20 + len(rows) * 17 + (18 if title else 0)

    def note(self, x, y, w, rows, title=""):
        h = self.note_height(rows, bool(title))
        self.parts.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="3" '
            f'fill="{PAPER}" stroke="{_grey(RULE)}" stroke-width="1.2"/>')
        cy = y + 21
        if title:
            self.text(x + 14, cy, title, 13, INK, weight=600)
            cy += 20
        self.lines(x + 14, cy, rows, 11.5, INK, step=17)
        return h

    # ---------------------------------------------------------------- save
    def save(self, stem):
        body = "\n".join(self.parts + self.late)
        svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" '
               f'height="{self.h}" viewBox="0 0 {self.w} {self.h}">\n{body}\n</svg>\n')
        with open(stem + ".svg", "w", encoding="utf-8") as handle:
            handle.write(svg)
        print("wrote", stem + ".svg")
