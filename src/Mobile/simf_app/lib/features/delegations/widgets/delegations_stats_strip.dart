import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

/// The header stats strip (Figma 1426:10781): a navy card with a faint gold
/// grid, the invited countries' flags scattered across it, and the two big-gold
/// stats — participating countries (left) + total participants (right).
class DelegationsStatsStrip extends StatelessWidget {
  const DelegationsStatsStrip({
    required this.countryCount,
    required this.totalParticipants,
    required this.flags,
    required this.l10n,
    super.key,
  });

  final int countryCount;
  final int totalParticipants;
  final List<String> flags;
  final AppL10n l10n;

  /// The Figma's decorative scatter positions (relative to the strip), filled
  /// with the real invited-country flags so the map reflects the data.
  static const List<Offset> _spots = <Offset>[
    Offset(0.60, 0.30),
    Offset(0.16, 0.25),
    Offset(0.42, 0.10),
    Offset(0.46, 0.16),
    Offset(0.48, 0.09),
    Offset(0.76, 0.22),
    Offset(0.63, 0.30),
    Offset(0.54, 0.18),
  ];

  @override
  Widget build(BuildContext context) {
    final count = flags.length < _spots.length ? flags.length : _spots.length;
    return Container(
      height: 100,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(14),
      ),
      child: LayoutBuilder(
        builder: (context, constraints) {
          final width = constraints.maxWidth;
          final height = constraints.maxHeight;
          return Stack(
            children: <Widget>[
              const Positioned.fill(
                child: CustomPaint(painter: _GridPainter()),
              ),
              for (var i = 0; i < count; i++)
                Positioned(
                  left: _spots[i].dx * width,
                  top: _spots[i].dy * height,
                  child: Text(
                    flags[i],
                    style: const TextStyle(fontSize: 14),
                  ),
                ),
              Positioned(
                left: SimfTokens.space4,
                bottom: SimfTokens.space3,
                child: _Stat(
                  value: countryCount,
                  label: l10n.delegationsCountriesStat,
                  alignEnd: false,
                ),
              ),
              Positioned(
                right: SimfTokens.space4,
                bottom: SimfTokens.space3,
                child: _Stat(
                  value: totalParticipants,
                  label: l10n.delegationsParticipantsStat,
                  alignEnd: true,
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _GridPainter extends CustomPainter {
  const _GridPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = SimfTokens.goldFill7
      ..strokeWidth = 1;
    for (var i = 1; i < 7; i++) {
      final x = size.width * i / 7;
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), paint);
    }
    for (var i = 1; i < 5; i++) {
      final y = size.height * i / 5;
      canvas.drawLine(Offset(0, y), Offset(size.width, y), paint);
    }
  }

  @override
  bool shouldRepaint(_GridPainter oldDelegate) => false;
}

class _Stat extends StatelessWidget {
  const _Stat({
    required this.value,
    required this.label,
    required this.alignEnd,
  });

  final int value;
  final String label;
  final bool alignEnd;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment:
          alignEnd ? CrossAxisAlignment.end : CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          '$value',
          style: const TextStyle(
            fontSize: SimfTokens.textXl,
            fontWeight: FontWeight.w700,
            color: SimfTokens.accent,
          ),
        ),
        Text(
          label,
          style: const TextStyle(
            fontSize: SimfTokens.textSm,
            color: SimfTokens.beigeBorder,
          ),
        ),
      ],
    );
  }
}
