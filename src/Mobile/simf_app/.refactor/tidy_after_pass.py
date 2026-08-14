"""Tidy the two side effects a mechanical pass leaves behind.

    python .refactor/tidy_after_pass.py <analyze-output>

1. `eol_at_end_of_file`: removing a declaration that was the last thing in a
   file leaves a trailing blank line.
2. `lines_longer_than_80_chars` on a rewritten call: the shared name is longer
   than the private one it replaced, so some call sites cross the limit. Only
   wraps a call whose whole argument list is on one line, and only under
   `data/`, so it cannot reflow anything it does not understand.
"""

import io
import re
import sys

SEP = chr(92)   # backslash, kept out of a literal so no heredoc can eat it

CALL = re.compile(
    r'^(\s*)(.*?)pickLocalized(OrNull)?'
    r'\(([^,()]+), ([^,()]+), isArabic: ([^,()]+)\);$')


def rows(path, rule):
    out = set()
    for line in io.open(path, encoding='utf-8', errors='replace'):
        if line.rstrip().endswith(rule):
            loc = line.split(' - ')[-2]
            out.add(loc.replace(SEP, '/'))
    return out


def main():
    analyze = sys.argv[1]

    files = {r.rsplit(':', 2)[0] for r in rows(analyze, 'eol_at_end_of_file')}
    for p in sorted(files):
        raw = io.open(p, 'rb').read()
        eol = b'\r\n' if raw.count(b'\r\n') * 2 > raw.count(b'\n') else b'\n'
        io.open(p, 'wb').write(raw.rstrip(b'\r\n') + eol)
        print('  trimmed %s' % p)

    wrapped = 0
    for loc in sorted(rows(analyze, 'lines_longer_than_80_chars'), reverse=True):
        p, ln, _col = loc.rsplit(':', 2)
        if '/data/' not in p:
            continue
        raw = io.open(p, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        lines = raw.replace('\r\n', '\n').split('\n')
        i = int(ln) - 1
        m = CALL.match(lines[i])
        if not m:
            continue
        ind, pre, orn, a, b, flag = m.groups()
        lines[i] = (
            '%s%spickLocalized%s(' % (ind, pre, orn or '')
            + eol.join([''])
        )
        lines[i] = '%s%spickLocalized%s(' % (ind, pre, orn or '')
        lines.insert(i + 1, '%s  %s,' % (ind, a))
        lines.insert(i + 2, '%s  %s,' % (ind, b))
        lines.insert(i + 3, '%s  isArabic: %s,' % (ind, flag))
        lines.insert(i + 4, '%s);' % ind)
        io.open(p, 'w', encoding='utf-8', newline='').write(
            '\n'.join(lines).replace('\n', eol))
        wrapped += 1
    print('  wrapped %d long call site(s)' % wrapped)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
