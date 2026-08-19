import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/about/widgets/about_cards.dart';
import 'package:simf_app/features/about/widgets/about_header.dart';
import 'package:simf_app/features/content/data/content_models.dart';

/// The scrolling body of About-the-forum (Figma frame `1116:16448`): the
/// anchor-mark header, the mission/vision cards, the details card, the optional
/// contact + version cards and the themes card.
class AboutForumBody extends ConsumerWidget {
  const AboutForumBody({required this.block, super.key});

  /// The CMS block behind the vision paragraph; null falls back to the bundled
  /// copy so the page always renders.
  final ContentBlock? block;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final isAr = l10n.isArabic;
    // D-495 — the edition-generic forum config; loaded at splash, persisted
    // locally, shared app-wide. Falls back to the bundled l10n copy until it's
    // available (first run / offline) so the page always renders.
    final profile = ref.watch(orgProfileProvider);

    final cmsBlock = block;
    final visionBody = (cmsBlock != null && cmsBlock.hasBody)
        ? cmsBlock.localizedBody(isArabic: isAr)
        : l10n.aboutHeroBody;

    final forumName =
        (profile != null && profile.nameFor(isArabic: isAr).isNotEmpty)
            ? profile.nameFor(isArabic: isAr)
            : l10n.aboutForumName;
    final forumTitle = profile != null ? profile.titleFor(isArabic: isAr) : '';
    final statusBadge = profile != null
        ? '${l10n.aboutStatus(profile.status)} · ${profile.currentYear}'
        : null;

    // Driven by the profile's about-items (the same vision/mission card design)
    // when present, else the bundled copy.
    final aboutCards = <Widget>[];
    if (profile != null && profile.aboutItems.isNotEmpty) {
      for (final item in profile.aboutItems) {
        if (aboutCards.isNotEmpty) {
          aboutCards.add(const SizedBox(height: SimfTokens.space4));
        }
        aboutCards.add(
          AboutTextCard(
            title: item.titleFor(isArabic: isAr),
            body: item.textFor(isArabic: isAr),
          ),
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

    return ListView(
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
    );
  }
}
