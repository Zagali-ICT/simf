import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../app/widgets/simf_search_field.dart';
import '../venuemap/data/venue_map_models.dart';
import '../venuemap/data/venue_map_repository.dart';
import 'widgets/booth_card.dart';

/// Page 022 — الأجنحة · Booths (#22, `/booths`, Guest+), rebuilt to the
/// KSA-Project Figma frame **922:2458 "Halls"** on the shared KSA shell.
///
/// **Public.** Reuses the shipped booth reads (`GET /app/booths` + `/{id}`,
/// D-199 / D-230) already wired in [VenueMapRepository] — the list of exhibitor
/// booths; tapping a booth opens the full exhibitor detail screen (Wave 3, Figma
/// 1439:11881).
///
/// Frame mapping: the navy scaffold + centred header (الأجنحة) and the shared
/// bottom nav from [SimfPageShell]; a bordered search field (ابحث عن جناح أو شركة);
/// then one exhibitor card per booth — a company header row (short name + full
/// name beside the square logo tile, gold-hairline divider), the gold-bordered
/// **code pill** (A-12) beside the deep-navy **hall box**, the booth-officer row
/// + email / phone contact boxes (D-432 — now on the wire, server resolves the
/// officer Contact-first), and a **guide-me** gold CTA. P6 — D-440: the logo tile
/// renders the exhibitor's real `CompanyLogo` asset (D-357) via
/// `{base}/app/assets/CompanyLogo/{exhibitorContactId}/image`, falling back to
/// initials when the exhibitor has no linked Contact.
class BoothsScreen extends ConsumerStatefulWidget {
  const BoothsScreen({super.key});

  @override
  ConsumerState<BoothsScreen> createState() => _BoothsScreenState();
}

class _BoothsScreenState extends ConsumerState<BoothsScreen> {
  bool _loading = true;
  bool _error = false;
  List<BoothSummary> _booths = const <BoothSummary>[];
  String _query = '';

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final booths = await ref.read(venueMapRepositoryProvider).getBooths();
      if (!mounted) {
        return;
      }
      setState(() {
        _booths = booths;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = true;
        _loading = false;
      });
    }
  }

  // Wave 3 — tapping a booth opens the full exhibitor detail screen (Figma
  // 1439:11881), replacing the earlier description bottom sheet.
  void _openBooth(BoothSummary booth) {
    context.pushNamed(
      RouteNames.exhibitorDetail,
      pathParameters: <String, String>{RouteParams.boothId: booth.id},
    );
  }

  // #9 — the booth's "أرشدني" CTA opens the venue map focused on this booth
  // (a pushed map instance that selects + centres the booth's node).
  void _openBoothMap(BoothSummary booth) {
    context.pushNamed(
      RouteNames.boothMap,
      pathParameters: <String, String>{RouteParams.boothId: booth.id},
    );
  }

  // The booths whose name / exhibitor / sector / code matches the query
  // (client-side filter, mirroring the frame's local search field).
  List<BoothSummary> _filtered(bool isArabic) {
    final q = _query.trim().toLowerCase();
    if (q.isEmpty) {
      return _booths;
    }
    return _booths.where((booth) {
      final haystack = <String?>[
        booth.localizedName(isArabic),
        booth.localizedExhibitor(isArabic),
        booth.localizedSector(isArabic),
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
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      // Pull-to-refresh also works in the error state so the user can pull to
      // retry (the short error content is hosted in the shared always-scrollable
      // SimfPullableHost so the gesture fires).
      return SimfPullToRefresh(
        onRefresh: _load,
        child: SimfPullableHost(
          child: SimfErrorState(
            message: l10n.boothsError,
            retryLabel: l10n.retryLabel,
            onRetry: () => unawaited(_load()),
          ),
        ),
      );
    }

    final isArabic = l10n.isArabic;
    final filtered = _filtered(isArabic);
    // The card builds {base}/app/assets/CompanyLogo/{exhibitorContactId}/image.
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
          // Pull-down-from-the-top re-fetches the booths (onRefresh: _load); the
          // empty / no-match short states use the shared always-scrollable
          // SimfPullableHost so the gesture fires; the list itself uses
          // AlwaysScrollableScrollPhysics for the same.
          child: SimfPullToRefresh(
            onRefresh: _load,
            child: _booths.isEmpty
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
