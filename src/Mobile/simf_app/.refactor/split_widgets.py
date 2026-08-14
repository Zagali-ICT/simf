"""One public widget per file (CLAUDE.md section 1).

Splits every public widget class out of a file except the one the file is named
for, taking its doc comment and its private `State` class with it.

    python .refactor/split_widgets.py <file>...

Prints what it moved and what it refused. It refuses rather than guesses when a
file has no class matching its own name, because picking the "main" widget of
such a file is a judgement call.

Importers are NOT rewritten here: run `flutter analyze` afterwards and let the
compiler name every one, which is the same loop `named_bool_pass.py` uses.
"""

import io
import os
import re
import sys

WIDGET = re.compile(
    r'^class\s+([A-Z]\w*)\s+extends\s+'
    r'(?:StatelessWidget|StatefulWidget|ConsumerWidget|ConsumerStatefulWidget)\b')
STATE = re.compile(r'^class\s+_(\w*?)State\s+extends\s+'
                   r'(?:ConsumerState|State)<(\w+)>')
ANY_TOP = re.compile(r'^(?:class|enum|mixin|extension|abstract|final|sealed)\b'
                     r'|^[A-Za-z_][\w<>?, ]*\s+\w+\s*[({=]')


def snake(name):
    return re.sub(r'(?<!^)(?=[A-Z])', '_', name).lower()


def doc_start(lines, idx):
    i = idx - 1
    while i >= 0 and (lines[i].lstrip().startswith('///')
                      or lines[i].lstrip().startswith('//')
                      or lines[i].strip().startswith('@')):
        i -= 1
    return i + 1


def top_level_blocks(lines):
    """(start, end, kind, name) for every top-level declaration."""
    marks = []
    for i, l in enumerate(lines):
        m = WIDGET.match(l)
        if m:
            marks.append((i, 'widget', m.group(1)))
            continue
        m = STATE.match(l)
        if m:
            marks.append((i, 'state', m.group(2)))
            continue
        if ANY_TOP.match(l) and not l.startswith(' '):
            marks.append((i, 'other', None))
    out = []
    for n, (i, kind, name) in enumerate(marks):
        start = doc_start(lines, i)
        end = doc_start(lines, marks[n + 1][0]) if n + 1 < len(marks) else len(lines)
        out.append((start, end, kind, name))
    return out


def imports_of(lines):
    out, i = [], 0
    while i < len(lines):
        if lines[i].startswith('import '):
            stmt = [lines[i]]
            while not stmt[-1].rstrip().endswith(';'):
                i += 1
                stmt.append(lines[i])
            out.append('\n'.join(stmt))
        i += 1
    return sorted(set(out))


def split(path):
    raw = io.open(path, encoding='utf-8', newline='').read()
    eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
    lines = raw.replace('\r\n', '\n').split('\n')

    blocks = top_level_blocks(lines)
    widgets = [b for b in blocks if b[2] == 'widget']
    if len(widgets) < 2:
        print('  skip (one widget)      %s' % path)
        return []

    own = os.path.basename(path)[:-len('.dart')]
    keep = [w for w in widgets if snake(w[3]) == own]
    if not keep:
        print('  REFUSED (no class matches the file name) %s -- %s'
              % (path, ', '.join(w[3] for w in widgets)))
        return []
    keep_name = keep[0][3]

    imports = imports_of(lines)
    # The moved widget almost always still uses something left behind (the
    # sibling widget it composes, an enum, a private helper), so it imports the
    # file it came from. One-way: the source is not made to depend on the split.
    pkg = 'package:simf_app/' + path[len('lib/'):]
    imports = sorted(set(imports + ["import '%s';" % pkg]))
    moved, drop = [], []
    for start, end, _kind, name in widgets:
        if name == keep_name:
            continue
        spans = [(start, end)]
        for s2, e2, k2, n2 in blocks:
            if k2 == 'state' and n2 == name:
                spans.append((s2, e2))
        body = '\n\n'.join('\n'.join(lines[a:b]).strip('\n') for a, b in spans)
        out = os.path.join(os.path.dirname(path), snake(name) + '.dart')
        io.open(out, 'w', encoding='utf-8', newline='').write(
            ('\n'.join(imports) + '\n\n' + body + '\n').replace('\n', eol))
        moved.append((name, out.replace('\\', '/')))
        drop.extend(spans)

    kept = [l for i, l in enumerate(lines)
            if not any(a <= i < b for a, b in drop)]
    text = re.sub(r'\n{3,}', '\n\n', '\n'.join(kept)).rstrip() + '\n'
    io.open(path, 'w', encoding='utf-8', newline='').write(text.replace('\n', eol))
    for name, out in moved:
        print('  moved %-32s -> %s' % (name, out))
    return moved


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    total = 0
    for path in sys.argv[1:]:
        total += len(split(path))
    print('widgets moved:', total)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())


# --- step two: point the importers at the new files -------------------------

MOVED_RE = re.compile(
    r"(?:The method|Undefined class|Undefined name|The name) '(\w+)'")


def fix_importers(analyze_path, index_path):
    """Add the new import wherever the compiler says a moved class is unknown.

    `index_path` is a `ClassName<TAB>lib/path.dart` map written by the split.
    """
    index = {}
    for line in io.open(index_path, encoding='utf-8'):
        name, target = line.rstrip('\n').split('\t')
        index[name] = 'package:simf_app/' + target[len('lib/'):]

    need = {}
    for line in io.open(analyze_path, encoding='utf-8', errors='replace'):
        if ' - error - ' not in line and not line.lstrip().startswith('error -'):
            continue
        m = MOVED_RE.search(line)
        if not m or m.group(1) not in index:
            continue
        loc = line.split(' - ')[-2]
        path = loc.rsplit(':', 2)[0].replace('\', '/')
        need.setdefault(path, set()).add(index[m.group(1)])

    for path in sorted(need):
        text = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if text.count('\r\n') * 2 > text.count('\n') else '\n'
        lines = text.replace('\r\n', '\n').split('\n')
        adds = [i for i in sorted(need[path])
                if "import '%s';" % i not in text]
        if not adds:
            continue
        idx = [n for n, l in enumerate(lines) if l.startswith('import ')]
        at = idx[-1] + 1 if idx else 0
        for i in adds:
            lines.insert(at, "import '%s';" % i)
        io.open(path, 'w', encoding='utf-8', newline='').write(
            '\n'.join(lines).replace('\n', eol))
        print('  import(s) added to', path)
    return len(need)
