"""Split EVERY public widget out of a heterogeneous file, then delete it.

For the files CLAUDE.md section 1 calls heterogeneous: the name describes none
of the contents, so there is no widget with a claim to stay. Each goes to its
own snake_case file and the original is removed; the compiler then names every
importer to repoint.

    python .refactor/split_all_widgets.py <file>...

Refuses a file with a private top-level symbol, because Dart privacy is
per-file: a private helper left behind is invisible to the widget that moved,
and that is exactly what broke the earlier batch attempt.
"""

import io
import os
import re
import sys

CLASS = re.compile(r'^class (\w+) extends ')
PRIVATE_TOP = re.compile(
    r'^(?:const |final |[A-Za-z_][\w<>?, ]*\s+)_\w+\s*[({=]')


def snake(name):
    return re.sub(r'(?<!^)(?=[A-Z])', '_', name).lower()


def split_all(src):
    raw = io.open(src, encoding='utf-8', newline='').read()
    eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
    lines = raw.replace('\r\n', '\n').split('\n')

    private = [l for l in lines if PRIVATE_TOP.match(l)]
    if private:
        print('  REFUSED (private top-level symbol) %s' % src)
        for p in private:
            print('     ', p.strip()[:78])
        return []

    imports, i = [], 0
    while i < len(lines):
        if lines[i].startswith('import '):
            stmt = [lines[i]]
            while not stmt[-1].rstrip().endswith(';'):
                i += 1
                stmt.append(lines[i])
            imports.append('\n'.join(stmt))
        i += 1
    imports = sorted(set(imports))

    starts = [(n, CLASS.match(l).group(1))
              for n, l in enumerate(lines) if CLASS.match(l)]
    if len(starts) < 2:
        print('  skip (one widget) %s' % src)
        return []

    def doc_start(idx):
        j = idx - 1
        while j >= 0 and lines[j].lstrip().startswith('//'):
            j -= 1
        return j + 1

    made = []
    folder = os.path.dirname(src)
    for n, (idx, name) in enumerate(starts):
        a = doc_start(idx)
        b = doc_start(starts[n + 1][0]) if n + 1 < len(starts) else len(lines)
        body = '\n'.join(lines[a:b]).strip('\n')
        out = os.path.join(folder, snake(name) + '.dart').replace(os.sep, '/')
        io.open(out, 'w', encoding='utf-8', newline='').write(
            ('\n'.join(imports) + '\n\n' + body + '\n').replace('\n', eol))
        made.append((name, out))
        print('  %-34s -> %s' % (name, out))

    os.remove(src)
    print('  removed %s' % src)
    return made


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    made = []
    for path in sys.argv[1:]:
        made += split_all(path)
    io.open('.refactor/moved_index.txt', 'w', encoding='utf-8').write(
        ''.join('%s\t%s\n' % (n, p) for n, p in made))
    print('widgets moved: %d' % len(made))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
