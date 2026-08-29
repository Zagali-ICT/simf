"""Word building blocks for the SIMF Control Panel operations manual.

python-docx has no first-class right-to-left support, so the Arabic volume is
built by writing the WordprocessingML elements Word actually reads:

  * ``w:bidi``       on a section  -> the page itself runs right-to-left
  * ``w:bidi``       on a paragraph -> the paragraph runs right-to-left
  * ``w:rtl``        on a run       -> the text in that run is right-to-left
  * ``w:bidiVisual`` on a table     -> column order mirrors
  * ``w:rFonts w:cs`` + ``w:szCs``  -> the COMPLEX-SCRIPT font and size, which
    is what Word applies to Arabic text; setting only the Latin font leaves
    Arabic rendering in Word's fallback face.

Every helper here is direction-agnostic and takes ``rtl`` as a flag, so the two
volumes are produced by one code path with one switch rather than by two
diverging generators.
"""

from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Pt

# The Latin face carries headings and body text in the English volume; the
# complex-script face is what Word uses for the Arabic runs. Both ship with
# Windows and with Office on macOS, so neither volume needs a font installed.
LATIN_FONT = "Segoe UI"
ARABIC_FONT = "Simplified Arabic"
MONO_FONT = "Consolas"


# WordprocessingML property containers are ORDERED sequences, so an element
# appended to the end is invalid even when Word tolerates it. These are the
# orders the schema requires for the elements this kit writes; anything not
# listed is appended, which is correct for a trailing element.
_ORDER = {
    "w:rPr": ["w:rStyle", "w:rFonts", "w:b", "w:bCs", "w:i", "w:iCs", "w:caps",
              "w:strike", "w:color", "w:spacing", "w:sz", "w:szCs", "w:u",
              "w:vertAlign", "w:rtl", "w:lang"],
    "w:pPr": ["w:pStyle", "w:keepNext", "w:keepLines", "w:pageBreakBefore",
              "w:numPr", "w:spacing", "w:ind", "w:jc", "w:outlineLvl", "w:rPr",
              "w:bidi"],
    "w:sectPr": ["w:headerReference", "w:footerReference", "w:type", "w:pgSz",
                 "w:pgMar", "w:cols", "w:docGrid"],
}


def _place(parent, element, tag):
    """Insert ``element`` at the position the schema requires."""
    order = _ORDER.get(parent.tag.split("}")[-1] and f"w:{parent.tag.split('}')[-1]}")
    if not order or tag not in order:
        parent.append(element)
        return
    index = order.index(tag)
    for following in order[index + 1:]:
        sibling = parent.find(qn(following))
        if sibling is not None:
            sibling.addprevious(element)
            return
    parent.append(element)


def _child(parent, tag):
    """Return ``parent``'s ``tag`` child, creating it in schema order."""
    existing = parent.find(qn(tag))
    if existing is not None:
        return existing
    created = OxmlElement(tag)
    _place(parent, created, tag)
    return created


def _flag(parent, tag, on=True):
    """Set a boolean WordprocessingML toggle such as ``w:bidi`` on or OFF.

    "Off" is written explicitly as ``w:val="0"`` rather than by removing the
    element. The two are not the same: the generator puts ``w:bidi`` on the
    Normal style, so a paragraph with no toggle of its own INHERITS
    right-to-left. Deleting the toggle therefore leaves the paragraph
    right-to-left, and a route keeps its leading "/" as a neutral character on
    an RTL line - which the bidirectional algorithm lays out at the visual end,
    rendering "/admin/visitors/new" as "admin/visitors/new/".
    """
    existing = _child(parent, tag)
    existing.set(qn("w:val"), "1" if on else "0")


def section_rtl(section, rtl):
    """Make a whole section right-to-left, including its margin mirroring."""
    _flag(section._sectPr, "w:bidi", rtl)


def paragraph_rtl(paragraph, rtl):
    """Mark one paragraph right-to-left and start it on the correct margin."""
    p_pr = paragraph._p.get_or_add_pPr()
    _flag(p_pr, "w:bidi", rtl)
    # LEFT/RIGHT are resolved against the paragraph's own direction once
    # w:bidi is set, so START is expressed by clearing the explicit alignment.
    if paragraph.alignment in (None, WD_ALIGN_PARAGRAPH.LEFT, WD_ALIGN_PARAGRAPH.RIGHT):
        paragraph.alignment = WD_ALIGN_PARAGRAPH.RIGHT if rtl else WD_ALIGN_PARAGRAPH.LEFT


def run_rtl(run, rtl):
    """Mark one run's text right-to-left."""
    _flag(run._r.get_or_add_rPr(), "w:rtl", rtl)


def run_font(run, latin=LATIN_FONT, complex_script=ARABIC_FONT, size_pt=None):
    """Set the Latin AND complex-script face (and size) on one run.

    Word picks the complex-script face for Arabic text and the Latin face for
    the rest, so a run that names only one of the two renders half the manual
    in a fallback font.
    """
    r_pr = run._r.get_or_add_rPr()
    fonts = _child(r_pr, "w:rFonts")
    fonts.set(qn("w:ascii"), latin)
    fonts.set(qn("w:hAnsi"), latin)
    fonts.set(qn("w:cs"), complex_script)
    if size_pt is not None:
        run.font.size = Pt(size_pt)
        sz_cs = _child(r_pr, "w:szCs")
        sz_cs.set(qn("w:val"), str(int(size_pt * 2)))  # half-points


def table_rtl(table, rtl):
    """Mirror a table's column order for a right-to-left page."""
    tbl_pr = table._tbl.tblPr
    _flag(tbl_pr, "w:bidiVisual", rtl)
    table.alignment = WD_TABLE_ALIGNMENT.RIGHT if rtl else WD_TABLE_ALIGNMENT.LEFT


def style_rtl(document, rtl):
    """Apply the direction and fonts to the styles the generator uses.

    Setting it on the style rather than on every run keeps the document small
    and makes Word's own heading navigation render in the right direction.
    """
    for name in ("Normal", "Title", "Caption",
                 "Heading 1", "Heading 2", "Heading 3", "Heading 4"):
        try:
            style = document.styles[name]
        except KeyError:
            continue
        p_pr = style.element.get_or_add_pPr()
        _flag(p_pr, "w:bidi", rtl)
        r_pr = style.element.get_or_add_rPr()
        _flag(r_pr, "w:rtl", rtl)
        fonts = _child(r_pr, "w:rFonts")
        fonts.set(qn("w:ascii"), LATIN_FONT)
        fonts.set(qn("w:hAnsi"), LATIN_FONT)
        fonts.set(qn("w:cs"), ARABIC_FONT)


def add_field(paragraph, instruction):
    """Insert a Word field (TOC, PAGE, NUMPAGES) that Word evaluates on open."""
    run = paragraph.add_run()
    begin = OxmlElement("w:fldChar")
    begin.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = instruction
    separate = OxmlElement("w:fldChar")
    separate.set(qn("w:fldCharType"), "separate")
    end = OxmlElement("w:fldChar")
    end.set(qn("w:fldCharType"), "end")
    for element in (begin, instr, separate, end):
        run._r.append(element)
    return run


def mark_dirty_fields(document):
    """Ask Word to recalculate every field (the TOC) when the file opens.

    Without this the table of contents renders as the placeholder text until
    somebody presses F9, which reads as a broken document.
    """
    _child(document.settings.element, "w:updateFields").set(qn("w:val"), "true")
