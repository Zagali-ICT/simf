"""Re-flow over-long comment blocks, one PARAGRAPH at a time.

    python .refactor/wrap_comments2.py <analyze-output>

`wrap_comments.py` works on whole blocks and refuses a block outright when any
line in it is a list item. That was the right call for a first pass - it never
mangles a deliberate layout - but it leaves the prose paragraphs of a mixed
block unwrapped, which is most of the doc headers in this app.

Two corrections over v1:

  * **Measures in UTF-16 code units, not Python characters.** Dart strings are
    UTF-16 and the analyzer counts that way, so a regional-indicator flag like
    the Saudi one costs 4 units where Python sees 2. v1 measured such a line as
    79 and left it alone while the analyzer called it 81.
  * **Works per paragraph, and wraps list items with a hanging indent.** A
    `/// - **row 2**: ...` item continues as `///   ...`, which is how markdown
    reads a continuation. Only the paragraph that cannot be handled is skipped,
    not its neighbours.

Comment text only; no code is touched. Still refuses, rather than guessing:

  * `// ignore:` / `// ignore_for_file:` - analyzer DIRECTIVES. Wrapping one
    silently stops it suppressing anything.
  * a block holding a fenced sample, a table rule or a trailing-backslash line.
  * a paragraph whose longest word cannot fit (a URL or a long path).
"""

import io
import re
import sys

LIMIT = 80
COMMENT = re.compile(r'^(\s*)(///?)(\s*)(.*)$')
DIRECTIVE = re.compile(r'^\s*//\s*ignore(_for_file)?:')
UNTOUCHABLE = re.compile(r'^\s*///?\s*(?:\||```|>)')
MARKER = re.compile(r'^([-*+]\s+|\d+[.)]\s+)')


def width(text):
    """The analyzer's column count: UTF-16 code units."""
    return len(text.encode('utf-16-le')) // 2


def flagged(analyze_path):
    out = {}
    for line in io.open(analyze_path, encoding='utf-8', errors='replace'):
        if not line.rstrip().endswith('lines_longer_than_80_chars'):
            continue
        loc = line.split(' - ')[-2]
        path, lineno, _col = loc.rsplit(':', 2)
        out.setdefault(path.replace(chr(92), '/'), set()).add(int(lineno))
    return out


def block_bounds(src, i):
    m = COMMENT.match(src[i])
    indent, marker = m.group(1), m.group(2)

    def same(j):
        if j < 0 or j >= len(src):
            return False
        mm = COMMENT.match(src[j])
        return (mm is not None and mm.group(1) == indent
                and mm.group(2) == marker and src[j].strip().startswith('//'))

    start = i
    while same(start - 1):
        start -= 1
    end = i
    while same(end + 1):
        end += 1
    return start, end + 1, indent, marker


def wrap_unit(text, prefix, hang):
    """`text` wrapped to the limit; continuation lines get the `hang` indent."""
    words = text.split()
    if not words:
        return []
    out, line, pre = [], '', prefix
    for word in words:
        if not line:
            line = word
        elif width(pre + line) + 1 + width(word) <= LIMIT:
            line += ' ' + word
        else:
            out.append((pre + line).rstrip())
            pre, line = prefix + hang, word
    out.append((pre + line).rstrip())
    return out


def reflow(lines, indent, marker):
    """The block re-wrapped, or None to leave it exactly as it is."""
    for line in lines:
        if (DIRECTIVE.match(line) or UNTOUCHABLE.match(line)
                or line.rstrip().endswith('\\')):
            return None

    prefix = indent + marker + ' '
    if LIMIT - width(prefix) < 24:
        return None

    texts = [COMMENT.match(l).group(4).rstrip() for l in lines]

    # Group into units: a blank line, a list item (marker plus its
    # continuation lines), or a run of plain prose.
    out, i = [], 0
    while i < len(texts):
        if not texts[i]:
            out.append((indent + marker).rstrip())
            i += 1
            continue
        m = MARKER.match(texts[i])
        if m:
            hang = ' ' * len(m.group(1))
            unit = [texts[i]]
            i += 1
            while i < len(texts) and texts[i] and not MARKER.match(texts[i]):
                unit.append(texts[i])
                i += 1
        else:
            hang = ''
            unit = []
            while i < len(texts) and texts[i] and not MARKER.match(texts[i]):
                unit.append(texts[i])
                i += 1
        out.extend(wrap_unit(' '.join(unit), prefix, hang))

    if any(width(l) > LIMIT for l in out):
        return None                     # an unbreakable token: leave the block
    return out if out != lines else None


def main():
    todo = flagged(sys.argv[1])
    blocks, files, skipped = 0, 0, 0
    for path in sorted(todo):
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        src = raw.replace('\r\n', '\n').split('\n')
        changed = False
        for lineno in sorted(todo[path], reverse=True):
            i = lineno - 1
            # `strip().startswith('//')` also rejects a trailing comment on
            # code (`foo(); // note`), whose indent is the code's, not the
            # comment's, and so cannot be re-flowed in place.
            if i >= len(src) or not src[i].strip().startswith('//'):
                skipped += 1
                continue
            start, end, indent, mk = block_bounds(src, i)
            out = reflow(src[start:end], indent, mk)
            if out is None:
                skipped += 1
                continue
            src[start:end] = out
            blocks += 1
            changed = True
        if changed:
            io.open(path, 'w', encoding='utf-8', newline='').write(
                '\n'.join(src).replace('\n', eol))
            files += 1
    print('comment blocks reflowed : %d across %d file(s)' % (blocks, files))
    print('left alone              : %d' % skipped)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
