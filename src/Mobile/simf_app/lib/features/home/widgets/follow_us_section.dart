import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/env/build_config.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/home/widgets/social_button.dart';
import 'package:simf_app/features/home/widgets/website_link.dart';

/// The "تابعنا" follow-us section (frame node 758:1183): the header, the brand
/// row and the @handle line. The links come from the CP-editable Organization
/// profile (downloaded at app start, cached, shared with About / Contact).
///
/// Owner 2026-06-27: a platform with **no URL is hidden** (not a dead/inert
/// button), and when **no** social link is set the whole section disappears
/// (header + row + handle). A set link, when tapped, asks to confirm leaving
/// the app, then opens it externally. Owns its leading gap so the layout stays
/// tidy whether it shows or hides.
class FollowUsSection extends ConsumerWidget {
  const FollowUsSection({required this.l10n, super.key});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = ref.watch(orgProfileProvider);
    final social = profile?.social;
    final website = profile?.contactWebsite?.trim() ?? '';
    // (asset, url, label) — the exact Figma beige glyphs (node 758:1186); kept
    // only when the URL is set so an unconfigured platform is hidden, not
    // inert.
    final links = <(String, String, String)>[
      (AppAssets.socialX, social?.x ?? BuildConfig.socialXUrl, 'X'),
      (
        AppAssets.socialInstagram,
        social?.instagram ?? BuildConfig.socialInstagramUrl,
        'Instagram',
      ),
      (
        AppAssets.socialLinkedin,
        social?.linkedin ?? BuildConfig.socialLinkedInUrl,
        'LinkedIn',
      ),
      (
        AppAssets.socialYoutube,
        social?.youtube ?? BuildConfig.socialYouTubeUrl,
        'YouTube',
      ),
      (
        AppAssets.socialTiktok,
        social?.tiktok ?? BuildConfig.socialTikTokUrl,
        'TikTok',
      ),
    ].where((l) => l.$2.trim().isNotEmpty).toList();

    // Nothing to show (no social link and no website) → hide the whole section.
    if (links.isEmpty && website.isEmpty) {
      return const SizedBox.shrink();
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const SizedBox(height: SimfTokens.space6),
        SimfSectionHeader(title: l10n.followUsSection),
        const SizedBox(height: SimfTokens.space4),
        if (links.isNotEmpty) ...<Widget>[
          // The brand row stays LTR (X · Instagram · … · TikTok) in any locale.
          Directionality(
            textDirection: TextDirection.ltr,
            child: Row(
              children: <Widget>[
                for (final (index, (asset, url, label))
                    in links.indexed) ...<Widget>[
                  if (index > 0) const SizedBox(width: SimfTokens.space4),
                  Expanded(
                    child: SocialButton(asset: asset, url: url, label: label),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.followUsHandle,
            textAlign: TextAlign.center,
            // Frame 758:1202 — handle line is Medium, beige.
            style: SimfTokens.labelBeigeMediumSm,
          ),
        ],
        // Owner 2026-07-08 — the org website on Home. No Figma home node
        // carries it (it also lives on About/Contact); shown only when the CP
        // sets one.
        if (website.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          WebsiteLink(url: website, label: l10n.websiteLabel),
        ],
      ],
    );
  }
}
