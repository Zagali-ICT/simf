import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';

/// FR-MOD-001 — one row on the moderator's "جلساتي / My sessions" list: a
/// session they actually hold the per-session grant on, tapping through to its
/// Q&A desk.
///
/// Built on the shared [SimfListRow] rather than a page-local card, so it reads
/// like every other navigable row in the app. Subtitle is hall + start on the
/// Saudi wall clock, 12-hour — no user-facing zoned stamp (D-219).
class ModeratedSessionTile extends StatelessWidget {
  const ModeratedSessionTile({
    required this.l10n,
    required this.session,
    required this.onTap,
    super.key,
  });

  final AppL10n l10n;
  final ModeratedSession session;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final hall = session.localizedHall(isArabic);
    final time = formatSaudiTime12(session.start);
    return SimfListRow(
      title: session.localizedTitle(isArabic),
      subtitle: hall.trim().isEmpty ? time : '$hall · $time',
      badgeOutlined: true,
      badge: const Icon(
        Icons.forum_outlined,
        size: SimfTokens.moderatedSessionTileSize,
        color: SimfTokens.accent,
      ),
      onTap: onTap,
    );
  }
}
