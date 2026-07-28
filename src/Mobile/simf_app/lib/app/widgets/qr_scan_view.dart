import 'package:flutter/material.dart';

import '../localization/app_l10n.dart';
import '../theme/tokens.dart';
import 'simf_scanner_body.dart';

/// The shared, EMUI-safe full-page QR scanner used by the visitor-facing
/// scanners (contact scan, exhibitor lead capture) and badge sign-in — D-430,
/// D-737. It is a thin navy host (header + back + [PopScope]) around the shared
/// [SimfScannerBody], which owns the whole scanning experience: the gold-bracket
/// viewfinder, the always-usable manual entry, the single dedupe/single-flight
/// policy, and the camera-error/permission-denied state.
///
/// The EMUI-safety contract is preserved (D-423/D-426): the camera is bounded to
/// the viewfinder card, the "stop camera" control sits OUTSIDE the camera
/// surface, the manual path always works, and there are two reliable exits (the
/// header back and the body "Back" button).
///
/// The host passes the labels + an [onCode] callback that resolves / captures /
/// branches on the scanned or typed code. [onCode] is awaited with a busy
/// overlay so the camera can't re-fire while it runs.
class QrScanView extends StatelessWidget {
  const QrScanView({
    required this.title,
    required this.fieldLabel,
    required this.continueLabel,
    required this.onCode,
    required this.onLeave,
    this.cameraLabel,
    this.hint,
    this.enableCamera = true,
    super.key,
  });

  final String title;
  final String? hint;
  final String fieldLabel;
  final String continueLabel;

  /// "Start camera" button label (shown after "Stop camera"); the shared body
  /// falls back to a default when null.
  final String? cameraLabel;

  /// Resolve / capture / branch on the scanned or typed code. Awaited; the
  /// scanner shows a busy state and blocks re-fires while it runs.
  final Future<void> Function(String code) onCode;

  /// Leave the screen (host owns the navigation semantics).
  final VoidCallback onLeave;

  /// Off in widget tests (no camera) so the manual-entry path drives the flow.
  final bool enableCamera;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) {
          onLeave();
        }
      },
      child: Scaffold(
        backgroundColor: SimfTokens.navySurface,
        body: SafeArea(
          child: Column(
            children: <Widget>[
              _ScannerHeader(title: title, onBack: onLeave),
              Expanded(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(SimfTokens.space4),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      SimfScannerBody(
                        fieldLabel: fieldLabel,
                        continueLabel: continueLabel,
                        cameraLabel: cameraLabel,
                        hint: hint,
                        enableCamera: enableCamera,
                        onCode: onCode,
                      ),
                      const SizedBox(height: SimfTokens.space4),
                      Center(
                        child: TextButton(
                          onPressed: onLeave,
                          style: TextButton.styleFrom(
                            foregroundColor: SimfTokens.beigeBorder,
                          ),
                          child: Text(l10n.qrBack),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The scanner's navy header band (56-high): a circular navy-deep back button at
/// the inline start over a centred white title — mirrors `AccountSubHeader` with
/// `circular: true` (kept local so `app/` doesn't depend on `features/`).
class _ScannerHeader extends StatelessWidget {
  const _ScannerHeader({required this.title, required this.onBack});

  final String title;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 56,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          Align(
            alignment: Alignment.centerLeft,
            child: Padding(
              padding: const EdgeInsets.only(left: 8),
              child: DecoratedBox(
                decoration: const BoxDecoration(
                  color: SimfTokens.navyDeep,
                  shape: BoxShape.circle,
                ),
                child: IconButton(
                  key: const ValueKey<String>('scannerBack'),
                  onPressed: onBack,
                  // Icon-only, over a live camera preview — the one control on
                  // the scanner a user must be able to find without reading the
                  // frame. Same defect and fix as SimfFormScaffold above and
                  // SimfCircledBackButton (DEF-SWEEP-003).
                  tooltip: MaterialLocalizations.of(context).backButtonTooltip,
                  icon: const Icon(
                    Icons.arrow_back_ios_new,
                    color: Colors.white,
                    size: 20,
                    textDirection: TextDirection.ltr,
                  ),
                ),
              ),
            ),
          ),
          Text(
            title,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 24,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }
}
