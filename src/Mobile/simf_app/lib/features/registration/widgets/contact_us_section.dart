import 'dart:async';

import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/env/build_config.dart';
import '../../../core/external_link.dart';

/// The success-frame "تواصل معنا" block (Figma 522:2223): the section title, the
/// call + mail tiles, and the social footer. Each tile opens the OS dialer /
/// mail app when its [BuildConfig] value is supplied; an empty value keeps the
/// tile inert (D-369).
class ContactUsSection extends StatelessWidget {
  const ContactUsSection({
    required this.title,
    required this.socialFooter,
    super.key,
  });

  final String title;
  final String socialFooter;

  /// Opening the dialer / mail app is best-effort — a missing handler must never
  /// crash the confirmation screen (the shared D-369 helper).
  static Future<void> _launchContact(Uri uri) => launchExternalUri(uri);

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Text(
          title,
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: SimfTokens.surface,
            fontSize: SimfTokens.textLg,
            fontWeight: FontWeight.w500,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        Row(
          children: <Widget>[
            // Frame 522:2223 (RTL): the mail tile leads (right edge), the call
            // tile trails (left) — so the mail tile is the first child.
            Expanded(
              child: _ContactTile(
                icon: Icons.mail_outline,
                onTap: BuildConfig.supportEmail.isEmpty
                    ? null
                    : () => unawaited(
                          _launchContact(
                            Uri(
                              scheme: 'mailto',
                              path: BuildConfig.supportEmail,
                            ),
                          ),
                        ),
              ),
            ),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: _ContactTile(
                icon: Icons.call_outlined,
                onTap: BuildConfig.supportPhone.isEmpty
                    ? null
                    : () => unawaited(
                          _launchContact(
                            Uri(
                              scheme: 'tel',
                              path: BuildConfig.supportPhone,
                            ),
                          ),
                        ),
              ),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(
          socialFooter,
          textAlign: TextAlign.center,
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

/// One bordered contact tile (Figma 522:2223). Inert (null [onTap]) until its
/// BuildConfig contact value is supplied (D-369).
class _ContactTile extends StatelessWidget {
  const _ContactTile({required this.icon, this.onTap});

  final IconData icon;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius10),
      child: Container(
        height: 52,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          border: Border.all(color: SimfTokens.tileBorderNavy, width: 0.8),
          borderRadius: BorderRadius.circular(SimfTokens.radius10),
        ),
        child: Icon(icon, color: SimfTokens.surface, size: 24),
      ),
    );
  }
}
