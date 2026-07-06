import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The full-bleed liveness camera (frames 758:4180 / 758:4248 / 758:4316): the
/// front-camera preview fills the screen edge-to-edge (a spinner until it is
/// ready). A prompt, a manual shutter and a "choose from gallery" action are
/// overlaid (owner 2026-07-06) so the photo can always be taken — the auto
/// smile → turn liveness still runs but can't complete on devices without
/// Google Play Services (ML Kit), where only the manual shutter / gallery work.
class LiveCaptureView extends StatelessWidget {
  const LiveCaptureView({
    required this.ready,
    required this.preview,
    required this.promptText,
    required this.captureLabel,
    required this.galleryLabel,
    required this.capturing,
    required this.onCapture,
    required this.onGallery,
    super.key,
  });

  final bool ready;
  final Widget? preview;
  final String promptText;
  final String captureLabel;
  final String galleryLabel;
  final bool capturing;
  final VoidCallback onCapture;
  final VoidCallback onGallery;

  @override
  Widget build(BuildContext context) {
    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        if (ready && preview != null)
          SizedBox.expand(
            child: FittedBox(fit: BoxFit.cover, child: _sized(preview!)),
          )
        else
          const Center(
            child: CircularProgressIndicator(color: SimfTokens.accent),
          ),
        if (ready) _controls(),
      ],
    );
  }

  Widget _controls() {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          children: <Widget>[
            _prompt(),
            const Spacer(),
            _shutter(),
            const SizedBox(height: SimfTokens.space4),
            TextButton.icon(
              onPressed: capturing ? null : onGallery,
              style: TextButton.styleFrom(foregroundColor: Colors.white),
              icon: const Icon(Icons.photo_library_outlined),
              label: Text(galleryLabel),
            ),
          ],
        ),
      ),
    );
  }

  Widget _prompt() {
    return DecoratedBox(
      decoration: const BoxDecoration(
        color: SimfTokens.navyFill90,
        borderRadius: SimfTokens.borderRadiusSmall,
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
        child: Text(
          promptText,
          textAlign: TextAlign.center,
          style: const TextStyle(color: Colors.white, fontSize: 14),
        ),
      ),
    );
  }

  Widget _shutter() {
    return Semantics(
      button: true,
      label: captureLabel,
      child: GestureDetector(
        onTap: capturing ? null : onCapture,
        child: Container(
          width: 76,
          height: 76,
          decoration: BoxDecoration(
            color: capturing ? SimfTokens.accent : Colors.white,
            shape: BoxShape.circle,
            border: Border.all(color: SimfTokens.accent, width: 4),
          ),
          child: capturing
              ? const Padding(
                  padding: EdgeInsets.all(20),
                  child: CircularProgressIndicator(
                    strokeWidth: 3,
                    color: SimfTokens.navy,
                  ),
                )
              : const Icon(
                  Icons.photo_camera,
                  color: SimfTokens.navy,
                  size: 32,
                ),
        ),
      ),
    );
  }

  // CameraPreview needs a finite size inside the FittedBox/BoxFit.cover.
  Widget _sized(Widget child) =>
      SizedBox(width: 1080, height: 1440, child: child);
}
