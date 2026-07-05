import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../account/biometric_auth.dart';
import '../../sessions/data/session_favourites.dart';
import '../data/myarea_models.dart';
import 'my_area_identity_card.dart';
import 'my_area_rows.dart';

/// The Approved-user My-Area dashboard body (frame 213:963 / 758:1283): the
/// identity card, the two share pills, the الإحصائيات stat tiles, the جدولي
/// اليوم schedule groups, and the المزيد rows. The stat tiles are
/// **display-only** — D-653 restored them to match the frame after D-609's
/// removal; they show the counts but no longer drill into list screens. Owns
/// the favourites-count watch; the three async account actions come from the
/// screen (they hold the ImagePicker / identity flow + dashboard reload).
class MyAreaDashboardBody extends ConsumerWidget {
  const MyAreaDashboardBody({
    required this.dashboard,
    required this.referenceNumber,
    required this.onShareContact,
    required this.onChangeAvatar,
    required this.onUploadIdDocument,
    super.key,
  });

  final MyAreaDashboard dashboard;
  final String? referenceNumber;
  final VoidCallback onShareContact;
  final VoidCallback onChangeAvatar;
  final VoidCallback onUploadIdDocument;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final identity = dashboard.identity;
    final tier = identity.localizedTier(isArabic);
    final enrolled =
        l10n.enrolledInSessions(dashboard.counters.bookedSessionsCount);
    final subtitle = tier == null ? enrolled : '$tier · $enrolled';
    // The reference number (SIMF-2026-…) is the displayed ID; fall back to the
    // qrId only until the reference loads.
    final reference = referenceNumber != null
        ? '#$referenceNumber'
        : (identity.qrId == null ? null : '#${identity.qrId}');
    // Frame 758:1283 — جدولي اليوم splits into a "جلسات" group and a "مقابلات"
    // group, each under its own gold sub-header.
    final sessions =
        dashboard.todaySchedule.where((i) => i.isSession).toList();
    final meetings =
        dashboard.todaySchedule.where((i) => !i.isSession).toList();

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        MyAreaIdentityCard(
          name: identity.localizedName(isArabic),
          line: subtitle,
          reference: reference,
          shareLabel: l10n.shareLabel,
          onShare: onShareContact,
          onAvatarTap: onChangeAvatar,
          avatarTooltip: l10n.avatarChangeTooltip,
        ),
        const SizedBox(height: SimfTokens.space4),
        // Frame 758:1283 — two horizontal share-action pills (h48, exact
        // iconify glyphs). مشاركة جهة اتصال at the inline-start (right),
        // مشاركة ملفي at the end (left).
        Row(
          children: <Widget>[
            Expanded(
              child: MyAreaShareTile(
                label: l10n.shareContact,
                iconAsset: 'assets/icons/share_contact.svg',
                onTap: onShareContact,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: MyAreaShareTile(
                label: l10n.shareMyProfile,
                iconAsset: 'assets/icons/share_profile.svg',
                onTap: () => context.pushNamed(RouteNames.shareMyContact),
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space6),
        // الإحصائيات (frame 758:1283) — display-only (D-653, owner). مقابلات =
        // the dashboard meetings count; جلسات محفوظة = the favourited-sessions
        // set (there is no dashboard "saved" counter, so the favourites set is
        // the source of truth). The tiles carry no onTap — the drill-down list
        // screens stay retired (D-609); the numbers are informational only.
        SimfSectionHeader(title: l10n.statisticsTitle),
        const SizedBox(height: SimfTokens.space3),
        SimfTileRow(
          children: <Widget>[
            SimfStatTile(
              value: dashboard.counters.meetingsCount,
              label: l10n.statMeetings,
            ),
            SimfStatTile(
              value:
                  ref.watch(sessionFavouritesProvider).valueOrNull?.length ?? 0,
              label: l10n.statBookedSessions,
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space6),
        SimfSectionHeader(title: l10n.todayScheduleTitle),
        const SizedBox(height: SimfTokens.space3),
        if (sessions.isEmpty && meetings.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: SimfTokens.space4),
            child: Text(
              l10n.scheduleEmpty,
              style: const TextStyle(color: SimfTokens.beigeBorder),
            ),
          )
        else
          for (final (groupLabel, items)
              in <(String, List<MyAreaScheduleItem>)>[
            (l10n.scheduleSessionsGroup, sessions),
            (l10n.scheduleMeetingsGroup, meetings),
          ])
            if (items.isNotEmpty) ...<Widget>[
              MyAreaScheduleGroupHeader(label: groupLabel),
              const SizedBox(height: SimfTokens.space3),
              for (final item in items)
                Padding(
                  padding: const EdgeInsets.only(bottom: SimfTokens.space3),
                  child: MyAreaScheduleRow(
                    item: item,
                    isArabic: isArabic,
                    onTap: item.isSession && item.sessionId != null
                        ? () => context.pushNamed(
                              RouteNames.sessionDetail,
                              pathParameters: <String, String>{
                                'sessionId': item.sessionId!,
                              },
                            )
                        : null,
                  ),
                ),
            ],
        const SizedBox(height: SimfTokens.space3),
        SimfSectionHeader(title: l10n.moreSectionSettings),
        const SizedBox(height: SimfTokens.space3),
        MyAreaMoreRow(
          label: l10n.smartBadgeLink,
          onTap: () => context.pushNamed(RouteNames.badge),
        ),
        const SizedBox(height: SimfTokens.space4),
        // D-500 (Wave 5, الطلبات) — the unified requests feed.
        MyAreaMoreRow(
          label: l10n.requestsLink,
          onTap: () => context.pushNamed(RouteNames.requests),
        ),
        const SizedBox(height: SimfTokens.space4),
        // D-485 — the standalone Join-a-session hub (the seat-booking flow; the
        // other entry is the Join CTA on each session page).
        MyAreaMoreRow(
          label: l10n.joinHubTitle,
          onTap: () => context.pushNamed(RouteNames.joinSessionHub),
        ),
        const SizedBox(height: SimfTokens.space4),
        // Photos-only profile edit (owner scope): re-upload the ID document
        // from the gallery. The face photo is changed by tapping the avatar.
        MyAreaMoreRow(
          label: l10n.updateIdPhotoLink,
          onTap: onUploadIdDocument,
        ),
        const SizedBox(height: SimfTokens.space4),
        MyAreaMoreRow(
          label: l10n.moreTitle,
          onTap: () => context.pushNamed(RouteNames.more),
        ),
        // Face-ID sign-in enable/disable (D-445) — self-hides when the device
        // has no usable biometric. Also offered in the side menu.
        const FaceIdToggleTile(),
        // Calendar export + sign-out moved to the shell's side drawer (D-396),
        // matching frame 213:963 which ends المزيد at اعدادات الحساب.
      ],
    );
  }
}
