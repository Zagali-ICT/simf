"""Mark deliberately un-awaited futures with `unawaited(...)`.

    python .refactor/fix_unawaited.py <analyze-output>

`discarded_futures` and `unawaited_futures` fire on a statement that returns a
Future nobody looks at. Every site here is genuinely fire-and-forget —
navigation pushes, animation controllers, dispose calls, semantics
announcements — so the fix is to SAY so rather than to await something the
caller must not wait for.

Paren-aware, so a call whose arguments span lines is wrapped whole. Adds
`dart:async` where the file does not already import it.

Refuses, rather than guessing:
  * a flagged line that is not the start of a bare statement (an assignment, a
    `return`, an argument) — awaiting or not is a real decision there
  * a statement it cannot find the end of
"""

import io
import re
import sys

STMT = re.compile(r'^(\s*)([A-Za-z_][\w.<>?!\[\]]*\()')
BARE = re.compile(r'^\s*(await |return |final |var |const |[\w<>, ]+ \w+ *=)')
IMPORT = "import 'dart:async';"


def flagged(analyze_path):
    out = {}
    for line in io.open(analyze_path, encoding='utf-8', errors='replace'):
        r = line.rstrip()
        if not (r.endswith('discarded_futures') or r.endswith('unawaited_futures')):
            continue
        loc = line.split(' - ')[-2]
        path, lineno, _col = loc.rsplit(':', 2)
        out.setdefault(path.replace(chr(92), '/'), set()).add(int(lineno))
    return out


def statement_end(src, i):
    depth = 0
    for j in range(i, min(i + 40, len(src))):
        for ch in src[j]:
            if ch in '([{':
                depth += 1
            elif ch in ')]}':
                depth -= 1
        if depth == 0 and src[j].rstrip().endswith(';'):
            return j
        if depth < 0:
            return None
    return None


def main():
    todo = flagged(sys.argv[1])
    wrapped, files, skipped = 0, 0, 0
    for path in sorted(todo):
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        src = raw.replace('\r\n', '\n').split('\n')
        touched = False
        for lineno in sorted(todo[path], reverse=True):
            i = lineno - 1
            if i >= len(src):
                continue
            line = src[i]
            if not STMT.match(line) or BARE.match(line) or 'unawaited(' in line:
                skipped += 1
                continue
            end = statement_end(src, i)
            if end is None:
                skipped += 1
                continue
            indent = STMT.match(line).group(1)
            body = src[i:end + 1]
            body[0] = indent + 'unawaited(' + body[0].lstrip()
            for k in range(1, len(body)):
                if body[k].strip():
                    body[k] = '  ' + body[k]
            last = body[-1].rstrip()
            body[-1] = last[:-1] + ');' if last.endswith(';') else last
            src[i:end + 1] = body
            wrapped += 1
            touched = True
        if touched:
            text = '\n'.join(src)
            if IMPORT not in text:
                lines = text.split('\n')
                first = next((n for n, l in enumerate(lines)
                              if l.startswith('import ')), 0)
                lines.insert(first, IMPORT)
                text = '\n'.join(lines)
            io.open(path, 'w', encoding='utf-8', newline='').write(
                text.replace('\n', eol))
            files += 1
    print('futures marked unawaited : %d across %d file(s)' % (wrapped, files))
    print('left for a human         : %d' % skipped)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
