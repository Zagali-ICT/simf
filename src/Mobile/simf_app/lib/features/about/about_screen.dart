import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/organization_profile/organization_profile.dart';
import '../content/data/content_models.dart';
import '../content/data/content_repository.dart';

/// Page 037 — عن الملتقى · About the forum (#37, `/about`, Guest+).
///
/// **Public.** Pixel-parity to the restructured KSA Figma frame `1116:16448`:
/// the navy [SimfPageShell] shell, the anchor-mark header, the **الرسالة** (mission)
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
    final isAr = l10n.isArabic;
    // D-495 — the edition-generic forum config; loaded at splash, persisted
    // locally, shared app-wide. Falls back to the bundled l10n copy until it's
    // available (first run / offline) so the page always renders.
    final profile = ref.watch(orgProfileProvider);

    final block = _block;
    final visionBody = (block != null && block.hasBody)
        ? block.localizedBody(isAr)
        : l10n.aboutHeroBody;

    final forumName = (profile != null && profile.nameFor(isAr).isNotEmpty)
        ? profile.nameFor(isAr)
        : l10n.aboutForumName;
    final forumTitle = profile != null ? profile.titleFor(isAr) : '';

    // The "about" cards — driven by the profile's about-items (the same
    // vision/mission card design) when present, else the bundled copy.
    final aboutCards = <Widget>[];
    if (profile != null && profile.aboutItems.isNotEmpty) {
      for (final item in profile.aboutItems) {
        if (aboutCards.isNotEmpty) {
          aboutCards.add(const SizedBox(height: SimfTokens.space4));
        }
        aboutCards.add(
          _AboutCard(
            title: item.titleFor(isAr),
            body: item.textFor(isAr),
          ),
        );
      }
    } else {
      aboutCards.add(
        _AboutCard(
          title: l10n.aboutMissionTitle,
          body: l10n.aboutHeroHeading,
        ),
      );
      aboutCards.add(const SizedBox(height: SimfTokens.space4));
      aboutCards.add(_AboutCard(title: l10n.aboutVisionTitle, body: visionBody));
    }

    // The "details" list — driven by the profile's details when present.
    final detailRows = (profile != null && profile.details.isNotEmpty)
        ? profile.details.map((d) => (d.nameFor(isAr), d.value)).toList()
        : <(String, String)>[
            (l10n.aboutDetailYearLabel, l10n.aboutDetailYearValue),
            (l10n.aboutDetailDateLabel, l10n.aboutDetailDateValue),
            (l10n.aboutDetailLocationLabel, l10n.aboutDetailLocationValue),
          ];

    // D-495 — contact rows (only the fields the admin actually set).
    final contactRows = <(String, String)>[
      if (profile?.contactPhone != null)
        (l10n.aboutContactPhone, profile!.contactPhone!),
      if (profile?.contactEmail != null)
        (l10n.aboutContactEmail, profile!.contactEmail!),
      if (profile?.contactWebsite != null)
        (l10n.aboutContactWebsite, profile!.contactWebsite!),
    ];

    final themes = <(String, String, String)>[
      ('01', l10n.aboutTheme1Title, l10n.aboutTheme1Body),
      ('02', l10n.aboutTheme2Title, l10n.aboutTheme2Body),
      ('03', l10n.aboutTheme3Title, l10n.aboutTheme3Body),
      ('04', l10n.aboutTheme4Title, l10n.aboutTheme4Body),
    ];

    return SimfPageShell(
      title: l10n.aboutTitle,
      onBack: () => backOrHome(context),
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          // Anchor-mark header (frame 1116:16448) — name, then title.
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              const Icon(Icons.anchor, color: SimfTokens.accent, size: 22),
              const SizedBox(width: SimfTokens.space2),
              Flexible(
                child: Text(
                  forumName,
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
          if (forumTitle.isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            Text(
              forumTitle,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
          // D-495 — the edition status badge (status · year).
          if (profile != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space3),
            Center(
              child: Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: SimfTokens.space3,
                  vertical: SimfTokens.space1,
                ),
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  borderRadius: BorderRadius.circular(SimfTokens.radius),
                ),
                child: Text(
                  '${l10n.aboutStatus(profile.status)} · ${profile.currentYear}',
                  style: const TextStyle(
                    color: SimfTokens.navyDeep,
                    fontSize: SimfTokens.textSm,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ),
          ],
          const SizedBox(height: SimfTokens.space5),
          ...aboutCards,
          const SizedBox(height: SimfTokens.space4),
          _DetailsCard(title: l10n.aboutDetailsTitle, rows: detailRows),
          // D-495 — contact + version cards (shown only when set).
          if (contactRows.isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            _DetailsCard(title: l10n.aboutContactTitle, rows: contactRows),
          ],
          if (profile?.version != null && profile!.version!.isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            _DetailsCard(
              title: l10n.aboutVersionTitle,
              rows: <(String, String)>[(l10n.aboutVersionLabel, profile.version!)],
            ),
          ],
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
