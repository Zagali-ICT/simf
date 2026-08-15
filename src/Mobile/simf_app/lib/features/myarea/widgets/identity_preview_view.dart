import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The confirm/retake step: the captured identity photo full-bleed, with save
/// and retake over a gradient scrim.
///
/// The third of the capture screen's view states, alongside `LiveCaptureView`
/// and `IdentityFallbackView`. It was the one left inline on the screen as a
/// `_previewView` method returning its own [Scaffold], which made the screen
/// read as if it had two states rather than three.
class IdentityPreviewView extends StatelessWidget {
  const IdentityPreviewView({
    required this.bytes,
    required this.l10n,
    required this.onSave,
    required this.onRetake,
    super.key,
  });

  /// The captured image, held in memory rather than on disk (D-662: the photo
  /// must come from a live camera, and is forwarded to the caller, not stored).
  final Uint8List bytes;

  final AppL10n l10n;

  /// Accept the photo and return it to the caller.
  final VoidCallback onSave;

  /// Discard and go back to the camera.
  final VoidCallback onRetake;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: SimfTokens.black,
      body: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          Image.memory(bytes, fit: BoxFit.contain),
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            child: Container(
              padding: EdgeInsets.only(
                left: SimfTokens.space4,
                right: SimfTokens.space4,
                top: SimfTokens.space8,
                bottom:
                    MediaQuery.of(context).padding.bottom + SimfTokens.space4,
              ),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: <Color>[
                    SimfTokens.transparent,
                    SimfTokens.navy.withValues(alpha: 0.9),
                  ],
                ),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: onSave,
                      child: Text(l10n.saveLabel),
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space2),
                  SizedBox(
                    width: double.infinity,
                    child: TextButton(
                      onPressed: onRetake,
                      style: TextButton.styleFrom(
                        foregroundColor: SimfTokens.beigeBorder,
                      ),
                      child: Text(l10n.retryLabel),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
