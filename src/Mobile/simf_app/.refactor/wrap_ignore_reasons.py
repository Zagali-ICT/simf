"""Re-flow the PROSE around an `// ignore:` directive to 80 columns.

    python .refactor/wrap_ignore_reasons.py

`wrap_comments.py` refuses any block containing a directive, and rightly so:
wrapping `// ignore: foo` across two lines silently stops it suppressing
anything. But that refusal takes the directive's REASON down with it, and a
reason is ordinary prose that must still fit in 80 columns.

So this splits a comment block at its directive lines and reflows only the
prose runs between them. A directive line is copied through byte for byte.

Deliberately narrower than `wrap_comments.py`:
  * only `//` blocks that actually contain a directive are considered;
  * a prose run that cannot be wrapped under the limit (an unbreakable token)
    is left exactly as it is rather than half-wrapped.
"""

import io
import os
import re

LIMIT = 80
COMMENT = re.compile(r'^(\s*)(//)(\s*)(.*)$')
DIRECTIVE = re.compile(r'^\s*//\s*ignore(_for_file)?:')


def dart_files(root):
    for base in ('lib', 'test', 'integration_test'):
        for dirpath, _dirs, names in os.walk(os.path.join(root, base)):
            for name in names:
                if name.endswith('.dart'):
                    yield os.path.join(dirpath, name).replace(chr(92), '/')


def wrap(words, prefix, width):
    out, line = [], ''
    for word in words:
        if not line:
            line = word
        elif len(line) + 1 + len(word) <= width:
            line += ' ' + word
        else:
            out.append((prefix + line).rstrip())
            line = word
    if line:
        out.append((prefix + line).rstrip())
    return out


def reflow_block(lines, indent):
    """The block with its prose runs re-wrapped, or None to leave it alone."""
    prefix = indent + '// '
    width = LIMIT - len(prefix)
    if width < 24:
        return None

    out, run = [], []

    def flush():
        if not run:
            return True
        wrapped = wrap(' '.join(run).split(), prefix, width)
        if any(len(l) > LIMIT for l in wrapped):
            return False            # an unbreakable token: refuse the block
        out.extend(wrapped)
        del run[:]
        return True

    for line in lines:
        if DIRECTIVE.match(line):
            if not flush():
                return None
            out.append(line)        # byte for byte
            continue
        run.append(COMMENT.match(line).group(4).rstrip())
    if not flush():
        return None
    return out if out != lines else None


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    blocks, files = 0, 0
    for path in sorted(dart_files(root)):
        raw = io.open(path, encoding='utf-8', newline='').read()
        if 'ignore:' not in raw and 'ignore_for_file:' not in raw:
            continue
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        src = raw.replace('\r\n', '\n').split('\n')

        changed, i = False, 0
        while i < len(src):
            m = COMMENT.match(src[i])
            # Only a comment that OWNS its line; a trailing `foo(); // note`
            # has an indent that is not the comment's.
            if not m or not src[i].strip().startswith('//'):
                i += 1
                continue
            indent = m.group(1)
            end = i
            while (end + 1 < len(src)
                   and src[end + 1].strip().startswith('//')
                   and COMMENT.match(src[end + 1]).group(1) == indent):
                end += 1
            block = src[i:end + 1]
            if (any(DIRECTIVE.match(l) for l in block)
                    and any(len(l) > LIMIT for l in block)):
                out = reflow_block(block, indent)
                if out is not None:
                    src[i:end + 1] = out
                    blocks += 1
                    changed = True
                    end = i + len(out) - 1
            i = end + 1

        if changed:
            io.open(path, 'w', encoding='utf-8', newline='').write(
                '\n'.join(src).replace('\n', eol))
            files += 1
    print('ignore-reason blocks reflowed : %d across %d file(s)' % (blocks, files))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
