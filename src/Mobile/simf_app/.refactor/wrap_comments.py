"""Re-flow over-long COMMENT blocks to 80 columns.

    python .refactor/wrap_comments.py <analyze-output>

Comment text carries no semantics, so this cannot change behaviour. It reflows
the whole contiguous BLOCK rather than the flagged line alone: wrapping one line
in isolation pushes its tail onto a line of its own and leaves orphans like

    /// Used by the Terms page (key
    /// `terms`)
    /// and any future static-content screen.

which is worse to read than the long line was.

Refuses, rather than guessing, on:

  * `// ignore:` / `// ignore_for_file:` — analyzer DIRECTIVES, not prose.
    Wrapping one silently stops it suppressing anything.
  * a block containing a fenced code sample, a bullet/numbered list, a table
    rule or a trailing-backslash line — all deliberately laid out.
  * a paragraph with no breakable space (a long URL or path).
  * a flagged line that is not a comment at all.

A blank `///` line separates paragraphs and is preserved. A trailing comment
after code (`foo(); // note`) is left alone: its indentation is not the comment's.
"""

import io
import re
import sys

LIMIT = 80
COMMENT = re.compile(r'^(\s*)(///?)(\s*)(.*)$')
DIRECTIVE = re.compile(r'^\s*//\s*ignore(_for_file)?:')
STRUCTURED = re.compile(r'^\s*///?\s*(?:[-*+]\s|\d+[.)]\s|\||```|>)')


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
        return mm is not None and mm.group(1) == indent and mm.group(2) == marker

    start = i
    while same(start - 1):
        start -= 1
    end = i
    while same(end + 1):
        end += 1
    return start, end + 1, indent, marker


def reflow(src, start, end, indent, marker):
    """The block re-wrapped, or None to leave it exactly as it is."""
    lines = src[start:end]
    for line in lines:
        if DIRECTIVE.match(line) or STRUCTURED.match(line) or line.rstrip().endswith('\\'):
            return None

    prefix = indent + marker + ' '
    width = LIMIT - len(prefix)
    if width < 24:
        return None

    # Split into paragraphs on blank comment lines, keeping the blanks.
    paras, cur = [], []
    for line in lines:
        text = COMMENT.match(line).group(4).rstrip()
        if not text:
            paras.append(cur)
            paras.append(None)          # a blank separator
            cur = []
        else:
            cur.append(text)
    paras.append(cur)

    out = []
    for para in paras:
        if para is None:
            out.append((indent + marker).rstrip())
            continue
        if not para:
            continue
        words = ' '.join(para).split()
        line = ''
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

    if any(len(l) > LIMIT for l in out):
        return None                     # an unbreakable token: leave the block
    return out if out != lines else None


def main():
    todo = flagged(sys.argv[1])
    wrapped_blocks, touched_files, skipped = 0, 0, 0
    for path in sorted(todo):
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        src = raw.replace('\r\n', '\n').split('\n')
        changed = False
        for lineno in sorted(todo[path], reverse=True):
            i = lineno - 1
            if i >= len(src):
                continue
            m = COMMENT.match(src[i])
            # A comment marker that is not at the start of the line is a
            # trailing comment on code — its column is not the comment's.
            if not m or src[i].lstrip()[:2] not in ('//', '//'):
                skipped += 1
                continue
            if not src[i].strip().startswith('//'):
                skipped += 1
                continue
            start, end, indent, marker = block_bounds(src, i)
            out = reflow(src, start, end, indent, marker)
            if out is None:
                skipped += 1
                continue
            src[start:end] = out
            wrapped_blocks += 1
            changed = True
        if changed:
            io.open(path, 'w', encoding='utf-8', newline='').write(
                '\n'.join(src).replace('\n', eol))
            touched_files += 1
    print('comment blocks reflowed : %d' % wrapped_blocks)
    print('files touched           : %d' % touched_files)
    print('left alone              : %d' % skipped)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
