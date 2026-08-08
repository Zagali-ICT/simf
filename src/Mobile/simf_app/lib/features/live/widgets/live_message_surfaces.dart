import 'package:flutter/material.dart';

import 'message_surface.dart';

/// The black surface shown when there is no live stream but a recording exists —
/// keeps the frame's black player band, with a recording note instead of a feed.
class RecordingSurface extends StatelessWidget {
  const RecordingSurface({required this.message, super.key});

  final String message;

  @override
  Widget build(BuildContext context) {
    return MessageSurface(
      icon: Icons.video_library_outlined,
      message: message,
    );
  }
}

/// The black surface shown when the session is neither live nor recorded.
class NotLiveSurface extends StatelessWidget {
  const NotLiveSurface({required this.message, super.key});

  final String message;

  @override
  Widget build(BuildContext context) {
    return MessageSurface(
      icon: Icons.live_tv_outlined,
      message: message,
    );
  }
}

