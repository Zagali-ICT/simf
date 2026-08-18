import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';

/// The beige card the walk-in form sits on, and the scroll it lives in.
///
/// The content cap is the responsive decision this widget holds (19c / §13.7):
/// a phone or compact window gets the 560 form width the Create-profile screen
/// uses, and a tablet the wider reading width so the two-column field grid has
/// room. Both are [MaxWidthBody]'s documented values.
class RegisterVisitorCard extends StatelessWidget {
  const RegisterVisitorCard({
    required this.wide,
    required this.child,
    super.key,
  });

  /// Tablet width. The form inside needs the same answer, so the window class
  /// is decided once by the caller and passed down rather than re-measured.
  final bool wide;

  final Widget child;

  static const double _maxWidthCompact = 560;
  static const double _maxWidthWide = 840;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        0,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      child: MaxWidthBody(
        maxWidth: wide ? _maxWidthWide : _maxWidthCompact,
        child: Material(
          color: SimfTokens.cardBeige,
          borderRadius: SimfTokens.borderRadiusSmall,
          child: Padding(
            padding: const EdgeInsets.all(SimfTokens.space6),
            child: child,
          ),
        ),
      ),
    );
  }
}
