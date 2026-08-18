import 'dart:async';

import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/validation/plate_validation.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet_launcher.dart';
import 'package:simf_app/features/account/widgets/plate_number_field.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// The رقم اللوحة entry state: three letter picks, the digits field, and the
/// assembled plate code the profile payload carries.
///
/// It lives in one object because the four pieces are never useful apart — the
/// assembled [value] is derived from the other three and has to be re-derived
/// on every change. The screen creates one of these and disposes it; the field
/// widget reads and mutates it, then reports the change.
class SignUpVisitorPlateState {
  final TextEditingController digits = TextEditingController();

  String? letter1;
  String? letter2;
  String? letter3;

  /// D-471 fix — a plate is valid in either order (letters-then-digits or
  /// digits-then-letters) and the canonical code PRESERVES that order. A
  /// digits-first stored plate is remembered so prefill → re-sync does not
  /// silently reorder it (e.g. "1234ABJ" must not be rewritten to "ABJ1234").
  bool digitsFirst = false;

  String _value = '';

  /// The assembled plate code submitted with the profile. Empty when nothing is
  /// picked — the plate is optional.
  String get value => _value;

  void setLetter(int position, String? code) {
    switch (position) {
      case 1:
        letter1 = code;
      case 2:
        letter2 = code;
      default:
        letter3 = code;
    }
    sync();
  }

  /// Re-assembles [value] from the picks + digits, preserving the stored order
  /// ([digitsFirst]). Letters-then-digits is the default for fresh entry.
  void sync() {
    _value = assemblePlate(
      letter1: letter1,
      letter2: letter2,
      letter3: letter3,
      digits: digits.text,
      digitsFirst: digitsFirst,
    );
  }

  /// Splits a stored plate code into the three letter picks + the digits field,
  /// then refreshes [value]. The stored value is first normalised to the
  /// canonical Latin code (so an Arabic-script or pre-D-459 plate still
  /// parses); a value the 17-letter pickers cannot represent is kept verbatim
  /// so an unrelated profile edit never silently erases it (D-468 review).
  void setFromCode(String? code) {
    final parts = parsePlate(code);
    letter1 = parts.letter1;
    letter2 = parts.letter2;
    letter3 = parts.letter3;
    digits.text = parts.digits;
    digitsFirst = parts.digitsFirst;
    final override = parts.rawOverride;
    if (override != null) {
      _value = override;
      return;
    }
    sync();
  }

  void dispose() {
    digits.dispose();
  }
}

/// رقم اللوحة on the sign-up profile step, with its letter pickers attached.
///
/// The three pickers differ only by position, so the sheet is opened here and
/// the pick is routed into [state] by position. The digits validator runs over
/// the ASSEMBLED plate, not the digits alone: "1234" with no letters is not a
/// plate, and only the assembled value can say so.
class SignUpVisitorPlateField extends StatelessWidget {
  const SignUpVisitorPlateField({
    required this.l10n,
    required this.state,
    required this.onChanged,
    super.key,
  });

  final AppL10n l10n;
  final SignUpVisitorPlateState state;

  /// Fired after [state] has been updated, so the screen can rebuild.
  final VoidCallback onChanged;

  @override
  Widget build(BuildContext context) {
    return PlateNumberField(
      l10n: l10n,
      letter1: state.letter1,
      letter2: state.letter2,
      letter3: state.letter3,
      digits: state.digits,
      onPickLetter: (position) => unawaited(_pickLetter(context, position)),
      onDigitsChanged: () {
        state.sync();
        onChanged();
      },
      validateDigits: (_) => validatePlate(state.value, l10n),
    );
  }

  /// Opens the shared searchable sheet over the 17 official plate letters
  /// (shown "Arabic · Latin") and stores the picked Latin code, then
  /// re-assembles the plate.
  Future<void> _pickLetter(BuildContext context, int position) async {
    final pickedCode = await showLookupSearchSheet(
      context: context,
      options: <PickerOption>[
        for (final SaudiPlateLetter letter in saudiPlateLetters)
          PickerOption(
            value: letter.code,
            label: '${letter.arabic} · ${letter.english}',
            search: '${letter.arabic} ${letter.english} ${letter.code}',
          ),
      ],
      searchHint: l10n.plateLetterHint,
      searchFieldKey: ValueKey<String>('plateLetterSearch$position'),
    );
    if (pickedCode == null || !context.mounted) {
      return;
    }
    state.setLetter(position, pickedCode);
    onChanged();
  }
}
