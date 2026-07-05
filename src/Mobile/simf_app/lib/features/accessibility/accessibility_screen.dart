import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/accessibility_controller.dart';
import 'widgets/accessibility_font_size_card.dart';
import 'widgets/accessibility_section_heading.dart';
import 'widgets/accessibility_toggle_row.dart';

/// Page 038 — إمكانية الوصول · Accessibility (#38, `/settings/accessibility`).
///
/// Pixel-parity to KSA Figma frame `1116:16630`: the navy [SimfPageShell] shell and
/// two grouped sections — **العرض** (font size: صغير / متوسط / كبير / أكبر, the
/// high-contrast switch and the reduce-motion switch) and **الصوت والقراءة**
/// (the screen-reader switch and the session-captions switch).
///
/// All choices are **persisted** ([AccessibilityController], prefs-backed) and
/// **applied app-wide**: the text scaler + reduce-motion ride the root
/// MediaQuery and high-contrast swaps the theme (`app/app.dart`); the
/// screen-reader switch drives the navigation announcer (`router.dart`); the
/// captions switch gates the live-broadcast caption strip.
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
          ),
          const SizedBox(height: SimfTokens.space3),
          AccessibilityToggleRow(
            title: l10n.accessibilityHighContrastTitle,
            hint: l10n.accessibilityHighContrastSubtitle,
            value: settings.highContrast,
            onChanged: controller.setHighContrast,
          ),
          const SizedBox(height: SimfTokens.space3),
          AccessibilityToggleRow(
            title: l10n.accessibilityReduceMotionTitle,
            hint: l10n.accessibilityReduceMotionSubtitle,
            value: settings.reduceMotion,
            onChanged: controller.setReduceMotion,
          ),
          const SizedBox(height: SimfTokens.space5),
          AccessibilitySectionHeading(l10n.accessibilitySectionSound),
          const SizedBox(height: SimfTokens.space3),
          AccessibilityToggleRow(
            title: l10n.accessibilityScreenReaderTitle,
            hint: l10n.accessibilityScreenReaderSubtitle,
            value: settings.screenReaderAssist,
            onChanged: (v) {
              controller.setScreenReaderAssist(v);
              // Immediate confirmation through the same channel the assist uses.
              if (v) {
                SemanticsService.sendAnnouncement(
                  View.of(context),
                  l10n.accessibilityScreenReaderTitle,
                  Directionality.of(context),
                );
              }
            },
          ),
          const SizedBox(height: SimfTokens.space3),
          AccessibilityToggleRow(
            title: l10n.accessibilityCaptionsTitle,
            hint: l10n.accessibilityCaptionsSubtitle,
            value: settings.captions,
            onChanged: controller.setCaptions,
          ),
        ],
      ),
    );
  }
}
