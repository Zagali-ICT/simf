import 'package:flutter/foundation.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';

/// The three lookups the walk-in form picks from, and the defaults they seed
/// the empty form with.
///
/// They travel together because they are loaded together and are useless apart:
/// the operator cannot finish a registration until every picker has its rows.
@immutable
class WalkInLookups {
  const WalkInLookups({
    required this.countries,
    required this.profileTypes,
    required this.organisations,
  });

  /// Before the first load lands, so the pickers render empty rather than the
  /// screen carrying a nullable through every read.
  static const WalkInLookups empty = WalkInLookups(
    countries: <CountryItem>[],
    profileTypes: <ProfileTypeItem>[],
    organisations: <OrganisationItem>[],
  );

  final List<CountryItem> countries;
  final List<ProfileTypeItem> profileTypes;
  final List<OrganisationItem> organisations;

  /// Saudi Arabia when the list carries it: the overwhelming majority of
  /// walk-ins at the desk are Saudi, and the operator can still change it.
  String? get defaultNationalityCode =>
      countries.any((c) => c.code == 'SA') ? 'SA' : null;

  /// 19g — the operator now PICKS the classification; this only seeds the
  /// field. The seeded "Normal" (عادي) audience tier is the sensible default
  /// (parity with the self-service sign-up's visitor lock, C5/D-371), falling
  /// back to the only row when the lookup has exactly one.
  String? get defaultProfileTypeId {
    final normal = profileTypes.where((t) => t.name == 'Normal').toList();
    if (normal.isNotEmpty) {
      return normal.first.id;
    }
    return profileTypes.length == 1 ? profileTypes.first.id : null;
  }
}

/// Loads all three in parallel. Throws `ApiFailure`, which the screen turns
/// into its retry surface — a half-loaded form is not worth showing.
Future<WalkInLookups> loadWalkInLookups(ProfileRepository repo) async {
  final results = await Future.wait(<Future<Object>>[
    repo.getCountries(),
    repo.getProfileTypes(isVisitor: true),
    repo.searchOrganisations(top: 200),
  ]);
  return WalkInLookups(
    countries: results[0] as List<CountryItem>,
    profileTypes: results[1] as List<ProfileTypeItem>,
    organisations: results[2] as List<OrganisationItem>,
  );
}
