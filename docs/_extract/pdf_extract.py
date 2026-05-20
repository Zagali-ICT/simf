# -*- coding: utf-8 -*-
import fitz, sys, os
sys.stdout.reconfigure(encoding='utf-8')

SRC = r"D:\SIMF\System\15-04-2024"
OUT = r"D:\SIMF\System\V1.0.0\docs\_extract"

files = {
    "مرحلة التحليل والتصميم للملتقى اخر اصدار2.pdf": "analysis_design.txt",
    "معيار أمن تطوير التطبيقات.pdf": "security_standard.txt",
}

for src, dst in files.items():
    path = os.path.join(SRC, src)
    doc = fitz.open(path)
    parts = []
    for i, page in enumerate(doc, 1):
        txt = page.get_text("text")
        parts.append("\n========== PAGE %d ==========\n" % i)
        parts.append(txt)
    full = "\n".join(parts)
    op = os.path.join(OUT, dst)
    with open(op, "w", encoding="utf-8") as f:
        f.write(full)
    print("%s -> %s : %d pages, %d chars" % (src, dst, doc.page_count, len(full)))
    doc.close()
