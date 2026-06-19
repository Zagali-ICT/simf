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
/// **Public.** Pixel-parity to the restructured KSA Figma frame `1116:16448`:
/// the navy [KsaPage] shell, the anchor-mark header, the **الرسالة** (mission)
/// and **الرؤية** (vision) cards, the **تفاصيل الملتقى** details card
/// (year / date / location) and the **المحاور الرئيسية** themes card with the
/// four fixed forum themes. The vision paragraph is hydrated from the CMS
/// (`GET /app/content/{key}`, key `about`, D-173) when present and falls back to
/// the static bilingual copy otherwise; the mission line, the details and the
/// themes are the forum's fixed framing (static — no structured CMS block).
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

  /// Best-effort hydrate of the vision paragraph from the CMS. Any failure (incl.
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
    final visionBody = (block != null && block.hasBody)
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
          // Anchor-mark header (frame 1116:16448).
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              const Icon(Icons.anchor, color: SimfTokens.accent, size: 22),
              const SizedBox(width: SimfTokens.space2),
              Flexible(
                child: Text(
                  l10n.aboutForumName,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: SimfTokens.accent,
                    fontSize: SimfTokens.textLg,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space5),
          _AboutCard(title: l10n.aboutMissionTitle, body: l10n.aboutHeroHeading),
          const SizedBox(height: SimfTokens.space4),
          _AboutCard(title: l10n.aboutVisionTitle, body: visionBody),
          const SizedBox(height: SimfTokens.space4),
          _DetailsCard(
            title: l10n.aboutDetailsTitle,
            rows: <(String, String)>[
              (l10n.aboutDetailYearLabel, l10n.aboutDetailYearValue),
              (l10n.aboutDetailDateLabel, l10n.aboutDetailDateValue),
              (l10n.aboutDetailLocationLabel, l10n.aboutDetailLocationValue),
            ],
          ),
          const SizedBox(height: SimfTokens.space4),
          _ThemesCard(title: l10n.aboutThemesTitle, themes: themes),
        ],
      ),
    );
  }
}

/// A titled navy-deep text card (الرسالة / الرؤية): the white heading over the
/// beige body paragraph.
class _AboutCard extends StatelessWidget {
  const _AboutCard({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _CardHeading(title),
          const SizedBox(height: SimfTokens.space2),
          Text(
            body,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              height: 1.6,
            ),
          ),
        ],
      ),
    );
  }
}

/// The تفاصيل الملتقى card: the heading over "label : value" rows.
class _DetailsCard extends StatelessWidget {
  const _DetailsCard({required this.title, required this.rows});

  final String title;
  final List<(String, String)> rows;

  @override
  Widget build(BuildContext context) {
    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _CardHeading(title),
          const SizedBox(height: SimfTokens.space3),
          for (final (index, (label, value)) in rows.indexed) ...<Widget>[
            if (index > 0) const SizedBox(height: SimfTokens.space2),
            Row(
              children: <Widget>[
                Text(
                  '$label :',
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textSm,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: Text(
                    value,
                    textDirection: TextDirection.ltr,
                    textAlign: TextAlign.start,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// The المحاور الرئيسية card: the heading over the numbered theme entries.
class _ThemesCard extends StatelessWidget {
  const _ThemesCard({required this.title, required this.themes});

  final String title;
  final List<(String, String, String)> themes;

  @override
  Widget build(BuildContext context) {
    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _CardHeading(title),
          const SizedBox(height: SimfTokens.space3),
          for (final (index, (number, themeTitle, body)) in themes.indexed)
            ...<Widget>[
            if (index > 0) const SizedBox(height: SimfTokens.space4),
            Row(
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
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        themeTitle,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: SimfTokens.textMd,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        body,
                        style: const TextStyle(
                          color: SimfTokens.beigeBorder,
                          fontSize: SimfTokens.textSm,
                          height: 1.5,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// The shared navy-deep card chrome for the About sections.
class _Card extends StatelessWidget {
  const _Card({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: child,
    );
  }
}

class _CardHeading extends StatelessWidget {
  const _CardHeading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      textAlign: TextAlign.start,
      style: const TextStyle(
        color: Colors.white,
        fontSize: SimfTokens.textMd,
        fontWeight: FontWeight.w700,
      ),
    );
  }
}
