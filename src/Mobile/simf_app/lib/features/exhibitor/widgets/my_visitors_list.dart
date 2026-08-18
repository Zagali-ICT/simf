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

/// The captured-lead rows, newest first, behind the BUG-025 note.
class MyVisitorsList extends ConsumerWidget {
  const MyVisitorsList({required this.visitors, super.key});

  final List<ExhibitorVisitor> visitors;

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
    final isArabic = l10n.isArabic;
    return SimfPullToRefresh(
      onRefresh: () => refreshAsync(ref, myVisitorsProvider.future),
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
