import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/utils/gregorian_month_names.dart';
import '../../../core/utils/weekday_names.dart';
import '../data/session_models.dart';
import 'category_pill.dart';
import 'header_action_button.dart';
import 'index_badge.dart';
import 'meta_item.dart';

/// The session header card (frame 889:2716): a navy box holding the title +
/// gold index badge, the category tag pill (PAR-D3, when the session carries a
/// category), the clock/calendar meta line, and the رابط الجلسة / ملخص الجلسة
/// action buttons — all right-aligned for RTL.
class SessionHeaderCard extends StatelessWidget {
  const SessionHeaderCard({
    required this.detail,
    required this.isArabic,
    required this.l10n,
    required this.onSessionLink,
    required this.onSessionSummary,
    required this.summaryEnabled,
    required this.liveEnabled,
    this.titleAndTimeOnly = false,
    super.key,
  });

  final SessionDetail detail;
  final bool isArabic;
  final AppL10n l10n;
  final VoidCallback onSessionLink;
  final VoidCallback onSessionSummary;

  /// Owner 2026-07-14 — the ملخص الجلسة button is active only once the session
  /// has ENDED (a future/live session has no محضر → greyed/inactive). The detail
  /// contract carries no summary flag, so this gates on the phase, not on whether
  /// a محضر was actually published; an ended-but-unsummarised session opens the
  /// summary screen's graceful empty note. (The list surfaces gate on the real
  /// hasPublishedSummary flag instead.)
  final bool summaryEnabled;

  /// Owner 2026-07-14 — the رابط الجلسة button is active only when the session
  /// is live AND carries an online feed; otherwise it shows greyed/inactive.
  final bool liveEnabled;

  /// #29 (owner Q10, 2026-07-30) — a **workshop** shows its title + time ONLY,
  /// so the card drops the category pill and the ملخص الجلسة / رابط الجلسة
  /// action row. The rest of the detail is suppressed by [SessionDetailBody].
  final bool titleAndTimeOnly;

  @override
  Widget build(BuildContext context) {
    // D-567 (Figma 889:2604) — the gold badge shows the session's 1-based
    // position within its day, zero-padded ("02"); it falls back to the session
    // code until the API supplies the ordinal (displayOrder 0, e.g. an older API).
    final badgeText = detail.displayOrder > 0
        ? detail.displayOrder.toString().padLeft(2, '0')
        : detail.code;
    // PAR-D3 — the category tag pill under the title. Shown only when the
    // session actually carries a category (the lookup ships empty pending the
    // client's list, OI-2 / D-226), and never on the #29 workshop reduction.
    final category = titleAndTimeOnly
        ? null
        : detail.localizedCategory(isArabic)?.trim();
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          // Frame 889:2706 — RTL: the gold index badge (the BOLD day-ordinal
          // "02", D-567) leads at the inline-start (physical right); the session
          // title follows to its inline-end (left), packed against the badge.
          Row(
            children: <Widget>[
              if (badgeText.isNotEmpty) ...<Widget>[
                IndexBadge(text: badgeText),
                const SizedBox(width: SimfTokens.space2),
              ],
              Expanded(
                child: Text(
                  detail.localizedTitle(isArabic),
                  textAlign: TextAlign.start,
                  // Frame 889:2705 — 16px SemiBold white title line.
                  style: SimfTokens.labelWhiteSemiboldLgTall,
                ),
              ),
            ],
          ),

          if (category != null && category.isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            Align(
              alignment: AlignmentDirectional.centerStart,
              child: CategoryPill(label: category),
            ),
          ],

          const SizedBox(height: SimfTokens.space4),
          _MetaRow(detail: detail, isArabic: isArabic),
          // Frame 889:2715 — both action buttons keep their slots (layout
          // unchanged), but each is GATED on the session's state (owner
          // 2026-07-14, superseding the 2026-06-30 "always both active"):
          // ملخص الجلسة (gold hairline) is active only once a summary exists;
          // رابط الجلسة (beige hairline) only while the session streams live.
          // A gated-off button greys out and stops tapping (inactive, not
          // hidden — the frame's two-chip row is preserved). #29: a workshop
          // has neither a summary nor a feed, so the row drops entirely.
          if (!titleAndTimeOnly) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            Row(
              children: <Widget>[
                Expanded(
                  child: HeaderActionButton(
                    label: l10n.sessionSummary,
                    accented: true,
                    enabled: summaryEnabled,
                    onTap: onSessionSummary,
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: HeaderActionButton(
                    label: l10n.sessionLink,
                    accented: false,
                    enabled: liveEnabled,
                    onTap: onSessionLink,
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// The meta line (frame 889:2698): a clock + time range, a separator dot, and a
/// calendar + weekday/day, in beige paragraph text. Laid **LTR** so each segment
/// is icon→text (icon leads at the left, owner 2026-06-30) and the time reads
/// "09:00 — 10:30" start→end; the time segment sits left, the date segment right
/// (matching the frame). The Arabic date still renders RTL within its own box.
class _MetaRow extends StatelessWidget {
  const _MetaRow({required this.detail, required this.isArabic});

  final SessionDetail detail;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final start = detail.startLocal;
    final end = detail.endLocal;
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Wrap(
        alignment: WrapAlignment.start,
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: SimfTokens.space3,
        runSpacing: SimfTokens.space1,
        children: <Widget>[
          MetaItem(
            icon: Icons.schedule_outlined,
            label: '${_time(start)} — ${_time(end)}',
          ),
          const Text(
            '·',
            // Frame 889:2702 — the separator dot is white (#FFFFFF), heavier than
            // the beige time/date items beside it.
            style: SimfTokens.labelWhiteBlackLg,
          ),
          MetaItem(
            icon: Icons.calendar_today_outlined,
            label: '${gregorianWeekdayName(start, isArabic)} · '
                '${start.day.toString().padLeft(2, '0')} '
                '${gregorianMonthName(start.month, isArabic)}',
          ),
        ],
      ),
    );
  }
}

/// Interim local time format: `HH:MM` (device-local). Final locale-aware
/// formatting lands with the designer pass (SIMF-VID-001).
String _time(DateTime local) {
  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');
  return '$hour:$minute';
}
