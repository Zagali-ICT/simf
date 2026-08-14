import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/widgets/icon_box.dart';
import 'package:simf_app/features/requests/widgets/request_status_style.dart';

/// One expandable request card: the type icon, headline + context line + date,
/// a status-coloured leading strip, and (when expanded) the status detail and a
/// cancel action for the user's own pending requests.
class RequestCard extends StatefulWidget {
  const RequestCard({
    required this.item,
    required this.isArabic,
    required this.l10n,
    required this.onCancel,
    super.key,
  });

  final AppRequestItem item;
  final bool isArabic;
  final AppL10n l10n;
  final VoidCallback onCancel;

  @override
  State<RequestCard> createState() => _RequestCardState();
}

class _RequestCardState extends State<RequestCard> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final item = widget.item;
    final l10n = widget.l10n;
    final statusColor = requestStatusColor(item.status);
    final subtitle = item.localizedSubtitle(isArabic: widget.isArabic);

    // NOT a DecoratedBox. Container insets its child by BoxDecoration.padding,
    // which is the border dimensions, and this decoration has a border — the
    // swap moved a golden by 2.42% when it was tried (2026-08-14).
    // ignore: use_decorated_box
    return Container(
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        // Full status-coloured hairline (Figma 1408:9773 — 0.5px all round).
        border: Border.all(color: statusColor, width: SimfTokens.hairlineBold),
      ),
      child: Column(
        children: <Widget>[
          InkWell(
            onTap: () => setState(() => _expanded = !_expanded),
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            child: Padding(
              padding: const EdgeInsets.all(SimfTokens.space2),
              child: Row(
                children: <Widget>[
                  IconBox(icon: _kindIcon(item.kind)),
                  const SizedBox(width: SimfTokens.space2),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        Text(
                          _kindHeadline(l10n, item.kind),
                          textAlign: TextAlign.start,
                          style: SimfTokens.labelWhiteMedium,
                        ),
                        if (subtitle.isNotEmpty) ...<Widget>[
                          const SizedBox(height: SimfTokens.space2),
                          Text(
                            subtitle,
                            textAlign: TextAlign.start,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: SimfTokens.labelBeigeSm,
                          ),
                        ],
                        const SizedBox(height: SimfTokens.space2),
                        Text(
                          _dateLine(l10n),
                          // Pinned LTR keeps the time/date reading L→R (Figma
                          // 1408:9782 — "07:45 AM · اليوم" today, else the date
                          // "12 يناير 2026"); align to the trailing edge under
                          // the right-aligned title.
                          textDirection: TextDirection.ltr,
                          textAlign: TextAlign.end,
                          style: SimfTokens.labelBeigeSemiboldXs,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: SimfTokens.space2),
                  // The exact Figma chevron (iconamoon:arrow-up-2, 1408:9774) —
                  // a left "‹" glyph rotated to point down (collapsed) / up
                  // (expanded); gold per the frame.
                  Transform.rotate(
                    angle: _expanded ? math.pi / 2 : -math.pi / 2,
                    child: const SimfSvgIcon(
                      AppAssets.chevronLeft,
                      size: SimfTokens.requestCardSizeMd,
                      color: SimfTokens.accent,
                    ),
                  ),
                ],
              ),
            ),
          ),
          if (_expanded) _buildDetail(l10n, item, statusColor),
        ],
      ),
    );
  }

  /// The card date line — "07:45 AM · اليوم" when the request's date is today,
  /// else the absolute date "12 يناير 2026" (Figma 1408:9782).
  String _dateLine(AppL10n l10n) {
    final date = saudiOf(widget.item.displayDate);
    final now = DateTime.now();
    final isToday =
        date.year == now.year && date.month == now.month && date.day == now.day;
    return isToday ? l10n.requestTimeToday(date) : l10n.requestDate(date);
  }

  Widget _buildDetail(AppL10n l10n, AppRequestItem item, Color statusColor) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space3,
        0,
        SimfTokens.space3,
        SimfTokens.space3,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const Divider(color: SimfTokens.line, height: SimfTokens.space4),
          Row(
            children: <Widget>[
              Container(
                width: SimfTokens.space2,
                height: SimfTokens.space2,
                decoration:
                    BoxDecoration(color: statusColor, shape: BoxShape.circle),
              ),
              const SizedBox(width: SimfTokens.space2),
              Text(
                requestStatusLabel(
                  l10n,
                  item.status,
                  checkedIn: item.checkedIn,
                ),
                style: TextStyle(
                  color: statusColor,
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
          if (item.responseNote != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            Text(
              item.responseNote!,
              textAlign: TextAlign.start,
              style: SimfTokens.labelBeigeSm,
            ),
          ],
          if (item.canCancel) ...<Widget>[
            const SizedBox(height: SimfTokens.space3),
            Align(
              alignment: AlignmentDirectional.centerEnd,
              child: OutlinedButton.icon(
                onPressed: widget.onCancel,
                icon: const Icon(Icons.close,
                    size: SimfTokens.requestCardSizeSm,
                    color: SimfTokens.danger,),
                label: Text(
                  l10n.requestCancel,
                  style: SimfTokens.bodyDanger,
                ),
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: SimfTokens.danger),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

String _kindHeadline(AppL10n l10n, AppRequestKind kind) {
  switch (kind) {
    case AppRequestKind.delegationMeeting:
      return l10n.requestKindDelegation;
    case AppRequestKind.sessionAttendance:
      return l10n.requestKindSession;
    case AppRequestKind.participationDocument:
      return l10n.requestKindDocument;
    case AppRequestKind.badgeUpdate:
      return l10n.requestKindBadge;
    case AppRequestKind.speakerMeeting:
      return l10n.requestKindSpeaker;
  }
}

IconData _kindIcon(AppRequestKind kind) {
  switch (kind) {
    case AppRequestKind.delegationMeeting:
      return Icons.flag_outlined;
    case AppRequestKind.sessionAttendance:
      return Icons.event_seat_outlined;
    case AppRequestKind.participationDocument:
      return Icons.description_outlined;
    case AppRequestKind.badgeUpdate:
      return Icons.badge_outlined;
    case AppRequestKind.speakerMeeting:
      return Icons.person_outline;
  }
}
