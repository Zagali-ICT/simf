"""Split an over-long string literal into two adjacent literals.

    python .refactor/split_long_strings.py <analyze-output>

Dart concatenates adjacent string literals at compile time, so `'ab ' 'cd'` and
`'ab cd'` are the same string down to the byte. That makes this the one way to
bring a long sentence under 80 columns without editing the sentence.

Used only where the sentence is the whole line - a test name, a fixture body, an
assert message. It is deliberately NOT used on `app_l10n.dart`, whose strings the
E2E catalogues grep for verbatim on screen; that file carries a file-level
suppression and its reason instead.

Splits at a space, never inside a word, and never between a `\\` and the
character it escapes. Refuses, rather than guessing:

  * a line that is not a bare literal with optional `,` / `;` / `)` after it
  * an interpolation `${...}` spanning the split point
  * a literal with no space in the splittable range (an unbreakable token)
  * a triple-quoted literal, whose newlines are content
"""

import io
import re
import sys

LIMIT = 80
LINE = re.compile(r"^(\s*)(r?)('|\")(.*)\3([,;)]*)\s*$")


def width(text):
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


def escaped(body, i):
    """True when body[i] is escaped by an odd run of backslashes before it."""
    n = 0
    while i - 1 - n >= 0 and body[i - 1 - n] == '\\':
        n += 1
    return n % 2 == 1


def interpolation_spans(body):
    """(start, end) of every `${...}` and `$name` in the literal."""
    spans, i = [], 0
    while i < len(body):
        if body[i] == '$' and not escaped(body, i):
            if i + 1 < len(body) and body[i + 1] == '{':
                depth, j = 0, i + 1
                while j < len(body):
                    if body[j] == '{':
                        depth += 1
                    elif body[j] == '}':
                        depth -= 1
                        if depth == 0:
                            break
                    j += 1
                spans.append((i, j))
                i = j + 1
                continue
            m = re.match(r'\$\w+', body[i:])
            if m:
                spans.append((i, i + m.end() - 1))
                i += m.end()
                continue
        i += 1
    return spans


def main():
    todo = flagged(sys.argv[1])
    split, files, skipped = 0, 0, 0
    for path in sorted(todo):
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        src = raw.replace('\r\n', '\n').split('\n')
        changed = False
        for lineno in sorted(todo[path], reverse=True):
            i = lineno - 1
            if i >= len(src):
                continue
            m = LINE.match(src[i])
            if not m or '"""' in src[i] or "'''" in src[i]:
                skipped += 1
                continue
            indent, rawpfx, quote, body, tail = m.groups()
            head = indent + rawpfx + quote
            spans = interpolation_spans(body)

            # The last space that leaves the first literal under the limit,
            # is not escaped, and is not inside an interpolation.
            cut = -1
            for j, ch in enumerate(body):
                if ch != ' ' or escaped(body, j):
                    continue
                if any(a <= j <= b for a, b in spans):
                    continue
                if width(head + body[:j + 1] + quote) > LIMIT:
                    break
                cut = j + 1
            if cut <= 0:
                skipped += 1
                continue

            first = head + body[:cut] + quote
            second = indent + '    ' + rawpfx + quote + body[cut:] + quote + tail
            if width(second) > LIMIT:
                skipped += 1
                continue
            src[i:i + 1] = [first, second]
            split += 1
            changed = True
        if changed:
            io.open(path, 'w', encoding='utf-8', newline='').write(
                '\n'.join(src).replace('\n', eol))
            files += 1
    print('literals split : %d across %d file(s)' % (split, files))
    print('left alone     : %d' % skipped)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
