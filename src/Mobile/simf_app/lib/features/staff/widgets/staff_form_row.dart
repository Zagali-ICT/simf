import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// Lays two fields side-by-side on a wide (tablet) card, stacked on a phone.
/// A null [end] (e.g. Saudi → no document-number field) collapses to [start].
class StaffFormRow extends StatelessWidget {
  const StaffFormRow({
    required this.wide,
    required this.start,
    this.end,
    super.key,
  });

  final bool wide;
  final Widget start;
  final Widget? end;

  @override
  Widget build(BuildContext context) {
    final second = end;
    if (second == null) {
      return start;
    }
    if (!wide) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          start,
          const SizedBox(height: SimfTokens.space4),
          second,
        ],
      );
    }
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Expanded(child: start),
        const SizedBox(width: SimfTokens.space4),
        Expanded(child: second),
      ],
    );
  }
}
