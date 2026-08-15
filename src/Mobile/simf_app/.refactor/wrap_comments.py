"""Re-flow over-long COMMENT blocks to 80 columns, one PARAGRAPH at a time.

    python .refactor/wrap_comments.py <analyze-output>

Comment text carries no semantics, so this cannot change behaviour.

This file is the merge of three earlier scripts, and the merge is the point:
they had drifted into three greedy line-wrappers with different width
accounting, and a bug fixed in one stayed alive in another.

  * the original `wrap_comments.py` measured with Python `len()` and refused a
    whole block whenever any line in it was a list item;
  * `wrap_comments2.py` fixed both but left the original in the tree, giving a
    reader no signal which of two 150-line tools to pick;
  * `wrap_ignore_reasons.py` existed ONLY because the original refused blocks
    containing an `// ignore:` directive - and, being a separate copy, it never
    received the UTF-16 fix, so it could not even see the blocks it was written
    for.

Two things the merged version gets right that the original did not:

  * **Measures in UTF-16 code units, not Python characters.** Dart strings are
    UTF-16 and the analyzer counts that way, so a regional-indicator flag like
    the Saudi one costs 4 units where Python sees 2. The original measured such
    a line as 79 and left it alone while the analyzer called it 81.
  * **Works per unit, not per block.** A block is split into units - a blank
    line, a list item (wrapped with a hanging indent, so its continuation reads
    as markdown), an `// ignore:` directive (copied through BYTE FOR BYTE,
    because wrapping one silently stops it suppressing anything), or a run of
    prose. Only the unit that cannot be handled is skipped, not its neighbours.

Still refuses, rather than guessing:

  * a block holding a fenced sample, a table rule or a trailing-backslash line;
  * a paragraph whose longest word cannot fit (a URL or a long path);
  * a trailing comment after code (`foo(); // note`), whose indentation belongs
    to the code and so cannot be re-flowed in place - use
    `lift_trailing_comments.py`, which moves it above the declaration instead.
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
    """The contiguous run of comment lines sharing i's indent and marker."""
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
    out, line, pre = [], '', prefix
    for word in text.split():
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
        if UNTOUCHABLE.match(line) or line.rstrip().endswith('\\'):
            return None

    prefix = indent + marker + ' '
    if LIMIT - width(prefix) < 24:
        return None

    out, i = [], 0
    while i < len(lines):
        raw = lines[i]
        text = COMMENT.match(raw).group(4).rstrip()
        if DIRECTIVE.match(raw):
            out.append(raw)                     # byte for byte
            i += 1
            continue
        if not text:
            out.append((indent + marker).rstrip())
            i += 1
            continue
        # A list item hangs its continuation under the text; prose does not.
        m = MARKER.match(text)
        hang = ' ' * len(m.group(1)) if m else ''
        unit = [text]
        i += 1
        while i < len(lines):
            nxt = COMMENT.match(lines[i]).group(4).rstrip()
            if not nxt or MARKER.match(nxt) or DIRECTIVE.match(lines[i]):
                break
            unit.append(nxt)
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
        # Resolve every flagged line to its BLOCK first, then re-flow each block
        # once. Working line-by-line re-derived the bounds after the block had
        # already been rewritten, so a block with two flagged lines was scanned
        # against shifted indices - and the second pass was counted as a
        # refusal, making the summary report lines it had in fact fixed.
        changed, targets = False, {}
        for lineno in sorted(todo[path]):
            i = lineno - 1
            # `strip().startswith('//')` also rejects a trailing comment on
            # code, whose indent is the code's and not the comment's.
            if i >= len(src) or not src[i].strip().startswith('//'):
                skipped += 1
                continue
            start, end, indent, mk = block_bounds(src, i)
            targets[start] = (end, indent, mk)

        for start in sorted(targets, reverse=True):
            end, indent, mk = targets[start]
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
