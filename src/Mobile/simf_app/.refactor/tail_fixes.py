"""The hand edits for the last over-long CODE lines. One-shot, not a tool.

Each entry is an exact before/after pair, asserted present before it is
applied, so a stale pattern fails loudly instead of silently doing nothing.
"""

import io

MIDDOT = chr(183)

EDITS = [
    # An interpolation cannot be broken, so the sentence splits into adjacent
    # literals - the same shape the branch four lines above already uses.
    ('lib/core/utils/event_date_range.dart', [(
        "    return '${s.day} ${gregorianMonthName(s.month, isArabic: isArabic)}"
        " - ${full(e)}';",
        "    return '${s.day} ${gregorianMonthName(s.month, isArabic: isArabic)}"
        " '\n        '- ${full(e)}';",
    )]),
    ('lib/features/notifications/widgets/notification_grouped_list.dart', [(
        "    return '${local.day} "
        "${gregorianMonthName(local.month, isArabic: isArabic)}';",
        "    final month = gregorianMonthName(local.month, isArabic: isArabic);"
        "\n    return '${local.day} $month';",
    )]),
    ('lib/features/sessions/widgets/seat_grid_row.dart', [(
        "        return '$id " + MIDDOT + " ${tier.isVvip ? "
        "l10n.seatTierVvipLocked : l10n.seatTierVipLocked}';",
        "        final locked =\n            tier.isVvip ? l10n.seatTierVvipLocked"
        " : l10n.seatTierVipLocked;\n        return '$id " + MIDDOT + " $locked';",
    )]),
    # The pad-to-two-digits rule was written out twice on one line; naming it
    # once shortens the line and removes the duplicate.
    ('lib/features/sessions/widgets/session_time_rail.dart', [(
        "  static String _hhmm(DateTime t) =>\n      "
        "'${t.hour.toString().padLeft(2, '0')}:"
        "${t.minute.toString().padLeft(2, '0')}';",
        "  static String _two(int value) => value.toString().padLeft(2, '0');"
        "\n\n  static String _hhmm(DateTime t) => "
        "'${_two(t.hour)}:${_two(t.minute)}';",
    )]),
    ('lib/features/gates/gate_scan_screen.dart', [(
        "    final title = showContext\n        ? '$gateName${directionForTitle"
        " == null ? '' : ' " + MIDDOT + " ${_directionLabel(l10n, "
        "directionForTitle)}'}'\n        : l10n.gateScanTitle;",
        "    final directionSuffix = directionForTitle == null\n        ? ''\n"
        "        : ' " + MIDDOT + " ${_directionLabel(l10n, directionForTitle)}';"
        "\n    final title =\n        showContext ? '$gateName$directionSuffix'"
        " : l10n.gateScanTitle;",
    )]),
    # A trailing comment cannot be re-flowed in place, so it moves above.
    ('test/core/validation/phone_validation_test.dart', [
        ("      expect(isStandardSaudiMobile('٠٥٠١٢"
         "٣٤٥٦٧'), isTrue); // Arabic-Indic digits",
         "      // Arabic-Indic digits.\n      expect(isStandardSaudiMobile("
         "'٠٥٠١٢٣٤٥٦٧'), "
         "isTrue);"),
        ("      expect(isStandardInternationalMobile('+0447700900123'), "
         "isFalse); // 0 lead",
         "      // A 0 leading the subscriber number, after the country code.\n"
         "      expect(isStandardInternationalMobile('+0447700900123'), "
         "isFalse);"),
    ]),
    # A JSON fixture: adjacent literals concatenate to the same bytes.
    ('test/features/account/profile_repository_upload_test.dart', [(
        "            '\"error\":{\"code\":\"bad_request\",\"messageEn\":\"Bad\","
        "\"messageAr\":\"خطأ\"},\"meta\":null}';",
        "            '\"error\":{\"code\":\"bad_request\",\"messageEn\":\"Bad\",'"
        "\n            '\"messageAr\":\"خطأ\"},\"meta\":null}';",
    )]),
    # A regex has no spaces to break at, so it splits after a literal `-`.
    ('test/features/gates/gate_scan_screen_test.dart', [(
        "        r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-"
        "[0-9a-f]{12}$',",
        "        r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-'\n"
        "        r'[89ab][0-9a-f]{3}-[0-9a-f]{12}$',",
    )]),
    ('test/features/meetings/meeting_confirm_screen_test.dart', [(
        "    expect(\n        find.text('This meeting is not awaiting "
        "confirmation'), findsOneWidget,);",
        "    expect(\n      find.text('This meeting is not awaiting "
        "confirmation'),\n      findsOneWidget,\n    );",
    )]),
]


def main():
    for path, pairs in EDITS:
        raw = io.open(path, encoding='utf-8', newline='').read()
        eol = '\r\n' if raw.count('\r\n') * 2 > raw.count('\n') else '\n'
        text = raw.replace('\r\n', '\n')
        for before, after in pairs:
            assert before in text, (path, before[:70])
            text = text.replace(before, after)
        io.open(path, 'w', encoding='utf-8', newline='').write(
            text.replace('\n', eol))
        print('  ok %s (%d edit(s))' % (path.split('/')[-1], len(pairs)))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
