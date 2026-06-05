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

  // Sign up — type (Page 004). Client-only account-type chooser (no API).
  String get signUpTypeTitle => _t('إنشاء حساب — النوع', 'Sign up — type');
  String get signUpTypeLead =>
      _t('اختر نوع الحساب', 'Choose your account type');
  String get signUpTypeVisitor => _t('زائر', 'Visitor');
  String get signUpTypeVisitorHelper => _t(
        'حساب لحضور الفعالية والتفاعل معها',
        'Account to attend and interact with the event',
      );
  String get signUpTypeExhibitor => _t('عارض', 'Exhibitor');
  String get signUpTypeSponsor => _t('راعٍ', 'Sponsor');
  String get signUpCpOnlyNote => _t(
        'تُدار حسابات العارضين والرعاة من لوحة التحكم',
        'Exhibitor & sponsor accounts are managed from the Control Panel',
      );
  // Sign up — form (Page 005).
  String get signUpTitle => _t('إنشاء حساب', 'Sign up');
  String get signUpButton => _t('إنشاء حساب', 'Create account');
  String get invalidEmail => _t('بريد إلكتروني غير صالح', 'Invalid email');
  String get passwordPolicyError => _t(
        'كلمة المرور لا تستوفي الشروط',
        'Password does not meet the requirements',
      );
  String get signUpCheckEmail =>
      _t('تحقق من بريدك الإلكتروني', 'Check your email');

  // Sign-up email verification (Page 006).
  String get emailVerifyTitle => _t('التحقق بالبريد', 'Email verification');
  String get emailVerifySentTo =>
      _t('أرسلنا رمزًا من 6 أرقام إلى', 'We sent a 6-digit code to');
  String get emailVerifiedToast => _t('تم التحقق من البريد', 'Email verified');
  String get resendCodeButton => _t('إعادة إرسال الرمز', 'Resend code');
  String resendCooldownText(int seconds) =>
      _t('إعادة الإرسال خلال $seconds ث', 'Resend in ${seconds}s');

  String get haveAccountQuestion =>
      _t('لديك حساب؟', 'Have an account?');

  // Sign up — visitor profile completion (Page 007).
  String get signUpVisitorTitle =>
      _t('إنشاء حساب · زائر', 'Sign up — visitor');
  String get profileSectionPersonal => _t('البيانات الشخصية', 'Personal');
  String get profileSectionAffiliation =>
      _t('الجهة والتصنيف', 'Affiliation');
  String get profileSectionInterests => _t('الاهتمامات', 'Interests');
  String get profileLoadError =>
      _t('تعذر تحميل النموذج.', 'Could not load the form.');
  String get arabicNameLabel => _t('الاسم الكامل (بالعربية)', 'Full name (Arabic)');
  String get englishNameLabel =>
      _t('الاسم الكامل (بالإنجليزية)', 'Full name (English)');
  String get jobTitleLabel => _t('المسمى الوظيفي (اختياري)', 'Job title (optional)');
  String get nationalityLabel => _t('الجنسية', 'Nationality');
  String get isSaudiLabel => _t('سعودي الجنسية', 'Saudi national');
  String get nationalIdLabel => _t('رقم الهوية الوطنية', 'National ID');
  String get documentTypeLabel => _t('نوع الوثيقة', 'Document type');
  String get iqamaSegment => _t('الإقامة', 'Iqama');
  String get passportSegment => _t('جواز السفر', 'Passport');
  String get iqamaNumberLabel => _t('رقم الإقامة', 'Iqama number');
  String get passportNumberLabel => _t('رقم جواز السفر', 'Passport number');
  String get saudiMobileLabel => _t('رقم الجوال (اختياري)', 'Mobile (optional)');
  String get internationalMobileLabel =>
      _t('رقم الجوال الدولي (اختياري)', 'International mobile (optional)');
  String get dateOfBirthLabel => _t('تاريخ الميلاد', 'Date of birth');
  String get placeOfBirthLabel => _t('مكان الميلاد (اختياري)', 'Place of birth (optional)');
  String get genderLabel => _t('الجنس', 'Gender');
  String get genderUnspecified => _t('غير محدد', 'Prefer not to say');
  String get genderMale => _t('ذكر', 'Male');
  String get genderFemale => _t('أنثى', 'Female');
  String get organisationLabel => _t('الجهة / المنظمة', 'Organisation');
  String get organisationSearchHint =>
      _t('ابحث عن جهتك (اختياري)', 'Search your organisation (optional)');
  String get organisationEmpty =>
      _t('لا توجد جهات مطابقة', 'No organisations found');
  String get organisationSelected => _t('الجهة محددة', 'Organisation selected');
  String get profileTypeLabel => _t('التصنيف', 'Profile type');
  String get interestsHelper =>
      _t('اختر من 1 إلى 10 اهتمامات', 'Pick 1 to 10 interests');
  String interestsCounter(int count) =>
      _t('$count / 10 مُختارة', '$count / 10 selected');
  String get interestsEmpty => _t('لا توجد اهتمامات', 'No interests available');
  String get attachIdImageLabel => _t('إرفاق صورة الهوية', 'Attach ID image');
  String get idImageAttachedLabel => _t('تم إرفاق الصورة', 'Image attached');
  String get removeLabel => _t('إزالة', 'Remove');
  String get clearLabel => _t('مسح', 'Clear');
  String get saveLabel => _t('حفظ', 'Save');
  String get profileSavedToast => _t('تم حفظ الملف الشخصي', 'Profile saved');
  String get idImageUploadFailed => _t(
        'تم حفظ الملف الشخصي، لكن تعذر رفع الصورة. حاول لاحقًا.',
        'Profile saved, but the image upload failed. Try again later.',
      );
  String get requiredField => _t('هذا الحقل مطلوب', 'This field is required');
  String get nationalityRequired => _t('الجنسية مطلوبة', 'Nationality is required');
  String get nationalIdInvalid => _t(
        'رقم الهوية الوطنية غير صحيح (10 أرقام تبدأ بـ 1)',
        'Invalid national ID (10 digits starting with 1)',
      );
  String get iqamaInvalid => _t(
        'رقم الإقامة غير صحيح (10 أرقام تبدأ بـ 2)',
        'Invalid Iqama number (10 digits starting with 2)',
      );
  String get passportInvalid => _t(
        'رقم جواز السفر غير صحيح (6–9 أحرف أو أرقام)',
        'Invalid passport number (6–9 letters or digits)',
      );
  String get documentRequired => _t(
        'يجب إدخال رقم الإقامة أو جواز السفر',
        'An Iqama or passport number is required',
      );
  String get phoneInvalid => _t('رقم الجوال غير صالح', 'Invalid phone number');
  String get dateOfBirthRequired =>
      _t('تاريخ الميلاد مطلوب', 'Date of birth is required');
  String get ageRequirement => _t(
        'يجب أن يكون عمرك 18 عامًا على الأقل',
        'You must be at least 18 years old',
      );
  String get interestsRequired =>
      _t('اختر اهتمامًا واحدًا على الأقل', 'Pick at least one interest');
  String get interestsMaxReached =>
      _t('الحد الأقصى 10 اهتمامات', 'You can pick at most 10 interests');

  // Terms & conditions (Page 009).
  String get termsTitle => _t('الشروط والأحكام', 'Terms & conditions');
  String termsLastUpdated(String date) =>
      _t('آخر تحديث · $date', 'Last updated · $date');
  String get termsEmpty => _t('لا يوجد محتوى', 'No content');
  String get termsAcceptCheckbox => _t(
        'أوافق على الشروط والأحكام',
        'I accept the terms and conditions',
      );
  String get termsAcceptButton => _t('موافقة ومتابعة', 'Accept & continue');
  String get declineLabel => _t('رفض', 'Decline');

  // Registration success (Page 010).
  String get registrationSuccessTitle =>
      _t('تم التسجيل بنجاح', 'Registration success');
  String get registrationSuccessMessage => _t(
        'تم استلام طلبك وهو قيد المراجعة من قبل الإدارة.',
        'Your request was received and is under admin review.',
      );
  String get registrationStatusButton =>
      _t('حالة التسجيل', 'Registration status');
  String get goHomeButton => _t('الانتقال للرئيسية', 'Go to home');

  // Registration status (Page 011).
  String get registrationStatusTitle =>
      _t('حالة التسجيل', 'Registration status');
  String get regPendingHeadline =>
      _t('حسابك قيد المراجعة', 'Your account is under review');
  String get regPendingMessage => _t(
        'تم استلام طلبك وسيراجعه فريق SIMF قريبًا.',
        'Your request was received and the SIMF team will review it soon.',
      );
  String get regApprovedHeadline =>
      _t('تم اعتماد حسابك', 'Your account is approved');
  String get regApprovedMessage =>
      _t('يمكنك الآن الدخول إلى التطبيق.', 'You can now enter the app.');
  String get regRejectedHeadline =>
      _t('لم يتم اعتماد حسابك', 'Your account was not approved');
  String get regRejectedMessage => _t(
        'نأسف، لم تتم الموافقة على طلبك. تواصل مع الدعم لمزيد من المعلومات.',
        'We are sorry — your request was not approved. Contact support for more information.',
      );
  String get regStatusError =>
      _t('تعذر تحميل حالة الحساب.', 'Could not load your account status.');
  String get reCheckButton => _t('إعادة التحقق', 'Re-check');
  String get signOutLink => _t('تسجيل الخروج', 'Sign out');
  String get stagesTitle => _t('المراحل', 'Stages');
  String get stageDataSubmitted => _t('إرسال البيانات', 'Data submitted');
  String get stageEmailConfirmed =>
      _t('تأكيد البريد الإلكتروني', 'Email confirmed');
  String get stageTeamReview => _t('مراجعة فريق SIMF', 'SIMF team review');
  String get stageActivation => _t('تفعيل الحساب', 'Account activation');

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

  // Splash branding (Page 001) — matches the mockup brand lockup.
  String get splashTagline => 'SAUDI · MOD · RSNF';
  String get splashTitle => _t(
        'الملتقى البحري السعودي الدولي',
        'Saudi International Maritime Forum',
      );
  String get splashEventLine => _t(
        'النسخة الرابعة · ٢٣–٢٥ نوفمبر ٢٠٢٦ · الرياض',
        '4th Edition · 23–25 Nov 2026 · Riyadh',
      );

  // Onboarding intro videos (Page 002 — interim placeholder frames; the real
  // YouTube clips introd_001..003 land with SIMF-VID-001).
  String get onboardingVideoLabel => _t('مقطع تعريفي', 'Intro video');
  String get onboardingMutedTooltip => _t('الصوت مكتوم', 'Sound muted');

  // Login header controls (Page 003) — buttons only for now (no wiring yet).
  String get themeToggleTooltip => _t('المظهر · ليلي/نهاري', 'Light / dark mode');
  String get languageToggleLabel => 'العربية · English';

  // Home — landing / router screen (Page 013). Interim copy + tile labels for
  // the functional skeleton; the final visuals come from SIMF-VID-001.
  String get homeTitle => _t('الرئيسية', 'Home');
  String get notificationsTooltip => _t('الإشعارات', 'Notifications');
  String get homeDiscoverTitle => _t('اكتشف', 'Discover');
  String get homeDiscoverSubtitle => _t(
        'كل ما تحتاجه عن الملتقى في مكان واحد.',
        'Everything you need about the forum, in one place.',
      );
  String get liveNowLabel => _t('مباشر', 'LIVE');
  String get liveBannerTitle => _t('البث المباشر', 'Live broadcast');
  String get liveBannerSubtitle =>
      _t('شاهد الجلسات مباشرةً', 'Watch the sessions live');
  String get guestPromptText => _t(
        'أنت تتصفح كضيف. سجّل دخولك للوصول إلى بطاقتك الذكية والإشعارات الشخصية.',
        'You are browsing as a guest. Sign in to access your smart badge and personal notifications.',
      );
  String get guestSignInCta => _t('تسجيل الدخول', 'Sign in');
  String get tileSessions => _t('الجلسات', 'Sessions');
  String get tileSpeakers => _t('المتحدثون', 'Speakers');
  String get tileVenueMap => _t('الخريطة', 'Venue map');
  String get tileBooths => _t('الأجنحة', 'Booths');
  String get tileSponsors => _t('الرعاة', 'Sponsors');
  String get tileNews => _t('الأخبار والتغطية', 'News & coverage');
  String get tileArchive => _t('الأرشيف', 'Archive');
  String get tileAbout => _t('عن الملتقى', 'About the forum');
  String get tileMyArea => _t('منطقتي', 'My area');
  String get tileEntryBadge => _t('بطاقتي الذكية', 'My smart badge');
  String get tileMeetPeople => _t('قابل أشخاص مثلك', 'Meet people like you');
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
