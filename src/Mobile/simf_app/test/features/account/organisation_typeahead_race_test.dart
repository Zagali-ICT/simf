import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_organisation_field.dart';

/// جهة العمل is a debounced type-ahead, and the debounce only cancels a pending
/// TIMER — it cannot cancel a request already in flight. So two searches can be
/// outstanding at once and the network decides which one lands last.
///
/// On the venue's congested WiFi that is routinely the OLDER one. Before
/// 2026-08-20 nothing checked, and the late response simply overwrote the list:
/// the visitor saw organisations matching text the box no longer contained.
/// Organisation is required (D-221), so the outcome is either the wrong
/// employer submitted or "no matches" shown for a query that has one.
///
/// The test drives the two responses out of order on purpose. It fails without
/// the generation guard in `_run`.
class _OrderedRepository implements ProfileRepository {
  final Map<String, Completer<List<OrganisationItem>>> pending =
      <String, Completer<List<OrganisationItem>>>{};

  @override
  Future<List<OrganisationItem>> searchOrganisations({
    String? search,
    int top = 20,
  }) {
    final completer = Completer<List<OrganisationItem>>();
    pending[search ?? ''] = completer;
    return completer.future;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

OrganisationItem _item(String name) =>
    OrganisationItem(id: name, nameAr: name, nameEn: name);

void main() {
  testWidgets('a slow earlier search does not overwrite the latest results',
      (tester) async {
    final repo = _OrderedRepository();

    await tester.pumpWidget(
      ProviderScope(
        retry: (count, error) => null,
        overrides: <Override>[
          profileRepositoryProvider.overrideWithValue(repo),
        ],
        child: MaterialApp(
          theme: SimfTheme.dark(),
          home: Scaffold(
            body: SignUpVisitorOrganisationField(
              l10n: const AppL10n(Locale('ar')),
              initialResults: const <OrganisationItem>[],
              selectedId: null,
              selectedLabel: null,
              showError: false,
              onSelected: (_) {},
              onCleared: () {},
            ),
          ),
        ),
      ),
    );

    final field = find.byType(TextField).first;

    // The user types 'min', waits past the debounce, then types 'ministry'.
    await tester.enterText(field, 'min');
    await tester.pump(const Duration(seconds: 1));
    await tester.enterText(field, 'ministry');
    await tester.pump(const Duration(seconds: 1));

    expect(repo.pending.keys, containsAll(<String>['min', 'ministry']));

    // The network delivers them out of order: the later query first.
    repo.pending['ministry']!.complete(<OrganisationItem>[_item('Ministry')]);
    await tester.pump();
    expect(find.text('Ministry'), findsOneWidget);

    // Now the stale 'min' response lands. It must be discarded.
    repo.pending['min']!.complete(<OrganisationItem>[_item('Minority')]);
    await tester.pump();

    expect(
      find.text('Minority'),
      findsNothing,
      reason: 'The response for a superseded query repainted the list. The box '
          'says "ministry" and the suggestions do not match it.',
    );
    expect(find.text('Ministry'), findsOneWidget);
  });
}
