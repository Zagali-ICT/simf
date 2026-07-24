import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/delegation_models.dart';

/// The header stats strip (Figma 1426:10781): a navy card with a faint gold
/// grid, the invited countries' flags scattered across it, and the two big-gold
/// stats — participating countries (left) + total participants (right). Each
/// scattered flag is a tap target that filters the list below to that country
/// (the selected flag is ringed; tapping it again clears — the screen owns the
/// selection state and the body renders the removable active-filter chip).
class DelegationsStatsStrip extends StatelessWidget {
  const DelegationsStatsStrip({
    required this.countryCount,
    required this.flagItems,
    required this.selectedCountryCode,
    required this.onFlagTap,
    required this.l10n,
    super.key,
  });

  final int countryCount;

  /// The invited countries whose flag renders a scattered tap target. Only the
  /// first [_spots] of these are placed (the strip is a fixed-size decorative
  /// map, not a full list).
  final List<DelegationItem> flagItems;

  /// The country whose flag is currently selected (filtering the list), or null
  /// when no flag filter is active.
  final String? selectedCountryCode;

  /// Fired with the tapped country's code so the screen can toggle the filter.
  final ValueChanged<String> onFlagTap;

  final AppL10n l10n;

  /// The transparent padding around each flag glyph that widens the tap target
  /// without moving the glyph — the [Positioned] offset is pulled back by the
  /// same amount, so an unselected flag renders at the exact original pixel.
  static const double _hitPad = 9;

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
    final count =
        flagItems.length < _spots.length ? flagItems.length : _spots.length;
    return Container(
      height: SimfTokens.statsStripHeight,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius14),
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
                  left: _spots[i].dx * width - _hitPad,
                  top: _spots[i].dy * height - _hitPad,
                  child: _FlagSpot(
                    flag: flagItems[i].flagEmoji,
                    selected: flagItems[i].countryCode == selectedCountryCode,
                    onTap: () => onFlagTap(flagItems[i].countryCode),
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
            ],
          );
        },
      ),
    );
  }
}

/// One scattered flag glyph as a tap target. The glyph sits inside
/// [DelegationsStatsStrip._hitPad] of transparent padding (widening the touch
/// area); when [selected] the padding box is ringed in gold to mark the active
/// filter.
class _FlagSpot extends StatelessWidget {
  const _FlagSpot({
    required this.flag,
    required this.selected,
    required this.onTap,
  });

  final String flag;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(DelegationsStatsStrip._hitPad),
        decoration: selected
            ? BoxDecoration(
                color: SimfTokens.goldFill6,
                border: Border.all(color: SimfTokens.accent),
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              )
            : null,
        child: Text(flag, style: const TextStyle(fontSize: 14)),
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
        Text('$value', style: SimfTokens.labelGoldBoldXl),
        Text(label, style: SimfTokens.labelBeigeSm),
      ],
    );
  }
}
