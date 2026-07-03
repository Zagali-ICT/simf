import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

/// Owner 2026-06-30 — the Join CTA for an approved account holding no
/// reservation: a single full-width gold "الانضمام إلى الجلسة" button (no hint,
/// no icon, no section heading). The seating behaviour is decided by the
/// screen's join handler (open-seating joins in place with a confirm;
/// assigned-seat opens the picker), so the button needs no mode of its own.
class SessionJoinButton extends StatelessWidget {
  const SessionJoinButton({
    required this.busy,
    required this.l10n,
    required this.onJoin,
    super.key,
  });

  final bool busy;
  final AppL10n l10n;
  final VoidCallback onJoin;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: busy ? null : onJoin,
      style: FilledButton.styleFrom(
        minimumSize: const Size.fromHeight(48),
        backgroundColor: SimfTokens.accent,
        foregroundColor: SimfTokens.surface,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
      ),
      child: Text(
        l10n.joinSessionCta,
        style: const TextStyle(
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textLg,
        ),
      ),
    );
  }
}

/// The cancel-reservation action (owner 2026-06-30): a centred, plain white
/// text button sitting on its own line under the reminder/calendar row —
/// replaces the old red ✕ "إلغاء الحجز" link inside the seat card.
class CancelReservationLink extends StatelessWidget {
  const CancelReservationLink({
    required this.label,
    required this.busy,
    required this.onCancel,
    super.key,
  });

  final String label;
  final bool busy;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: TextButton(
        onPressed: busy ? null : onCancel,
        child: Text(
          label,
          style: const TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w600,
            fontSize: SimfTokens.textMd,
          ),
        ),
      ),
    );
  }
}

/// The two CTAs (frame 897:2872): أضف إلى تقويمي (gold filled, calendar icon)
/// fills the inline start, تذكير (outlined, clock icon) trails it. RTL places
/// the gold button on the right and تذكير on the left, as the frame shows.
class SessionCtaRow extends StatelessWidget {
  const SessionCtaRow({
    required this.l10n,
    required this.onAddToCalendar,
    required this.onRemind,
    super.key,
  });

  final AppL10n l10n;
  final VoidCallback onAddToCalendar;
  final VoidCallback onRemind;

  @override
  Widget build(BuildContext context) {
    // RTL: the first child is at the inline start (physical right). The frame
    // puts أضف إلى تقويمي (gold) on the right and تذكير (outlined) on the left,
    // so the gold Expanded button leads and the reminder button trails.
    // Frame 897:2872 — in each button the ICON leads (inline-start = physical
    // right) and the label follows (owner 2026-06-30: "icon first"), so each
    // inner Row is [icon, gap, label].
    return Row(
      children: <Widget>[
        Expanded(
          child: FilledButton(
            onPressed: onAddToCalendar,
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(48),
              backgroundColor: SimfTokens.accent,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                const Icon(
                  Icons.calendar_today_outlined,
                  size: 24,
                  color: Colors.white,
                ),
                const SizedBox(width: SimfTokens.space2),
                Flexible(
                  child: Text(
                    l10n.addToCalendar,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w700,
                      fontSize: SimfTokens.textLg,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        OutlinedButton(
          onPressed: onRemind,
          style: OutlinedButton.styleFrom(
            // Height 48, width sized to content — this is a non-Expanded child
            // of the Row, so Size.fromHeight (width = infinity) would force an
            // infinite width and crash layout. The calendar button beside it
            // takes the remaining width via Expanded.
            minimumSize: const Size(0, 48),
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space6),
            side: const BorderSide(color: SimfTokens.accent),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              const Icon(
                Icons.schedule_outlined,
                size: 24,
                color: Colors.white,
              ),
              const SizedBox(width: SimfTokens.space2),
              Flexible(
                child: Text(
                  l10n.reminder,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textLg,
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
