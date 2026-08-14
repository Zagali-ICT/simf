import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/widgets/my_area_identity_card.dart';
import 'package:simf_app/features/myarea/widgets/my_area_more_row.dart';
import 'package:simf_app/features/myarea/widgets/my_area_schedule_group_header.dart';
import 'package:simf_app/features/myarea/widgets/my_area_schedule_row.dart';
import 'package:simf_app/features/myarea/widgets/my_area_share_tile.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';

/// The Approved-user My-Area dashboard body (frame 213:963 / 758:1283): the
/// identity card, the share pill, the الإحصائيات stat tiles, the جدولي
/// اليوم schedule groups, and the المزيد rows. The stat tiles are
/// **display-only** — D-653 restored them to match the frame after D-609's
/// removal; they show the counts but no longer drill into list screens. Owns
/// the favourites-count watch; the avatar-change action comes from the screen
/// (it holds the identity-verification flow + dashboard reload). Both share
/// affordances (the identity-card button + the مشاركة جهة اتصال pill) open the
/// in-app "شارك جهة اتصالي" QR screen (#21).
class MyAreaDashboardBody extends ConsumerWidget {
  const MyAreaDashboardBody({
    required this.dashboard,
    required this.referenceNumber,
    required this.onChangeAvatar,
    super.key,
  });

  final MyAreaDashboard dashboard;
  final String? referenceNumber;
  final VoidCallback onChangeAvatar;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final identity = dashboard.identity;
    final tier = identity.localizedTier(isArabic: isArabic);
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
    final sessions = dashboard.todaySchedule.where((i) => i.isSession).toList();
    final meetings =
        dashboard.todaySchedule.where((i) => !i.isSession).toList();

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        MyAreaIdentityCard(
          name: identity.localizedName(isArabic: isArabic),
          line: subtitle,
          reference: reference,
          shareLabel: l10n.shareLabel,
          onShare: () => context.pushNamed(RouteNames.shareMyContact),
          onAvatarTap: onChangeAvatar,
          avatarTooltip: l10n.avatarChangeTooltip,
        ),
        const SizedBox(height: SimfTokens.space4),
        // The مشاركة جهة اتصال pill (frame 758:1283, h48) opens the in-app
        // "شارك جهة اتصالي" QR screen (#21). The old مشاركة ملفي pill was a
        // duplicate of this exact navigation and was dropped (owner).
        MyAreaShareTile(
          label: l10n.shareContact,
          iconAsset: AppAssets.shareContact,
          onTap: () => context.pushNamed(RouteNames.shareMyContact),
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
              style: SimfTokens.hintBeige,
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
                                RouteParams.sessionId: item.sessionId!,
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
        // #14 — edit the account's interests after sign-up (same interests page
        // as sign-up, in edit mode).
        MyAreaMoreRow(
          label: l10n.interestsTitle,
          onTap: () => context.pushNamed(RouteNames.myInterests),
        ),
        const SizedBox(height: SimfTokens.space4),
        // Owner 2026-07-26 — add / edit the mobile number (validate only, no
        // OTP), next to the other self-service profile edits.
        MyAreaMoreRow(
          label: l10n.myMobileTitle,
          onTap: () => context.pushNamed(RouteNames.myMobile),
        ),
        const SizedBox(height: SimfTokens.space4),
        // D-485 — the standalone Join-a-session hub (the seat-booking flow; the
        // other entry is the Join CTA on each session page).
        MyAreaMoreRow(
          label: l10n.joinHubTitle,
          onTap: () => context.pushNamed(RouteNames.joinSessionHub),
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
