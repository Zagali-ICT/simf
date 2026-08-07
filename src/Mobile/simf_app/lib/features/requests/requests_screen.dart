import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_confirm_dialog.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/refresh.dart';
import '../account/data/profile_repository.dart';
import '../speakers/widgets/meeting_request_sheet.dart';
import 'data/request_models.dart';
import 'data/requests_repository.dart';
import 'widgets/request_action_row.dart';
import 'widgets/request_card.dart';
import 'widgets/request_status_chips.dart';

/// D-500 (Wave 5, الطلبات 1408:9726) — the unified requests feed. A "طلب جديد"
/// action plus status filter chips (with counts), over expandable cards across
/// every request kind the user submitted (speaker / delegation / session
/// attendance / participation-document / badge-update). Supersedes the read-only
/// My-meetings screen. The user can cancel their own pending speaker / document
/// / badge requests.
class RequestsScreen extends ConsumerStatefulWidget {
  const RequestsScreen({super.key});

  @override
  ConsumerState<RequestsScreen> createState() => _RequestsScreenState();
}

class _RequestsScreenState extends ConsumerState<RequestsScreen> {
  AppRequestStatus? _filter;

  void _toast(String message) {
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  /// "طلب جديد" (owner 2026-07-08) opens the meeting-request sheet (Figma
  /// 1776:5036) with the speaker picker — a new bilateral-meeting request. The
  /// list refreshes when the sheet closes so a just-submitted request shows.
  Future<void> _openNewRequest() async {
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
      // The beige "طلب مقابلة" sheet with its own gold drag handle.
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

  Future<void> _cancel(AppRequestItem item) async {
    final l10n = AppL10n.of(context);
    final confirmed = await SimfConfirmDialog.show(
      context,
      title: l10n.requestCancelConfirm,
      confirmLabel: l10n.requestCancel,
      cancelLabel: l10n.requestCancelKeep,
      isDestructive: true,
    );
    if (!confirmed) {
      return;
    }
    try {
      await ref
          .read(requestsRepositoryProvider)
          .cancelRequest(kind: item.kind, id: item.id);
      if (!mounted) {
        return;
      }
      ref.invalidate(myRequestsProvider);
      _toast(l10n.requestCancelled);
    } on ApiFailure {
      _toast(l10n.requestCancelFailed);
    }
  }

  /// Pull-to-refresh — re-fetch the requests feed (invalidate + await next).
  Future<void> _refresh() => refreshAsync(ref, myRequestsProvider.future);

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.requestsTitle,
      onBack: () => backOrHome(context),
      body: ref.watch(myRequestsProvider).when(
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
            data: (items) => _buildBody(l10n, items),
          ),
    );
  }

  Widget _buildBody(AppL10n l10n, List<AppRequestItem> items) {
    final isArabic = l10n.isArabic;
    // Bi-Meeting rework — the "طلب جديد" here opens the SPEAKER meeting sheet, so
    // it shows only to users holding AllowsSpeakerMeeting (endpoint also gates).
    final canRequestSpeakerMeeting =
        ref.watch(currentUserMeetingAccessProvider).value?.speaker ?? false;
    // A selected status whose chip has dropped to zero items (e.g. the user just
    // cancelled their only pending request) falls back to "All" so the screen
    // never strands the user on a chip-less "no results" view.
    final effectiveFilter =
        (_filter != null && items.any((i) => i.status == _filter))
            ? _filter
            : null;
    final filtered = effectiveFilter == null
        ? items
        : items.where((i) => i.status == effectiveFilter).toList();

    return SimfPullToRefresh(
      onRefresh: _refresh,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          RequestActionRow(
            l10n: l10n,
            filter: effectiveFilter,
            onNew: () => unawaited(_openNewRequest()),
            onSelect: (status) => setState(() => _filter = status),
            showNew: canRequestSpeakerMeeting,
          ),
          const SizedBox(height: SimfTokens.space4),
          if (items.isNotEmpty)
            RequestStatusChips(
              items: items,
              selected: effectiveFilter,
              l10n: l10n,
              onSelect: (status) => setState(() => _filter = status),
            ),
          const SizedBox(height: SimfTokens.space4),
          if (items.isEmpty)
            SimfEmptyState(
              icon: Icons.inbox_outlined,
              message: l10n.requestsEmpty,
            )
          else if (filtered.isEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: SimfTokens.space6),
              child: Center(
                child: Text(
                  l10n.requestsNoResults,
                  style: SimfTokens.hintBeige,
                ),
              ),
            )
          else
            for (final item in filtered)
              Padding(
                padding: const EdgeInsets.only(bottom: SimfTokens.space3),
                // Key on kind+id so the card's expanded state follows the request
                // identity, not its list position, across a cancel/refetch.
                child: RequestCard(
                  key: ValueKey<String>('${item.kind.wireValue}:${item.id}'),
                  item: item,
                  isArabic: isArabic,
                  l10n: l10n,
                  onCancel: () => unawaited(_cancel(item)),
                ),
              ),
        ],
      ),
    );
  }
}
