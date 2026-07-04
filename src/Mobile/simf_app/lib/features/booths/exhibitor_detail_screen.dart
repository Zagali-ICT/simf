import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/confirm_external_link.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../exhibition/entity_detail_helpers.dart';
import '../exhibition/entity_detail_scaffold.dart';
import '../exhibition/entity_logo_image.dart';
import '../venuemap/data/venue_map_models.dart';
import '../venuemap/data/venue_map_repository.dart';

/// `GET /app/booths/{id}` → the booth/exhibitor detail (Figma 1439:11881).
final exhibitorDetailProvider =
    FutureProvider.autoDispose.family<BoothDetail, String>((ref, id) async {
  return ref.watch(venueMapRepositoryProvider).getBoothDetail(id);
});

/// **Exhibitor detail** — App "العارض" (Figma 1439:11881, Guest+), opened by
/// tapping a booth in the exhibition list. The shared
/// [EntityDetailScaffold]: the exhibitor's logo + name, the city·country line,
/// the tier pill, the stand-code→map row, the "نبذة عن العارض" about, and the
/// website row. Reads `GET /app/booths/{id}` (the detail extended in Wave 3).
class ExhibitorDetailScreen extends ConsumerWidget {
  const ExhibitorDetailScreen({required this.boothId, super.key});

  final String boothId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final detail = ref.watch(exhibitorDetailProvider(boothId));

    return detail.when(
      loading: () => SimfPageShell(
        title: l10n.exhibitorDetailTitle,
        onBack: () => backOrHome(context),
        body: const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
      ),
      error: (_, __) => SimfPageShell(
        title: l10n.exhibitorDetailTitle,
        onBack: () => backOrHome(context),
        body: SimfErrorState(
          message: l10n.entityDetailError,
          retryLabel: l10n.retryLabel,
          onRetry: () => ref.invalidate(exhibitorDetailProvider(boothId)),
        ),
      ),
      data: (booth) => _build(context, ref, l10n, booth),
    );
  }

  Widget _build(
    BuildContext context,
    WidgetRef ref,
    AppL10n l10n,
    BoothDetail booth,
  ) {
    final isArabic = l10n.isArabic;
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    final name = booth.localizedExhibitor(isArabic) ?? booth.localizedName(isArabic);

    return EntityDetailScaffold(
      headerTitle: l10n.exhibitorDetailTitle,
      aboutHeader: l10n.exhibitorAboutHeader,
      websiteLabel: l10n.websiteLabel,
      logo: EntityLogoImage(
        url: booth.exhibitorContactId == null
            ? null
            : '$baseUrl/app/assets/CompanyLogo/${booth.exhibitorContactId}/image',
        initials: entityInitials(name),
      ),
      name: name,
      locationLine: entityLocationLine(
        booth.localizedCity(isArabic),
        booth.localizedCountry(isArabic),
        isArabic,
      ),
      countryId: booth.countryId,
      tierPill: booth.tier == null || (booth.tierName ?? '').isEmpty
          ? null
          : l10n.exhibitorTierPill(booth.tierName!),
      standLabel: l10n.standLocationLabel,
      standCode: booth.code,
      onMap: booth.code.isEmpty
          ? null
          : () => context.pushNamed(
                RouteNames.boothMap,
                pathParameters: <String, String>{'boothId': booth.id},
              ),
      about: booth.localizedDescription(isArabic),
      website: booth.website,
      onWebsite: () => _openWebsite(context, booth.website),
    );
  }

  void _openWebsite(BuildContext context, String? url) {
    final uri = entityHttpUri(url);
    if (uri != null) {
      unawaited(confirmThenLaunchExternal(context, uri.toString()));
    }
  }
}
