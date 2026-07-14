import 'package:flutter/material.dart';

import '../../features/badge/badge_screen.dart';
import '../../features/home/home_screen.dart';
import '../../features/myarea/my_area_screen.dart';
import '../../features/sessions/sessions_screen.dart';
import '../../features/venuemap/venue_map_screen.dart';
import 'simf_bottom_nav.dart' show SimfTab;

/// Maps a [SimfTab] to its index in the shell's tab list.
int tabIndex(SimfTab tab) => SimfTab.values.indexOf(tab);

/// Inherited scope that lets child widgets (bottom nav, greeting header, etc.)
/// switch the shell's active tab without going through go_router.
///
/// Pushed flat routes (outside the shell) don't inherit this scope, so their
/// [SimfBottomNav] falls back to `context.goNamed(...)`.
class SimfShellScope extends InheritedWidget {
  const SimfShellScope({
    required this.switchTab,
    required super.child,
    super.key,
  });

  final void Function(int tabIndex) switchTab;

  static SimfShellScope? maybeOf(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<SimfShellScope>();

  @override
  bool updateShouldNotify(SimfShellScope oldWidget) =>
      oldWidget.switchTab != switchTab;
}

/// Replaces `StatefulShellRoute.indexedStack`.
///
/// Renders the five bottom-nav tabs in an [IndexedStack]. Each tab screen
/// already wraps itself in `SimfPageShell` (its own `Scaffold`, drawer, and
/// bottom nav), so this widget is deliberately thin — no extra chrome.
///
/// Tab switching is purely internal. The [SimfShellScope] lets child widgets
/// switch tabs without go_router, so Flutter's `_debugCheckDuplicatedPageKeys`
/// assertion is never triggered (there is only one route in the parent
/// Navigator, not one per branch).
class SimfAppShell extends StatefulWidget {
  const SimfAppShell({super.key});

  @override
  State<SimfAppShell> createState() => _SimfAppShellState();
}

class _SimfAppShellState extends State<SimfAppShell> {
  int _currentIndex = 0;

  void _switchTab(int index) {
    if (index == _currentIndex) return;
    setState(() => _currentIndex = index.clamp(0, _tabs.length - 1));
  }

  static const List<Widget> _tabs = <Widget>[
    HomeScreen(),
    SessionsScreen(),
    BadgeScreen(),
    VenueMapScreen(),
    MyAreaScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    return SimfShellScope(
      switchTab: _switchTab,
      child: IndexedStack(index: _currentIndex, children: _tabs),
    );
  }
}
