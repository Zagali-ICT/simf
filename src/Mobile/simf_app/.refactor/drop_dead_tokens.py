"""Delete a named, verified-unused `SimfTokens` declaration and its doc block.

    python .refactor/drop_dead_tokens.py <name> [<name> ...]

Only ever run against names the audit proved have ZERO `SimfTokens.<name>` uses
across `lib`, `test` and `integration_test`, and zero `[<name>]` doc links.
Deleting a token that something still reads is a compile error, not a silent
regression - but a token still named in a doc comment fails `comment_references`
instead, which is why both are checked before this runs.

Removes the declaration, its `///` doc block, and nothing else. Refuses a name
it cannot find exactly once.
"""

import io
import re
import sys

PATH = 'lib/app/theme/tokens.dart'


def main():
    raw = io.open(PATH, encoding='utf-8', newline='').read()
    eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
    src = raw.replace('\r\n', '\n').split('\n')

    dropped, refused = 0, []
    for name in sys.argv[1:]:
        decl = re.compile(r'^\s*static const \w+(?:<[^>]*>)? %s\s*=' % name)
        starts = [i for i, l in enumerate(src) if decl.match(l)]
        if len(starts) != 1:
            refused.append('%s (%d declarations)' % (name, len(starts)))
            continue
        i = starts[0]
        # The statement runs to its terminating `;` at depth 0.
        depth, end = 0, None
        for j in range(i, len(src)):
            for ch in src[j]:
                if ch in '([{':
                    depth += 1
                elif ch in ')]}':
                    depth -= 1
            if depth == 0 and src[j].rstrip().endswith(';'):
                end = j
                break
        if end is None:
            refused.append('%s (no statement end)' % name)
            continue
        # Absorb the doc block above it, if any.
        start = i
        while start - 1 >= 0 and src[start - 1].lstrip().startswith('///'):
            start -= 1
        del src[start:end + 1]
        dropped += 1

    io.open(PATH, 'w', encoding='utf-8', newline='').write(
        '\n'.join(src).replace('\n', eol))
    print('tokens dropped : %d' % dropped)
    for r in refused:
        print('  REFUSED', r)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
