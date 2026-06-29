import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../localization/app_l10n.dart';
import '../localization/locale_controller.dart';
import '../theme/tokens.dart';
import 'simf_logo.dart';
import 'simf_page_shell.dart';

/// The shared navy-sweep scaffold for every account/entry form (login, sign-up,
/// profile, forgot/reset/badge): the decorative sweep, the forced-LTR
/// back-chevron + globe language toggle, the logo + forum-name header, then
/// [child]. One owner for the chrome the account screens used to each hand-roll.
///
/// [pinnedHeader] `false` (default) scrolls the header + [child] together,
/// vertically centred — right for the short auth cards. `true` keeps the header
/// fixed and gives [child] the rest of the height (the child owns its scroll) —
/// right for the long profile form. [sweep] overrides the decorative sweep
/// (e.g. the profile screen's different offset); [busy] disables the controls.
class SimfFormScaffold extends ConsumerWidget {
  const SimfFormScaffold({
    required this.child,
    required this.onBack,
    this.busy = false,
    this.pinnedHeader = false,
    this.sweep = const SimfSweepBackground(),
    super.key,
  });

  /// The content below the header (wrap form bodies in a [SimfFormCard]).
  final Widget child;

  /// Back-chevron action (pop or a fallback route).
  final VoidCallback onBack;

  /// Disables the chrome controls while a request runs.
  final bool busy;

  /// Fixed header + scrolling [child] (profile) vs centred-scroll-all (auth).
  final bool pinnedHeader;

  /// The decorative diagonal sweep behind the header.
  final Widget sweep;

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

    final topBar = Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
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
                  borderRadius: SimfTokens.borderRadiusSmall,
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
    );

    final header = Row(
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
    );

    final Widget content;
    if (pinnedHeader) {
      content = Column(
        children: <Widget>[
          topBar,
          header,
          const SizedBox(height: 24),
          Expanded(child: child),
        ],
      );
    } else {
      content = Stack(
        children: <Widget>[
          topBar,
          Center(
            child: SingleChildScrollView(
              padding:
                  const EdgeInsets.symmetric(horizontal: 16, vertical: 56),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 400),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    header,
                    const SizedBox(height: 40),
                    // Non-pinned (auth) mode owns the beige card; pinned
                    // (profile) places [child] raw so it can use a Material
                    // card for its interactive-tile ink.
                    Container(
                      padding: const EdgeInsets.all(24),
                      decoration: const BoxDecoration(
                        color: SimfTokens.cardBeige,
                        borderRadius: SimfTokens.borderRadiusSmall,
                      ),
                      child: child,
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      );
    }

    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: Stack(
        children: <Widget>[
          sweep,
          SafeArea(child: content),
        ],
      ),
    );
  }
}
