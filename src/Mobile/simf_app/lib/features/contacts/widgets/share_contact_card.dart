import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The Share-my-contact card: the framed QR over the hint, the OS-share button
/// and the rotate button (disabled + spinner while [rotating]).
class ShareContactCard extends StatelessWidget {
  const ShareContactCard({
    required this.qrData,
    required this.rotating,
    required this.onShare,
    required this.onRotate,
    super.key,
  });

  /// The QR payload — the vCard with the share token embedded (D-737).
  final String qrData;
  final bool rotating;
  final VoidCallback onShare;
  final VoidCallback onRotate;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            // NOT a DecoratedBox. Container insets its child by
            // BoxDecoration.padding, which is the border dimensions, and this
            // decoration has a border — the swap moved a golden by 2.42% when
            // it was tried (2026-08-14).
            // ignore: use_decorated_box
            Container(
              decoration: BoxDecoration(
                color: SimfTokens.surface,
                borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
                border: Border.all(color: SimfTokens.accent),
              ),
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space6),
                child: QrImageView(
                  data: qrData,
                  size: SimfTokens.shareMyContactScreenSize,
                ),
              ),
            ),
            const SizedBox(height: SimfTokens.space5),
            Text(
              l10n.shareMyContactHint,
              textAlign: TextAlign.center,
              style: SimfTokens.bodyWhite70,
            ),
            const SizedBox(height: SimfTokens.space5),
            FilledButton.icon(
              onPressed: onShare,
              style: FilledButton.styleFrom(
                minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
                backgroundColor: SimfTokens.accent,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
              ),
              icon: const Icon(Icons.ios_share, color: SimfTokens.surface),
              label: Text(
                l10n.shareContact,
                style: SimfTokens.labelWhiteBoldLg,
              ),
            ),
            const SizedBox(height: SimfTokens.space2),
            OutlinedButton.icon(
              onPressed: rotating ? null : onRotate,
              style: OutlinedButton.styleFrom(
                minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
                side: const BorderSide(color: SimfTokens.accent),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
              ),
              icon: rotating
                  ? const SizedBox(
                      width: SimfTokens.space4,
                      height: SimfTokens.space4,
                      child: CircularProgressIndicator(
                        strokeWidth: SimfTokens.shareMyContactScreenStrokeWidth,
                      ),
                    )
                  : const Icon(Icons.autorenew, color: SimfTokens.accent),
              label: Text(
                l10n.shareMyContactRotate,
                style: SimfTokens.labelGoldBoldLg,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
