import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/localization/app_l10n.dart';
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
    final social = ref.watch(orgProfileProvider)?.social;
    // (asset, url, label) — the exact Figma beige glyphs (node 758:1186); kept
    // only when the URL is set so an unconfigured platform is hidden, not inert.
    final links = <(String, String, String)>[
      ('assets/icons/social_x.svg', social?.x ?? BuildConfig.socialXUrl, 'X'),
      (
        'assets/icons/social_instagram.svg',
        social?.instagram ?? BuildConfig.socialInstagramUrl,
        'Instagram',
      ),
      (
        'assets/icons/social_linkedin.svg',
        social?.linkedin ?? BuildConfig.socialLinkedInUrl,
        'LinkedIn',
      ),
      (
        'assets/icons/social_youtube.svg',
        social?.youtube ?? BuildConfig.socialYouTubeUrl,
        'YouTube',
      ),
      (
        'assets/icons/social_tiktok.svg',
        social?.tiktok ?? BuildConfig.socialTikTokUrl,
        'TikTok',
      ),
    ].where((l) => l.$2.trim().isNotEmpty).toList();

    // No social link set → hide the entire section.
    if (links.isEmpty) {
      return const SizedBox.shrink();
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const SizedBox(height: SimfTokens.space6),
        SimfSectionHeader(title: l10n.followUsSection),
        const SizedBox(height: SimfTokens.space4),
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
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
            fontWeight: FontWeight.w500,
          ),
        ),
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
      color: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(10),
        side: const BorderSide(color: SimfTokens.navyDeep, width: 0.8),
      ),
      child: InkWell(
        onTap: url.isEmpty
            ? null
            : () => unawaited(confirmThenLaunchExternal(context, url)),
        borderRadius: BorderRadius.circular(10),
        child: SizedBox(
          height: 48,
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
