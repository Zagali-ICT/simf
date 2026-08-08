import '../../../app/localization/app_l10n.dart';
import '../../../core/validation/email_validation.dart';
import '../../../core/validation/required_validation.dart';

/// Field validators for the sign-in form.
///
/// Pure functions of their input, so they read without the screen around them
/// and are testable without pumping the form. Separate from
/// `visitor_profile_validators.dart` on purpose: signing IN and registering a
/// profile answer different questions about the same field.
String? validateSignInEmail(String? value, AppL10n l10n) {
  final email = value?.trim() ?? '';
  if (isBlank(email)) {
    return l10n.requiredField;
  }
  return isValidEmail(email) ? null : l10n.invalidEmail;
}

/// Sign-in only requires a NON-EMPTY password. It deliberately does not apply
/// the sign-up password policy: an account created before a policy change must
/// still be able to sign in, and the server is what authenticates the value.
///
/// Tightening this to match the sign-up rules would lock out existing users, so
/// the difference is load-bearing rather than an oversight.
String? validateSignInPassword(String? value, AppL10n l10n) =>
    isBlank(value) ? l10n.requiredField : null;
