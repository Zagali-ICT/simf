import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The bordered filter-search field (Figma 1426:10819 / 1388:8402): a 48-high
/// navy-deep field with a beige hairline, a 16px search glyph + hint at the
/// inline start (right in RTL), and a divider-walled 40px filter (tune) cell at
/// the inline end (left in RTL). radius-4.
///
/// Shared by the session-summaries list + الوفود (delegations) — the same frame
/// shape. [controller] is optional (delegations drives its field through one;
/// the summaries list only needs [onChanged]).
class SimfFilterSearchField extends StatelessWidget {
  const SimfFilterSearchField({
    required this.hint,
    required this.onChanged,
    this.controller,
    this.showFilterIcon = true,
    super.key,
  });

  final String hint;
  final ValueChanged<String> onChanged;
  final TextEditingController? controller;

  /// Shows the tune/filter glyph at the inline end (Figma 1426:10819).
  final bool showFilterIcon;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: SimfTokens.simfFilterSearchFieldHeight,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Row(
        children: <Widget>[
          const Padding(
            padding: EdgeInsetsDirectional.only(start: SimfTokens.space3),
            child: Icon(Icons.search,
                color: SimfTokens.beigeBorder,
                size: SimfTokens.simfFilterSearchFieldSize,),
          ),
          Expanded(
            // The placeholder is a separate node that disappears once the user
            // types, so the field itself carried no accessible name (BUG-012).
            child: Semantics(
              label: hint,
              textField: true,
              child: TextField(
                controller: controller,
                onChanged: onChanged,
                style: SimfTokens.bodyWhiteMd,
                cursorColor: SimfTokens.accent,
                decoration: InputDecoration(
                  hintText: hint,
                  hintStyle: SimfTokens.bodyBeigeMd,
                  isDense: true,
                  border: InputBorder.none,
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: SimfTokens.space3,
                    vertical: SimfTokens.space2,
                  ),
                ),
              ),
            ),
          ),
          if (showFilterIcon)
            Container(
              width: SimfTokens.space10,
              // Fill the 48-high field so the divider is a full-height wall,
              // not a short stub (a bare Row centres children on the cross
              // axis).
              height: double.infinity,
              alignment: Alignment.center,
              decoration: const BoxDecoration(
                border: BorderDirectional(
                  start: BorderSide(
                    color: SimfTokens.beigeBorder,
                    width: SimfTokens.hairline,
                  ),
                ),
              ),
              child: const Icon(
                Icons.tune,
                color: SimfTokens.beigeBorder,
                size: SimfTokens.simfFilterSearchFieldSize,
              ),
            ),
        ],
      ),
    );
  }
}
