import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../account/data/profile_repository.dart';
import '../requests/data/request_models.dart';
import '../requests/data/requests_repository.dart';
import '../speakers/widgets/meeting_request_sheet.dart';
import 'data/meetings_provider.dart';
import 'widgets/meeting_action_row.dart';
import 'widgets/meeting_card.dart';

/// Bilateral meetings — اللقاءات الثنائية · route: [RouteNames.meetings]
/// Purpose: a VIP-only page listing the user's **approved + upcoming** bilateral
///   meetings (speaker + delegation), with a "طلب جديد" create action and a
///   "السجل" link to the full requests history. Split from the requests-history
///   page (owner 2026-07-11, D-745) which stays in My-Area.
/// Data: [upcomingMeetingsProvider] (a filtered view of [myRequestsProvider] —
///   `GET /app/my-requests`), gated by [currentUserIsVipProvider].
/// Figma: 1408:9726 (اللقاءات الثنائية).
/// Perf: non-lazy ListView over the (small) meetings subset; pull-to-refresh.
/// Contract: reads the D-219-frozen my-requests feed; VIP enforced server-side
///   (the meeting-request endpoint 403s non-VIP) and mirrored here in-screen.
class MeetingsScreen extends ConsumerStatefulWidget {
  const MeetingsScreen({super.key});

  @override
  ConsumerState<MeetingsScreen> createState() => _MeetingsScreenState();
}

class _MeetingsScreenState extends ConsumerState<MeetingsScreen> {
  /// "طلب جديد" — open the meeting-request sheet (Figma 1776:5036) with the
  /// speaker picker for a new bilateral-meeting request. The feed refreshes when
  /// the sheet closes so a just-submitted meeting can appear once approved.
  Future<void> _openNewMeeting() async {
    final auth = ref.read(authControllerProvider);
    if (auth is! AuthStateSignedIn) {
      if (mounted) {
        context.pushNamed(RouteNames.signIn);
      }
      return;
    }
    final l10n = AppL10n.of(context);
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: SimfTokens.cardBeige,
      showDragHandle: false,
      shape: const RoundedRectangleBorder(
        borderRadius:
            BorderRadius.vertical(top: Radius.circular(SimfTokens.radius)),
      ),
      builder: (_) => MeetingRequestSheet(
        speakerId: null, // no fixed speaker → the picker is shown
        defaultName: auth.session.user.displayName,
        baseUrl: ref.read(simfDataConfigProvider).baseUrl,
        l10n: l10n,
      ),
    );
    if (mounted) {
      ref.invalidate(myRequestsProvider);
    }
  }

  /// Pull-to-refresh — re-fetch the shared feed the meetings view derives from.
  Future<void> _refresh() async {
    ref.invalidate(myRequestsProvider);
    await ref.read(upcomingMeetingsProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // Gate the whole page on VIP (the create endpoint is VIP-only). Resolve the
    // VIP flag first so non-VIP never briefly sees the list; a failed check is
    // treated as non-VIP (safe default).
    return SimfPageShell(
      title: l10n.meetingsTitle,
      onBack: () => backOrHome(context),
      body: ref.watch(currentUserIsVipProvider).when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (_, __) => _vipOnly(l10n),
            data: (isVip) => isVip ? _meetingsBody(l10n) : _vipOnly(l10n),
          ),
    );
  }

  Widget _vipOnly(AppL10n l10n) => Center(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: SimfEmptyState(
            icon: Icons.workspace_premium_outlined,
            message: l10n.meetingVipOnly,
          ),
        ),
      );

  Widget _meetingsBody(AppL10n l10n) {
    return ref.watch(upcomingMeetingsProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => SimfPullToRefresh(
            onRefresh: _refresh,
            child: SimfPullableHost(
              child: SimfErrorState(
                message: l10n.requestsError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(myRequestsProvider),
              ),
            ),
          ),
          data: (items) => _buildList(l10n, items),
        );
  }

  Widget _buildList(AppL10n l10n, List<AppRequestItem> items) {
    final isArabic = l10n.isArabic;
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    return SimfPullToRefresh(
      onRefresh: _refresh,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          MeetingActionRow(
            l10n: l10n,
            onNew: () => unawaited(_openNewMeeting()),
            onHistory: () => context.pushNamed(RouteNames.requests),
          ),
          const SizedBox(height: SimfTokens.space4),
          if (items.isEmpty)
            SimfEmptyState(
              icon: Icons.handshake_outlined,
              message: l10n.myMeetingsEmpty,
            )
          else
            for (final item in items)
              Padding(
                padding: const EdgeInsets.only(bottom: SimfTokens.space4),
                child: MeetingCard(
                  key: ValueKey<String>('${item.kind.wireValue}:${item.id}'),
                  item: item,
                  isArabic: isArabic,
                  l10n: l10n,
                  baseUrl: baseUrl,
                  // A speaker meeting opens the speaker profile; a delegation
                  // meeting has no speaker to open.
                  onTap: item.speakerId != null
                      ? () => context.pushNamed(
                            RouteNames.speakerProfile,
                            pathParameters: <String, String>{
                              RouteParams.speakerId: item.speakerId!,
                            },
                          )
                      : null,
                ),
              ),
        ],
      ),
    );
  }
}
