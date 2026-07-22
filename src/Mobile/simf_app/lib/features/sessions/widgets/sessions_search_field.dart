import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// The bordered search field (frame node 883:2316).
///
/// Deliberately NOT the shared [SimfSearchField]: this frame's field is a
/// different design (18px white magnifier packed 8px from the edge, white hint,
/// navy-deep fill, 0.5px beige resting border) from the 908/922/758 search
/// frames the shared field renders.
class SessionsSearchField extends StatelessWidget {
  const SessionsSearchField({
    required this.l10n,
    required this.onChanged,
    super.key,
  });

  final AppL10n l10n;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      onChanged: onChanged,
      textInputAction: TextInputAction.search,
      style: SimfTokens.bodyWhiteSm,
      decoration: InputDecoration(
        isDense: true,
        filled: true,
        fillColor: SimfTokens.navyDeep,
        hintText: l10n.sessionsSearchHint,
        hintStyle: SimfTokens.bodyWhiteSm,
        // Frame 883:2316 — the 18px magnifier hugs the inline-start (physical
        // right under RTL), packed next to the hint (8px from the edge, 8px
        // before the text); the rest of the field is empty. A prefixIcon lands
        // at the inline-start in RTL (a suffixIcon would push it to the left).
        prefixIcon: const Padding(
          padding: EdgeInsetsDirectional.only(
            start: SimfTokens.space2,
            end: SimfTokens.space2,
          ),
          child: SimfSvgIcon(
            AppAssets.icSearch,
            size: 18,
            color: SimfTokens.surface,
          ),
        ),
        prefixIconConstraints: const BoxConstraints(minWidth: 0, minHeight: 0),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space3,
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairlineBold,
          ),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(color: SimfTokens.accent),
        ),
      ),
    );
  }
}
