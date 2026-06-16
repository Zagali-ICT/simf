import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/accessibility_controller.dart';

/// Page 038 — إمكانية الوصول · Accessibility (#38, `/settings/accessibility`, public).
///
/// **Public, no API.** A client-local accessibility settings panel: an intro
/// line, a text-size choice (Small / Default / Large), a high-contrast switch
/// and a reduce-motion switch.
///
/// The choices are **persisted** via [AccessibilityController] (prefs-backed)
/// and **applied app-wide** — the text scaler and reduce-motion flag are
/// injected into the root MediaQuery and high-contrast swaps the theme (see
/// `app/app.dart`). Each control reads/writes the controller, so the selection
/// survives a restart.
class AccessibilityScreen extends ConsumerWidget {
  const AccessibilityScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final settings = ref.watch(accessibilityControllerProvider);
    final controller = ref.read(accessibilityControllerProvider.notifier);
    return Scaffold(
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.accessibilityTitle)),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(SimfTokens.space4),
          children: <Widget>[
            Text(
              l10n.accessibilityIntro,
              style: const TextStyle(color: SimfTokens.inkMuted),
            ),
            const SizedBox(height: SimfTokens.space5),
            _SectionHeading(l10n.accessibilityTextSizeLabel),
            const SizedBox(height: SimfTokens.space2),
            Wrap(
              spacing: SimfTokens.space2,
              children: <Widget>[
                ChoiceChip(
                  label: Text(l10n.accessibilityTextSizeSmall),
                  selected: settings.textSize == AppTextSize.small,
                  onSelected: (_) => controller.setTextSize(AppTextSize.small),
                ),
                ChoiceChip(
                  label: Text(l10n.accessibilityTextSizeDefault),
                  selected: settings.textSize == AppTextSize.normal,
                  onSelected: (_) => controller.setTextSize(AppTextSize.normal),
                ),
                ChoiceChip(
                  label: Text(l10n.accessibilityTextSizeLarge),
                  selected: settings.textSize == AppTextSize.large,
                  onSelected: (_) => controller.setTextSize(AppTextSize.large),
                ),
              ],
            ),
            const SizedBox(height: SimfTokens.space5),
            Card(
              clipBehavior: Clip.antiAlias,
              child: Column(
                children: <Widget>[
                  SwitchListTile(
                    value: settings.highContrast,
                    onChanged: controller.setHighContrast,
                    title: Text(l10n.accessibilityHighContrastTitle),
                    subtitle: Text(l10n.accessibilityHighContrastSubtitle),
                  ),
                  const Divider(height: 1),
                  SwitchListTile(
                    value: settings.reduceMotion,
                    onChanged: controller.setReduceMotion,
                    title: Text(l10n.accessibilityReduceMotionTitle),
                    subtitle: Text(l10n.accessibilityReduceMotionSubtitle),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SectionHeading extends StatelessWidget {
  const _SectionHeading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        fontWeight: FontWeight.w700,
        fontSize: SimfTokens.textLg,
      ),
    );
  }
}
