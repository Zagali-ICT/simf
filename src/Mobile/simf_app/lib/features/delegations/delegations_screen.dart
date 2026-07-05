import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/delegations_repository.dart';
import 'widgets/delegations_body.dart';

/// The Delegations screen — App "الوفود" (Figma 1426:10771): the invited
/// countries' delegations, each card showing the flag, country name, head of
/// delegation (name + role), the date range and member count, topped by a
/// stats strip (participating countries + total participants) and a search box.
/// Public (anonymous `GET /app/delegations`).
class DelegationsScreen extends ConsumerStatefulWidget {
  const DelegationsScreen({super.key});

  @override
  ConsumerState<DelegationsScreen> createState() => _DelegationsScreenState();
}

class _DelegationsScreenState extends ConsumerState<DelegationsScreen> {
  final TextEditingController _searchController = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = Directionality.of(context) == TextDirection.rtl;
    final delegations = ref.watch(delegationsProvider);

    Future<void> onRefresh() async {
      ref.invalidate(delegationsProvider);
      await ref.read(delegationsProvider.future);
    }

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
          ),
        ),
      ),
    );
  }
}
