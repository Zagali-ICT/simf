/// Gender wire enum — mirrors `SIMF.Common.Enums.Gender` (Unspecified=0,
/// Male=1, Female=2). Sent as the integer; decoded tolerantly (unknown →
/// Unspecified) per the append-only wire rule (D-219).
enum AppGender {
  unspecified(0),
  male(1),
  female(2);

  const AppGender(this.value);
  final int value;

  static AppGender fromValue(int? value) {
    return AppGender.values.firstWhere(
      (g) => g.value == value,
      orElse: () => AppGender.unspecified,
    );
  }
}
