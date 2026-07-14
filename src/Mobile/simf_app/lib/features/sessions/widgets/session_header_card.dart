import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/utils/gregorian_month_names.dart';
import '../../../core/utils/weekday_names.dart';
import '../data/session_models.dart';

/// The session header card (frame 889:2716): a navy box holding the title +
/// gold index badge, the clock/calendar meta line, and the رابط الجلسة /
/// ملخص الجلسة action buttons — all right-aligned for RTL.
class SessionHeaderCard extends StatelessWidget {
  const SessionHeaderCard({
    required this.detail,
    required this.isArabic,
    required this.l10n,
    required this.onSessionLink,
    required this.onSessionSummary,
    required this.summaryEnabled,
    required this.liveEnabled,
    super.key,
  });

  final SessionDetail detail;
  final bool isArabic;
  final AppL10n l10n;
  final VoidCallback onSessionLink;
  final VoidCallback onSessionSummary;

  /// Owner 2026-07-14 — the ملخص الجلسة button is active only when a summary
  /// can exist (the session has ended, and — where known — has a published
  /// محضر); a future/live session shows it greyed/inactive.
  final bool summaryEnabled;

  /// Owner 2026-07-14 — the رابط الجلسة button is active only when the session
  /// is live AND carries an online feed; otherwise it shows greyed/inactive.
  final bool liveEnabled;

  @override
  Widget build(BuildContext context) {
    // D-567 (Figma 889:2604) — the gold badge shows the session's 1-based
    // position within its day, zero-padded ("02"); it falls back to the session
    // code until the API supplies the ordinal (displayOrder 0, e.g. an older API).
    final badgeText = detail.displayOrder > 0
        ? detail.displayOrder.toString().padLeft(2, '0')
        : detail.code;
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
                _IndexBadge(text: badgeText),
                const SizedBox(width: SimfTokens.space2),
              ],
              Expanded(
                child: Text(
                  detail.localizedTitle(isArabic),
                  textAlign: TextAlign.start,
                  // Frame 889:2705 — 16px SemiBold white title line.
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w600,
                    fontSize: SimfTokens.textLg,
                    height: 1.4,
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: SimfTokens.space4),
          _MetaRow(detail: detail, isArabic: isArabic),
          const SizedBox(height: SimfTokens.space4),
          // Frame 889:2715 — both action buttons keep their slots (layout
          // unchanged), but each is GATED on the session's state (owner
          // 2026-07-14, superseding the 2026-06-30 "always both active"):
          // ملخص الجلسة (gold hairline) is active only once a summary exists;
          // رابط الجلسة (beige hairline) only while the session streams live.
          // A gated-off button greys out and stops tapping (inactive, not
          // hidden — the frame's two-chip row is preserved).
          Row(
            children: <Widget>[
              Expanded(
                child: _HeaderActionButton(
                  label: l10n.sessionSummary,
                  accented: true,
                  enabled: summaryEnabled,
                  onTap: onSessionSummary,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: _HeaderActionButton(
                  label: l10n.sessionLink,
                  accented: false,
                  enabled: liveEnabled,
                  onTap: onSessionLink,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// One header-card action button (frame 889:2708/889:2709): a 34-high navy chip
/// on the 4px radius with a centred 12px SemiBold label. The accented variant
/// (ملخص الجلسة) carries the 0.5px gold hairline + gold text; the plain variant
/// (رابط الجلسة) the 0.2px beige hairline + white text.
class _HeaderActionButton extends StatelessWidget {
  const _HeaderActionButton({
    required this.label,
    required this.accented,
    required this.enabled,
    required this.onTap,
  });

  final String label;
  final bool accented;

  /// False greys the chip and drops the tap (owner 2026-07-14): an inactive
  /// action whose state does not apply yet (a summary on a future session).
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Disabled overrides the accent/plain palette with the shell's disabled
    // tokens (same greying the locked اسأل المحاور / بطاقتي cards use).
    final fg = !enabled
        ? SimfTokens.navyDisabledText
        : (accented ? SimfTokens.accent : Colors.white);
    final borderColor = !enabled
        ? SimfTokens.navyDisabledBorder
        : (accented ? SimfTokens.accent : SimfTokens.beigeBorder);
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        onTap: enabled ? onTap : null,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Container(
          height: 34,
          alignment: Alignment.center,
          padding: const EdgeInsets.all(SimfTokens.space2),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            border: Border.all(
              color: borderColor,
              width: accented ? SimfTokens.hairlineBold : SimfTokens.hairline,
            ),
          ),
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: fg,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ),
    );
  }
}

/// The gold index badge (frame 889:2604): a 40×40 gold rounded square with the
/// day-ordinal in white extrabold, always LTR (e.g. "02"); a longer fallback
/// code scales down to fit rather than overflowing the badge.
class _IndexBadge extends StatelessWidget {
  const _IndexBadge({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 40,
      height: 40,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusLarge + 2),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4),
        // A two-digit ordinal ("02") shows at full size; a longer fallback code
        // ("S-001" / "S-TODAY") scales down to fit the 40×40 badge instead of
        // overflowing or wrapping.
        child: FittedBox(
          fit: BoxFit.scaleDown,
          child: Text(
            text,
            textDirection: TextDirection.ltr,
            maxLines: 1,
            softWrap: false,
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w800,
              fontSize: SimfTokens.textLg,
            ),
          ),
        ),
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
          _MetaItem(
            icon: Icons.schedule_outlined,
            label: '${_time(start)} — ${_time(end)}',
          ),
          const Text(
            '·',
            // Frame 889:2702 — the separator dot is white (#FFFFFF), heavier than
            // the beige time/date items beside it.
            style: TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w900,
              fontSize: SimfTokens.textLg,
            ),
          ),
          _MetaItem(
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

/// One icon + label pair in the meta line (frame 889:2687/889:2686).
class _MetaItem extends StatelessWidget {
  const _MetaItem({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Icon(icon, size: 14, color: SimfTokens.beigeBorder),
        const SizedBox(width: SimfTokens.space2),
        Text(
          label,
          // SemiBold (owner 2026-06-30): the time/date numbers read bolder than
          // the frame's Regular. The ambient Directionality is LTR so the time
          // digits read start→end and the icon leads on the left.
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
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
