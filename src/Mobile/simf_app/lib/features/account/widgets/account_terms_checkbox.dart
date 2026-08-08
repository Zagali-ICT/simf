import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// Mandatory "I accept the terms and conditions" checkbox on sign-up step 1
/// (D-719, owner batch 2026-07-09). Registration must be gated on an explicit
/// accept — a checkbox, not a link (profile keeps the read-only link). The
/// box styling mirrors the sign-in remember-me box; the "الشروط والأحكام" span
/// is tappable and opens Page 009 in consent mode via [onOpenTerms], and the
/// screen auto-checks the box when the visitor taps موافق there.
///
/// Acceptance state lives on the screen (this widget is a pure control): tapping
/// the box (or the lead text) calls [onChanged]; the terms link calls
/// [onOpenTerms]. When [showError] is set the box border turns red and the
/// bilingual "you must accept" message shows beneath.
class AccountTermsCheckbox extends StatelessWidget {
  const AccountTermsCheckbox({
    required this.accepted,
    required this.onChanged,
    required this.onOpenTerms,
    this.enabled = true,
    this.showError = false,
    super.key,
  });

  final bool accepted;
  final ValueChanged<bool> onChanged;

  /// Opens the terms screen (Page 009) in consent mode; the caller auto-checks
  /// the box if the visitor accepts there.
  final VoidCallback onOpenTerms;
  final bool enabled;
  final bool showError;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            SizedBox(
              width: SimfTokens.accountTermsCheckboxWidthMd,
              height: SimfTokens.accountTermsCheckboxHeightMd,
              child: Checkbox(
                value: accepted,
                onChanged: enabled ? (v) => onChanged(v ?? false) : null,
                activeColor: SimfTokens.accent,
                side: BorderSide(
                  color: showError ? SimfTokens.danger : SimfTokens.greyText,
                  width: SimfTokens.accountTermsCheckboxWidthSm,
                ),
                shape: const RoundedRectangleBorder(
                  borderRadius: SimfTokens.borderRadiusSmall,
                ),
                materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: GestureDetector(
                // Tapping the lead text toggles the box; the terms span below
                // claims its own taps and opens Page 009 instead.
                onTap: enabled ? () => onChanged(!accepted) : null,
                behavior: HitTestBehavior.opaque,
                child: Text.rich(
                  TextSpan(
                    style: const TextStyle(
                      fontSize: SimfTokens.accountTermsCheckboxFontSize,
                      color: SimfTokens.headlineInk,
                    ),
                    children: <InlineSpan>[
                      TextSpan(text: '${l10n.termsAcceptLead} '),
                      WidgetSpan(
                        alignment: PlaceholderAlignment.middle,
                        child: GestureDetector(
                          onTap: enabled ? onOpenTerms : null,
                          child: Text(
                            l10n.termsTitle,
                            style: const TextStyle(
                              fontSize: SimfTokens.accountTermsCheckboxFontSize,
                              fontWeight: FontWeight.w600,
                              color: SimfTokens.navy,
                              decoration: TextDecoration.underline,
                              decorationColor: SimfTokens.navy,
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
        if (showError) ...<Widget>[
          const SizedBox(height: SimfTokens.accountTermsCheckboxHeightSm),
          Padding(
            padding: const EdgeInsetsDirectional.only(start: 27),
            child: Text(
              l10n.termsMustAccept,
              style: SimfTokens.labelDangerSm,
            ),
          ),
        ],
      ],
    );
  }
}
