# -*- coding: utf-8 -*-
import fitz, sys, os, glob
sys.stdout.reconfigure(encoding='utf-8')

SRC = r"D:\SIMF\System\15-04-2024"
OUT = r"D:\SIMF\System\V1.0.0\docs\_extract"

# find the security standard by keyword match on the directory listing
target = None
for p in glob.glob(os.path.join(SRC, "*.pdf")):
    base = os.path.basename(p)
    if "معيار" in base or "امن" in base or "أمن" in base:
        target = p
        break

if not target:
    print("NOT FOUND. files:")
    for p in glob.glob(os.path.join(SRC, "*")):
        print("  ", os.path.basename(p))
else:
    doc = fitz.open(target)
    parts = []
    for i, page in enumerate(doc, 1):
        parts.append("\n========== PAGE %d ==========\n" % i)
        parts.append(page.get_text("text"))
    full = "\n".join(parts)
    with open(os.path.join(OUT, "security_standard.txt"), "w", encoding="utf-8") as f:
        f.write(full)
    print("%s : %d pages, %d chars" % (os.path.basename(target), doc.page_count, len(full)))
    doc.close()
