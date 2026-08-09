import 'dart:async';
import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/confirm_external_link.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// The org website line under the social row: a centred gold globe + label that
/// asks to confirm leaving the app, then opens the site externally (same launch
/// path as the social buttons). Rendered only when the CP sets a website.
class WebsiteLink extends StatelessWidget {
  const WebsiteLink({required this.url, required this.label, super.key});

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
                size: SimfTokens.websiteLinkSize,
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
