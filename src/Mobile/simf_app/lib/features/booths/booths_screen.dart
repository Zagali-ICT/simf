import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_search_field.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/booths/data/booths_repository.dart';
import 'package:simf_app/features/booths/widgets/booth_card.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Booths — route: `RouteNames.booths` · Figma 922:2458
/// Reads `GET /app/booths` + `/{id}` via the venue-map repository (D-199 / D-230).
class BoothsScreen extends ConsumerStatefulWidget {
  const BoothsScreen({super.key});

  @override
  ConsumerState<BoothsScreen> createState() => _BoothsScreenState();
}

class _BoothsScreenState extends ConsumerState<BoothsScreen> {
  String _query = '';

  Future<void> _refresh() => refreshAsync(ref, boothsListProvider.future);

  // Wave 3 — tapping a booth opens the full exhibitor detail screen (Figma
  // 1439:11881), replacing the earlier description bottom sheet.
  void _openBooth(BoothSummary booth) {
    unawaited(context.pushNamed(
        RouteNames.exhibitorDetail,
        pathParameters: <String, String>{RouteParams.boothId: booth.id},
      ),);
  }

  // #9 — the booth's "أرشدني" CTA opens the venue map focused on this booth
  // (a pushed map instance that selects + centres the booth's node).
  void _openBoothMap(BoothSummary booth) {
    unawaited(context.pushNamed(
        RouteNames.boothMap,
        pathParameters: <String, String>{RouteParams.boothId: booth.id},
      ),);
  }

  // The booths whose name / exhibitor / sector / code matches the query
  // (client-side filter, mirroring the frame's local search field).
  List<BoothSummary> _filtered(List<BoothSummary> booths, bool isArabic) {
    final q = _query.trim().toLowerCase();
    if (q.isEmpty) {
      return booths;
    }
    return booths.where((booth) {
      final haystack = <String?>[
        booth.localizedName(isArabic: isArabic),
        booth.localizedExhibitor(isArabic: isArabic),
        booth.localizedSector(isArabic: isArabic),
        booth.code,
      ].whereType<String>().join(' ').toLowerCase();
      return haystack.contains(q);
    }).toList(growable: false);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      // Frame 922:2464 titles the screen "المعرض" (the nav tile/route stay "الأجنحة").
      title: l10n.boothsExhibitionTitle,
      onBack: () => backOrHome(context),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    return ref.watch(boothsListProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          // Pull-to-refresh also works in the error state so the user can pull
          // to retry (the short error content is hosted in the shared
          // always-scrollable SimfPullableHost so the gesture fires).
          error: (_, __) => SimfRefreshableMessage(
            onRefresh: _refresh,
            child: SimfErrorState(
              message: l10n.boothsError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(boothsListProvider),
            ),
          ),
          data: (booths) => _buildList(l10n, booths),
        );
  }

  Widget _buildList(AppL10n l10n, List<BoothSummary> booths) {
    final isArabic = l10n.isArabic;
    final filtered = _filtered(booths, isArabic);
    // The card builds {base}/app/assets/BoothLogo/{booth.id}/image.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;

    return Column(
      children: <Widget>[
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space2,
            SimfTokens.space4,
            0,
          ),
          child: SimfSearchField(
            hint: l10n.boothsSearchHint,
            onChanged: (value) => setState(() => _query = value),
          ),
        ),
        Expanded(
          // Pull-down-from-the-top re-fetches the booths; the empty / no-match
          // short states use the shared always-scrollable SimfPullableHost so
          // the gesture fires; the list itself uses
          // AlwaysScrollableScrollPhysics for the same.
          child: SimfPullToRefresh(
            onRefresh: _refresh,
            child: booths.isEmpty
                ? SimfPullableHost(
                    child: SimfEmptyState(
                      icon: Icons.storefront_outlined,
                      message: l10n.boothsEmpty,
                    ),
                  )
                : filtered.isEmpty
                    ? SimfPullableHost(
                        child: SimfEmptyState(
                          icon: Icons.search_off_outlined,
                          message: l10n.boothsNoMatch,
                        ),
                      )
                    : ListView.separated(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.fromLTRB(
                          SimfTokens.space4,
                          SimfTokens.space4,
                          SimfTokens.space4,
                          SimfTokens.space6,
                        ),
                        itemCount: filtered.length,
                        separatorBuilder: (_, __) =>
                            const SizedBox(height: SimfTokens.space4),
                        itemBuilder: (context, index) => BoothCard(
                          booth: filtered[index],
                          l10n: l10n,
                          isArabic: isArabic,
                          baseUrl: baseUrl,
                          onTap: () => _openBooth(filtered[index]),
                          onGuide: () => _openBoothMap(filtered[index]),
                        ),
                      ),
          ),
        ),
      ],
    );
  }
}
