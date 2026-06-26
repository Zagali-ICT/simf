import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../core/external_link.dart';
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
      loading: () => KsaPage(
        title: l10n.exhibitorDetailTitle,
        onBack: () => ksaBackOrHome(context),
        body: const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
      ),
      error: (_, __) => KsaPage(
        title: l10n.exhibitorDetailTitle,
        onBack: () => ksaBackOrHome(context),
        body: KsaErrorState(
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
        initials: _initials(name),
      ),
      name: name,
      locationLine: _locationLine(
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
      onWebsite: () => _openWebsite(booth.website),
    );
  }

  void _openWebsite(String? url) {
    final uri = _httpUri(url);
    if (uri != null) {
      unawaited(launchExternalUri(uri));
    }
  }
}

/// Joins "City، Country" (Arabic comma in RTL); either side may be null.
String? _locationLine(String? city, String? country, bool isArabic) {
  final parts = <String>[
    if ((city ?? '').trim().isNotEmpty) city!.trim(),
    if ((country ?? '').trim().isNotEmpty) country!.trim(),
  ];
  if (parts.isEmpty) {
    return null;
  }
  return parts.join(isArabic ? '، ' : ', ');
}

/// The first two letters of a name, upper-cased, for the logo fallback.
String _initials(String name) {
  final trimmed = name.trim();
  if (trimmed.isEmpty) {
    return '';
  }
  return trimmed.substring(0, trimmed.length >= 2 ? 2 : 1).toUpperCase();
}

/// Parses a website into an http(s) [Uri] (prepending https:// when the scheme
/// is missing); null when blank / unparseable.
Uri? _httpUri(String? raw) {
  final value = (raw ?? '').trim();
  if (value.isEmpty) {
    return null;
  }
  final withScheme =
      value.startsWith('http://') || value.startsWith('https://')
          ? value
          : 'https://$value';
  return Uri.tryParse(withScheme);
}
