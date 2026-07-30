import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_identity_cell.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/refresh.dart';
import 'data/meet_repository.dart';
import 'data/partner_directory_models.dart';

// `partnerDirectoryProvider` lives in `data/meet_repository.dart`; re-exported so
// the existing imports (the meet screen test) keep resolving off this screen.
export 'data/meet_repository.dart';

/// Page 035 — قابل أشخاص مثلك · Meet people (#35, `/meet`, approved account).
///
/// **Build #13 rework** — this was the AI "% match" recommender; it is now the
/// curated + opt-in **partner directory**: Sponsors, Speakers and Booth companies
/// plus opted-in "Other"-type members (Normal / VIP visitors excluded). Tapping a
/// speaker opens the speaker profile, a sponsor the sponsor detail, a booth its
/// exhibitor detail; an opted-in person shows their data on the card (no per-person
/// screen). The whole feature is gated by the CP switch — when off the backend
/// returns an empty list (and the Home entry point is hidden). Data:
/// `partnerDirectoryProvider` → `GET /app/networking/partner-directory`. Reuses the
/// speakers/sponsors list visual language via the shared [SimfIdentityCell] (no
/// bespoke Figma node; owner-approved reuse of the existing list chrome).
class MeetPeopleScreen extends ConsumerWidget {
  const MeetPeopleScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final directory = ref.watch(partnerDirectoryProvider);
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;

    Future<void> onRefresh() => refreshAsync(ref, partnerDirectoryProvider.future);

    return SimfPageShell(
      title: l10n.meetPeopleTitle,
      onBack: () => backOrHome(context),
      body: directory.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => SimfPullToRefresh(
          onRefresh: onRefresh,
          child: SimfPullableHost(
            child: SimfErrorState(
              message: l10n.meetPeopleError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(partnerDirectoryProvider),
            ),
          ),
        ),
        data: (entries) {
          final isArabic = l10n.isArabic;
          if (entries.isEmpty) {
            return SimfPullToRefresh(
              onRefresh: onRefresh,
              child: SimfPullableHost(
                child: SimfEmptyState(
                icon: Icons.people_outline,
                message: l10n.meetPeopleEmpty,
              ),
              ),
            );
          }
          return SimfPullToRefresh(
            onRefresh: onRefresh,
            child: ListView.separated(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(SimfTokens.space4),
              itemCount: entries.length,
              separatorBuilder: (_, __) =>
                  const SizedBox(height: SimfTokens.space4),
              itemBuilder: (context, index) {
                final entry = entries[index];
                return SimfIdentityCell(
                  title: entry.localizedName(isArabic),
                  subtitle: entry.localizedSubtitle(isArabic),
                  imageUrl: entry.logoUrl(baseUrl),
                  countryId: entry.countryId,
                  onTap: _onTapFor(context, entry),
                );
              },
            ),
          );
        },
      ),
    );
  }

  /// The per-kind tap target: speaker → speaker profile, sponsor → sponsor
  /// detail, booth → exhibitor detail. An opted-in person has no detail screen,
  /// so their row is non-tappable (null).
  VoidCallback? _onTapFor(BuildContext context, PartnerDirectoryEntry entry) {
    if (entry.isSpeaker) {
      return () => context.pushNamed(
            RouteNames.speakerProfile,
            pathParameters: <String, String>{RouteParams.speakerId: entry.id},
          );
    }
    if (entry.isSponsor) {
      return () => context.pushNamed(
            RouteNames.sponsorDetail,
            pathParameters: <String, String>{RouteParams.sponsorId: entry.id},
          );
    }
    if (entry.isBooth) {
      return () => context.pushNamed(
            RouteNames.exhibitorDetail,
            pathParameters: <String, String>{RouteParams.boothId: entry.id},
          );
    }
    return null;
  }
}
