"""Figure 3. SIMF system and data flow.

Data flow diagram in Gane and Sarson notation: a level 0 context view, and a
level 1 decomposition that balances with it. Processes follow the module list in
SIMF-LLD-002 section 5; the data stores are the two databases and the file store
from section 6.

The three third-party services appear HERE and not on the component sheet. They
are outbound calls to public URLs, not components of this solution, so what
matters about them is the data that crosses the boundary, which is a data-flow
question. Every flow to one is labelled with the payload it carries (owner
decision, 2026-08-10).
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from svgkit import Sheet  # noqa: E402

OUT = r"d:\SIMF\System\V1.0.0\docs\diagrams\SIMF-Fig3-Data-Flow"

s = Sheet(1660, 1200,
          "SIMF system and data flow",
          "Data flow diagram, Gane and Sarson notation.  Level 1 balances with "
          "the level 0 context above it.")

# ------------------------------------------------------------- level 0
s.band(40, 110, 1580, 220, "Level 0, context")
s.box(80, 150, 220, 60, "Attendee", "entity")
s.box(80, 225, 220, 60, "Administrator", "entity")
s.process(700, 155, 300, 110, "0", "SIMF platform")
s.box(1340, 140, 220, 52, "E-mail relay", "entity")
s.box(1340, 202, 220, 52, "Video platform", "entity")
s.box(1340, 264, 220, 52, "AI service", "entity")

s.path([(300, 180), (700, 180)], "requests")
s.path([(700, 205), (300, 205)], "responses and notices")
s.path([(300, 255), (700, 255)], "decisions and content")
s.path([(1000, 166), (1340, 166)], "notifications", label_at=(1170, 166))
s.path([(1000, 215), (1250, 215), (1250, 228), (1340, 228)],
       "live stream, summary video", label_at=(1170, 242))
s.path([(1000, 250), (1200, 250), (1200, 290), (1340, 290)],
       "text for AI processing", label_at=(1270, 290))

# ------------------------------------------------------------- level 1
s.band(40, 340, 1580, 660, "Level 1, decomposition")

s.box(40, 380, 240, 130, "Attendee", "entity")
s.box(40, 540, 240, 110, "Administrator", "entity",
      "and Scientific Committee")

s.process(400, 370, 420, 80, "1", "Registration and approval",
          "sign up, review, badge issue")
s.process(400, 470, 420, 80, "2", "Authentication and access",
          "sign in, second factor, permissions")
s.process(400, 570, 420, 80, "3", "Programme and sessions",
          "agenda, speakers, live session")
s.process(400, 670, 420, 80, "4", "Bookings and attendance",
          "seat reservation, gate scan")
s.process(400, 770, 420, 80, "5", "Engagement and feedback",
          "questions, comments, ratings")
s.process(400, 860, 420, 80, "6", "Content and notifications",
          "news, media, notices")

# The right column carries three stores and three external entities. It is spaced
# to the band rather than to the stores alone: adding the AI service without
# re-spacing would have pushed the last entity past the band's lower edge.
s.store(1000, 375, 460, 58, "D1", "SIMF_Identity", "accounts, roles, tokens")
s.store(1000, 495, 460, 58, "D2", "SIMF_App", "programme, bookings, engagement")
s.store(1000, 615, 460, 58, "D3", "File share", "stored files")

s.box(1000, 740, 460, 62, "Video platform", "entity", "YouTube")
s.box(1000, 818, 460, 62, "E-mail relay", "entity", "internal SMTP")
s.box(1000, 896, 460, 62, "AI service", "entity", "Gemini API")

# ------------------------------------------------------- entity to process
s.path([(400, 390), (280, 390)], "badge, approval notice", label_at=(340, 383))
s.path([(280, 415), (400, 415)], "registration", label_at=(340, 408))
s.path([(280, 445), (370, 445), (370, 510), (400, 510)], "credentials",
       label_at=(325, 438))
s.path([(280, 470), (355, 470), (355, 710), (400, 710)], "seat reservation",
       label_at=(340, 463))
s.path([(280, 495), (340, 495), (340, 810), (400, 810)], "question, rating",
       label_at=(340, 488))
s.path([(280, 560), (385, 560), (385, 430), (400, 430)], "approval decision",
       label_at=(340, 553))
s.path([(280, 590), (365, 590), (365, 610), (400, 610)], "programme",
       label_at=(325, 583))
s.path([(280, 620), (330, 620), (330, 900), (400, 900)], "content and media",
       label_at=(330, 770))

# -------------------------------------------------------- process to store
s.path([(820, 410), (940, 410), (940, 404), (1000, 404)], "account record",
       label_at=(900, 397))
s.path([(820, 510), (960, 510), (960, 420), (1000, 420)], "credential record",
       label_at=(890, 503))
s.path([(820, 610), (950, 610), (950, 524), (1000, 524)], "session record",
       label_at=(885, 603))
s.path([(820, 710), (935, 710), (935, 540), (1000, 540)], "reservation, scan",
       label_at=(878, 703))
s.path([(820, 810), (925, 810), (925, 552), (1000, 552)], "question, rating",
       label_at=(872, 803))
s.path([(820, 900), (915, 900), (915, 644), (1000, 644)], "media file",
       label_at=(868, 893))

# ----------------------------------------------------- process to external
# What actually crosses the boundary to each third party, named rather than
# implied. These labels are the whole reason the three appear on this sheet.
s.path([(820, 640), (985, 640), (985, 770), (1000, 770)],
       "live session stream, summary video", label_at=(893, 662))
s.path([(820, 925), (975, 925), (975, 848), (1000, 848)], "notifications",
       label_at=(963, 880))
s.path([(820, 835), (895, 835), (895, 918), (1000, 918)],
       "subtitles, summary, questions", label_at=(886, 766))
s.path([(820, 935), (970, 935), (970, 940), (1000, 940)], "FAQ and support text",
       label_at=(893, 962))

# ------------------------------------------------------------- apparatus
s.legend(40, 1030, 760, [
    ("process", "Process, numbered"),
    ("store", "Data store, identified D1 to D3"),
    ("box", "External entity"),
    ("line", "Data flow, labelled with the data it carries"),
    ("band", "Diagram level"),
])

s.note(840, 1030, 780, [
    "Processes: the module list in SIMF-LLD-002 section 5.",
    "Data stores: the two databases and the file store, section 6.",
    "Notation: Gane and Sarson.",
    "Every flow crossing the level 0 boundary reappears at level 1.",
    "Payloads to the three third-party services: owner decision, 2026-08-10.",
], "Sources")

s.save(OUT)
