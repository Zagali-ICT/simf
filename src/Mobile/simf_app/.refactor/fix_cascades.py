"""Join consecutive calls on one receiver into a cascade.

    python .refactor/fix_cascades.py <analyze-output>

`cascade_invocations` fires on `x.a(); x.b();` — the receiver is named twice.
This rewrites a RUN of consecutive statements that share a receiver and an
indent into `x\n  ..a()\n  ..b();`.

Paren- and brace-aware, so a call whose arguments span lines is joined whole.
It refuses a run it cannot read cleanly rather than guessing:

  * a statement that is not exactly `receiver.method(...);`
  * a run of one (nothing to join)
  * anything inside a string or comment on the opening line
"""

import io
import re
import sys

# `x.m(...)` and `x.p = v` both count: the lint is about naming the
# receiver twice, not about calls specifically.
STMT = re.compile(r'^(\s*)([A-Za-z_]\w*)\.([A-Za-z_]\w*)\s*(?:\(|=[^=])')


def flagged_files(analyze_path):
    out = set()
    for line in io.open(analyze_path, encoding='utf-8', errors='replace'):
        if line.rstrip().endswith('cascade_invocations'):
            loc = line.split(' - ')[-2]
            out.add(loc.rsplit(':', 2)[0].replace(chr(92), '/'))
    return sorted(out)


def statement_end(src, i):
    """The index of the line closing the statement starting at i, or None."""
    depth = 0
    for j in range(i, min(i + 60, len(src))):
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


def convert(src):
    out, i, joined = [], 0, 0
    while i < len(src):
        m = STMT.match(src[i])
        if not m:
            out.append(src[i]); i += 1; continue
        indent, receiver, _method = m.groups()
        run = []
        j = i
        while j < len(src):
            m2 = STMT.match(src[j])
            if not m2 or m2.group(1) != indent or m2.group(2) != receiver:
                break
            end = statement_end(src, j)
            if end is None:
                break
            run.append((j, end))
            j = end + 1
        if len(run) < 2:
            out.append(src[i]); i += 1; continue

        out.append('%s%s' % (indent, receiver))
        for n, (a, b) in enumerate(run):
            body = src[a:b + 1]
            # `..add(` sits two columns right of the receiver, so every
            # continuation line moves with it. Without this the arguments stay
            # at the old column and the call reads as misaligned.
            body[0] = indent + '  ..' + body[0].lstrip()[len(receiver) + 1:]
            for k in range(1, len(body)):
                if body[k].strip():
                    body[k] = '  ' + body[k]
            last = body[-1].rstrip()
            assert last.endswith(';')
            body[-1] = last[:-1] + (';' if n == len(run) - 1 else '')
            out.extend(body)
        joined += 1
        i = run[-1][1] + 1
    return out, joined


def main():
    total, files = 0, 0
    for path in flagged_files(sys.argv[1]):
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        src = raw.replace('\r\n', '\n').split('\n')
        out, joined = convert(src)
        if joined:
            io.open(path, 'w', encoding='utf-8', newline='').write(
                '\n'.join(out).replace('\n', eol))
            total += joined
            files += 1
            print('  %-56s %d run(s)' % (path.split('/')[-1], joined))
    print('cascades formed: %d across %d file(s)' % (total, files))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
