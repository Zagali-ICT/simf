"""Re-derive the `Data:` line in every screen header that already has one.

    python .refactor/refresh_data_lines.py

`screen_header_fields.py` skips a file that already carries `/// Perf:`, which
is right for adding the fields once and wrong for CORRECTING them. The first
run matched `ref.watch(` / `ref.read(` only when the two were adjacent, so 12
screens that break a long read across lines - `await ref\n  .read(xProvider)` -
were written up as "Data: none - renders what it is given". A header that says
none when the answer is one is worse than a header with no line at all.

Replaces only the `Data:` line (and its hanging continuations), leaving `Route:`
, `Perf:` and every word of prose alone.
"""

import io
import os
import re
import sys

# Run from the package root, but import the sibling generator regardless of
# which directory the run started in.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
os.chdir(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import screen_header_fields as gen  # noqa: E402


def main():
    fixed = []
    for dirpath, _dirs, names in os.walk('lib'):
        for name in sorted(names):
            if not name.endswith('_screen.dart'):
                continue
            path = os.path.join(dirpath, name).replace(chr(92), '/')
            raw = io.open(path, encoding='utf-8', newline='').read()
            eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
            src = raw.replace('\r\n', '\n').split('\n')
            text = '\n'.join(src)

            start = next((i for i, l in enumerate(src)
                          if l.lstrip().startswith('/// Data:')), None)
            if start is None:
                continue
            end = start + 1
            while (end < len(src) and src[end].lstrip().startswith('///')
                   and not re.match(r'^\s*/// \w+:', src[end])):
                end += 1

            indent = src[start][:len(src[start]) - len(src[start].lstrip())]
            providers = sorted(set(gen.PROVIDER.findall(text)))
            new = gen.wrapped(
                'Data',
                (', '.join('[%s]' % p for p in providers) + '.') if providers
                else 'none — renders what it is given.',
                indent)
            if new == src[start:end]:
                continue
            src[start:end] = new
            io.open(path, 'w', encoding='utf-8', newline='').write(
                '\n'.join(src).replace('\n', eol))
            fixed.append(path)

    print('Data: lines corrected : %d' % len(fixed))
    for f in fixed:
        print('  ', f)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
