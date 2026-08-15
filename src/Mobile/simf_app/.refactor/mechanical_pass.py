"""The mechanical half of the per-file loop, for one wave of features.

Closes only the analyzer rows that can be fixed without judgement:

  * `static X fromJson(...)`  ->  `factory X.fromJson(...)`  (decision 2)
  * a trailing BLANK line at end of file (`eol_at_end_of_file`)

`dart fix` handles the rest of the mechanical codes and is run by the caller.

Usage:  python .refactor/mechanical_pass.py <analyze-output> <path-prefix>...

Run from the package root. Reads an existing `flutter analyze` capture rather
than shelling out, so the caller controls when analysis happens.
"""

import io
import re
import sys


def rows_for(analyze_path, rule, prefixes):
    """Every (file, line) the analyzer reported for `rule` under `prefixes`."""
    out = {}
    for line in io.open(analyze_path, encoding='utf-8', errors='replace'):
        if not line.rstrip().endswith(rule):
            continue
        loc = line.split(' - ')[-2]
        path, lineno, _col = loc.rsplit(':', 2)
        path = path.replace('\\', '/')
        if any(path.startswith(p) for p in prefixes):
            out.setdefault(path, []).append(int(lineno))
    return out


def trim_trailing_blank_lines(analyze_path, prefixes):
    """`eol_at_end_of_file` here means a trailing BLANK line, not a missing
    newline. Preserve the file's own line ending: the tree is CRLF."""
    hit = rows_for(analyze_path, 'eol_at_end_of_file', prefixes)
    for path in sorted(hit):
        raw = io.open(path, 'rb').read()
        eol = b'\r\n' if raw.count(b'\r\n') * 2 > raw.count(b'\n') else b'\n'
        io.open(path, 'wb').write(raw.rstrip(b'\r\n') + eol)
    return len(hit)


STATIC_PARSER = re.compile(r'^(\s*)static\s+([A-Za-z_][\w<>, ?]*?)\s+(from\w+)\(')


def statics_to_factories(analyze_path, prefixes):
    """Only safe where the return type IS the enclosing class: a factory cannot
    return null, and an enum cannot have one at all. Those keep `static` and are
    reported so they can be renamed `tryParse` by hand instead."""
    hit = rows_for(analyze_path, 'prefer_constructors_over_static_methods', prefixes)
    converted, skipped = 0, []
    for path in sorted(hit):
        lines = io.open(path, encoding='utf-8', newline='').read().split('\n')
        for lineno in sorted(hit[path]):
            i = lineno - 1
            m = STATIC_PARSER.match(lines[i])
            if not m:
                skipped.append('%s:%d  %s' % (path, lineno, lines[i].strip()[:70]))
                continue
            rtype = m.group(2).strip()
            if rtype.endswith('?'):
                skipped.append('%s:%d  nullable return, cannot be a factory' % (path, lineno))
                continue
            enclosing = _enclosing_class(lines, i)
            if enclosing != rtype:
                skipped.append('%s:%d  returns %s inside %s' % (path, lineno, rtype, enclosing))
                continue
            lines[i] = STATIC_PARSER.sub(
                r'\1factory %s.%s(' % (rtype, m.group(3)), lines[i], count=1)
            converted += 1
        io.open(path, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
    return converted, skipped


CLASS_DECL = re.compile(r'^(?:abstract\s+|final\s+|sealed\s+)*class\s+([A-Za-z_]\w*)')
ENUM_DECL = re.compile(r'^enum\s+([A-Za-z_]\w*)')


def _enclosing_class(lines, idx):
    for j in range(idx, -1, -1):
        m = CLASS_DECL.match(lines[j])
        if m:
            return m.group(1)
        if ENUM_DECL.match(lines[j]):
            return None      # enums forbid factory constructors outright
    return None


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    analyze_path, prefixes = sys.argv[1], sys.argv[2:]
    trimmed = trim_trailing_blank_lines(analyze_path, prefixes)
    converted, skipped = statics_to_factories(analyze_path, prefixes)
    print('trailing blank lines trimmed : %d file(s)' % trimmed)
    print('static parsers -> factory    : %d' % converted)
    if skipped:
        print('left as static, by hand      : %d' % len(skipped))
        for s in skipped:
            print('   ', s)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
