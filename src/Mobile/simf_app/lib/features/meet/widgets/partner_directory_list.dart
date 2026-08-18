import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_identity_cell.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/meet/data/partner_directory_models.dart';

/// The partner-directory rows on the "قابل أشخاص مثلك" screen: one shared
/// [SimfIdentityCell] per entry, or the empty state when the CP switch is off
/// (the backend then returns no entries).
class PartnerDirectoryList extends StatelessWidget {
  const PartnerDirectoryList({
    required this.entries,
    required this.isArabic,
    required this.baseUrl,
    required this.emptyMessage,
    required this.onRefresh,
    super.key,
  });

  final List<PartnerDirectoryEntry> entries;
  final bool isArabic;
  final String baseUrl;
  final String emptyMessage;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    if (entries.isEmpty) {
      return SimfRefreshableMessage(
        onRefresh: onRefresh,
        child: SimfEmptyState(
          icon: Icons.people_outline,
          message: emptyMessage,
        ),
      );
    }
    return SimfPullToRefresh(
      onRefresh: onRefresh,
      child: ListView.separated(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space4),
        itemCount: entries.length,
        separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space4),
        itemBuilder: (context, index) {
          final entry = entries[index];
          return SimfIdentityCell(
            title: entry.localizedName(isArabic: isArabic),
            subtitle: entry.localizedSubtitle(isArabic: isArabic),
            imageUrl: entry.logoUrl(baseUrl),
            countryId: entry.countryId,
            onTap: _onTapFor(context, entry),
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
