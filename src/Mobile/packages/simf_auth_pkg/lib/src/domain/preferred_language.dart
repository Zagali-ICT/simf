/// The two languages the app supports (SIMF-MAA-001 §10).
enum PreferredLanguage {
  arabic('ar'),
  english('en');

  const PreferredLanguage(this.code);

  /// The IETF-language-tag short form, used in the `Accept-Language`
  /// header and on the wire.
  final String code;

  static PreferredLanguage fromJson(Object? value) {
    if (value is String) {
      for (final l in PreferredLanguage.values) {
        if (l.code == value) {
          return l;
        }
      }
    }
    // Arabic is the primary / default language (SIMF-MAA-001 §10).
    return PreferredLanguage.arabic;
  }

  String toJson() => code;
}
