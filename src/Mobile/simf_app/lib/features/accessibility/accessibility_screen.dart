import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/accessibility_controller.dart';

/// Page 038 — إمكانية الوصول · Accessibility (#38, `/settings/accessibility`).
///
/// Pixel-parity to KSA Figma frame `1116:16630`: the navy [KsaPage] shell and
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
    return KsaPage(
      title: l10n.accessibilityTitle,
      onBack: () => ksaBackOrHome(context),
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          _SectionHeading(l10n.accessibilitySectionDisplay),
          const SizedBox(height: SimfTokens.space3),
          _FontSizeCard(
            value: settings.textSize,
            onChanged: controller.setTextSize,
          ),
          const SizedBox(height: SimfTokens.space3),
          _ToggleRow(
            title: l10n.accessibilityHighContrastTitle,
            hint: l10n.accessibilityHighContrastSubtitle,
            value: settings.highContrast,
            onChanged: controller.setHighContrast,
          ),
          const SizedBox(height: SimfTokens.space3),
          _ToggleRow(
            title: l10n.accessibilityReduceMotionTitle,
            hint: l10n.accessibilityReduceMotionSubtitle,
            value: settings.reduceMotion,
            onChanged: controller.setReduceMotion,
          ),
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.accessibilitySectionSound),
          const SizedBox(height: SimfTokens.space3),
          _ToggleRow(
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
          _ToggleRow(
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

/// The "حجم الخط" card (frame 1116:16630): the label over a row of four pill
/// chips, the selected one filled gold.
class _FontSizeCard extends StatelessWidget {
  const _FontSizeCard({required this.value, required this.onChanged});

  final AppTextSize value;
  final ValueChanged<AppTextSize> onChanged;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // Reading order (RTL) صغير · متوسط · كبير · أكبر.
    final options = <(AppTextSize, String)>[
      (AppTextSize.small, l10n.accessibilityTextSizeSmall),
      (AppTextSize.normal, l10n.accessibilityTextSizeDefault),
      (AppTextSize.large, l10n.accessibilityTextSizeLarge),
      (AppTextSize.extraLarge, l10n.accessibilityTextSizeExtraLarge),
    ];
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.accessibilityTextSizeLabel,
            textAlign: TextAlign.start,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          Row(
            children: <Widget>[
              for (final (index, (size, label)) in options.indexed) ...<Widget>[
                if (index > 0) const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: _SizeChip(
                    label: label,
                    selected: size == value,
                    onTap: () => onChanged(size),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

/// One font-size pill: gold fill when [selected], else a beige-hairline outline.
class _SizeChip extends StatelessWidget {
  const _SizeChip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      selected: selected,
      label: label,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          height: 36,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: selected ? SimfTokens.accent : Colors.transparent,
            border: selected
                ? null
                : Border.all(
                    color: SimfTokens.beigeBorder,
                    width: SimfTokens.hairline,
                  ),
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: selected ? Colors.white : SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
            ),
          ),
        ),
      ),
    );
  }
}

/// One labelled switch row (frames 1116:16630): the navy-deep box with the title
/// at the inline start and the gold switch at the inline end. [hint] rides the
/// switch as a semantics hint (this is the accessibility screen, after all).
class _ToggleRow extends StatelessWidget {
  const _ToggleRow({
    required this.title,
    required this.hint,
    required this.value,
    required this.onChanged,
  });

  final String title;
  final String hint;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.start,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          Semantics(
            hint: hint,
            child: Switch(
              value: value,
              onChanged: onChanged,
              activeThumbColor: Colors.white,
              activeTrackColor: SimfTokens.accent,
              inactiveThumbColor: Colors.white,
              inactiveTrackColor: SimfTokens.navy,
              trackOutlineColor:
                  WidgetStateProperty.all(SimfTokens.beigeBorder),
            ),
          ),
        ],
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
      textAlign: TextAlign.start,
      style: const TextStyle(
        color: Colors.white,
        fontWeight: FontWeight.w600,
        fontSize: SimfTokens.textLg,
      ),
    );
  }
}
