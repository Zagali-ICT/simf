"""The building blocks a manual chapter is written from.

One definition, shared by every content module. They were copied into three
files, which is three places for the same thing to drift.

A block is a plain dict with a "t" (type) and an "en"/"ar" pair, so the two
volumes come out of one description and cannot differ in structure.
"""


def t(en, ar):
    """An English/Arabic pair."""
    return {"en": en, "ar": ar}


def p(en, ar):
    return {"t": "p", "en": en, "ar": ar}


def h2(en, ar):
    return {"t": "h2", "en": en, "ar": ar}


def h3(en, ar):
    return {"t": "h3", "en": en, "ar": ar}


def note(en, ar):
    """A quieter paragraph, for a caveat beside the thing it qualifies."""
    return {"t": "note", "en": en, "ar": ar}


def code(en, ar=None):
    """A command. Identical in both volumes unless a comment is translated."""
    return {"t": "code", "en": en, "ar": ar if ar is not None else en}


def bullets(en, ar):
    return {"t": "bullets", "en": en, "ar": ar}


def figure(image, en, ar):
    return {"t": "figure", "image": image, "caption": {"en": en, "ar": ar}}


def table(headers_en, headers_ar, rows_en, rows_ar):
    return {"t": "table",
            "headers": {"en": headers_en, "ar": headers_ar},
            "rows": {"en": rows_en, "ar": rows_ar}}


def pagebreak():
    return {"t": "pagebreak"}
