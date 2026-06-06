import 'package:flutter/material.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';

/// Page 038 — إمكانية الوصول · Accessibility (#38, `/settings/accessibility`, public).
///
/// **Public, no API.** A client-local accessibility settings panel: an intro
/// line, a text-size choice (Small / Default / Large), a high-contrast switch
/// and a reduce-motion switch.
///
/// **State is LOCAL only.** Persistence (writing the choices to a settings
/// store) and app-wide application (actually scaling the text / swapping the
/// theme / disabling animations across the app) are DEFERRED to a later
/// settings-store pass — this screen just captures the preferences in widget
/// state so the controls behave; nothing is saved or applied yet.
class AccessibilityScreen extends StatefulWidget {
  const AccessibilityScreen({super.key});

  @override
  State<AccessibilityScreen> createState() => _AccessibilityScreenState();
}

/// The local text-size choices (index 0 = small, 1 = default, 2 = large).
enum _TextSize { small, normal, large }

class _AccessibilityScreenState extends State<AccessibilityScreen> {
  _TextSize _textSize = _TextSize.normal;
  bool _highContrast = false;
  bool _reduceMotion = false;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.accessibilityTitle)),
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
                  selected: _textSize == _TextSize.small,
                  onSelected: (_) => setState(() => _textSize = _TextSize.small),
                ),
                ChoiceChip(
                  label: Text(l10n.accessibilityTextSizeDefault),
                  selected: _textSize == _TextSize.normal,
                  onSelected: (_) =>
                      setState(() => _textSize = _TextSize.normal),
                ),
                ChoiceChip(
                  label: Text(l10n.accessibilityTextSizeLarge),
                  selected: _textSize == _TextSize.large,
                  onSelected: (_) => setState(() => _textSize = _TextSize.large),
                ),
              ],
            ),
            const SizedBox(height: SimfTokens.space5),
            Card(
              clipBehavior: Clip.antiAlias,
              child: Column(
                children: <Widget>[
                  SwitchListTile(
                    value: _highContrast,
                    onChanged: (value) =>
                        setState(() => _highContrast = value),
                    title: Text(l10n.accessibilityHighContrastTitle),
                    subtitle: Text(l10n.accessibilityHighContrastSubtitle),
                  ),
                  const Divider(height: 1),
                  SwitchListTile(
                    value: _reduceMotion,
                    onChanged: (value) =>
                        setState(() => _reduceMotion = value),
                    title: Text(l10n.accessibilityReduceMotionTitle),
                    subtitle: Text(l10n.accessibilityReduceMotionSubtitle),
                  ),
                ],
              ),
            ),
            const SizedBox(height: SimfTokens.space5),
            Text(
              l10n.accessibilityDeferredNote,
              style: const TextStyle(
                color: SimfTokens.inkMuted,
                fontSize: SimfTokens.textSm,
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
