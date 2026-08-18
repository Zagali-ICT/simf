import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/confirm_external_link.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/net/asset_urls.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/exhibition/entity_detail_helpers.dart';
import 'package:simf_app/features/exhibition/widgets/entity_detail_scaffold.dart';
import 'package:simf_app/features/exhibition/widgets/entity_logo_image.dart';
import 'package:simf_app/features/sponsors/data/sponsor_models.dart';
import 'package:simf_app/features/sponsors/data/sponsors_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Sponsor detail — route: `RouteNames.sponsorDetail` · Figma 1439:11826
class SponsorDetailScreen extends ConsumerWidget {
  const SponsorDetailScreen({required this.sponsorId, super.key});

  final String sponsorId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final detail = ref.watch(sponsorDetailProvider(sponsorId));

    return detail.when(
      loading: () => SimfPageShell(
        title: l10n.sponsorDetailTitle,
        onBack: () => backOrHome(context),
        body: const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
      ),
      error: (_, __) => SimfPageShell(
        title: l10n.sponsorDetailTitle,
        onBack: () => backOrHome(context),
        body: SimfRefreshableMessage(
          onRefresh: () =>
              refreshAsync(ref, sponsorDetailProvider(sponsorId).future),
          child: SimfErrorState(
            message: l10n.entityDetailError,
            retryLabel: l10n.retryLabel,
            onRetry: () => ref.invalidate(sponsorDetailProvider(sponsorId)),
          ),
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
    final name = sponsor.localizedName(isArabic: isArabic);

    return EntityDetailScaffold(
      onRefresh: () =>
          refreshAsync(ref, sponsorDetailProvider(sponsor.id).future),
      headerTitle: l10n.sponsorDetailTitle,
      aboutHeader: l10n.sponsorAboutHeader,
      websiteLabel: l10n.websiteLabel,
      logo: EntityLogoImage(
        url: AssetUrls.image(baseUrl, AssetKind.sponsorLogo, sponsor.id),
        initials: entityInitials(name),
        name: name,
      ),
      name: name,
      locationLine: entityLocationLine(
        sponsor.localizedCity(isArabic: isArabic),
        sponsor.localizedCountry(isArabic: isArabic),
        isArabic: isArabic,
      ),
      countryId: sponsor.countryId,
      tierPill: sponsor.tierName.isEmpty
          ? null
          : l10n.sponsorTierPill(sponsor.tierName),
      about: sponsor.localizedAbout(isArabic: isArabic),
      website: sponsor.url,
      onWebsite: () => _openWebsite(context, sponsor.url),
    );
  }

  void _openWebsite(BuildContext context, String? url) {
    final uri = entityHttpUri(url);
    if (uri != null) {
      unawaited(confirmThenLaunchExternal(context, uri.toString()));
    }
  }
}
