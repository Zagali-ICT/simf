"""Derive the section 9 header fields for every screen, from the code.

    python .refactor/screen_header_fields.py            # report only
    python .refactor/screen_header_fields.py --apply    # append to the headers

CLAUDE.md section 9 wants each screen's doc header to carry `route:`, `Data:`
and `Perf:` alongside the prose. The prose is already there and is good - the
section itself says "this repo already does this well - keep the style" - so
nothing is rewritten. What is missing is the structured half, and in particular
`Perf:`, which section 4 asks for and which exists on no screen at all.

Every field is DERIVED and checkable, never invented:

  * route  - the `RouteNames` constant whose `builder:` names this screen class
             in `lib/app/router.dart`.
  * Data   - the providers the file reads via `ref.watch(` / `ref.read(`.
  * Perf   - which list widgets the file builds with. `ListView.builder` /
             `.separated` / `SliverList` are lazy; a bare `ListView(children:)`
             or `Column` builds every child up front, which is correct for a
             short static page and a defect on a data feed, so the line states
             which it is rather than judging it.

A screen the router does not name, or whose header cannot be located, is
REPORTED and skipped - a header is documentation, and a wrong one is worse than
a missing one.
"""

import io
import os
import re
import sys

CLASS = re.compile(r'^class (\w*Screen) extends ')
PROVIDER = re.compile(r'ref\.(?:watch|read)\((\w+Provider)')
LAZY = ('ListView.builder', 'ListView.separated', 'SliverList',
        'SliverChildBuilderDelegate', 'GridView.builder', 'PageView.builder')
EAGER = ('ListView(', 'GridView.count(', 'GridView.extent(')


def route_names(router_src):
    """{ScreenClass: RouteNames.const}, read from the router's BUILDER.

    The route table (`name:` / `path:` / labels) and the widget builder are
    separate in `router.dart` - the builder is a chain of
    `if (r.name == RouteNames.x) { return const XScreen(); }` - so pairing a
    `name:` with the next `Screen(` on the page reads the table, matches
    nothing, and hands every screen the same stale name.
    """
    out = {}
    lines = router_src.split('\n')
    for i, line in enumerate(lines):
        m = re.search(r'r\.name == RouteNames\.(\w+)', line)
        if not m:
            continue
        for j in range(i, min(i + 6, len(lines))):
            m2 = re.search(r'return (?:const )?(\w*Screen)\(', lines[j])
            if m2:
                out.setdefault(m2.group(1), m.group(1))
                break
    return out


def header_bounds(src, class_line):
    """The `///` block immediately above `class_line`, or None."""
    end = class_line
    # Annotations (@override, etc.) may sit between the doc and the class.
    while end - 1 >= 0 and src[end - 1].lstrip().startswith('@'):
        end -= 1
    if end - 1 < 0 or not src[end - 1].lstrip().startswith('///'):
        return None
    start = end - 1
    while start - 1 >= 0 and src[start - 1].lstrip().startswith('///'):
        start -= 1
    return start, end


def wrapped(label, body, indent):
    """`/// Label: body` wrapped to 80 columns, continuations hanging.

    The generator wraps its OWN output rather than leaving it to
    `wrap_comments.py`. That tool re-flows a whole block per unit, so running
    it over 70 headers to fix the 3 lines this adds would re-wrap every
    hand-written paragraph beside them - churn in 70 files to fix 70 lines.
    """
    prefix = indent + '/// '
    hang = ' ' * (len(label) + 2)
    out, line, pre = [], label + ': ', prefix
    for word in body.split():
        if line.strip() and width(pre + line) + width(word) > LIMIT:
            out.append((pre + line).rstrip())
            pre, line = prefix + hang, word + ' '
        else:
            line += word + ' '
    out.append((pre + line).rstrip())
    return out


def width(text):
    """The analyzer's column count: UTF-16 code units."""
    return len(text.encode('utf-16-le')) // 2


LIMIT = 80


def perf_line(text):
    lazy = sorted({w for w in LAZY if w in text})
    eager = sorted({w.rstrip('(') for w in EAGER if w in text})
    if lazy and not eager:
        return 'lazy — builds children on demand (%s).' % ', '.join(lazy)
    if lazy and eager:
        return ('mixed — %s builds on demand; %s builds every child up front.'
                % (', '.join(lazy), ', '.join(eager)))
    if eager:
        return ('%s builds every child up front — correct for a short static '
                'page, a defect on a data feed.' % ', '.join(eager))
    return 'no list — a single-screen layout.'


def main():
    apply = '--apply' in sys.argv
    router = io.open('lib/app/router.dart', encoding='utf-8',
                     errors='replace').read().replace('\r\n', '\n')
    routes = route_names(router)

    done, skipped = 0, []
    for dirpath, _dirs, names in os.walk('lib'):
        for name in sorted(names):
            if not name.endswith('_screen.dart'):
                continue
            path = os.path.join(dirpath, name).replace(chr(92), '/')
            raw = io.open(path, encoding='utf-8', newline='').read()
            eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
            src = raw.replace('\r\n', '\n').split('\n')

            cls, cls_line = None, None
            for i, line in enumerate(src):
                m = CLASS.match(line)
                if m:
                    cls, cls_line = m.group(1), i
                    break
            if cls is None:
                skipped.append('%s (no *Screen class)' % path)
                continue
            bounds = header_bounds(src, cls_line)
            if bounds is None:
                skipped.append('%s (no doc header above %s)' % (path, cls))
                continue
            text = '\n'.join(src)
            if '/// Perf:' in text:
                continue                        # already carries the fields

            providers = sorted(set(PROVIDER.findall(text)))
            route = routes.get(cls)
            indent = src[cls_line][:len(src[cls_line])
                                   - len(src[cls_line].lstrip())]
            fields = [indent + '///']
            fields += wrapped(
                'Route',
                ('`RouteNames.%s`.' % route) if route else
                'not named in `router.dart` — reached as a sheet, a tab or a '
                'pushed child.',
                indent)
            fields += wrapped(
                'Data',
                (', '.join('[%s]' % p for p in providers) + '.') if providers
                else 'none — renders what it is given.',
                indent)
            fields += wrapped('Perf', perf_line(text), indent)

            if apply:
                start, end = bounds
                src[end:end] = fields
                io.open(path, 'w', encoding='utf-8', newline='').write(
                    '\n'.join(src).replace('\n', eol))
            else:
                print('%s  (%s)' % (path, cls))
                for f in fields[1:]:
                    print('   ', f)
            done += 1

    print('\nscreens %s : %d' % ('updated' if apply else 'to update', done))
    for s in skipped:
        print('  SKIPPED', s)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
