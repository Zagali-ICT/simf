import 'package:flutter/services.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The system bars' appearance under Android edge-to-edge, where the app draws
/// behind the status and navigation bars and both must disappear into it.
///
/// The fills come from [SimfTokens.transparent] rather than
/// `Colors.transparent`: `design_token_ratchet_test` fails the build on a raw
/// colour anywhere outside `tokens.dart`, and this file reddened it from the
/// moment edge-to-edge landed.
class SimfSystemUi {
  const SimfSystemUi._();

  static const SystemUiOverlayStyle edgeToEdge = SystemUiOverlayStyle(
    statusBarColor: SimfTokens.transparent,
    statusBarBrightness: Brightness.dark,
    statusBarIconBrightness: Brightness.light,
    systemNavigationBarColor: SimfTokens.transparent,
    systemNavigationBarDividerColor: SimfTokens.transparent,
    systemNavigationBarIconBrightness: Brightness.light,
    systemNavigationBarContrastEnforced: false,
    systemStatusBarContrastEnforced: false,
  );
}
