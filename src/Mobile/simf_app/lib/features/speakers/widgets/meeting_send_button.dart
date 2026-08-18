import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The full-width gold "ارسال الطلب" button (Figma 1776:5083). G3 — the sheet
/// disables it while the slots load, and once they are loaded and EMPTY (no
/// free slot ⇒ the server would 409), so the user is never invited to send a
/// request that cannot succeed. A failed fetch also leaves it disabled — the
/// Retry in the slot section is the way forward there.
class MeetingSendButton extends StatelessWidget {
  const MeetingSendButton({
    required this.label,
    required this.enabled,
    required this.onTap,
    super.key,
  });

  final String label;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Opacity(
        opacity: enabled ? 1 : SimfTokens.opacityDisabled,
        child: Material(
          color: SimfTokens.accent,
          borderRadius: SimfTokens.borderRadiusSmall,
          child: InkWell(
            onTap: enabled ? onTap : null,
            borderRadius: SimfTokens.borderRadiusSmall,
            child: Container(
              height: SimfTokens.controlHeight,
              alignment: Alignment.center,
              child: Text(label, style: SimfTokens.labelWhiteBoldLg),
            ),
          ),
        ),
      );
}
