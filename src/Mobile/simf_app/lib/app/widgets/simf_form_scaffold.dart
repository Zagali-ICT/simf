import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_language_toggle.dart';
import 'package:simf_app/app/widgets/simf_logo.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

/// The shared navy-sweep scaffold for every account/entry form (login, sign-up,
/// profile, forgot/reset/badge): the decorative sweep, the forced-LTR
/// back-chevron + the language toggle pill, the logo + forum-name header, then
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
            // The glyph carries no text, so without this a screen reader
            // announced a bare "button" on every screen using this shared
            // header — all the auth / sign-up / profile forms, ~18 of them.
            // Same defect and same fix as SimfCircledBackButton (DEF-SWEEP-003,
            // e26eb507); this header was simply never swept, because the sweep
            // reached seven screens and none of them used it. The localized
            // Material string keeps it bilingual with no new l10n key.
            tooltip: MaterialLocalizations.of(context).backButtonTooltip,
            icon: const Icon(
              Icons.arrow_back_ios_new,
              color: SimfTokens.surface,
              size: 20,
              textDirection: TextDirection.ltr,
            ),
          ),
          const Spacer(),
          // The EN/عر pill (Figma 1967:3661) — replaces the old gold globe so
          // every auth/profile form matches sign-in/sign-up (D-674).
          SimfLanguageToggle(
            onPressed: () => _toggleLanguage(ref),
            busy: busy,
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
              color: SimfTokens.surface,
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
          const SizedBox(height: SimfTokens.space6),
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
              // Form content cap (CLAUDE.md §13.7): 560 fills the tablet width
              // the owner runs on; at phone width the body is narrower than this
              // anyway, so the phone goldens are unaffected.
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: SimfTokens.simfFormScaffoldMaxWidth),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    header,
                    const SizedBox(height: SimfTokens.space10),
                    // Non-pinned (auth) mode owns the beige card; pinned
                    // (profile) places [child] raw so it can use a Material
                    // card for its interactive-tile ink.
                    Container(
                      padding: const EdgeInsets.all(SimfTokens.space6),
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
