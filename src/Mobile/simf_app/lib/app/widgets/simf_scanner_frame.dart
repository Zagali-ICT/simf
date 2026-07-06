import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// The gold border weight of the viewfinder's corner brackets (Figma 758:4579).
const BorderSide _bracketSide = BorderSide(color: SimfTokens.accent, width: 2.36);

/// The QR-scanner viewfinder card from Figma node 758:4735 — a navy card holding
/// a black camera window (gold corner brackets, a gold scan line and a centred
/// scan glyph) above a "searching" caption + progress bar.
///
/// The live camera is injected as [camera] (e.g. a `flutter_zxing` reader) so
/// this widget stays plugin-free and rendable in tests; when it is null the
/// window paints the brackets + glyph on black (the camera-off / preview state).
class SimfScannerFrame extends StatelessWidget {
  const SimfScannerFrame({
    required this.statusLabel,
    this.camera,
    this.progress = 0.3,
    super.key,
  });

  /// The live camera preview painted behind the overlay; null = camera-off.
  final Widget? camera;

  /// The "actively searching" caption shown beside the percentage.
  final String statusLabel;

  /// The decorative scan-progress fraction (0–1) — drives the bar + the label.
  final double progress;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 343,
      padding: const EdgeInsets.symmetric(vertical: 16),
      decoration: BoxDecoration(
        color: SimfTokens.scannerCard,
        borderRadius: BorderRadius.circular(24),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x40000000),
            blurRadius: 60,
            offset: Offset(0, 24),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: _buildWindow(),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 20, 16, 4),
            child: _buildProgress(),
          ),
        ],
      ),
    );
  }

  Widget _buildWindow() {
    return ClipRRect(
      borderRadius: BorderRadius.circular(16),
      child: SizedBox(
        height: 300,
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            ColoredBox(color: Colors.black, child: camera),
            const ColoredBox(color: Color(0x59000000)), // black @ 35% overlay
            const Positioned(top: 16, left: 16, child: _Bracket(top: true, left: true)),
            const Positioned(top: 16, right: 16, child: _Bracket(top: true, left: false)),
            const Positioned(bottom: 16, left: 16, child: _Bracket(top: false, left: true)),
            const Positioned(bottom: 16, right: 16, child: _Bracket(top: false, left: false)),
            Positioned(
              top: 93,
              left: 16,
              right: 16,
              child: Container(
                height: 2,
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: <Color>[
                      Color(0x00C9A84C),
                      SimfTokens.accent,
                      Color(0x00C9A84C),
                    ],
                  ),
                  boxShadow: const <BoxShadow>[
                    BoxShadow(color: SimfTokens.accent, blurRadius: 8),
                  ],
                ),
              ),
            ),
            const Center(
              child: Icon(
                Icons.qr_code_scanner,
                color: SimfTokens.accent,
                size: 64,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildProgress() {
    // Forced LTR so the percentage sits at the start (left) and the caption at
    // the end (right), and the bar fills from the left — as in the frame.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: <Widget>[
              Text(
                '${(progress * 100).round()}%',
                style: const TextStyle(
                  color: SimfTokens.accent,
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                ),
              ),
              Text(
                statusLabel,
                textDirection: TextDirection.rtl,
                style: const TextStyle(color: SimfTokens.mutedBlue, fontSize: 12),
              ),
            ],
          ),
          const SizedBox(height: 8),
          ClipRRect(
            borderRadius: BorderRadius.circular(100),
            child: SizedBox(
              height: 6,
              child: ColoredBox(
                color: SimfTokens.scannerTrack,
                child: FractionallySizedBox(
                  alignment: Alignment.centerLeft,
                  widthFactor: progress.clamp(0.0, 1.0),
                  child: const DecoratedBox(
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: <Color>[SimfTokens.accent, Color(0xFFE8C96E)],
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// One gold L-shaped corner bracket of the viewfinder (Figma 758:4579-4582):
/// a 28px square drawing the [top]-or-bottom and [left]-or-right edges in gold.
class _Bracket extends StatelessWidget {
  const _Bracket({required this.top, required this.left});

  final bool top;
  final bool left;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 28,
      height: 28,
      decoration: BoxDecoration(
        border: Border(
          top: top ? _bracketSide : BorderSide.none,
          bottom: top ? BorderSide.none : _bracketSide,
          left: left ? _bracketSide : BorderSide.none,
          right: left ? BorderSide.none : _bracketSide,
        ),
      ),
    );
  }
}
