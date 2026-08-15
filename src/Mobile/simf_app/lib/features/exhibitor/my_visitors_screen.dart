import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/contacts/widgets/contact_card.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_repository.dart';
import 'package:simf_app/features/exhibitor/widgets/captured_visitor_sheet.dart';
import 'package:simf_app/features/exhibitor/widgets/exhibitor_centered.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-426 — زوار جناحي / My Booth Visitors. The exhibitor's ("Other" profile
/// type) captured visitors: everyone they scanned at their booth, newest first,
/// each with the visitor's full card resolved live. Reached from the side
/// drawer (Other-only), the exhibitor home's tools row, and after a successful
/// scan. Approved + non-visitor only (a visitor-tier caller gets 403 → the
/// limited/forbidden surface).
///
/// BUG-025 — this is NOT "My Contacts" (`/contacts`, visitor-to-visitor card
/// sharing). The two lists stay separate pending an owner ruling, so the title
/// names the booth and a [SimfPageNote] states the difference in both
/// languages.
///
/// Route: `RouteNames.myVisitors`.
/// Data: [exhibitorRepositoryProvider], [myVisitorsProvider].
/// Perf: lazy — builds children on demand (ListView.separated).
/// The booth's captured leads (`GET /app/exhibitor/visitors`).
///
/// A THIRD shape, after the fold-to-null of `termsBlockProvider` and the plain
/// list of `savedContactsProvider`: the 403 stays an ERROR and the screen
/// branches on it inside `when`'s error callback. A 403 here means "your
/// account is not linked to a booth yet", which is a failure with its own copy
/// — not an empty result — so folding it into the data branch would be lying
/// about what happened.
final myVisitorsProvider = FutureProvider.autoDispose<List<ExhibitorVisitor>>(
  (ref) => ref.watch(exhibitorRepositoryProvider).listMyVisitors(),
);

class MyVisitorsScreen extends ConsumerWidget {
  const MyVisitorsScreen({super.key});
  Future<void> _refresh(WidgetRef ref) =>
      refreshAsync(ref, myVisitorsProvider.future);

  /// FR-EXH-002 — opens the lead's detail sheet (export vCard / remove). A
  /// confirmed removal pops `true`, so the list reloads and toasts.
  Future<void> _openDetail(
    BuildContext context,
    WidgetRef ref,
    ExhibitorVisitor visitor,
  ) async {
    // Captured before the sheet's async gap.
    final messenger = ScaffoldMessenger.of(context);
    final removedText = AppL10n.of(context).myVisitorsRemoved;
    final removed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => CapturedVisitorSheet(visitor: visitor),
    );
    if (removed ?? false) {
      messenger.showSnackBar(SnackBar(content: Text(removedText)));
      ref.invalidate(myVisitorsProvider);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.myVisitorsTitle,
      onBack: () => backOrHome(context),
      body: _buildBody(context, ref, l10n),
    );
  }

  Widget _buildBody(BuildContext context, WidgetRef ref, AppL10n l10n) {
    return ref.watch(myVisitorsProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) {
            // The 403 branch especially: an exhibitor whose booth link lands
            // after the first load would otherwise be stuck on it with no way
            // to re-check, which is why BOTH failures stay refreshable.
            final forbidden =
                error is ApiFailure && error.httpStatus == 403;
            return SimfRefreshableMessage(
              onRefresh: () => _refresh(ref),
              child: forbidden
                  ? ExhibitorCentered(text: l10n.scanVisitorForbidden)
                  : SimfErrorState(
                      message: l10n.scanVisitorError,
                      retryLabel: l10n.retryLabel,
                      onRetry: () => ref.invalidate(myVisitorsProvider),
                    ),
            );
          },
          data: (visitors) => visitors.isEmpty
              ? SimfRefreshableMessage(
                  onRefresh: () => _refresh(ref),
                  child: ExhibitorCentered(text: l10n.myVisitorsEmpty),
                )
              : _buildList(context, ref, l10n, visitors),
        );
  }

  Widget _buildList(
    BuildContext context,
    WidgetRef ref,
    AppL10n l10n,
    List<ExhibitorVisitor> visitors,
  ) {
    final isArabic = l10n.isArabic;
    return SimfPullToRefresh(
      onRefresh: () => _refresh(ref),
      child: ListView.separated(
        padding: const EdgeInsets.all(SimfTokens.space4),
        // +1 leading row: the BUG-025 "these are booth scans, not My Contacts"
        // note, scrolled with the list so it never steals viewport height.
        itemCount: visitors.length + 1,
        separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space3),
        itemBuilder: (context, index) {
          if (index == 0) {
            return SimfPageNote(text: l10n.myVisitorsNote);
          }
          final v = visitors[index - 1];
          final card = v.card;
          // FR-EXH-002 — a row now opens the detail sheet (export vCard /
          // remove), the same affordance My Contacts has had since D-286.
          return InkWell(
            onTap: () => unawaited(_openDetail(context, ref, v)),
            child: ContactCard(
              name: card.localizedName(isArabic: isArabic),
              available: card.available,
              jobTitle: card.localizedJobTitle(isArabic: isArabic),
              organisation: card.localizedOrganisation(isArabic: isArabic),
              country: card.localizedCountry(isArabic: isArabic),
              email: card.email,
              saudiMobile: card.saudiMobile,
              internationalMobile: card.internationalMobile,
            ),
          );
        },
      ),
    );
  }
}
