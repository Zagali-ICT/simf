import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';

/// Announces the page [title] once on mount through the platform accessibility
/// channel, but only when the Page-038 screen-reader assist is enabled. Renders
/// nothing; lives invisibly in the [SimfPageShell] stack so every shell page that
/// carries a title participates without per-screen wiring.
class ScreenAnnouncer extends ConsumerStatefulWidget {
  const ScreenAnnouncer({required this.title});

  final String? title;

  @override
  ConsumerState<ScreenAnnouncer> createState() => _ScreenAnnouncerState();
}

class _ScreenAnnouncerState extends ConsumerState<ScreenAnnouncer> {
  @override
  void initState() {
    super.initState();
    final title = widget.title?.trim();
    if (title == null || title.isEmpty) {
      return;
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }
      bool assist;
      try {
        assist = ref.read(accessibilityControllerProvider).screenReaderAssist;
      } catch (_) {
        // Accessibility DI not wired (e.g. a widget test that builds a SimfPageShell
        // without overriding the controller). The announcer is best-effort and
        // must never break a page, so skip silently.
        return;
      }
      if (!assist) {
        return;
      }
      final l10n = AppL10n.of(context);
      SemanticsService.sendAnnouncement(
        View.of(context),
        l10n.accessibilityScreenAnnouncement(title),
        Directionality.of(context),
      );
    });
  }

  @override
  Widget build(BuildContext context) => const SizedBox.shrink();
}
