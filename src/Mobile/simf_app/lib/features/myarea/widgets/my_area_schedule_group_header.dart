import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// A جدولي اليوم sub-group header (frame nodes 1041:2042 / 1041:2044): the gold
/// "جلسات" / "مقابلات" label, at the inline start, above each group of rows.
class MyAreaScheduleGroupHeader extends StatelessWidget {
  const MyAreaScheduleGroupHeader({required this.label, super.key});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Text(
        label,
        style: SimfTokens.labelGoldSemiboldSm,
      ),
    );
  }
}
