import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../content/data/content_models.dart';
import '../content/data/content_repository.dart';

/// Page 037 — عن الملتقى · About the forum (#37, `/about`, Guest+).
///
/// **Public.** Pixel-parity to KSA Figma frame `1082:15307`: the navy [KsaPage]
/// shell, an intro card (the `SIMF · 2026` kicker, the gold heading and the
/// forum paragraph) and the "المحاور الرئيسية" list of the four fixed forum
/// themes. The paragraph is hydrated from the CMS content layer
/// (`GET /app/content/{key}`, key `about`, D-173) when present and falls back to
/// the static bilingual copy otherwise; the heading and the four themes are
/// static (the forum's fixed framing — no structured CMS block exists for them).
class AboutScreen extends ConsumerStatefulWidget {
  const AboutScreen({super.key});

  @override
  ConsumerState<AboutScreen> createState() => _AboutScreenState();
}

class _AboutScreenState extends ConsumerState<AboutScreen> {
  ContentBlock? _block;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  /// Best-effort hydrate of the intro paragraph from the CMS. Any failure (incl.
  /// a 404 = key not seeded) leaves [_block] null and the screen renders the
  /// static fallback paragraph — the page always shows the forum content.
  Future<void> _load() async {
    try {
      final block =
          await ref.read(contentRepositoryProvider).getContentBlock('about');
      if (!mounted) {
        return;
      }
      setState(() => _block = block);
    } on ApiFailure {
      // Static fallback already covers this — nothing to surface.
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final block = _block;
    final paragraph = (block != null && block.hasBody)
        ? block.localizedBody(l10n.isArabic)
        : l10n.aboutHeroBody;

    final themes = <(String, String, String)>[
      ('01', l10n.aboutTheme1Title, l10n.aboutTheme1Body),
      ('02', l10n.aboutTheme2Title, l10n.aboutTheme2Body),
      ('03', l10n.aboutTheme3Title, l10n.aboutTheme3Body),
      ('04', l10n.aboutTheme4Title, l10n.aboutTheme4Body),
    ];

    return KsaPage(
      title: l10n.aboutTitle,
      onBack: () => ksaBackOrHome(context),
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          _AboutHeroCard(heading: l10n.aboutHeroHeading, body: paragraph),
          const SizedBox(height: SimfTokens.space5),
          Text(
            l10n.aboutThemesTitle,
            style: const TextStyle(
              fontSize: SimfTokens.textLg,
              fontWeight: FontWeight.w500,
              color: Colors.white,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          for (final (number, title, sub) in themes) ...<Widget>[
            _ThemeCard(number: number, title: title, subtitle: sub),
            const SizedBox(height: SimfTokens.space4),
          ],
        ],
      ),
    );
  }
}

/// The intro card (frame `1082:15566`): a borderless navy-deep card, radius 8,
/// with the gold `SIMF · 2026` kicker, the white SemiBold heading and the
/// beige forum paragraph.
class _AboutHeroCard extends StatelessWidget {
  const _AboutHeroCard({required this.heading, required this.body});

  final String heading;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const Text(
            'SIMF · 2026',
            style: TextStyle(
              color: SimfTokens.accent,
              fontSize: SimfTokens.textSm,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          Text(
            heading,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            body,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              height: 18 / 12,
            ),
          ),
        ],
      ),
    );
  }
}

/// One "المحاور الرئيسية" theme card (frames `1082:15578`…`15620`): the standard
/// navy-deep + beige-hairline [KsaCard], the gold number at the inline end and
/// the white bold title over the beige subtitle.
class _ThemeCard extends StatelessWidget {
  const _ThemeCard({
    required this.number,
    required this.title,
    required this.subtitle,
  });

  final String number;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              number,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontSize: SimfTokens.textLg,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    title,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: SimfTokens.textMd,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    subtitle,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                    ),
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
