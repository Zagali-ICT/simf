import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/confirm_external_link.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import '../../../core/env/build_config.dart';
import '../../../core/organization_profile/organization_profile.dart';

/// The "تابعنا" follow-us section (frame node 758:1183): the header, the brand
/// row and the @handle line. The links come from the CP-editable Organization
/// profile (downloaded at app start, cached, shared with About / Contact).
///
/// Owner 2026-06-27: a platform with **no URL is hidden** (not a dead/inert
/// button), and when **no** social link is set the whole section disappears
/// (header + row + handle). A set link, when tapped, asks to confirm leaving the
/// app, then opens it externally. Owns its leading gap so the layout stays tidy
/// whether it shows or hides.
class FollowUsSection extends ConsumerWidget {
  const FollowUsSection({required this.l10n, super.key});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = ref.watch(orgProfileProvider);
    final social = profile?.social;
    final website = profile?.contactWebsite?.trim() ?? '';
    // (asset, url, label) — the exact Figma beige glyphs (node 758:1186); kept
    // only when the URL is set so an unconfigured platform is hidden, not inert.
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
                for (final (index, (asset, url, label)) in links.indexed)
                  ...<Widget>[
                  if (index > 0) const SizedBox(width: SimfTokens.space4),
                  Expanded(
                    child: _SocialButton(asset: asset, url: url, label: label),
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
        // Owner 2026-07-08 — the org website on Home. No Figma home node carries
        // it (it also lives on About/Contact); shown only when the CP sets one.
        if (website.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          _WebsiteLink(url: website, label: l10n.websiteLabel),
        ],
      ],
    );
  }
}

class _SocialButton extends StatelessWidget {
  const _SocialButton({
    required this.asset,
    required this.url,
    required this.label,
  });

  final String asset;
  final String url;

  /// The platform name, exposed to screen readers via the image's semantic
  /// label (the button is otherwise icon-only).
  final String label;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius10),
        side: const BorderSide(
          color: SimfTokens.navyDeep,
          width: SimfTokens.hairlineWide,
        ),
      ),
      child: InkWell(
        onTap: url.isEmpty
            ? null
            : () => unawaited(confirmThenLaunchExternal(context, url)),
        borderRadius: BorderRadius.circular(SimfTokens.radius10),
        child: SizedBox(
          height: SimfTokens.controlHeight,
          child: Semantics(
            button: true,
            label: label,
            child: Center(
              child: SimfSvgIcon(
                asset,
                size: 20,
                color: SimfTokens.beigeBorder,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// The org website line under the social row: a centred gold globe + label that
/// asks to confirm leaving the app, then opens the site externally (same launch
/// path as the social buttons). Rendered only when the CP sets a website.
class _WebsiteLink extends StatelessWidget {
  const _WebsiteLink({required this.url, required this.label});

  final String url;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: InkWell(
        onTap: () => unawaited(confirmThenLaunchExternal(context, url)),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space3,
            vertical: SimfTokens.space2,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              const SimfSvgIcon(
                AppAssets.authGlobe,
                size: 16,
                color: SimfTokens.accent,
              ),
              const SizedBox(width: SimfTokens.space2),
              Text(
                label,
                style: SimfTokens.labelGoldSemiboldSm,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
