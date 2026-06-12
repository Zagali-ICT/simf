import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/localization/locale_controller.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_logo.dart';

/// Shared KSA field/button styling for screens built on [KsaAuthScaffold]
/// (the same shapes the sign-in/sign-up cards use).
const TextStyle ksaInputTextStyle = TextStyle(
  fontSize: 14,
  fontWeight: FontWeight.w500,
  color: SimfTokens.inputInk,
);

const OutlineInputBorder _ksaRestingBorder = OutlineInputBorder(
  borderRadius: KsaAuthScaffold._radius4,
  borderSide: BorderSide(color: SimfTokens.beigeBorder),
);
const OutlineInputBorder _ksaFocusedBorder = OutlineInputBorder(
  borderRadius: KsaAuthScaffold._radius4,
  borderSide: BorderSide(color: SimfTokens.accent),
);

InputDecoration ksaInputDecoration({Widget? suffixIcon}) {
  return InputDecoration(
    counterText: '',
    isDense: true,
    filled: false,
    contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 15),
    enabledBorder: _ksaRestingBorder,
    focusedBorder: _ksaFocusedBorder,
    disabledBorder: _ksaRestingBorder,
    suffixIcon: suffixIcon,
  );
}

final ButtonStyle ksaGoldButtonStyle = FilledButton.styleFrom(
  backgroundColor: SimfTokens.accent,
  disabledBackgroundColor: SimfTokens.accent.withValues(alpha: 0.5),
  minimumSize: const Size.fromHeight(48),
  shape: const RoundedRectangleBorder(
    borderRadius: KsaAuthScaffold._radius4,
  ),
);

/// The KSA card's gold submit button with the busy spinner — shared by the
/// forgot/reset screens so the spinner/typography stays in one place.
class KsaSubmitButton extends StatelessWidget {
  const KsaSubmitButton({
    required this.label,
    required this.busy,
    required this.onPressed,
    super.key,
  });

  final String label;
  final bool busy;

  /// Null disables the button (the busy spinner still shows when [busy]).
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: onPressed,
      style: ksaGoldButtonStyle,
      child: busy
          ? const SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Colors.white,
              ),
            )
          : Text(
              label,
              style: const TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w700,
                color: Colors.white,
              ),
            ),
    );
  }
}

/// A small field label aligned to the inline start (right under RTL).
class KsaFieldLabel extends StatelessWidget {
  const KsaFieldLabel({required this.text, super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Text(
        text,
        style: const TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w500,
          color: SimfTokens.greyText,
        ),
      ),
    );
  }
}

/// D-374 — the KSA entry chrome as a shared scaffold: the navy surface with
/// the rotated sweep, the forced-LTR back-chevron + globe language-toggle
/// row (the D-363 pattern), the logo + forum-name header, and the beige
/// card holding [child]. First consumers: the rebuilt forgot/reset-password
/// screens; migrating the earlier auth screens onto it is the parked
/// chrome-extraction follow-up (D-370 simplify note).
class KsaAuthScaffold extends ConsumerWidget {
  const KsaAuthScaffold({
    required this.child,
    required this.onBack,
    this.busy = false,
    super.key,
  });

  /// The beige card's content.
  final Widget child;

  /// Back-chevron action (pop or a fallback route).
  final VoidCallback onBack;

  /// Disables the chrome controls while a request runs.
  final bool busy;

  static const BorderRadius _radius4 =
      BorderRadius.all(Radius.circular(SimfTokens.radiusSmall));

  void _toggleLanguage(WidgetRef ref) {
    final isArabic = ref.read(localeControllerProvider).languageCode == 'ar';
    unawaited(
      ref
          .read(localeControllerProvider.notifier)
          .setLanguage(isArabic ? 'en' : 'ar'),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          // Decorative diagonal sweep (the login frame's 28.28° rectangle).
          Positioned(
            top: -156,
            left: 60,
            child: Transform.rotate(
              angle: 0.4936,
              child: Container(
                width: 313,
                height: 323,
                decoration: BoxDecoration(
                  color: SimfTokens.surfaceTint,
                  borderRadius: BorderRadius.circular(40),
                ),
              ),
            ),
          ),
          SafeArea(
            child: Stack(
              children: <Widget>[
                Padding(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                  child: Row(
                    textDirection: TextDirection.ltr,
                    children: <Widget>[
                      IconButton(
                        onPressed: busy ? null : onBack,
                        icon: const Icon(
                          Icons.arrow_back_ios_new,
                          color: Colors.white,
                          size: 20,
                          textDirection: TextDirection.ltr,
                        ),
                      ),
                      const Spacer(),
                      SizedBox(
                        width: 40,
                        height: 40,
                        child: IconButton(
                          tooltip: l10n.languageToggleLabel,
                          onPressed: busy ? null : () => _toggleLanguage(ref),
                          style: IconButton.styleFrom(
                            backgroundColor: SimfTokens.navyDeep,
                            shape: const RoundedRectangleBorder(
                              borderRadius: _radius4,
                            ),
                          ),
                          icon: const Icon(
                            Icons.language,
                            color: SimfTokens.accent,
                            size: 24,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                Center(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 56,
                    ),
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 400),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: <Widget>[
                          Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: <Widget>[
                              const SimfLogo(size: 44),
                              const SizedBox(width: 16),
                              Flexible(
                                child: Text(
                                  l10n.signInForumTitle,
                                  style: const TextStyle(
                                    fontSize: 24,
                                    fontWeight: FontWeight.w500,
                                    color: Colors.white,
                                  ),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 40),
                          Container(
                            padding: const EdgeInsets.all(24),
                            decoration: const BoxDecoration(
                              color: SimfTokens.cardBeige,
                              borderRadius: _radius4,
                            ),
                            child: child,
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
