import 'package:flutter/foundation.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// The visitor-profile fields BOTH registration surfaces collect, and the
/// lookup lists they choose from.
///
/// `sign_up_visitor_screen` and `staff/register_visitor_screen` each declared
/// these separately: 17 identically-named fields across two 1300-line screens,
/// with 13 of them serialised into byte-identical payload entries. The two
/// copies have already drifted once (the walk-in desk grew a per-field
/// server-error echo), so one definition is the point of this class.
///
/// **Text fields stay as `TextEditingController`s on the screens.** A
/// controller already owns its own value and notifies its own listeners, and it
/// must be disposed by whoever creates it. Moving those here would take on
/// disposal ownership and gain nothing, so this class holds only the values
/// that are plain state: the picks, the lookups and the submit flag.
class VisitorProfileFormState extends ChangeNotifier {
  // ── Lookups, loaded once per screen open ────────────────────────────────
  List<CountryItem> _countries = const <CountryItem>[];
  List<ProfileTypeItem> _profileTypes = const <ProfileTypeItem>[];

  List<CountryItem> get countries => _countries;
  List<ProfileTypeItem> get profileTypes => _profileTypes;

  /// Both screens load their lookups concurrently and assign them together, so
  /// they land in ONE notification rather than one per list.
  void setLookups({
    List<CountryItem>? countries,
    List<ProfileTypeItem>? profileTypes,
  }) {
    _countries = countries ?? _countries;
    _profileTypes = profileTypes ?? _profileTypes;
    notifyListeners();
  }

  // ── The user's picks ────────────────────────────────────────────────────
  String? _nationalityCode;
  String? _profileTypeId;
  String? _organisationId;
  AppGender _gender = AppGender.unspecified;
  VisitorDocType _docType = VisitorDocType.iqama;

  String? get nationalityCode => _nationalityCode;
  String? get profileTypeId => _profileTypeId;
  String? get organisationId => _organisationId;
  AppGender get gender => _gender;
  VisitorDocType get docType => _docType;

  /// Saudi-ness derives from the nationality pick rather than its own switch
  /// (D-373), and it decides which document and mobile fields apply.
  bool get isSaudi => _nationalityCode == 'SA';

  set nationalityCode(String? value) {
    if (_nationalityCode == value) {
      return;
    }
    _nationalityCode = value;
    notifyListeners();
  }

  set profileTypeId(String? value) {
    if (_profileTypeId == value) {
      return;
    }
    _profileTypeId = value;
    notifyListeners();
  }

  set organisationId(String? value) {
    if (_organisationId == value) {
      return;
    }
    _organisationId = value;
    notifyListeners();
  }

  set gender(AppGender value) {
    if (_gender == value) {
      return;
    }
    _gender = value;
    notifyListeners();
  }

  set docType(VisitorDocType value) {
    if (_docType == value) {
      return;
    }
    _docType = value;
    notifyListeners();
  }

  // ── Submit gate ─────────────────────────────────────────────────────────
  bool _triedSubmit = false;

  /// True once submit has been pressed at least once.
  ///
  /// Both screens use it the same way: a required field shows its error only
  /// AFTER a submit attempt, so an untouched form is not covered in red. The
  /// walk-in desk also resets it when the form is cleared for the next visitor.
  bool get triedSubmit => _triedSubmit;

  set triedSubmit(bool value) {
    if (_triedSubmit == value) {
      return;
    }
    _triedSubmit = value;
    notifyListeners();
  }

  /// Clears the picks and the submit flag for a fresh entry, keeping the
  /// loaded lookups: the walk-in desk registers visitor after visitor and
  /// re-fetching the country list each time would be wasteful.
  void resetForNextEntry() {
    _nationalityCode = null;
    _profileTypeId = null;
    _organisationId = null;
    _gender = AppGender.unspecified;
    _docType = VisitorDocType.iqama;
    _triedSubmit = false;
    notifyListeners();
  }
}
