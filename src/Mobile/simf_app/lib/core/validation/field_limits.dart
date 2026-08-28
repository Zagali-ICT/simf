/// Maximum input lengths for the app's form fields.
///
/// These are NOT design tokens. Each one mirrors a backend constraint, the
/// FluentValidation `MaximumLength(N)` on the request validator and the EF
/// `HasMaxLength(N)` on the column, and the three have to agree: a field that
/// accepts more than the column holds fails at the API with a 400 the user
/// cannot act on, and one that accepts less silently truncates. Putting them in
/// `SimfTokens` would file a backend contract under "design", where a palette
/// change would sit next to a validation rule.
///
/// They live beside the validators in `core/validation/` that enforce them, and
/// are shared rather than per-feature on purpose: the same column backs the
/// email field on sign-up, on forgot-password and on the staff walk-in desk, so
/// one limit must serve all three. Two features disagreeing about one column is
/// the defect this file exists to prevent.
///
/// **Changing a value here is a contract change.** Verify the backend first.
abstract final class FieldLimits {
  /// Account email. Backend: `MaximumLength(50)`.
  static const int email = 50;

  /// Password. Backend: `MaximumLength(128)`.
  static const int password = 128;

  /// Emailed one-time code (6 digits).
  static const int otpCode = 6;

  /// A person's full name. Backend: `MaximumLength(100)`.
  static const int fullName = 100;

  /// A profile name part, Arabic or English: `UserProfile.Name` /
  /// `NameArabic`, nvarchar(50) in EF and `MaximumLength(50)` in both
  /// UpsertUserProfileRequestValidator and
  /// AdminWalkInRegistrationRequestValidator.
  ///
  /// DEF-STF-003: the walk-in desk used to accept 100 here, so a long name
  /// round-tripped into a 400 with nothing highlighted. It is the same column
  /// on both registration surfaces, so it is one constant.
  ///
  /// It happens to equal [email], which is why sign-up was reading THAT name.
  /// A shared value is not a shared meaning: if the email column ever widens,
  /// a name field must not silently widen with it.
  static const int profileName = 50;

  /// Place of birth. Backend: `MaximumLength(128)`.
  static const int placeOfBirth = 128;

  /// Saudi national ID: exactly 10 digits, so the field caps at its own length.
  static const int nationalId = 10;

  /// Phone number. Covers Saudi `05XXXXXXXX` / `+9665XXXXXXXX` /
  /// `009665XXXXXXXX` and international `+[1-9]{7,14}` / `00[1-9]{7,14}`,
  /// whose longest accepted form is 17 characters.
  static const int phone = 17;

  /// The numeric block of a vehicle plate (up to 4 digits).
  static const int plateDigits = 4;

  /// Free-text note saved against a scanned contact.
  static const int contactNote = 280;

  /// Audience question to a session. Backend: `MaximumLength(500)`.
  static const int sessionQuestion = 500;

  /// Subject/message on a meeting request. Backend: `MaximumLength(1000)`.
  static const int meetingRequestMessage = 1000;

  /// Free-text feedback comment. Backend: `MaximumLength(2000)`.
  static const int feedbackComment = 2000;

  /// Employer typed in when the curated list has no match (D-944). Matches
  /// `UserProfile.OrganisationOther` — and `Organisation.NameArabic`'s own
  /// ceiling, so a name later promoted into the lookup is not truncated.
  static const int organisationOther = 150;
}
