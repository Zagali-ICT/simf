"""Route three raw `ApiFailure.message` renders through `localizedMessage`.

    python .refactor/localize_server_messages.py

`lib/core/errors/api_error_l10n.dart` states the rule in its own docstring: a
client-SYNTHESIZED failure (clientNetwork / clientTimeout /
clientMalformedResponse / clientCancelled) carries a raw ENGLISH developer
string, so no screen may render `ApiFailure.message` directly or an Arabic user
reads English. Three screens did.

`localizedMessage` is a strict improvement at each site: an envelope failure's
message is already bilingual and passes through unchanged, so only the
synthesized cases change - and those change from a stack-trace fragment to real
copy.

NOT changed: `gate_scan_screen._serverMessage`. It is reached only under
`case 403`, which is always an envelope failure, and its `String?` return drives
a `?? l10n.gateForbidden` fallback that `localizedMessage` (never empty) would
silently disable.
"""

import io

IMPORT = "import 'package:simf_app/core/errors/api_error_l10n.dart';\n"

EDITS = [
    # The avatar upload's own comment claimed the message was "user-safe,
    # bilingual" - true of the envelope case it was written for, and false the
    # moment the phone loses signal mid-upload.
    ('lib/features/myarea/my_area_screen.dart', [(
        """        final serverMsg = e.message.trim();""",
        """        final serverMsg = e.localizedMessage(l10n).trim();""",
    )]),
    # Reached via `_ when moving`, after the specific seat codes are handled -
    # so a dropped connection while moving seats showed the raw string.
    ('lib/features/sessions/seat_picker_screen.dart', [(
        """      failureMessage = failure.message.trim();""",
        """      failureMessage = failure.localizedMessage(l10n).trim();""",
    )]),
]

# NOT changed, checked rather than assumed: `register_visitor_screen` assigns
# `_loadError = failure.message`, which looks like a fourth site. It is not -
# `_buildLoadError` renders the fixed `l10n.staffRegisterError` and never the
# captured string, so `_loadError` is a null-flag wearing a String's clothes and
# nothing raw ever reaches the user. Localising it would have been a fix to a
# non-bug. (That the field could be a bool is a real simplification, but the
# file is one of the two device-blocked registration screens.)


def main():
    for path, pairs in EDITS:
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        text = raw.replace('\r\n', '\n')
        for before, after in pairs:
            assert before in text, (path, before.strip())
            text = text.replace(before, after)
        if 'api_error_l10n.dart' not in text:
            lines = text.split('\n')
            at = max(i for i, l in enumerate(lines)
                     if l.startswith("import 'package:simf_app/")
                     and l < IMPORT.strip()) + 1
            lines.insert(at, IMPORT.strip())
            text = '\n'.join(lines)
        io.open(path, 'w', encoding='utf-8', newline='').write(
            text.replace('\n', eol))
        print('  ok', path.split('/')[-1])
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
