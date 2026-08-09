import 'package:flutter/widgets.dart';
import 'package:simf_app/core/responsive/breakpoints.dart';

/// How many columns a tile grid should use at the current window size.
///
/// Every grid in the app was written with the column count from its Figma
/// frame, and every frame is drawn at 375px. Those counts are correct on a
/// phone and wrong on the tablet the owner runs, because none of these screens
/// caps its content width: the columns simply get wider.
///
/// Measured on the real tile geometry:
///
/// | screen | sponsors, 3 col | gallery, 2 col |
/// |--------|-----------------|----------------|
/// | 375    | 120             | 180            |
/// | 800    | 261             | 392            |
/// | 1024   | 336             | 504            |
///
/// The gallery tile is designed at 164x104 and carries a fixed aspect ratio, so
/// at 1024 it renders 504x319: a card at triple size, with its image stretched
/// to match. Adding columns as the window grows keeps each tile near the size it
/// was designed at, which is what CLAUDE.md section 13.7 asks for.
///
/// Gallery tile width, measured before and after (design width 164):
///
/// | screen | before | after      |
/// |--------|--------|------------|
/// | 375    | 180    | 180, 2 col |
/// | 800    | 392    | 256, 3 col |
/// | 1024   | 504    | 244, 4 col |
/// | 1280   | 632    | 308, 4 col |
///
/// The phone number is unchanged, which is what keeps every golden byte
/// identical: they all render at 375.
///
/// This does NOT stretch tiles to fill the width, and it is not a
/// `maxCrossAxisExtent` grid: the frames specify an exact column count per
/// layout, and a count keeps the phone rendering byte-identical while letting
/// the tablet show more.
int responsiveGridColumns(
  BuildContext context, {
  required int compact,
}) {
  switch (WindowSize.of(context)) {
    case WindowSize.compact:
      return compact;
    case WindowSize.medium:
      // ~600-905: roughly twice the phone width, so one extra column keeps the
      // tile close to its design size rather than doubling it.
      return compact + 1;
    case WindowSize.expanded:
    case WindowSize.large:
      // 905+: two extra. Three phone-width columns fit here, and going wider
      // than that starts to leave tiles smaller than the frame intends.
      return compact + 2;
  }
}
