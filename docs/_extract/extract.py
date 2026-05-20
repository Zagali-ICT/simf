# -*- coding: utf-8 -*-
import zipfile, re, sys, os, glob
sys.stdout.reconfigure(encoding='utf-8')

SRC = r"D:\SIMF\System\15-04-2024"
OUT = r"D:\SIMF\System\V1.0.0\docs\_extract"
os.makedirs(OUT, exist_ok=True)

def strip_xml(xml):
    # turn paragraph/row breaks into newlines, drop tags
    xml = re.sub(r'</w:p>', '\n', xml)
    xml = re.sub(r'</a:p>', '\n', xml)
    xml = re.sub(r'<w:tab/>', '\t', xml)
    xml = re.sub(r'<a:tab/>', '\t', xml)
    xml = re.sub(r'<[^>]+>', '', xml)
    return xml

def docx_text(path):
    out = []
    with zipfile.ZipFile(path) as z:
        if 'word/document.xml' in z.namelist():
            out.append(strip_xml(z.read('word/document.xml').decode('utf-8','ignore')))
        for n in sorted(z.namelist()):
            if n.startswith('word/header') or n.startswith('word/footer'):
                out.append(strip_xml(z.read(n).decode('utf-8','ignore')))
    return '\n'.join(out)

def pptx_text(path):
    out = []
    with zipfile.ZipFile(path) as z:
        slides = sorted([n for n in z.namelist() if re.match(r'ppt/slides/slide\d+\.xml$', n)],
                        key=lambda x: int(re.search(r'(\d+)', x).group(1)))
        for s in slides:
            out.append('\n===== %s =====' % s)
            out.append(strip_xml(z.read(s).decode('utf-8','ignore')))
        notes = sorted([n for n in z.namelist() if re.match(r'ppt/notesSlides/notesSlide\d+\.xml$', n)],
                       key=lambda x: int(re.search(r'(\d+)', x).group(1)))
        for s in notes:
            out.append('\n----- NOTES %s -----' % s)
            out.append(strip_xml(z.read(s).decode('utf-8','ignore')))
    return '\n'.join(out)

def xlsx_text(path):
    with zipfile.ZipFile(path) as z:
        shared = []
        if 'xl/sharedStrings.xml' in z.namelist():
            ss = z.read('xl/sharedStrings.xml').decode('utf-8','ignore')
            for m in re.finditer(r'<si>(.*?)</si>', ss, re.S):
                txt = re.sub(r'<[^>]+>', '', m.group(1))
                shared.append(txt)
        out = []
        sheets = sorted([n for n in z.namelist() if re.match(r'xl/worksheets/sheet\d+\.xml$', n)])
        for sh in sheets:
            out.append('\n===== %s =====' % sh)
            data = z.read(sh).decode('utf-8','ignore')
            for row in re.finditer(r'<row[^>]*>(.*?)</row>', data, re.S):
                cells = []
                for c in re.finditer(r'<c[^>]*?(?:\st="([^"]*)")?[^>]*>(?:<v>(.*?)</v>)?', row.group(1)):
                    t, v = c.group(1), c.group(2)
                    if v is None:
                        continue
                    if t == 's':
                        cells.append(shared[int(v)] if v.isdigit() and int(v) < len(shared) else v)
                    else:
                        cells.append(v)
                if cells:
                    out.append(' | '.join(cells))
        return '\n'.join(out)

def pdf_pages(path):
    with open(path,'rb') as f:
        data = f.read()
    c = len(re.findall(rb'/Type\s*/Page[^s]', data))
    m = re.findall(rb'/Count\s+(\d+)', data)
    counts = [int(x) for x in m] if m else []
    return c, (max(counts) if counts else 0)

for f in glob.glob(os.path.join(SRC,'*')):
    name = os.path.basename(f)
    low = name.lower()
    try:
        if low.endswith('.docx'):
            txt = docx_text(f)
        elif low.endswith('.pptx'):
            txt = pptx_text(f)
        elif low.endswith('.xlsx'):
            txt = xlsx_text(f)
        elif low.endswith('.pdf'):
            a,b = pdf_pages(f)
            print('PDF\t%s\tpage-objs=%d\tmax-count=%d' % (name,a,b))
            continue
        else:
            continue
        safe = re.sub(r'[^A-Za-z0-9]+','_', os.path.splitext(name)[0]).strip('_')
        op = os.path.join(OUT, safe + '.txt')
        with open(op,'w',encoding='utf-8') as o:
            o.write(txt)
        print('OK\t%s\t-> %s\t(%d chars)' % (name, safe+'.txt', len(txt)))
    except Exception as e:
        print('ERR\t%s\t%s' % (name, e))
