"""Collapse a hand-nested refresh pair into the shared widget.

    python .refactor/collapse_refresh_pairs.py

`SimfRefreshableMessage` (app/widgets/simf_refresh.dart) is DEFINED as exactly
`SimfPullToRefresh(child: SimfPullableHost(child: ...))` - its whole reason for
existing is that screens would otherwise hand-nest the pair at every empty and
error branch. 21 sites hand-nest it anyway, so the shared widget is carrying
the documentation and not the code.

The rewrite is render-identical by construction, because the widget it collapses
to builds the very tree it replaces. Goldens must therefore hold WITHOUT
--update; if one moves, this script is wrong, not the golden.

    SimfPullToRefresh(              SimfRefreshableMessage(
      onRefresh: X,          ->      onRefresh: X,
      child: SimfPullableHost(       child: BODY,
        child: BODY,               ),
      ),
    ),

Paren-aware, so BODY may span any number of lines. Refuses, rather than
guessing:

  * a pair whose arguments are not exactly `onRefresh:` then `child:`
  * a `SimfPullableHost` that is not the immediate child
  * a `child:` whose value starts on the same line (nothing to dedent safely)
  * anything it cannot find the closing paren for
"""

import io
import os
import re

# The widget rarely starts its line - it is usually the body of an arrow, as in
# `error: (_, __) => SimfPullToRefresh(` - so match the line's TAIL and keep
# whatever precedes it. The indent that matters is the argument lines', taken
# from `onRefresh:` below, not this line's.
OUTER = re.compile(r'^(.*?)SimfPullToRefresh\($')
ON_REFRESH = re.compile(r'^(\s*)onRefresh: .*,$')
HOST = re.compile(r'^\s*child: SimfPullableHost\($')


def closing_line(src, line, col):
    """Index of the line closing the bracket that OPENS at `src[line][col]`.

    Scanning must start at that column, not at the start of the line. The
    widget is usually the body of an arrow - `error: (_, __) =>
    SimfPullToRefresh(` - and counting from column 0 sees `(_, __)` open and
    close first, hits depth 0 before the real bracket is even reached, and
    reports the opening line as its own closing line.
    """
    depth = 0
    for j in range(line, min(line + 200, len(src))):
        for ch in (src[j][col:] if j == line else src[j]):
            if ch in '([{':
                depth += 1
            elif ch in ')]}':
                depth -= 1
                if depth == 0:
                    return j
    return None


def collapse(src):
    out, i, done, refused = [], 0, 0, 0
    while i < len(src):
        m = OUTER.match(src[i])
        if not m:
            out.append(src[i])
            i += 1
            continue
        prefix = m.group(1)
        # Expect: onRefresh line, then the host line.
        if i + 2 >= len(src) or not ON_REFRESH.match(src[i + 1]) \
                or not HOST.match(src[i + 2]):
            out.append(src[i])
            i += 1
            refused += 1
            continue
        indent = ON_REFRESH.match(src[i + 1]).group(1)
        # Each bracket is located by column, so an earlier `(` on the same line
        # cannot be mistaken for it.
        host_end = closing_line(src, i + 2, src[i + 2].index('SimfPullableHost'))
        outer_end = closing_line(src, i, len(prefix))
        if host_end is None or outer_end is None or outer_end <= host_end:
            out.append(src[i])
            i += 1
            refused += 1
            continue

        body = src[i + 3:host_end]
        # Every body line loses the two columns SimfPullableHost added.
        dedented = []
        for line in body:
            if not line.strip():
                dedented.append(line)
            elif line.startswith(indent + '  '):
                dedented.append(line[2:])
            else:
                dedented = None
                break
        if dedented is None:
            out.append(src[i])
            i += 1
            refused += 1
            continue

        out.append(prefix + 'SimfRefreshableMessage(')
        out.append(src[i + 1])
        out.extend(dedented)
        # host_end held `),` for the host; outer_end holds the outer close,
        # which survives unchanged.
        out.extend(src[host_end + 1:outer_end + 1])
        done += 1
        i = outer_end + 1
    return out, done, refused


def main():
    total, files, refusals = 0, 0, 0
    for dirpath, _dirs, names in os.walk('lib'):
        for name in sorted(names):
            if not name.endswith('.dart'):
                continue
            path = os.path.join(dirpath, name).replace(chr(92), '/')
            if path.endswith('app/widgets/simf_refresh.dart'):
                continue        # the definition itself
            raw = io.open(path, encoding='utf-8', newline='').read()
            if 'SimfPullableHost' not in raw:
                continue
            eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
            src = raw.replace('\r\n', '\n').split('\n')
            out, done, refused = collapse(src)
            refusals += refused
            if done:
                io.open(path, 'w', encoding='utf-8', newline='').write(
                    '\n'.join(out).replace('\n', eol))
                total += done
                files += 1
                print('  %-52s %d collapsed' % (path.split('/')[-1], done))
    print('pairs collapsed : %d across %d file(s)' % (total, files))
    print('left for a human: %d' % refusals)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
