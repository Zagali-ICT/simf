import 'package:flutter/material.dart';
import 'package:flutter_zxing/flutter_zxing.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

/// The live scanner stage — the ZXing camera (native, no Google Play Services,
/// works on Huawei/HMS — D-426) plus the always-usable manual-entry path.
class GateScannerView extends StatelessWidget {
  const GateScannerView({
    required this.l10n,
    required this.enableCamera,
    required this.manual,
    required this.onScan,
    required this.onBack,
    required this.onManual,
    super.key,
  });

  final AppL10n l10n;
  final bool enableCamera;
  final TextEditingController manual;
  final void Function(Code) onScan;
  final VoidCallback onBack;
  final VoidCallback onManual;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Expanded(
            child: ClipRRect(
              borderRadius: BorderRadius.circular(SimfTokens.radius),
              child: DecoratedBox(
                decoration: BoxDecoration(
                  color: SimfTokens.navyDeep,
                  borderRadius: BorderRadius.circular(SimfTokens.radius),
                  border: Border.all(color: SimfTokens.accent, width: 2),
                ),
                child: Stack(
                  fit: StackFit.expand,
                  children: <Widget>[
                    if (enableCamera)
                      ReaderWidget(
                        onScan: onScan,
                        codeFormat: Format.qrCode,
                        showGallery: false,
                        showToggleCamera: false,
                        tryInverted: true,
                        // Back inside flutter_zxing's overlay — tappable over the
                        // live camera where the AppBar back is swallowed (D-426).
                        onActionSecondButton: onBack,
                        actionSecondButtonIcon: const Icon(Icons.arrow_back),
                        loading: const Center(
                          child: Icon(
                            Icons.qr_code_2,
                            size: 72,
                            color: SimfTokens.beigeBorder,
                          ),
                        ),
                      )
                    else
                      const Center(
                        child: Icon(
                          Icons.qr_code_2,
                          size: 72,
                          color: SimfTokens.beigeBorder,
                        ),
                      ),
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: SimfTokens.space3,
                      child: Text(
                        l10n.gateScanHint,
                        textAlign: TextAlign.center,
                        style: const TextStyle(color: Colors.white),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          Row(
            children: <Widget>[
              Expanded(
                child: TextField(
                  controller: manual,
                  style: const TextStyle(color: Colors.white),
                  decoration: InputDecoration(
                    hintText: l10n.gateManualHint,
                    hintStyle: const TextStyle(color: SimfTokens.beigeBorder),
                    enabledBorder: const OutlineInputBorder(
                      borderSide: BorderSide(color: SimfTokens.beigeBorder),
                    ),
                    focusedBorder: const OutlineInputBorder(
                      borderSide: BorderSide(color: SimfTokens.accent),
                    ),
                  ),
                  onSubmitted: (_) => onManual(),
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              FilledButton(
                onPressed: onManual,
                style: FilledButton.styleFrom(
                  backgroundColor: SimfTokens.accent,
                  foregroundColor: SimfTokens.navy,
                  minimumSize: const Size(72, 52),
                ),
                child: Text(l10n.gateManualSubmit),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
