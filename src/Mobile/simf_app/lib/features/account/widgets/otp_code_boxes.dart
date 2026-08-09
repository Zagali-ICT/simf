import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/field_limits.dart';

/// The KSA OTP-frame box border (Figma 505:987 — D-364, promoted to a shared
/// widget when the 2FA screen became the second consumer, D-369).
const Color otpCodeBoxBorder = SimfTokens.navyDisabledBorder;

/// The OTP frame's muted blue caption colour (countdown label) — the shared
/// [SimfTokens.mutedBlue] under the screen-local name its callers already use.
const Color otpMutedBlue = SimfTokens.mutedBlue;

/// Six segmented code boxes rendered over one invisible capture field — the
/// KSA-Project OTP entry (Figma 505:987). Tapping anywhere on the row focuses
/// the field natively (so `tester.enterText` keeps working); the box at the
/// caret highlights gold while focused. The parent owns the controller/focus
/// node and rebuilds on [onChanged].
class OtpCodeBoxes extends StatelessWidget {
  const OtpCodeBoxes({
    required this.controller, required this.focusNode, required this.enabled, required this.onChanged, required this.onSubmitted, super.key,
  });

  final TextEditingController controller;
  final FocusNode focusNode;
  final bool enabled;
  final VoidCallback onChanged;
  final VoidCallback onSubmitted;

  @override
  Widget build(BuildContext context) {
    final digits = controller.text;
    final activeIndex = digits.length.clamp(0, 5);
    return SizedBox(
      height: SimfTokens.otpCodeBoxesHeightSm,
      child: Stack(
        children: <Widget>[
          Positioned.fill(
            child: TextField(
              controller: controller,
              focusNode: focusNode,
              keyboardType: TextInputType.number,
              maxLength: FieldLimits.otpCode,
              enabled: enabled,
              autocorrect: false,
              showCursor: false,
              enableInteractiveSelection: false,
              style: const TextStyle(color: SimfTokens.transparent, fontSize: 1),
              inputFormatters: <TextInputFormatter>[
                FilteringTextInputFormatter.digitsOnly,
              ],
              onChanged: (_) => onChanged(),
              onSubmitted: (_) => onSubmitted(),
              decoration: const InputDecoration(
                counterText: '',
                isCollapsed: true,
                filled: false,
                // Kill every theme border — the capture field must be
                // invisible behind the rendered boxes.
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                disabledBorder: InputBorder.none,
              ),
            ),
          ),
          IgnorePointer(
            child: Row(
              // Code digits read left → right regardless of locale.
              textDirection: TextDirection.ltr,
              children: <Widget>[
                for (int i = 0; i < 6; i++) ...<Widget>[
                  if (i > 0) const SizedBox(width: SimfTokens.space4),
                  Expanded(
                    child: Container(
                      height: SimfTokens.otpCodeBoxesHeightSm,
                      decoration: BoxDecoration(
                        color: SimfTokens.navy,
                        borderRadius: BorderRadius.circular(SimfTokens.radius14),
                        border: Border.all(
                          width: SimfTokens.otpCodeBoxesWidthMd,
                          color: focusNode.hasFocus && i == activeIndex
                              ? SimfTokens.accent
                              : otpCodeBoxBorder,
                        ),
                      ),
                      alignment: Alignment.center,
                      child: Text(
                        i < digits.length ? digits[i] : '',
                        style: SimfTokens.labelWhiteBoldXl,
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// The OTP frame's gold-ringed circular mark (mail icon on the verify
/// screens — Figma 505:969).
class OtpMark extends StatelessWidget {
  const OtpMark({required this.icon, super.key});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.otpCodeBoxesWidthLg,
      height: SimfTokens.otpCodeBoxesHeightMd,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: SimfTokens.navyDeep,
        border: Border.all(color: SimfTokens.accent, width: SimfTokens.otpCodeBoxesWidthSm),
      ),
      alignment: Alignment.center,
      child: Icon(icon, color: SimfTokens.accent, size: SimfTokens.otpCodeBoxesSize),
    );
  }
}
