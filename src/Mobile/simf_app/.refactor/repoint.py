"""Replace one barrel import with the specific files a caller actually needs.

    python .refactor/repoint.py <file> <old-import-path> <new-module>...

Paths are package-relative under lib/, e.g.
    features/myarea/widgets/my_area_rows.dart
"""

import io
import sys

PKG = 'package:simf_app/'


def main():
    path, old = sys.argv[1], sys.argv[2]
    mods = sys.argv[3:]
    raw = io.open(path, encoding='utf-8', newline='').read()
    eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
    text = raw.replace('\r\n', '\n')
    old_line = "import '%s%s';" % (PKG, old)
    if old_line not in text:
        print('  MISS %s -- %s' % (path, old_line))
        return 1
    new_lines = '\n'.join("import '%s%s';" % (PKG, m) for m in sorted(mods))
    text = text.replace(old_line, new_lines, 1)
    io.open(path, 'w', encoding='utf-8', newline='').write(text.replace('\n', eol))
    print('  %-58s %d import(s)' % (path, len(mods)))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
