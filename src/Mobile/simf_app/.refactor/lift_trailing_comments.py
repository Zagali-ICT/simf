"""Lift a trailing `// note` off a declaration into a `///` doc comment above.

    python .refactor/lift_trailing_comments.py <file> [<file> ...]

A trailing comment on a `static const` is the one comment shape
`wrap_comments.py` will not touch: its indentation belongs to the code, not to
the comment, so re-flowing it in place is not possible. Moving it above the
declaration solves both problems at once - the line fits, and the note becomes
a real doc comment the IDE shows on hover.

Comment text only. No declaration, value or name is altered.

Deliberately has NO length gate: it lifts EVERY trailing note in the files it
is handed, not only the over-long ones. Lifting the 57 that overflowed
`tokens.dart` and leaving the other 82 behind would have left one table
documented two different ways, which reads worse than the long lines did.

Refuses, rather than guessing:
  * a line that is not exactly `<indent>static const ... ; // <text>`
  * an analyzer directive (`// ignore:`), which must stay attached to its line
  * a `//` inside a string literal on that line (an unbalanced quote count)
"""

import io
import re
import sys

DECL = re.compile(r'^(\s*)(static const .*?;)\s*//\s*(\S.*?)\s*$')
DIRECTIVE = re.compile(r'^\s*ignore(_for_file)?:')


def has_comment_in_string(code):
    """True when the `//` we matched sits inside a quoted literal."""
    return code.count("'") % 2 or code.count('"') % 2


def main():
    total, files = 0, 0
    for path in sys.argv[1:]:
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        src = raw.replace('\r\n', '\n').split('\n')

        out, lifted = [], 0
        for line in src:
            m = DECL.match(line)
            if not m:
                out.append(line)
                continue
            indent, decl, note = m.groups()
            if DIRECTIVE.match(note) or has_comment_in_string(decl):
                out.append(line)
                continue
            # An existing doc block above absorbs the note as a final line,
            # so the two never end up as two separate comments.
            out.append('%s/// %s' % (indent, note))
            out.append(indent + decl)
            lifted += 1

        if lifted:
            io.open(path, 'w', encoding='utf-8', newline='').write(
                '\n'.join(out).replace('\n', eol))
            total += lifted
            files += 1
            print('  %-40s %d lifted' % (path.split('/')[-1], lifted))
    print('trailing comments lifted : %d across %d file(s)' % (total, files))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
