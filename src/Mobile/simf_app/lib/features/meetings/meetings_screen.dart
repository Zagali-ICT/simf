import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/delegations/widgets/delegation_meeting_request_sheet.dart';
import 'package:simf_app/features/meetings/data/meetings_provider.dart';
import 'package:simf_app/features/meetings/widgets/meeting_action_row.dart';
import 'package:simf_app/features/meetings/widgets/meeting_card.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/data/requests_repository.dart';
import 'package:simf_app/features/speakers/widgets/meeting_request_sheet.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Bilateral meetings — اللقاءات الثنائية · route: [RouteNames.meetings]
/// Purpose: lists **all** the user's bilateral meeting requests (speaker +
/// delegation), each carrying its status (pending / accepted / rejected /
/// cancelled), above the two "request meeting" buttons and the "السجل" link to
/// the full requests history (R9, D-767; was accepted+upcoming only, D-745).
/// Data: [authControllerProvider], [currentUserMeetingAccessProvider],
///       [myMeetingRequestsProvider], [simfDataConfigProvider].
/// 1408:9726 (اللقاءات الثنائية). Perf: non-lazy ListView over the (small)
/// meetings subset; pull-to-refresh. Contract: reads the D-219-frozen
/// my-requests feed; VIP enforced server-side (the meeting-request endpoint
/// 403s non-VIP) and mirrored here in-screen.
///
/// Route: `RouteNames.meetings`.
/// Data: [authControllerProvider], [currentUserMeetingAccessProvider],
///       [myMeetingRequestsProvider], [simfDataConfigProvider].
/// Perf: ListView builds every child up front — correct for a short static
///       page, a defect on a data feed.
class MeetingsScreen extends ConsumerStatefulWidget {
  const MeetingsScreen({super.key});

  @override
  ConsumerState<MeetingsScreen> createState() => _MeetingsScreenState();
}

class _MeetingsScreenState extends ConsumerState<MeetingsScreen> {
  /// "طلب مقابلة متحدث" — open the SPEAKER meeting-request sheet (Figma
  /// 1776:5036) with the speaker picker. The feed refreshes when the sheet
  /// closes so a just-submitted meeting can appear once approved.
  Future<void> _openSpeakerMeeting() async {
    final auth = ref.read(authControllerProvider);
    if (auth is! AuthStateSignedIn) {
      if (mounted) {
        unawaited(context.pushNamed(RouteNames.signIn));
      }
      return;
    }
    final l10n = AppL10n.of(context);
    await _openMeetingSheet(
      (_) => MeetingRequestSheet(
        speakerId: null, // no fixed speaker → the picker is shown
        defaultName: auth.session.user.displayName,
        baseUrl: ref.read(simfDataConfigProvider).baseUrl,
        l10n: l10n,
      ),
    );
  }

  /// "طلب اجتماع وفد" — open the DELEGATION meeting-request sheet with the
  /// delegation picker.
  Future<void> _openDelegationMeeting() async {
    final auth = ref.read(authControllerProvider);
    if (auth is! AuthStateSignedIn) {
      if (mounted) {
        unawaited(context.pushNamed(RouteNames.signIn));
      }
      return;
    }
    final l10n = AppL10n.of(context);
    await _openMeetingSheet(
      (_) => DelegationMeetingRequestSheet(country: null, l10n: l10n),
    );
  }

  /// Shared modal-sheet host for both request flows; refreshes the feed on
  /// close.
  Future<void> _openMeetingSheet(WidgetBuilder builder) async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: SimfTokens.cardBeige,
      showDragHandle: false,
      shape: const RoundedRectangleBorder(
        borderRadius:
            BorderRadius.vertical(top: Radius.circular(SimfTokens.radius)),
      ),
      builder: builder,
    );
    if (mounted) {
      ref.invalidate(myRequestsProvider);
    }
  }

  /// Pull-to-refresh — re-fetch the shared feed the meetings view derives from.
  Future<void> _refresh() {
    ref.invalidate(myRequestsProvider);
    return refreshAsync(ref, myMeetingRequestsProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // Bi-Meeting rework — gate the whole page on the two per-user meeting flags
    // (speaker OR delegation). Resolve access first so an unentitled user never
    // briefly sees the list; a failed check is treated as no-access (safe
    // default).
    return SimfPageShell(
      title: l10n.meetingsTitle,
      onBack: () => backOrHome(context),
      body: ref.watch(currentUserMeetingAccessProvider).when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (_, __) => _noAccess(l10n),
            data: (access) =>
                access.any ? _meetingsBody(l10n, access) : _noAccess(l10n),
          ),
    );
  }

  Widget _noAccess(AppL10n l10n) => Center(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: SimfEmptyState(
            icon: Icons.workspace_premium_outlined,
            message: l10n.meetingAccessRequired,
          ),
        ),
      );

  Widget _meetingsBody(AppL10n l10n, MeetingAccess access) {
    return ref.watch(myMeetingRequestsProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => SimfRefreshableMessage(
            onRefresh: _refresh,
            child: SimfErrorState(
              message: l10n.requestsError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(myRequestsProvider),
            ),
          ),
          data: (items) => _buildList(l10n, access, items),
        );
  }

  Widget _buildList(
      AppL10n l10n, MeetingAccess access, List<AppRequestItem> items,) {
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
            showSpeaker: access.speaker,
            showDelegation: access.delegation,
            onRequestSpeaker: () => unawaited(_openSpeakerMeeting()),
            onRequestDelegation: () => unawaited(_openDelegationMeeting()),
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
