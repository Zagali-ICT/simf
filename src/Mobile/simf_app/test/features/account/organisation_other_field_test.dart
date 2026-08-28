import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/widgets/organisation_typeahead_field.dart';

/// D-944 — the catch-all row's free-text box.
///
/// The organisations list is a curated government import and organisation is a
/// REQUIRED field (D-221), so before this a visitor whose employer was absent
/// hit "no matches" and could not finish registering at all. The server flags
/// one row `isOther`; choosing it reveals the box these tests cover.
void main() {
  Widget host({
    required bool isOther,
    required TextEditingController controller,
    bool showOtherError = false,
  }) =>
      MaterialApp(
        theme: SimfTheme.dark(),
        home: Scaffold(
          body: OrganisationTypeaheadField(
            l10n: const AppL10n(Locale('en')),
            controller: TextEditingController(),
            // A chosen organisation, which is the state that renders the
            // selected row plus (when it is the catch-all) the text box.
            selectedId: 'picked-id',
            selectedLabel: isOther ? 'Other' : 'Saudi Ports Authority',
            searching: false,
            searchFailed: false,
            results: const <OrganisationItem>[],
            showError: false,
            isOther: isOther,
            otherController: controller,
            showOtherError: showOtherError,
            onSearchChanged: (_) {},
            onRetry: () {},
            onSelected: (_) {},
            onCleared: () {},
          ),
        ),
      );

  testWidgets('an ordinary pick shows no free-text box', (tester) async {
    final controller = TextEditingController();
    addTearDown(controller.dispose);

    await tester.pumpWidget(host(isOther: false, controller: controller));
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey<String>('organisationOtherField')),
        findsNothing,);
  });

  testWidgets('choosing the catch-all reveals the box', (tester) async {
    final controller = TextEditingController();
    addTearDown(controller.dispose);

    await tester.pumpWidget(host(isOther: true, controller: controller));
    await tester.pumpAndSettle();

    final box = find.byKey(const ValueKey<String>('organisationOtherField'));
    expect(box, findsOneWidget);
    expect(find.text("Type your organisation's name"), findsOneWidget);

    await tester.enterText(box, 'Sudanese Maritime Authority');
    expect(controller.text, 'Sudanese Maritime Authority');
  });

  testWidgets('an empty box after a submit attempt shows its own error',
      (tester) async {
    // The server answers 400 for "Other" with nothing typed. Surfacing it here
    // turns a round trip into an inline message.
    final controller = TextEditingController();
    addTearDown(controller.dispose);

    await tester.pumpWidget(
      host(isOther: true, controller: controller, showOtherError: true),
    );
    await tester.pumpAndSettle();

    expect(find.text("Enter your organisation's name"), findsOneWidget);
  });

  testWidgets('the 150-character cap matches the column and the validator',
      (tester) async {
    // Organisation.NameArabic is nvarchar(150), so a name promoted into the
    // lookup later must fit. A shorter cap here would silently lose the tail of
    // exactly the long government-body names this field exists for.
    final controller = TextEditingController();
    addTearDown(controller.dispose);

    await tester.pumpWidget(host(isOther: true, controller: controller));
    await tester.pumpAndSettle();

    final field = tester.widget<TextField>(
      find.byKey(const ValueKey<String>('organisationOtherField')),
    );
    expect(field.maxLength, 150);
  });
}
