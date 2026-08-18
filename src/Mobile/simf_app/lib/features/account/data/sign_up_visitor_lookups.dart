import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// What the sign-up profile form (Page 007) opens with: the caller's existing
/// profile and the three lookups its pickers offer.
class SignUpVisitorInitialData {
  const SignUpVisitorInitialData({
    required this.profile,
    required this.countries,
    required this.profileTypes,
    required this.organisations,
  });

  final UserProfileResponse profile;
  final List<CountryItem> countries;
  final List<ProfileTypeItem> profileTypes;
  final List<OrganisationItem> organisations;
}

/// The four opening reads, fired CONCURRENTLY — the form is useless until all
/// of them land, and four round trips in series is what the user would feel.
///
/// Throws [ApiFailure] if any of them fails, which is what the screen's retry
/// branch is for.
Future<SignUpVisitorInitialData> loadSignUpVisitorData(
  ProfileRepository repository, {
  required bool isVisitor,
}) async {
  final results = await Future.wait(<Future<Object>>[
    repository.getMyProfile(),
    repository.getCountries(),
    repository.getProfileTypes(isVisitor: isVisitor),
    repository.searchOrganisations(),
  ]);
  return SignUpVisitorInitialData(
    profile: results[0] as UserProfileResponse,
    countries: results[1] as List<CountryItem>,
    profileTypes: results[2] as List<ProfileTypeItem>,
    organisations: results[3] as List<OrganisationItem>,
  );
}

/// Re-reads the ProfileType lookup for the other نوع التسجيل tab, or null when
/// the fetch failed.
///
/// D-375 — the caller turns that null into a visible inline retry. Pre-D-375 a
/// failure here silently hid the الفئة (category) field altogether, which is
/// the owner-reported "removed list", so an empty list and a failure must stay
/// distinguishable.
Future<List<ProfileTypeItem>?> fetchVisitorProfileTypes(
  ProfileRepository repository, {
  required bool isVisitor,
}) async {
  try {
    return await repository.getProfileTypes(isVisitor: isVisitor);
  } on ApiFailure {
    return null;
  }
}

/// نوع التسجيل: the Visitor / Other tab (D-332 — a client-only `?isVisitor=`
/// filter, never persisted) together with the state of the ProfileType lookup
/// it re-reads.
///
/// The three values only ever move together — switching the tab starts a fetch,
/// and the fetch ends in either a new list or a failure the picker must show —
/// so the transitions live here rather than as three flags a screen sets by
/// hand in four places. [VisitorProfileFormState.triedSubmit] is the same kind
/// of value in the same layer.
class SignUpVisitorTypeSelection {
  bool isVisitor = true;

  // D-375 — an API-fed picker always surfaces its fetch state: a spinner while
  // it is in flight, a visible retry when it failed. Never a silently missing
  // or empty control.
  bool loading = false;
  bool failed = false;

  /// Under the Visitor tab the picker is hidden, so the id is assigned rather
  /// than chosen.
  void lock(VisitorProfileFormState picks) {
    if (!isVisitor) {
      return;
    }
    picks.profileTypeId = lockedVisitorProfileTypeId(picks.profileTypes);
  }

  void beginFetch() {
    loading = true;
    failed = false;
  }

  /// Applies a finished fetch: a null [types] is the failure the retry hangs
  /// off, and it deliberately leaves the previous lookup alone.
  void endFetch(VisitorProfileFormState picks, List<ProfileTypeItem>? types) {
    loading = false;
    failed = types == null;
    if (types == null) {
      return;
    }
    picks.setLookups(profileTypes: types);
    lock(picks);
  }
}

/// C5 (D-371) — under the Visitor tab the profile type is locked to the single
/// seeded **"Normal" (عادي)** type: no picker is shown and the id is
/// auto-assigned (overriding any prefill — an admin-assigned tier still wins
/// server-side via the D-190 precedence). Falls back to the only row when the
/// lookup has exactly one; an empty lookup gives null (admin assigns).
String? lockedVisitorProfileTypeId(List<ProfileTypeItem> profileTypes) {
  for (final type in profileTypes) {
    if (type.name == 'Normal') {
      return type.id;
    }
  }
  return profileTypes.length == 1 ? profileTypes.first.id : null;
}

/// The country lookup as searchable options, matched on both names so an
/// English query still finds an Arabic-labelled row and back.
List<PickerOption> countryPickerOptions(
  List<CountryItem> countries, {
  required bool isArabic,
}) {
  return <PickerOption>[
    for (final c in countries)
      PickerOption(
        value: c.code,
        label: isArabic ? c.nameArabic : c.name,
        search: '${c.name} ${c.nameArabic}',
      ),
  ];
}
