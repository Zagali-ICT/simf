import 'package:flutter/widgets.dart';

/// Hand-rolled localisation lookup for the WS3 skeleton.
///
/// SIMF-MAA-001 §10 specifies `intl` + ARB files as the long-term path;
/// the .arb files in `l10n/` are the source of truth for translations.
/// To avoid coupling the skeleton's compile to the `flutter gen-l10n` step,
/// the strings the skeleton actually needs are mirrored here. When the
/// project moves to generated localisation, the call sites
/// (`AppL10n.of(context).xxx`) stay; only the implementation switches.
///
/// **Phase 3 will not add per-screen strings here**: the `mkp_*` screens
/// reference Mockup.html's Arabic copy directly, so they don't add to the
/// translation surface that needs to survive the designer swap.
class AppL10n {
  const AppL10n(this.locale);

  final Locale locale;

  static AppL10n of(BuildContext context) {
    return Localizations.of<AppL10n>(context, AppL10n) ??
        const AppL10n(Locale('ar'));
  }

  static const supportedLocales = <Locale>[Locale('ar'), Locale('en')];
  static const localizationsDelegates = <LocalizationsDelegate<Object>>[
    _AppL10nDelegate(),
  ];

  bool get isArabic => locale.languageCode == 'ar';

  String _t(String ar, String en) => isArabic ? ar : en;

  String get appName => _t('الملتقى البحري', 'SIMF');
  String get comingSoonTitle => _t('قريباً', 'Coming soon');
  String get comingSoonBody => _t(
        'هذه الشاشة قيد التطوير. سيتم استبدالها بنسخة UI/UX النهائية لاحقاً.',
        'This screen is under construction. It will be replaced by the final UI/UX shortly.',
      );
  String get backLabel => _t('رجوع', 'Back');
  String get continueLabel => _t('متابعة', 'Continue');
  String get cancelLabel => _t('إلغاء', 'Cancel');
  String get retryLabel => _t('إعادة المحاولة', 'Retry');
  String get loadingLabel => _t('جارٍ التحميل…', 'Loading…');
  String get errorTitle => _t('حدث خطأ', 'Something went wrong');
  String get networkErrorBody => _t(
        'تعذر الاتصال بالخادم. تحقق من الاتصال بالإنترنت وحاول مرة أخرى.',
        'Could not reach the server. Check your internet connection and try again.',
      );

  // Splash / store-update dialog (Page 001 — Logic L-2).
  String get updateRequiredTitle => _t('تحديث مطلوب', 'Update required');
  String get updateRequiredBody => _t(
        'يتوفر إصدار جديد من التطبيق ويجب تثبيته للمتابعة.',
        'A new version of the app is available and must be installed to continue.',
      );
  String get updateOptionalTitle => _t('يتوفر تحديث', 'Update available');
  String get updateOptionalBody => _t(
        'يتوفر إصدار جديد من التطبيق. ننصح بالتحديث للحصول على أحدث التحسينات.',
        'A new version of the app is available. We recommend updating for the latest improvements.',
      );
  String get updateNowLabel => _t('تحديث الآن', 'Update now');
  String get updateLaterLabel => _t('لاحقاً', 'Later');

  // Onboarding (Page 002). Interim slide copy standing in for the intro videos.
  String get onboardingSkip => _t('تخطي', 'Skip');
  String get onboardingNext => _t('التالي', 'Next');
  String get onboardingGetStarted => _t('ابدأ', 'Get started');
  String get onboardingTitle1 => _t(
        'مرحباً بك في تطبيق الملتقى',
        'Welcome to the SIMF app',
      );
  String get onboardingBody1 => _t(
        'دليلك المتكامل: الأجندة، المتحدثون، الخريطة التفاعلية، البطاقة الذكية، والبث المباشر — في تطبيق واحد.',
        'Your complete guide: the agenda, speakers, interactive map, smart badge and live broadcast — in one app.',
      );
  String get onboardingTitle2 => _t(
        'تابع الجلسات والمتحدثين',
        'Follow the sessions and speakers',
      );
  String get onboardingBody2 => _t(
        'تصفّح الأجندة، احجز مقعدك، واستكشف أجنحة المعرض والخريطة التفاعلية للمكان.',
        'Browse the agenda, reserve your seat, and explore the exhibition booths and the interactive venue map.',
      );
  String get onboardingTitle3 => _t(
        'بطاقتك الذكية وتواصلك',
        'Your smart badge and networking',
      );
  String get onboardingBody3 => _t(
        'بطاقة دخول QR، إشعارات فورية، وتواصل مع مشاركين يشاركونك الاهتمامات.',
        'A QR entry badge, instant notifications, and connect with attendees who share your interests.',
      );

  // Sign in (Page 003).
  String get signInTitle => _t('تسجيل الدخول', 'Sign in');
  String get emailLabel => _t('البريد الإلكتروني', 'Email');
  String get passwordLabel => _t('كلمة المرور', 'Password');
  String get signInButton => _t('دخول', 'Sign in');
  String get forgotPasswordLink => _t('نسيت كلمة المرور؟', 'Forgot password?');
  String get createAccountQuestion =>
      _t('ليس لديك حساب؟', "Don't have an account?");
  String get createAccountLink => _t('إنشاء حساب', 'Create account');
  String get showPasswordTooltip => _t('إظهار كلمة المرور', 'Show password');
  String get hidePasswordTooltip => _t('إخفاء كلمة المرور', 'Hide password');
  String get biometricSignInTooltip =>
      _t('الدخول بالبصمة / الوجه', 'Sign in with biometrics');

  // Email-OTP second factor + reset flow (Page 003 L-5/L-6).
  String get otpTitle => _t('رمز التحقق', 'Verification code');
  String get otpBody => _t(
        'أدخل الرمز المُرسَل إلى بريدك الإلكتروني.',
        'Enter the code we sent to your email.',
      );
  String get otpLabel => _t('الرمز', 'Code');
  String get verifyButton => _t('تحقّق', 'Verify');
  String get forgotPasswordTitle =>
      _t('استعادة كلمة المرور', 'Reset password');
  String get forgotPasswordBody => _t(
        'أدخل بريدك الإلكتروني وسنرسل لك رمزاً لإعادة التعيين.',
        'Enter your email and we will send you a reset code.',
      );
  String get sendCodeButton => _t('إرسال الرمز', 'Send code');
  String get resetPasswordTitle =>
      _t('تعيين كلمة مرور جديدة', 'Set a new password');
  String get newPasswordLabel => _t('كلمة المرور الجديدة', 'New password');
  String get confirmPasswordLabel =>
      _t('تأكيد كلمة المرور', 'Confirm password');
  String get resetPasswordButton => _t('تعيين', 'Reset');
  String get passwordsDoNotMatch =>
      _t('كلمتا المرور غير متطابقتين.', 'The passwords do not match.');
  String get resetPasswordSent => _t(
        'إن كان البريد مسجلاً فستصلك رسالة بالرمز.',
        'If that email is registered, a code is on its way.',
      );
}

class _AppL10nDelegate extends LocalizationsDelegate<AppL10n> {
  const _AppL10nDelegate();

  @override
  bool isSupported(Locale locale) =>
      locale.languageCode == 'ar' || locale.languageCode == 'en';

  @override
  Future<AppL10n> load(Locale locale) async => AppL10n(locale);

  @override
  bool shouldReload(_AppL10nDelegate old) => false;

  @override
  Type get type => AppL10n;
}
