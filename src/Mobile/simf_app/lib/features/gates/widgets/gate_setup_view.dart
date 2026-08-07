import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/gate_models.dart';
import 'gate_direction_button.dart';
import 'gate_picker.dart';

/// Figma 758:4651 — the setup card: a QR glyph, the gate picker, the دخول/خروج
/// movement toggle, and the big "سكان الرمز" button (enabled once a movement
/// type is chosen).
class GateSetupView extends StatelessWidget {
  const GateSetupView({
    required this.l10n,
    required this.isArabic,
    required this.gates,
    required this.gate,
    required this.direction,
    required this.onGate,
    required this.onDirection,
    required this.onScan,
    super.key,
  });

  final AppL10n l10n;
  final bool isArabic;
  final List<OperatorGate> gates;
  final OperatorGate gate;
  final ScanDirection? direction;
  final ValueChanged<OperatorGate> onGate;
  final ValueChanged<ScanDirection> onDirection;
  final VoidCallback onScan;

  @override
  Widget build(BuildContext context) {
    final locked = gate.directionMode != GateDirectionMode.both;
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const Spacer(),
          // QR glyph in a gold-tinted, gold-bordered tile (Figma 758:4658).
          Center(
            child: Container(
              width: SimfTokens.qrTileSize,
              height: SimfTokens.qrTileSize,
              decoration: BoxDecoration(
                color: SimfTokens.accent.withValues(
                  alpha: SimfTokens.fillOpacitySubtle,
                ),
                borderRadius: BorderRadius.circular(SimfTokens.radius6),
                border: Border.all(
                  color: SimfTokens.accent,
                  width: SimfTokens.borderThick,
                ),
              ),
              child: const Icon(
                Icons.qr_code_2,
                size: 60,
                color: SimfTokens.accent,
              ),
            ),
          ),
          const Spacer(),
          _label(l10n.gateSelectGate),
          const SizedBox(height: SimfTokens.space4),
          GatePicker(
            isArabic: isArabic,
            gates: gates,
            gate: gate,
            onGate: onGate,
          ),
          // DEF-STF-006 — a selected inactive gate denies every scan, so say so
          // BEFORE the operator waves the first badge at it.
          if (!gate.isActive) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                const Icon(
                  Icons.warning_amber_outlined,
                  color: SimfTokens.danger,
                  size: 18,
                ),
                const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: Text(
                    l10n.gateInactiveWarning,
                    style: SimfTokens.bodyWhiteMd,
                  ),
                ),
              ],
            ),
          ],
          const SizedBox(height: SimfTokens.space6),
          _label(l10n.gateMovementType),
          const SizedBox(height: SimfTokens.space4),
          Row(
            children: <Widget>[
              Expanded(
                child: GateDirectionButton(
                  label: l10n.gateDirectionIn,
                  icon: Icons.login,
                  selected: direction == ScanDirection.checkIn,
                  enabled: !locked || gate.directionMode == GateDirectionMode.inOnly,
                  onTap: () => onDirection(ScanDirection.checkIn),
                ),
              ),
              const SizedBox(width: SimfTokens.space4),
              Expanded(
                child: GateDirectionButton(
                  label: l10n.gateDirectionOut,
                  icon: Icons.logout,
                  selected: direction == ScanDirection.checkOut,
                  enabled:
                      !locked || gate.directionMode == GateDirectionMode.outOnly,
                  onTap: () => onDirection(ScanDirection.checkOut),
                ),
              ),
            ],
          ),
          if (direction == null) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            Text(
              l10n.gateChooseDirectionFirst,
              textAlign: TextAlign.center,
              style: SimfTokens.bodyWhiteMd,
            ),
          ],
          const Spacer(flex: 2),
          SizedBox(
            height: SimfTokens.controlHeight,
            child: FilledButton.icon(
              onPressed: direction == null ? null : onScan,
              style: FilledButton.styleFrom(
                backgroundColor: SimfTokens.accent,
                foregroundColor: SimfTokens.surface,
                disabledBackgroundColor: SimfTokens.accent.withValues(
                  alpha: SimfTokens.opacityDisabledFill,
                ),
                disabledForegroundColor: SimfTokens.surface.withValues(
                  alpha: SimfTokens.opacityDisabledText,
                ),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
              ),
              icon: const Icon(Icons.photo_camera_outlined, size: 22),
              label: Text(
                l10n.gateScanCode,
                style: SimfTokens.titleBold,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _label(String text) => Text(
        text,
        textAlign: TextAlign.end,
        style: SimfTokens.labelWhiteMediumLg,
      );
}
