import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/about/widgets/about_cards.dart';
import 'package:simf_app/features/about/widgets/about_header.dart';
import 'package:simf_app/features/content/data/content_models.dart';
import 'package:simf_app/features/content/data/content_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 037 — عن الملتقى · About the forum (#37, `/about`, Guest+).
///
/// **Public.** Pixel-parity to the restructured KSA Figma frame `1116:16448`:
/// the navy [SimfPageShell] shell, the anchor-mark header, the **الرسالة**
/// (mission) and **الرؤية** (vision) cards, the **تفاصيل الملتقى** details card
/// (year / date / location) and the **المحاور الرئيسية** themes card with the
/// four fixed forum themes. The vision paragraph is hydrated from the CMS (`GET
/// /app/content/{key}`, key `about`, D-173) when present and falls back to the
/// static bilingual copy otherwise; the mission line, the details and the
/// themes are the forum's fixed framing (static — no structured CMS block).
///
/// Route: `RouteNames.aboutForum`.
/// Data: [contentRepositoryProvider], [orgProfileProvider].
/// Perf: ListView builds every child up front — correct for a short static
///       page, a defect on a data feed.
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

  /// Best-effort hydrate of the vision paragraph from the CMS. Any failure
  /// (incl. a 404 = key not seeded) leaves [_block] null and the screen renders
  /// the static fallback paragraph — the page always shows the forum content.
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

  /// Owner rule: every data page pulls to refresh. The page renders the CMS
  /// block AND the edition config, so both are re-read. Each swallows its own
  /// failure (the static fallback covers it), so the gesture always completes.
  Future<void> _refresh() async {
    await Future.wait<void>(<Future<void>>[
      _load(),
      ref.read(orgProfileProvider.notifier).warm(),
    ]);
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
        ? block.localizedBody(isArabic: isAr)
        : l10n.aboutHeroBody;

    final forumName =
        (profile != null && profile.nameFor(isArabic: isAr).isNotEmpty)
            ? profile.nameFor(isArabic: isAr)
            : l10n.aboutForumName;
    final forumTitle = profile != null ? profile.titleFor(isArabic: isAr) : '';
    final statusBadge = profile != null
        ? '${l10n.aboutStatus(profile.status)} · ${profile.currentYear}'
        : null;

    // The "about" cards — driven by the profile's about-items (the same
    // vision/mission card design) when present, else the bundled copy.
    final aboutCards = <Widget>[];
    if (profile != null && profile.aboutItems.isNotEmpty) {
      for (final item in profile.aboutItems) {
        if (aboutCards.isNotEmpty) {
          aboutCards.add(const SizedBox(height: SimfTokens.space4));
        }
        aboutCards.add(
          AboutTextCard(
              title: item.titleFor(isArabic: isAr),
              body: item.textFor(isArabic: isAr),),
        );
      }
    } else {
      aboutCards
        ..add(
          AboutTextCard(
            title: l10n.aboutMissionTitle,
            body: l10n.aboutHeroHeading,
          ),
        )
        ..add(const SizedBox(height: SimfTokens.space4))
        ..add(
          AboutTextCard(title: l10n.aboutVisionTitle, body: visionBody),
        );
    }

    // The "details" list — driven by the profile's details when present.
    final detailRows = (profile != null && profile.details.isNotEmpty)
        ? profile.details
            .map((d) => (d.nameFor(isArabic: isAr), d.valueFor(isArabic: isAr)))
            .toList()
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
      body: SimfPullToRefresh(
        onRefresh: _refresh,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(SimfTokens.space4),
          children: <Widget>[
            AboutHeader(
              forumName: forumName,
              forumTitle: forumTitle,
              statusBadge: statusBadge,
            ),
            const SizedBox(height: SimfTokens.space5),
            ...aboutCards,
            const SizedBox(height: SimfTokens.space4),
            AboutDetailsCard(title: l10n.aboutDetailsTitle, rows: detailRows),
            // D-495 — contact + version cards (shown only when set).
            if (contactRows.isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              AboutDetailsCard(
                title: l10n.aboutContactTitle,
                rows: contactRows,
              ),
            ],
            if (profile?.version != null &&
                profile!.version!.isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              AboutDetailsCard(
                title: l10n.aboutVersionTitle,
                rows: <(String, String)>[
                  (l10n.aboutVersionLabel, profile.version!),
                ],
              ),
            ],
            const SizedBox(height: SimfTokens.space4),
            AboutThemesCard(title: l10n.aboutThemesTitle, themes: themes),
          ],
        ),
      ),
    );
  }
}
