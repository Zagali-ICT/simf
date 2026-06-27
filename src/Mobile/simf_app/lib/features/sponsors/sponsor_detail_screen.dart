import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/confirm_external_link.dart';
import '../../app/widgets/ksa_shell.dart';
import '../exhibition/entity_detail_scaffold.dart';
import '../exhibition/entity_logo_image.dart';
import 'data/sponsor_models.dart';

/// `GET /app/sponsors/{id}` → the full sponsor detail (Figma 1439:11826).
final sponsorDetailProvider =
    FutureProvider.autoDispose.family<SponsorDetail, String>((ref, id) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<SponsorDetail>(
    '/app/sponsors/$id',
    decodeData: SponsorDetail.fromData,
  );
});

/// **Sponsor detail** — App "الراعي" (Figma 1439:11826, Guest+), opened by
/// tapping a sponsor on the sponsors screen. The same shared
/// [EntityDetailScaffold] as the exhibitor detail (the owner: reuse 11881): the
/// sponsor's logo + name, the city·country line, the tier pill, the
/// "نبذة عن الراعي" about, and the website row. No stand-code row (sponsors are
/// not on the venue map). Reads `GET /app/sponsors/{id}` (Wave 3).
class SponsorDetailScreen extends ConsumerWidget {
  const SponsorDetailScreen({required this.sponsorId, super.key});

  final String sponsorId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final detail = ref.watch(sponsorDetailProvider(sponsorId));

    return detail.when(
      loading: () => KsaPage(
        title: l10n.sponsorDetailTitle,
        onBack: () => ksaBackOrHome(context),
        body: const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
      ),
      error: (_, __) => KsaPage(
        title: l10n.sponsorDetailTitle,
        onBack: () => ksaBackOrHome(context),
        body: KsaErrorState(
          message: l10n.entityDetailError,
          retryLabel: l10n.retryLabel,
          onRetry: () => ref.invalidate(sponsorDetailProvider(sponsorId)),
        ),
      ),
      data: (sponsor) => _build(context, ref, l10n, sponsor),
    );
  }

  Widget _build(
    BuildContext context,
    WidgetRef ref,
    AppL10n l10n,
    SponsorDetail sponsor,
  ) {
    final isArabic = l10n.isArabic;
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    final name = sponsor.localizedName(isArabic);

    return EntityDetailScaffold(
      headerTitle: l10n.sponsorDetailTitle,
      aboutHeader: l10n.sponsorAboutHeader,
      websiteLabel: l10n.websiteLabel,
      logo: EntityLogoImage(
        url: '$baseUrl/app/assets/SponsorLogo/${sponsor.id}/image',
        initials: _initials(name),
      ),
      name: name,
      locationLine: _locationLine(
        sponsor.localizedCity(isArabic),
        sponsor.localizedCountry(isArabic),
        isArabic,
      ),
      countryId: sponsor.countryId,
      tierPill: sponsor.tierName.isEmpty
          ? null
          : l10n.sponsorTierPill(sponsor.tierName),
      about: sponsor.localizedAbout(isArabic),
      website: sponsor.url,
      onWebsite: () => _openWebsite(context, sponsor.url),
    );
  }

  void _openWebsite(BuildContext context, String? url) {
    final uri = _httpUri(url);
    if (uri != null) {
      unawaited(confirmThenLaunchExternal(context, uri.toString()));
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

String _initials(String name) {
  final trimmed = name.trim();
  if (trimmed.isEmpty) {
    return '';
  }
  return trimmed.substring(0, trimmed.length >= 2 ? 2 : 1).toUpperCase();
}

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
