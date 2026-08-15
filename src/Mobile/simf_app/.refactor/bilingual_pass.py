"""Replace the fourteen private bilingual `_pick` copies with one shared pair.

The mapping has to be per FILE, not per name: `_pick` returns a non-null String
in archive / contacts / moderation / news / speakers, and a nullable one in
gallery / venuemap. `faq` also takes its arguments in a different order.

    python .refactor/bilingual_pass.py

Rewrites call sites with a paren-matching scan so a nested call in an argument
is not truncated, and reports anything it cannot rewrite rather than guessing.
"""

import io
import re

REQUIRED = 'pickLocalized'
OPTIONAL = 'pickLocalizedOrNull'

# file -> {private name: (target, args_are_isArabic_first)}
MAP = {
    'lib/features/ai_summary/data/session_summary_models.dart': {'_picked': (REQUIRED, False)},
    'lib/features/archive/data/archive_models.dart': {'_pick': (REQUIRED, False), '_pickOpt': (OPTIONAL, False)},
    'lib/features/contacts/data/contact_models.dart': {'_pick': (REQUIRED, False)},
    'lib/features/delegations/data/delegation_models.dart': {'_pickRequired': (REQUIRED, False), '_pickOptional': (OPTIONAL, False)},
    'lib/features/faq/data/faq_models.dart': {'_pick': (REQUIRED, True)},
    'lib/features/gallery/data/media_models.dart': {'_pick': (OPTIONAL, False)},
    'lib/features/moderation/data/moderation_models.dart': {'_pick': (REQUIRED, False)},
    'lib/features/myarea/data/my_sessions_models.dart': {'_pickRequired': (REQUIRED, False), '_pickOptional': (OPTIONAL, False)},
    'lib/features/news/data/news_models.dart': {'_pick': (REQUIRED, False), '_pickOpt': (OPTIONAL, False)},
    'lib/features/sessions/data/presentation_models.dart': {'_pickRequired': (REQUIRED, False), '_pickOptional': (OPTIONAL, False)},
    'lib/features/sessions/data/session_models.dart': {'_pickRequired': (REQUIRED, False), '_pickOptional': (OPTIONAL, False)},
    'lib/features/speakers/data/speaker_models.dart': {'_pick': (REQUIRED, False), '_pickOpt': (OPTIONAL, False)},
    'lib/features/sponsors/data/sponsor_models.dart': {'_pickRequired': (REQUIRED, False), '_pickOptional': (OPTIONAL, False)},
    'lib/features/venuemap/data/venue_map_models.dart': {'_pick': (OPTIONAL, False)},
}

IMPORT = "import 'package:simf_app/core/utils/bilingual.dart';"


def split_args(text):
    """Top-level comma split, so a nested call's commas do not split it."""
    out, depth, cur = [], 0, ''
    for ch in text:
        if ch in '([{':
            depth += 1
        elif ch in ')]}':
            depth -= 1
        if ch == ',' and depth == 0:
            out.append(cur.strip())
            cur = ''
        else:
            cur += ch
    if cur.strip():
        out.append(cur.strip())
    return out


def rewrite_calls(text, name, target, is_arabic_first):
    """`name(a, b, flag)` -> `target(a, b, isArabic: flag)`."""
    out, i, count, failed = '', 0, 0, 0
    pat = re.compile(r'(?<![\w$])' + re.escape(name) + r'\(')
    while True:
        m = pat.search(text, i)
        if not m:
            out += text[i:]
            break
        out += text[i:m.start()]
        depth, j = 0, m.end() - 1
        while j < len(text):
            if text[j] == '(':
                depth += 1
            elif text[j] == ')':
                depth -= 1
                if depth == 0:
                    break
            j += 1
        args = split_args(text[m.end():j])
        if len(args) != 3:
            out += text[m.start():j + 1]
            failed += 1
        else:
            flag, a, b = (args[0], args[1], args[2]) if is_arabic_first \
                else (args[2], args[0], args[1])
            out += '%s(%s, %s, isArabic: %s)' % (target, a, b, flag)
            count += 1
        i = j + 1
    return out, count, failed


def strip_declaration(text, name):
    """Remove the private helper and the doc comment above it."""
    pat = re.compile(
        r'(?:^[ \t]*///[^\n]*\n)*^[ \t]*(?:static )?String\??[ \t]+'
        + re.escape(name) + r'\([^)]*\)[ \t]*\{.*?^[ \t]*\}\n',
        re.MULTILINE | re.DOTALL)
    return pat.subn('', text, count=1)


def main():
    total_calls, total_decls, problems = 0, 0, []
    for path, names in sorted(MAP.items()):
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        text = raw.replace('\r\n', '\n')
        calls = 0
        for name, (target, first) in names.items():
            # Strip the declaration FIRST. Rewriting calls first also matches
            # the declaration's own name and turns its PARAMETER list into an
            # argument list, which is a syntax error in 22 files at once.
            text, removed = strip_declaration(text, name)
            if removed:
                total_decls += 1
            else:
                problems.append('%s: declaration %s not removed' % (path, name))
                continue
            text, n, failed = rewrite_calls(text, name, target, first)
            calls += n
            if failed:
                problems.append('%s: %d call(s) of %s not 3-arg' % (path, failed, name))
        if IMPORT not in text:
            lines = text.split('\n')
            idx = [n for n, l in enumerate(lines) if l.startswith('import ')]
            lines.insert(idx[-1] + 1 if idx else 0, IMPORT)
            text = '\n'.join(lines)
        text = re.sub(r'\n{3,}', '\n\n', text)
        io.open(path, 'w', encoding='utf-8', newline='').write(text.replace('\n', eol))
        total_calls += calls
        print('  %-56s %2d call(s)' % (path[len('lib/features/'):], calls))
    print('call sites rewritten: %d | declarations removed: %d' % (total_calls, total_decls))
    for p in problems:
        print('  PROBLEM', p)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
