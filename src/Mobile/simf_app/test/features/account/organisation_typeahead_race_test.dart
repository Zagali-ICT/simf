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

/// Pins the generation guard in `_run`: the debounce cancels only the pending
/// TIMER, so a stale response would repaint the list — and organisation is
/// required (D-221), so that submits the wrong employer.
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
