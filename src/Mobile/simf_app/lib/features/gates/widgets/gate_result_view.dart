import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/gates/data/gate_models.dart';

/// The green مسموح / red ممنوع verdict card (Figma 758:4886 / 758:4819): the
/// outcome icon + label + subtitle, the holder/reference/type/gate/direction
/// detail rows, and the "سكان مرة أخرى" button.
class GateResultView extends StatelessWidget {
  const GateResultView({
    required this.l10n,
    required this.isArabic,
    required this.result,
    required this.gateName,
    required this.reference,
    required this.onScanAgain,
    super.key,
  });

  final AppL10n l10n;
  final bool isArabic;
  final GateScanResult result;
  final String gateName;

  /// The scanned/entered badge code — shown as the frame's "الرقم المرجعي".
  final String reference;
  final VoidCallback onScanAgain;

  @override
  Widget build(BuildContext context) {
    final allowed = result.isAllowed;
    final accent = allowed ? SimfTokens.success : SimfTokens.danger;
    final name = result.userProfile?.localizedName(isArabic);
    final type = result.userProfile?.profileTypeName;
    final direction = result.direction == ScanDirection.checkOut
        ? l10n.gateDirectionOut
        : l10n.gateDirectionIn;
    final notice = (result.noticeMessage?.trim().isNotEmpty ?? false)
        ? result.noticeMessage!.trim()
        : null;
    return SingleChildScrollView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Container(
        padding: const EdgeInsets.all(SimfTokens.space5),
        decoration: BoxDecoration(
          color: accent.withValues(alpha: SimfTokens.fillOpacitySubtle),
          borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
          border: Border.all(color: accent),
        ),
        child: Column(
          children: <Widget>[
            Icon(
              allowed ? Icons.check_circle_outline : Icons.cancel_outlined,
              size: SimfTokens.gateResultViewSize,
              color: accent,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              allowed ? l10n.gateAllowed : l10n.gateDenied,
              style: TextStyle(
                color: accent,
                fontSize: SimfTokens.textXl,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: SimfTokens.space2),
            Text(
              allowed
                  ? l10n.gateAllowedSub
                  : (result.denialMessage?.trim().isNotEmpty ?? false
                      ? result.denialMessage!
                      : l10n.gateDeniedSub),
              textAlign: TextAlign.center,
              style: SimfTokens.textAccent,
            ),
            // DEF-CHK-004 — allowed, but the server flagged something the
            // operator has to know (today: no session running in this hall,
            // so no attendance was recorded). Arrives already localized.
            if (notice != null) ...<Widget>[
              const SizedBox(height: SimfTokens.space2),
              Text(
                notice,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: SimfTokens.warning,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
            const SizedBox(height: SimfTokens.space4),
            Container(
              padding: const EdgeInsets.all(SimfTokens.space4),
              decoration: BoxDecoration(
                color: SimfTokens.navyDeep,
                borderRadius: BorderRadius.circular(SimfTokens.radius),
              ),
              child: Column(
                children: <Widget>[
                  _row(
                    l10n.gateFieldName,
                    (name?.trim().isNotEmpty ?? false)
                        ? name!
                        : l10n.gateUnregistered,
                  ),
                  _row(
                    l10n.gateFieldReference,
                    reference.trim().isNotEmpty ? reference : l10n.gateNone,
                  ),
                  _row(
                    l10n.gateFieldType,
                    (type?.trim().isNotEmpty ?? false) ? type! : '—',
                  ),
                  _row(l10n.gateFieldGate, gateName),
                  _row(l10n.gateFieldDirection, direction, valueColor: accent),
                ],
              ),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton.icon(
              onPressed: onScanAgain,
              style: FilledButton.styleFrom(
                backgroundColor: accent,
                foregroundColor: SimfTokens.surface,
                minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
              ),
              icon: const Icon(Icons.qr_code_scanner),
              label: Text(l10n.gateScanAgain),
            ),
          ],
        ),
      ),
    );
  }

  Widget _row(String label, String value, {Color? valueColor}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              value,
              style: TextStyle(
                color: valueColor ?? SimfTokens.surface,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ),
          Text(
            label,
            style: SimfTokens.labelBeigeSm,
          ),
        ],
      ),
    );
  }
}
