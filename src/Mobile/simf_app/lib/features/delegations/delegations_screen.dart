import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_repository.dart';
import 'package:simf_app/features/delegations/widgets/delegation_meeting_request_sheet.dart';
import 'package:simf_app/features/delegations/widgets/delegations_body.dart';

/// The Delegations screen — App "الوفود" (Figma 1426:10771): the invited
/// countries' delegations, each card showing the flag, country name, head of
/// delegation (name + role), the date range and member count, topped by a
/// stats strip (participating countries + total participants) and a search box.
/// Public (anonymous `GET /app/delegations`).
///
/// Route: `RouteNames.delegations`.
/// Data: [currentUserMeetingAccessProvider], [delegationsProvider].
/// Perf: ListView builds every child up front — correct for a short static
///       page, a defect on a data feed.
class DelegationsScreen extends ConsumerStatefulWidget {
  const DelegationsScreen({super.key});

  @override
  ConsumerState<DelegationsScreen> createState() => _DelegationsScreenState();
}

class _DelegationsScreenState extends ConsumerState<DelegationsScreen> {
  final TextEditingController _searchController = TextEditingController();
  String _query = '';

  /// The country code selected by tapping its flag in the stats strip, or null
  /// when the list is unfiltered by flag. Composes with the [_query] search.
  String? _selectedFlagCode;

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  /// Toggle the flag filter: tapping the already-selected country's flag again
  /// clears it.
  void _onFlagTap(String code) {
    setState(() => _selectedFlagCode = _selectedFlagCode == code ? null : code);
  }

  /// Bi-Meeting rework — open the delegation meeting-request sheet for a tapped
  /// country (only wired when the user holds AllowsDelegationMeeting).
  Future<void> _openRequest(DelegationItem country) async {
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
      builder: (_) =>
          DelegationMeetingRequestSheet(country: country, l10n: l10n),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = Directionality.of(context) == TextDirection.rtl;
    final delegations = ref.watch(delegationsProvider);
    // Bi-Meeting rework — a signed-in user holding AllowsDelegationMeeting can
    // tap a country card to request a meeting; others see plain info cards.
    final canRequestDelegation =
        ref.watch(currentUserMeetingAccessProvider).value?.delegation ?? false;

    Future<void> onRefresh() => refreshAsync(ref, delegationsProvider.future);

    return SimfPageShell(
      title: l10n.delegationsTitle,
      onBack: () => backOrHome(context),
      body: delegations.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
        error: (_, __) => SimfPullToRefresh(
          onRefresh: onRefresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            children: <Widget>[
              SimfErrorState(
                message: l10n.delegationsError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(delegationsProvider),
              ),
            ],
          ),
        ),
        data: (data) => SimfPullToRefresh(
          onRefresh: onRefresh,
          child: DelegationsBody(
            data: data,
            query: _query,
            isArabic: isArabic,
            l10n: l10n,
            searchController: _searchController,
            onQueryChanged: (value) => setState(() => _query = value),
            selectedCountryCode: _selectedFlagCode,
            onFlagTap: _onFlagTap,
            onClearFilter: () => setState(() => _selectedFlagCode = null),
            onRequestMeeting: canRequestDelegation ? _openRequest : null,
          ),
        ),
      ),
    );
  }
}
