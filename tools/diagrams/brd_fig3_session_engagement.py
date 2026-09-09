"""BRD Figure 3. Process 3: Session engagement.

The BRD's three process sheets had no source in the repository; they were
pre-rendered PNGs embedded in the .docx and could not be regenerated. This
script restores a source for the one that had to change.

The sheet it replaces was titled "Process 3: Live session engagement" and its
flow ran on attendee comments, which are FR-706 and FR-707, both withdrawn and
not delivered. Every step below is grounded in a delivered requirement:
FR-703 the question and its recipient, FR-704 the open window and the
hall-arrival gate, FR-705 the moderator's ordering and hiding, and the AI
filter stated in the BRD's engagement narrative.

Style is copied from the two sheets that stay: 1180 px canvas at 160 dpi, title
bar #0D5341, box border #0C5240 on #FFFFFF, gateway #FFF6E0 on #D9A441, and a
#373737 terminator.
"""
import os

W, H = 1180, 1060
BAR, BORDER, GATE_F, GATE_S = "#0D5341", "#0C5240", "#FFF6E0", "#D9A441"
INK, MUTED, LINE, END_F, START_F = "#1A1A1A", "#4E5B58", "#8A9A95", "#373737", "#E3F0EC"
p = []


def esc(t):
    return t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def text(x, y, s, size=15, fill=INK, weight=400, anchor="middle"):
    p.append(f'<text x="{x}" y="{y}" font-family="Segoe UI,Arial,sans-serif" '
             f'font-size="{size}" fill="{fill}" font-weight="{weight}" '
             f'text-anchor="{anchor}">{esc(s)}</text>')


def box(cx, cy, w, h, rows, size=15):
    p.append(f'<rect x="{cx-w/2}" y="{cy-h/2}" width="{w}" height="{h}" rx="10" '
             f'fill="#FFFFFF" stroke="{BORDER}" stroke-width="1.6"/>')
    y0 = cy - (len(rows) - 1) * 11
    for i, r in enumerate(rows):
        text(cx, y0 + i * 22 + 5, r, size)


def gateway(cx, cy, w, h, rows):
    p.append(f'<path d="M {cx} {cy-h/2} L {cx+w/2} {cy} L {cx} {cy+h/2} L {cx-w/2} {cy} Z" '
             f'fill="{GATE_F}" stroke="{GATE_S}" stroke-width="1.6"/>')
    y0 = cy - (len(rows) - 1) * 10
    for i, r in enumerate(rows):
        text(cx, y0 + i * 20 + 5, r, 14)


def circle(cx, cy, r, fill, label, fg):
    p.append(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{fill}" stroke="{BORDER}" stroke-width="1.6"/>')
    text(cx, cy + 5, label, 14, fg, 600)


def arrow(x1, y1, x2, y2, label=None):
    p.append(f'<path d="M {x1} {y1} L {x2} {y2}" stroke="{LINE}" stroke-width="1.6" '
             f'fill="none" marker-end="url(#a)"/>')
    if label:
        mx, my = (x1 + x2) / 2, (y1 + y2) / 2
        p.append(f'<rect x="{mx-22}" y="{my-11}" width="44" height="22" rx="3" '
                 f'fill="#FFFFFF" stroke="{LINE}" stroke-width="1"/>')
        text(mx, my + 5, label, 12.5, INK, 600)


def elbow(x1, y1, x2, y2, label=None):
    p.append(f'<path d="M {x1} {y1} L {x1} {y2} L {x2} {y2}" stroke="{LINE}" '
             f'stroke-width="1.6" fill="none" marker-end="url(#a)"/>')
    if label:
        p.append(f'<rect x="{x1-22}" y="{(y1+y2)/2-11}" width="44" height="22" rx="3" '
                 f'fill="#FFFFFF" stroke="{LINE}" stroke-width="1"/>')
        text(x1, (y1 + y2) / 2 + 5, label, 12.5, INK, 600)


p.append(f'<rect width="{W}" height="{H}" fill="#FBFBFB"/>')
p.append(f'<rect width="{W}" height="46" fill="{BAR}"/>')
text(W / 2, 30, "Process 3: Session engagement", 17, "#FFFFFF", 600)
p.append('<defs><marker id="a" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" '
         f'markerHeight="7" orient="auto"><path d="M 0 0 L 10 5 L 0 10 z" fill="{LINE}"/>'
         '</marker></defs>')

CX, RX = 430, 930
circle(CX, 100, 26, START_F, "Start", INK)
arrow(CX, 126, CX, 162)
box(CX, 196, 380, 56, ["Attendee checks in; session questions open"])
arrow(CX, 224, CX, 264)
box(CX, 298, 380, 56, ["Attendee submits a question to the moderator,",
                       "addressed to the speaker or the host"], 14)
arrow(CX, 326, CX, 372)
gateway(CX, 440, 300, 136, ["Hall carries a geofence?"])
arrow(CX + 150, 440, RX - 150, 440, "No")
box(RX, 440, 300, 70, ["Question accepted;", "presence cannot be verified"], 14)
arrow(CX, 508, CX, 552, "Yes")
box(CX, 586, 380, 56, ["Attendee's hall check-in record required"])
arrow(CX, 614, CX, 654)
box(CX, 688, 380, 56, ["An AI filter tags the question"])
arrow(CX, 716, CX, 756)
box(CX, 790, 380, 56, ["Moderator reviews, reorders or hides"])
arrow(CX, 818, CX, 858)
box(CX, 892, 380, 56, ["Selected questions put to the speaker"])
arrow(CX, 920, CX, 952)
circle(CX, 978, 26, END_F, "End", "#FFFFFF")
p.append(f'<path d="M {RX} 475 L {RX} 688 L {CX+190} 688" stroke="{LINE}" '
         f'stroke-width="1.6" fill="none" marker-end="url(#a)"/>')
p.append(f'<rect x="80" y="1000" width="1020" height="44" rx="8" fill="#F1F6F4" '
         f'stroke="#D7E3DF" stroke-width="1"/>')
text(590, 1028, "The session is presented by embedding the broadcast at the address "
                "an administrator records in the Control Panel.", 13.5, MUTED)

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__)))), "docs", "diagrams", "SIMF-BRD-Fig3-Session-Engagement")
with open(OUT + ".svg", "w", encoding="utf-8") as h:
    h.write(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" '
            f'viewBox="0 0 {W} {H}">\n' + "\n".join(p) + "\n</svg>\n")
print("wrote", OUT + ".svg")
