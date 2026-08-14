"""Turn an unresolvable doc link `[Name]` into a code span `` `Name` ``.

    python .refactor/fix_comment_refs.py <analyze-output>

`comment_references` fires when a doc comment links a name the file cannot see.
There are two honest fixes: import the name, or stop pretending it is a link.

Importing is right when the file genuinely uses the symbol. It is wrong for the
cases here — a class-level doc mentioning a method's PARAMETER, or a golden test
naming the widget whose render it describes — where the import would exist only
to satisfy a comment. Those become code spans, which read identically and claim
nothing the analyzer has to verify.

The analyzer does not name the symbol, but it gives the column, so each fix is
anchored to the exact bracket it complained about rather than to every `[…]` on
the line.
"""

import io
import re
import sys

WORD = re.compile(r'[\w.<>]')


def flagged(analyze_path):
    out = {}
    for line in io.open(analyze_path, encoding='utf-8', errors='replace'):
        if not line.rstrip().endswith('comment_references'):
            continue
        loc = line.split(' - ')[-2]
        path, lineno, col = loc.rsplit(':', 2)
        out.setdefault(path.replace(chr(92), '/'), []).append(
            (int(lineno), int(col)))
    return out


def span(text, col):
    """The [Name] bracket around the 1-based `col`, as (start, end, name)."""
    i = col - 1
    if i >= len(text):
        return None
    # The column points inside the brackets; walk out to them.
    start = text.rfind('[', 0, i + 1)
    if start == -1:
        return None
    end = text.find(']', start)
    if end == -1:
        return None
    name = text[start + 1:end]
    if not name or not all(WORD.match(c) for c in name):
        return None
    return start, end, name


def main():
    todo = flagged(sys.argv[1])
    fixed, skipped, files = 0, 0, 0
    for path in sorted(todo):
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        src = raw.replace('\r\n', '\n').split('\n')
        touched = False
        # Right-to-left within a line so earlier columns stay valid.
        for lineno, col in sorted(todo[path], key=lambda r: (-r[0], -r[1])):
            i = lineno - 1
            if i >= len(src) or not src[i].lstrip().startswith('///'):
                skipped += 1
                continue
            found = span(src[i], col)
            if found is None:
                skipped += 1
                continue
            start, end, name = found
            src[i] = src[i][:start] + '`' + name + '`' + src[i][end + 1:]
            fixed += 1
            touched = True
        if touched:
            io.open(path, 'w', encoding='utf-8', newline='').write(
                '\n'.join(src).replace('\n', eol))
            files += 1
    print('doc links turned into code spans : %d across %d file(s)' % (fixed, files))
    print('left alone                       : %d' % skipped)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
