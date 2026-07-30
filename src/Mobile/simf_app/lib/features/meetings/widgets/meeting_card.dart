import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import '../../../core/country_flag.dart';
import '../../../core/utils/saudi_time.dart';
import '../../requests/data/request_models.dart';
import '../../requests/widgets/request_status_style.dart';
import '../../speakers/widgets/speaker_photo_tile.dart';

/// One bilateral-meeting card on the اللقاءات الثنائية page (Figma 1408:9726): a
/// navy card with a green (accepted) hairline, carrying —
/// - **row 1**: the meeting-kind headline over the speaker's rank, with the
///   nationality **flag badge** at the inline end;
/// - **row 2**: the speaker **photo** + name (gold), with a chevron when the card
///   is tappable (a speaker meeting opens the speaker profile);
/// - **row 3**: the meeting date/time with a clock glyph.
///
/// R9 (D-767) — the card lists ANY of the user's meeting requests, so the border
/// colour and a status pill reflect the request's status (pending amber / accepted
/// green / rejected red / cancelled grey), reusing the requests-feed palette.
/// Speaker photo + flag come from the D-745 append-only wire fields
/// ([AppRequestItem.speakerId] / [AppRequestItem.countryId]); a delegation meeting
/// has no speaker photo and shows the anchor placeholder.
class MeetingCard extends StatelessWidget {
  const MeetingCard({
    required this.item,
    required this.isArabic,
    required this.l10n,
    required this.baseUrl,
    this.onTap,
    super.key,
  });

  final AppRequestItem item;
  final bool isArabic;
  final AppL10n l10n;
  final String baseUrl;

  /// Tapping the card — set for a speaker meeting (opens the speaker profile);
  /// null for a delegation meeting (no speaker to open, so no chevron).
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final card = Container(
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: requestStatusColor(item.status),
          width: SimfTokens.hairline,
        ),
      ),
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          _headlineRow(),
          const SizedBox(height: SimfTokens.space4),
          _speakerRow(),
          const SizedBox(height: SimfTokens.space4),
          _timeRow(),
        ],
      ),
    );

    if (onTap == null) {
      return card;
    }
    return Material(
      color: SimfTokens.transparent,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: card,
      ),
    );
  }

  // Row 1 — the kind headline over the rank line, with the flag badge at the end.
  Widget _headlineRow() {
    final flag = countryFlagEmoji(item.countryId);
    final rank = item.localizedRank(isArabic)?.trim() ?? '';
    return Container(
      padding: const EdgeInsets.only(bottom: SimfTokens.space2),
      decoration: const BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairline,
          ),
        ),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(
                  _headline(),
                  textAlign: TextAlign.start,
                  style: SimfTokens.labelWhiteBoldMd,
                ),
                if (rank.isNotEmpty) ...<Widget>[
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    rank,
                    textAlign: TextAlign.start,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: SimfTokens.labelBeigeSm,
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          _statusPill(),
          if (flag != null) ...<Widget>[
            const SizedBox(width: SimfTokens.space3),
            _FlagBadge(flag: flag),
          ],
        ],
      ),
    );
  }

  // Row 2 — the speaker photo + name (gold), with a chevron when tappable.
  Widget _speakerRow() {
    final photoUrl = item.speakerId != null
        ? '$baseUrl/app/assets/SpeakerPhoto/${item.speakerId}/image'
        : null;
    return Container(
      padding: const EdgeInsets.only(bottom: SimfTokens.space2),
      decoration: const BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairline,
          ),
        ),
      ),
      child: Row(
        children: <Widget>[
          SpeakerPhotoTile(imageUrl: photoUrl, size: 38),
          const SizedBox(width: SimfTokens.space2),
          Flexible(
            child: Text(
              item.localizedSubtitle(isArabic),
              textAlign: TextAlign.start,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: SimfTokens.labelGoldSemiboldSm,
            ),
          ),
          const Spacer(),
          if (onTap != null)
            Transform.rotate(
              angle: -math.pi / 2,
              child: const SimfSvgIcon(
                AppAssets.chevronLeft,
                size: 20,
                color: SimfTokens.accent,
              ),
            ),
        ],
      ),
    );
  }

  // Row 3 — the meeting date/time with a clock glyph, pinned to the inline start.
  Widget _timeRow() {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        // Keep "07:45 AM · اليوم" + clock reading L→R like the frame.
        textDirection: TextDirection.ltr,
        children: <Widget>[
          Text(
            _dateLine(),
            style: SimfTokens.labelBeigeMediumXs,
          ),
          const SizedBox(width: SimfTokens.space1),
          const Icon(
            Icons.schedule,
            size: 12,
            color: SimfTokens.beigeBorder,
          ),
        ],
      ),
    );
  }

  // R9 — the request's status pill (status colour at a soft fill + border with the
  // localized status label), reusing the requests-feed status palette + opacities.
  Widget _statusPill() {
    final color = requestStatusColor(item.status);
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: color.withValues(alpha: SimfTokens.chipFillOpacity),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: color.withValues(alpha: SimfTokens.chipBorderOpacity),
        ),
      ),
      child: Text(
        requestStatusLabel(l10n, item.status),
        style: TextStyle(
          color: color,
          fontSize: SimfTokens.textSm,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }

  String _headline() => item.kind == AppRequestKind.delegationMeeting
      ? l10n.requestKindDelegation
      : l10n.requestKindSpeaker;

  /// "07:45 AM · اليوم" when the meeting is today, else the absolute date (the
  /// same format the requests card uses, Figma 1408:9782).
  String _dateLine() {
    final date = saudiOf(item.displayDate);
    final now = DateTime.now();
    final isToday =
        date.year == now.year && date.month == now.month && date.day == now.day;
    return isToday ? l10n.requestTimeToday(date) : l10n.requestDate(date);
  }
}

/// The 48×48 nationality flag badge (Figma 1408:9726): the flag emoji on a soft
/// green well.
class _FlagBadge extends StatelessWidget {
  const _FlagBadge({required this.flag});

  final String flag;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 48,
      height: 48,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.flagBadgeBg,
        borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
        border: Border.all(color: SimfTokens.flagBadgeBorder),
      ),
      child: Text(
        flag,
        textDirection: TextDirection.ltr,
        // The frame's 28px flag glyph (textHero token == 28).
        style: const TextStyle(fontSize: SimfTokens.textHero, height: 1),
      ),
    );
  }
}
