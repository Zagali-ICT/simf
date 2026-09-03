import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_app/features/accessibility/widgets/accessibility_font_size_card.dart';
import 'package:simf_app/features/accessibility/widgets/accessibility_section_heading.dart';

/// Accessibility — إمكانية الوصول · route: `RouteNames.accessibility`
/// Figma 1116:16630
///
/// ONE control, deliberately. The text scaler is real: it rides the root
/// MediaQuery (`app/app.dart`), composed on top of the platform's own Dynamic
/// Type rather than replacing it.

/// High contrast, reduce motion, the screen-reader announcer and captions were
/// each wired to a provider and each observably inert — the theme swap reached
/// almost nothing (the app paints from `SimfTokens`, not `ColorScheme`),
/// `disableAnimations` is read here by nothing, the announcer fired on mount
/// rather than on navigation, and the "caption" was one static admin string.
/// The controller fields, storage keys and wire contract are all KEPT; only the
/// dead controls are withdrawn. Restoring one means building it first.
class AccessibilityScreen extends ConsumerWidget {
  const AccessibilityScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final settings = ref.watch(accessibilityControllerProvider);
    final controller = ref.read(accessibilityControllerProvider.notifier);
    return SimfPageShell(
      title: l10n.accessibilityTitle,
      onBack: () => backOrHome(context),
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          AccessibilitySectionHeading(l10n.accessibilitySectionDisplay),
          const SizedBox(height: SimfTokens.space3),
          AccessibilityFontSizeCard(
            value: settings.textSize,
            onChanged: controller.setTextSize,
            note: platformTextScaleAtCeiling(MediaQuery.textScalerOf(context))
                ? l10n.accessibilityTextSizeSystemControls
                : null,
          ),
        ],
      ),
    );
  }
}
