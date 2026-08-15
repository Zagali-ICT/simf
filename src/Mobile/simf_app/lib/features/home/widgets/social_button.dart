import 'dart:async';
import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/confirm_external_link.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

class SocialButton extends StatelessWidget {
  const SocialButton({
    required this.asset,
    required this.url,
    required this.label,
    super.key,
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
                size: SimfTokens.socialButtonSize,
                color: SimfTokens.beigeBorder,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
