"""Turn `localizedX(bool isArabic)` into `localizedX({required bool isArabic})`.

Decision 2. Every one of the 135 sites is the same shape: a DTO accessor taking
one positional bool that selects the language. A bare `true` at a call site says
nothing; `isArabic: true` says what it is.

Two steps, because the call sites cross features and only the compiler knows all
of them:

  1. `--declare <analyze-output> <path-prefix>...`
     rewrites the flagged declarations, and prints the method names it changed.
  2. `--calls <analyze-output>`
     rewrites the call sites the analyzer then reports as errors, using a
     paren-matching scan from the reported column so a nested call in the
     argument is handled.

Run step 1, re-analyze, run step 2, re-analyze, repeat until clean.
"""

import io
import re
import sys

DECL = re.compile(r'\(\s*bool\s+(\w+)\s*\)')


def rows(analyze_path, rule, prefixes=None):
    out = []
    for line in io.open(analyze_path, encoding='utf-8', errors='replace'):
        if rule not in line:
            continue
        loc = line.split(' - ')[-2]
        path, lineno, col = loc.rsplit(':', 2)
        path = path.replace('\\', '/')
        if prefixes and not any(path.startswith(p) for p in prefixes):
            continue
        out.append((path, int(lineno), int(col)))
    return out


def declare(analyze_path, prefixes):
    hit = {}
    for path, lineno, _col in rows(
            analyze_path, 'avoid_positional_boolean_parameters', prefixes):
        hit.setdefault(path, set()).add(lineno)

    names = set()
    for path in sorted(hit):
        lines = io.open(path, encoding='utf-8', newline='').read().split('\n')
        for lineno in sorted(hit[path]):
            i = lineno - 1
            m = DECL.search(lines[i])
            if not m:
                print('  SKIP %s:%d  %s' % (path, lineno, lines[i].strip()[:70]))
                continue
            name = re.match(r'\s*[\w<>?, ]+?\s+(\w+)\(', lines[i])
            if name:
                names.add(name.group(1))
            lines[i] = (lines[i][:m.start()]
                        + '({required bool %s})' % m.group(1)
                        + lines[i][m.end():])
        io.open(path, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
    print('declarations rewritten in %d file(s)' % len(hit))
    print('methods:', ' '.join(sorted(names)))
    return names


def _rewrite_call(line, col, param):
    """`…name(EXPR)` -> `…name(param: EXPR)`, matching parens so a nested call
    inside EXPR does not truncate it."""
    open_i = line.find('(', col - 1)
    if open_i == -1:
        return None
    depth = 0
    for j in range(open_i, len(line)):
        if line[j] == '(':
            depth += 1
        elif line[j] == ')':
            depth -= 1
            if depth == 0:
                arg = line[open_i + 1:j]
                if not arg.strip() or arg.lstrip().startswith(param + ':'):
                    return None
                return line[:open_i + 1] + '%s: %s' % (param, arg) + line[j:]
    return None


def calls(analyze_path, param='isArabic'):
    sites = rows(analyze_path, 'missing_required_argument')
    sites += rows(analyze_path, 'extra_positional_arguments_could_be_named')
    sites += rows(analyze_path, 'extra_positional_arguments')
    by = {}
    for path, lineno, col in sites:
        by.setdefault(path, {}).setdefault(lineno, col)

    fixed = 0
    for path in sorted(by):
        lines = io.open(path, encoding='utf-8', newline='').read().split('\n')
        for lineno in sorted(by[path], reverse=True):
            i = lineno - 1
            out = _rewrite_call(lines[i], by[path][lineno], param)
            if out is None:
                print('  MANUAL %s:%d  %s' % (path, lineno, lines[i].strip()[:70]))
                continue
            lines[i] = out
            fixed += 1
        io.open(path, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
    print('call sites rewritten: %d across %d file(s)' % (fixed, len(by)))
    return fixed


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    mode, analyze_path = sys.argv[1], sys.argv[2]
    if mode == '--declare':
        declare(analyze_path, sys.argv[3:])
    elif mode == '--calls':
        calls(analyze_path)
    else:
        print(__doc__)
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
