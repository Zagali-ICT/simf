import 'package:flutter/services.dart';

/// The system-bar icon appearance while the native Activity owns edge-to-edge.
///
/// Do not set system-bar colors here. AndroidX `enableEdgeToEdge()` provides
/// the backward-compatible transparent bars, while Android 15 deprecates the
/// Window color APIs that Flutter uses for those color parameters.
class SimfSystemUi {
  const SimfSystemUi._();

  static const SystemUiOverlayStyle edgeToEdge = SystemUiOverlayStyle(
    statusBarBrightness: Brightness.dark,
    statusBarIconBrightness: Brightness.light,
    systemNavigationBarIconBrightness: Brightness.light,
  );
}
