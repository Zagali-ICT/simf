import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// Header band (Figma 505:1558): chevron left, centred title. The Terms page
/// draws its own header rather than using `SimfPageShell`, because the frame
/// puts it over the navy sweep.
class TermsHeaderBar extends StatelessWidget {
  const TermsHeaderBar({required this.title, required this.onBack, super.key});

  final String title;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: SimfTokens.termsScreenHeightSm,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Align(
            alignment: Alignment.centerLeft,
            child: Padding(
              padding: const EdgeInsets.only(left: SimfTokens.space2),
              child: IconButton(
                onPressed: onBack,
                tooltip: MaterialLocalizations.of(context).backButtonTooltip,
                icon: const Icon(
                  Icons.arrow_back_ios_new,
                  color: SimfTokens.surface,
                  size: SimfTokens.termsScreenSize,
                  textDirection: TextDirection.ltr,
                ),
              ),
            ),
          ),
          Text(
            title,
            style: const TextStyle(
              color: SimfTokens.surface,
              fontSize: SimfTokens.text24,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }
}
