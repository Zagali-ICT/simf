import 'dart:async';

import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/env/build_config.dart';
import '../../../core/external_link.dart';
import 'contact_tile.dart';

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
          style: SimfTokens.labelWhiteMediumLg,
        ),
        const SizedBox(height: SimfTokens.space4),
        Row(
          children: <Widget>[
            // Frame 522:2223 (RTL): the mail tile leads (right edge), the call
            // tile trails (left) — so the mail tile is the first child.
            Expanded(
              child: ContactTile(
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
              child: ContactTile(
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
          style: SimfTokens.labelBeigeMediumSm,
        ),
      ],
    );
  }
}

