import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/live/widgets/toggle_pill.dart';

/// Swaps the player between the main feed and the sign-language feed (Page_025
/// L-3). Shown only when the session carries both.
class FeedToggle extends StatelessWidget {
  const FeedToggle({
    required this.showSignLanguage,
    required this.mainLabel,
    required this.signLabel,
    required this.onChanged,
    super.key,
  });

  final bool showSignLanguage;
  final String mainLabel;
  final String signLabel;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: TogglePill(
              label: mainLabel,
              active: !showSignLanguage,
              onTap: () => onChanged(false),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: TogglePill(
              label: signLabel,
              active: showSignLanguage,
              icon: Icons.sign_language_outlined,
              onTap: () => onChanged(true),
            ),
          ),
        ],
      ),
    );
  }
}
