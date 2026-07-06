import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/organization_profile/organization_profile.dart';
import '../more/widgets/more_list.dart';
import 'widgets/about_cards.dart';

/// عن التطبيق · About the app (D-668, `/about-app`, public).
///
/// The app's own about page (distinct from [AboutScreen] "عن الملتقى", which is
/// about the forum): the app **version** + **release date** + **organizer** in
/// an [AboutDetailsCard], the **support / contact** details from the edition's
/// organisation profile (only the fields the admin set), and quick links to
/// Contact us + Terms. Reached from the end of the side drawer.
class AboutAppScreen extends ConsumerWidget {
  const AboutAppScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    // The edition's org profile (loaded at splash, persisted, null until then) —
    // reused so "support" shows the same contact the forum-about page does,
    // never a second hardcoded copy.
    final profile = ref.watch(orgProfileProvider);
    final contactRows = <(String, String)>[
      if (profile?.contactEmail != null)
        (l10n.aboutContactEmail, profile!.contactEmail!),
      if (profile?.contactPhone != null)
        (l10n.aboutContactPhone, profile!.contactPhone!),
      if (profile?.contactWebsite != null)
        (l10n.aboutContactWebsite, profile!.contactWebsite!),
    ];

    return SimfPageShell(
      title: l10n.aboutAppTitle,
      onBack: () => backOrHome(context),
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          AboutDetailsCard(
            title: l10n.aboutAppInfoTitle,
            rows: <(String, String)>[
              (l10n.aboutVersionLabel, l10n.moreVersion),
              (l10n.aboutAppReleaseDateLabel, l10n.aboutAppReleaseDate),
              (l10n.aboutAppOrganizerLabel, l10n.aboutAppOrganizerValue),
            ],
          ),
          if (contactRows.isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            AboutDetailsCard(title: l10n.aboutContactTitle, rows: contactRows),
          ],
          const SizedBox(height: SimfTokens.space4),
          MoreSection(
            title: l10n.aboutAppLinksTitle,
            rows: <Widget>[
              MoreRow(
                title: l10n.contactUsTitle,
                onTap: () => context.pushNamed(RouteNames.contactUs),
              ),
              MoreRow(
                title: l10n.moreTerms,
                onTap: () => context.pushNamed(RouteNames.terms),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
