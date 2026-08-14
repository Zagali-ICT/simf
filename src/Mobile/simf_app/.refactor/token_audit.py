"""Measure every `SimfTokens` declaration's real use count (Decision 6).

    python .refactor/token_audit.py

Writes `.refactor/TOKEN-AUDIT-<date>.md`. The date is passed in rather than
read from the clock so re-running cannot silently produce a second file.

THE GOTCHA THIS EXISTS TO ENCODE: a token is referenced two different ways, and
counting only one gives a wrong answer that looks right until you act on it.

  * Outside the file:  `SimfTokens.textXxl`
  * Inside tokens.dart: the BARE identifier `textXxl`, because the composite
    styles are built from the scale entries.

The first pass counted only the qualified form, called 14 tokens dead, and
deleting them broke the build on 5 of them. Count both, or do not delete.
"""

import collections
import io
import os
import re

TOKENS = 'lib/app/theme/tokens.dart'
DECL = re.compile(r'static const \w+(?:<[^>]*>)? (\w+)\s*=')
DATE = '2026-08-14'


def counts():
    tsrc = io.open(TOKENS, encoding='utf-8', errors='replace').read()
    declared = set(DECL.findall(tsrc))
    used = collections.Counter({t: 0 for t in declared})

    # Strip the declarations themselves, then any surviving bare mention of a
    # token name inside this file is a real internal use.
    body = DECL.sub(' ', tsrc)
    for word in re.findall(r'\b(\w+)\b', body):
        if word in declared:
            used[word] += 1

    for base in ('lib', 'test', 'integration_test'):
        for dirpath, _dirs, names in os.walk(base):
            for name in names:
                if not name.endswith('.dart'):
                    continue
                path = os.path.join(dirpath, name).replace(chr(92), '/')
                if path.endswith(TOKENS):
                    continue
                text = io.open(path, encoding='utf-8',
                               errors='replace').read()
                for tok in re.findall(r'SimfTokens\.(\w+)', text):
                    if tok in used:
                        used[tok] += 1
    return declared, used


def main():
    declared, used = counts()
    dead = sorted(t for t in declared if used[t] == 0)
    once = sorted(t for t in declared if used[t] == 1)
    many = sorted(t for t in declared if used[t] >= 2)

    out = io.open('.refactor/TOKEN-AUDIT-%s.md' % DATE, 'w', encoding='utf-8')
    out.write('# SimfTokens usage audit - %s (Decision 6)\n\n' % DATE)
    out.write('%d declarations, measured across `lib`, `test` and '
              '`integration_test`.\n\n' % len(declared))
    out.write('| Uses | Count |\n|---|---|\n')
    out.write('| 0 (dead) | %d |\n| exactly 1 | %d |\n| 2 or more | %d |\n\n'
              % (len(dead), len(once), len(many)))
    out.write(__doc__.split('THE GOTCHA')[1].join(
        ['## The measurement gotcha, banked\n\nTHE GOTCHA', '\n']))
    out.write(
        '## The single-use names are NOT a defect list\n\n'
        'Decision 6 splits this deliberately: the dead ones go, the single-use\n'
        'ones get this report and their own decision. Folding %d names back\n'
        'into their call sites would be a very large diff in the app\'s highest\n'
        'blast-radius file, and it would reverse a completed wave - the\n'
        'tokenisation programme that took inline `TextStyle` from 526 to 0\n'
        'created most of them by design. A name used once is not automatically\n'
        'wrong: it is wrong when the name says LESS than the value it hides,\n'
        'and right when it says more.\n\n' % len(once))
    for title, names in (('Dead (0 uses)', dead),
                         ('Used exactly once', once),
                         ('Used twice or more', many)):
        out.write('## %s - %d\n\n```\n%s\n```\n\n'
                  % (title, len(names), '\n'.join(names) or '(none)'))
    out.close()
    print('declared %d | dead %d | single-use %d | 2+ %d'
          % (len(declared), len(dead), len(once), len(many)))
    print('wrote .refactor/TOKEN-AUDIT-%s.md' % DATE)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
