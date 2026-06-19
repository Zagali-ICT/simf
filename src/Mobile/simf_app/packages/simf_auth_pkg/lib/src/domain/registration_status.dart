/// The registration state a user can be in after sign-up (SIMF-API-001
/// §12 + SIMF-SRS-001 once gate D1 confirms the field set).
///
/// Drives the routing decision after sign-in: `Pending` routes to the
/// Registration Status screen, `Approved` routes to Home, `Rejected`
/// routes to a rejection screen.
enum RegistrationStatus {
  pending('Pending'),
  approved('Approved'),
  rejected('Rejected');

  const RegistrationStatus(this.wireName);
  final String wireName;

  static RegistrationStatus fromJson(Object? value) {
    if (value is String) {
      for (final s in RegistrationStatus.values) {
        if (s.wireName == value) {
          return s;
        }
      }
    }
    return RegistrationStatus.pending;
  }

  String toJson() => wireName;
}
