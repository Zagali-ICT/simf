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
  String get lookupLoadError =>
      _t('تعذر تحميل القائمة.', 'Could not load the list.');
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
  // Step bodies — the KSA-Project onboarding copy (Figma 148:22 / 159:942 /
  // 159:1052, D-362). All three steps share onboardingTitle1 as their title.
  String get onboardingBody1 => _t(
        'دليلك المتكامل: الأجندة، المتحدثون، الخريطة التفاعلية، البطاقة الذكية، والبث المباشر في تطبيق واحد.',
        'Your complete guide: the agenda, speakers, interactive map, smart badge and live broadcast in one app.',
      );
  String get onboardingTitle2 => _t(
        'تابع الجلسات والمتحدثين',
        'Follow the sessions and speakers',
      );
  String get onboardingBody2 => _t(
        'كل ما تحتاجه في مكان واحد: جدول الفعاليات، المتحدثون الرئيسيون، خريطة الموقع، معلومات التسجيل، والبث المباشر، كله في تطبيق واحد.',
        'Everything you need in one place: the events schedule, keynote speakers, the venue map, registration information and the live broadcast — all in one app.',
      );
  String get onboardingTitle3 => _t(
        'بطاقتك الذكية وتواصلك',
        'Your smart badge and networking',
      );
  String get onboardingBody3 => _t(
        'كل ما تحتاجه في مكان واحد: جدول الفعاليات، المتحدثون، خريطة الموقع، معلومات الدخول، والبث المباشر.',
        'Everything you need in one place: the events schedule, speakers, the venue map, entry information and the live broadcast.',
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
      // D-373 — the digit count is visually obvious from the code boxes.
      _t('أرسلنا رمز التحقق إلى', 'We sent a verification code to');
  String get emailVerifiedToast => _t('تم التحقق من البريد', 'Email verified');
  String get resendCodeButton => _t('إعادة إرسال الرمز', 'Resend code');
  // KSA-Project OTP frame copy (Figma 505:837, D-364).
  String get enterOtpTitle =>
      _t('أدخل رمز التحقق', 'Enter the verification code');
  String get resendInLabel => _t('إعادة الإرسال خلال', 'Resend in');
  String get noCodeQuestion => _t('لم يصلك الرمز؟', "Didn't get the code?");
  String get resendAction => _t('إعادة الإرسال', 'Resend');
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
      _t('ابحث عن جهتك', 'Search your organisation');
  String get organisationEmpty =>
      _t('لا توجد جهات مطابقة', 'No organisations found');
  String get organisationSelected => _t('الجهة محددة', 'Organisation selected');
  // B3 — D-221 (الجهة): required-field message on the sign-up screen.
  String get organisationRequired =>
      _t('اختر جهتك من القائمة', 'Pick your organisation from the list');
  String get profileTypeLabel => _t('التصنيف', 'Profile type');
  // Page 007 — نوع التسجيل (Visitor/Other) filter (D-332). Visitor reuses
  // [signUpTypeVisitor]; Other is new.
  String get registrationTypeLabel => _t('نوع التسجيل', 'Registration type');
  String get signUpTypeOther => _t('أخرى', 'Other');
  // KSA-Project interests frame copy (Figma 505:1083, D-365).
  String get interestsChooseTitle =>
      _t('اختر اهتماماتك', 'Choose your interests');
  String get interestsHelper => _t(
        'اختر ما لا يقل عن واحد وبحد أقصى 10 اهتمامات تُستخدم لاقتراح أشخاص وجلسات مناسبة لك.',
        'Pick at least one and up to 10 interests — used to suggest people and sessions for you.',
      );
  String interestsCounter(int count) =>
      _t('$count / 10 مُختارة', '$count / 10 selected');
  String get interestsEmpty => _t('لا توجد اهتمامات', 'No interests available');
  String get attachIdImageLabel => _t('إرفاق صورة الهوية', 'Attach ID image');
  // KSA-Project profile frame copy (Figma 168:2972, D-368).
  String get createProfileTitle => _t('إنشاء ملف شخصى', 'Create profile');
  String get documentNumberLabel => _t('رقم الوثيقة', 'Document number');
  String get attachmentsLabel => _t(
        'المرفقات (صورة الهوية / الإقامة / الجواز)',
        'Attachments (ID / Iqama / passport image)',
      );
  String get attachFileLabel => _t('إرفاق ملف', 'Attach file');
  String get termsAgreeQuestion => _t(
        'الموافقة على الشروط والأحكام؟',
        'Agree to the terms & conditions?',
      );
  String get idImageAttachedLabel => _t('تم إرفاق الصورة', 'Image attached');
  String get removeLabel => _t('إزالة', 'Remove');
  String get clearLabel => _t('مسح', 'Clear');
  String get saveLabel => _t('حفظ', 'Save');
  // Page 007 advances to the interests screen with Next (D-332); Page 007‑01 title.
  String get nextLabel => _t('التالي', 'Next');
  String get interestsTitle => _t('اهتماماتي', 'My interests');
  String get profileSavedToast => _t('تم حفظ الملف الشخصى', 'Profile saved');
  String get idImageUploadFailed => _t(
        'تم حفظ الملف الشخصى، لكن تعذر رفع الصورة. حاول لاحقًا.',
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
  // D-373 — the searchable country picker.
  String get searchCountryHint =>
      _t('ابحث عن الجنسية', 'Search for a country');
  // C7 (D-371) — the male-mandatory camera photo + face check.
  String get idImageRequiredForMen => _t(
        'الصورة الشخصية مطلوبة — التقطها بالكاميرا',
        'A photo is required — capture it with the camera',
      );
  String get noFaceDetectedError => _t(
        'لم يتم التعرف على وجه في الصورة — أعد التقاط صورة واضحة للوجه',
        'No face was detected in the photo — retake a clear photo of the face',
      );
  // C6 (D-371) — رقم اللوحة, optional; Saudi standard when filled.
  String get plateNumberLabel =>
      _t('رقم اللوحة (اختياري)', 'Plate number (optional)');
  String get plateNumberInvalid => _t(
        'يجب أن يتكوّن رقم اللوحة من 3 أحرف وحتى 4 أرقام (المعيار السعودي)',
        'The plate number must be 3 letters and up to 4 digits (Saudi standard)',
      );
  // C5 (D-371) — under "Other" the profile-type pick is required.
  String get profileTypeRequired =>
      _t('يجب اختيار التصنيف', 'A profile type selection is required');
  // C4 (D-371) — the standard phone shapes, mirrored client/server.
  String get saudiMobileInvalid => _t(
        'يجب أن يكون رقم الجوال السعودي بصيغة 05XXXXXXXX أو +9665XXXXXXXX',
        'The Saudi mobile must be 05XXXXXXXX or +9665XXXXXXXX',
      );
  String get internationalMobileInvalid => _t(
        'يجب أن يكون رقم الجوال الدولي بالصيغة الدولية (E.164)',
        'The international mobile must be in the +<country code><number> (E.164) format',
      );
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
  // KSA-Project terms frame copy (Figma 505:1553, D-367).
  String get termsImportantInfoTitle => _t(
        'معلومات هامة لزوار الملتقى',
        'Important information for forum visitors',
      );
  String get termsAcceptButton => _t('موافق', 'Agree');
  String get declineLabel => _t('رفض', 'Decline');

  // Registration success (Page 010).
  String get registrationSuccessTitle =>
      _t('تم التسجيل بنجاح', 'Registration success');
  // KSA-Project success frame copy (Figma 505:1451, D-366).
  String get registrationSuccessMessage => _t(
        'تم استلام طلبك ومراجعته\nستصلك رسالة تأكيد على بريدك الإلكتروني.',
        'Your request was received and is under review.\nA confirmation email will reach your inbox.',
      );
  String get registrationStatusButton =>
      _t('حالة التسجيل', 'Registration status');
  String get goHomeButton => _t('الانتقال للرئيسية', 'Go to home');
  String get regSuccessHeaderTitle => _t('تم التسجيل', 'Registered');
  String get referenceNumberLabel =>
      _t('رقم البطاقة المرجعي', 'Reference badge number');
  String get contactUsTitle => _t('تواصل معانا', 'Contact us');
  String get simfSocialFooter => _t(
        '@SIMF_RSNF · الملتقى البحري السعودي الدولي',
        '@SIMF_RSNF · Saudi International Maritime Forum',
      );

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
  // D-373 — the My-Area sign-out confirmation.
  String get signOutConfirmBody => _t(
        'هل تريد تسجيل الخروج من حسابك؟',
        'Do you want to sign out of your account?',
      );
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
  String get browseAsGuestLink =>
      _t('تصفّح بدون تسجيل الدخول', 'Browse without signing in');
  String get showPasswordTooltip => _t('إظهار كلمة المرور', 'Show password');
  String get hidePasswordTooltip => _t('إخفاء كلمة المرور', 'Hide password');
  // Sign in — KSA-Project design (Figma 168:2800, D-358/D-360/D-363).
  String get guestSignInLink => _t('الدخول كزائر', 'Enter as guest');
  String get signInForumTitle =>
      _t('الملتقى الدولى البحرى', 'International Maritime Forum');
  String get rememberMeLabel => _t('تذكرنى', 'Remember me');
  String get orDividerLabel => _t('او', 'or');
  String get faceIdSignInButton =>
      _t('التسجيل ببصمة الوجه', 'Sign in with Face ID');

  String get biometricSignInTooltip =>
      _t('الدخول بالبصمة / الوجه', 'Sign in with biometrics');

  /// No OS face/fingerprint is enrolled on the device (D-422).
  String get biometricUnavailable => _t(
        'لا توجد بصمة أو بصمة وجه مفعّلة على هذا الجهاز. سجّل الدخول بكلمة المرور.',
        'No face or fingerprint is set up on this device. Sign in with your password.',
      );

  /// Face login needs a prior password sign-in on this device to enrol the
  /// device key first (D-422).
  String get biometricNotEnrolled => _t(
        'سجّل الدخول بكلمة المرور مرة واحدة على هذا الجهاز لتفعيل الدخول بالوجه.',
        'Sign in with your password once on this device to enable face login.',
      );

  // Email-OTP second factor + reset flow (Page 003 L-5/L-6).
  String get otpTitle => _t('رمز التحقق', 'Verification code');
  String get otpBody => _t(
        'أدخل الرمز المُرسَل إلى بريدك الإلكتروني.',
        'Enter the code we sent to your email.',
      );
  String get otpLabel => _t('الرمز', 'Code');
  String get verifyButton => _t('تحقّق', 'Verify');
  // Email-OTP screen (frame 758:2616).
  String get otpHeaderTitle => _t('التحقق بالبريد', 'Email verification');
  String get otpSentToPrefix => _t('أرسلنا رمزاً الى', 'We sent a code to');
  String get otpResendCountdown => _t('إعادة الإرسال خلال', 'Resend in');
  String get otpDidntReceive => _t('لم يصلك الرمز؟', 'Didn\'t get the code?');
  String get otpResendAction => _t('إعادة الإرسال', 'Resend');
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

  // Part B (D-430) — badge-QR sign-in / activation.
  String get badgeSignInButton =>
      _t('الدخول بمسح الشارة', 'Sign in by scanning your badge');
  String get badgeScanTitle => _t('امسح شارتك', 'Scan your badge');
  String get badgeScanHint => _t(
        'وجّه الكاميرا نحو رمز QR المطبوع على شارتك.',
        'Point the camera at the QR code on your badge.',
      );
  String get badgeManualLabel =>
      _t('أو أدخل رمز الشارة يدويًا', 'Or enter the badge code manually');
  String get badgeManualField => _t('رمز الشارة', 'Badge code');
  String get badgeResolveButton => _t('متابعة', 'Continue');
  // Shared QR-scanner chrome (used by the badge, contact and exhibitor scanners).
  String get qrStopCamera => _t('إيقاف الكاميرا', 'Stop camera');
  String get qrBack => _t('رجوع', 'Back');
  String get qrManualLabel =>
      _t('أو أدخل الرمز يدويًا', 'Or enter the code manually');
  String get badgeNotRecognised =>
      _t('تعذّر التعرّف على الشارة.', 'The badge was not recognised.');
  String get badgeScanError =>
      _t('تعذّرت قراءة الشارة. حاول مجددًا.', 'Could not read the badge. Try again.');
  String get badgeActivateTitle =>
      _t('تفعيل حسابك', 'Activate your account');
  String get badgeActivateEmailIntro => _t(
        'أدخل بريدك الإلكتروني لإرسال رمز التحقق.',
        'Enter your email so we can send a verification code.',
      );
  String badgeActivateCodeSent(String maskedEmail) => _t(
        'أرسلنا رمز التحقق إلى $maskedEmail.',
        'We sent a verification code to $maskedEmail.',
      );
  String get badgeSendCodeButton => _t('إرسال الرمز', 'Send code');
  String get badgeActivateButton => _t('تفعيل وتعيين كلمة المرور', 'Activate & set password');
  String get badgeActivatedDone => _t(
        'تم تفعيل حسابك. سجّل الدخول الآن.',
        'Your account is activated. Sign in now.',
      );
  String get emailLabelGeneric => _t('البريد الإلكتروني', 'Email');

  // Splash branding (Page 001) — matches the mockup brand lockup.
  String get splashTagline => 'SAUDI · MOD · RSNF';
  String get splashTitle => _t(
        'الملتقى البحري السعودي الدولي',
        'Saudi International Maritime Forum',
      );
  // Two lines per the KSA-Project splash frame (Figma 159:573, D-361).
  String get splashEventLine => _t(
        'النسخة الرابعة\n٢٣–٢٥ نوفمبر ٢٠٢٦ · الرياض',
        '4th Edition\n23–25 Nov 2026 · Riyadh',
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

  // Bottom navigation (KSA Wave-2 shell — frames 512:1492 / 213:963).
  String get navAgenda => _t('الأجندة', 'Agenda');
  String get navProfile => _t('الملف الشخصى', 'Profile');

  // Home — KSA Wave-2 redesign (guest 512:1492 / signed-in 203:1236).
  String get homeGuestTitle => _t('الرئيسية • ضيف', 'Home • Guest');
  String get guestBannerPrefix => _t(
        'أنت تتصفح كضيف، سجّل دخولك للوصول إلى ',
        'You are browsing as a guest. Sign in to access ',
      );
  String get guestBannerHighlight => _t('البطاقة الذكية', 'the smart badge');
  String get guestBannerSuffix => _t(
        '، طلبات المقابلات، والإشعارات الشخصية.',
        ', meeting requests, and personal notifications.',
      );
  String get tileExhibition => _t('المعرض', 'Exhibition');
  String get tileMyBadgeShort => _t('بطاقتي', 'My badge');
  String get homeOpenInfoSection =>
      _t('معلومات مفتوحة للجميع', 'Open to everyone');
  String get faqRowTitle => _t('الأسئلة الشائعة', 'FAQ');
  String get faqRowSubtitle =>
      _t('FAQ • معلومات الموقع والفعالية', 'FAQ • Venue & event info');
  String get discoverSaudiTitle => _t('روح السعودية', 'Spirit of Saudi');
  String get discoverSaudiSubtitle =>
      _t('Visit Saudi · استكشف الرياض', 'Visit Saudi · Discover Riyadh');
  String get greetingMorning => _t('صباح الخير', 'Good morning');
  String get greetingEvening => _t('مساء الخير', 'Good evening');
  String get homeLiveTitle => _t(
        'الجلسة الافتتاحية تُبث الآن',
        'The opening session is live now',
      );
  String get homeLiveSubtitle =>
      _t('شاهد البث المباشر', 'Watch the live stream');
  String get homeAboutSection =>
      _t('عن الملتقى · المحاور', 'About the forum · Themes');
  String get homeSmartSection => _t('الميزات الذكية', 'Smart features');
  String get tileBilateralMeetings =>
      _t('اللقاءات الثنائية', 'Bilateral meetings');
  String get tileSessionSummary => _t('ملخص الجلسات', 'Session summaries');
  String get followUsSection => _t('تابعنا', 'Follow us');
  // The official handle line — a proper noun, identical in both languages.
  String get followUsHandle => '@SIMF_RSNF · الملتقى البحري السعودي الدولي';
  String get discoverSection => _t('اكتشف', 'Discover');
  // The top discovery hero banner on the signed-in home (frame 758:1134 node
  // 758:1203): the gold "اكتشف" title reuses [discoverSection]; this is the
  // white sub-line over the event photo.
  String get discoverBannerSubtitle =>
      _t('تعال واكتشف جديدك المفضل', 'Come discover your favourites');
  // أحدث منشوراتنا — the latest-news teaser card on the signed-in home (frame
  // 203:1236 node 522:2345). The engagement counts in the frame have no backend
  // data (the news model carries no like/comment/repost counts) so they are
  // omitted rather than faked.
  String get latestPostsSection => _t('أحدث منشوراتنا', 'Latest posts');
  String get postSourceName =>
      _t('الملتقى البحري السعودي الدولي', 'Saudi Maritime Forum');
  String get postTimeJustNow => _t('الآن', 'just now');
  String postTimeMinutesAgo(int n) => _t('قبل $n دقيقة', '$n min ago');
  String postTimeHoursAgo(int n) => _t('قبل $n ساعة', '$n h ago');
  String postTimeDaysAgo(int n) => _t('قبل $n يوم', '$n d ago');

  // My Area — personal dashboard (Page 014).
  // Frame 758:1283 header (matches the bottom-nav "الملف الشخصى" label).
  String get myAreaTitle => _t('الملف الشخصى', 'Profile');
  String enrolledInSessions(int count) =>
      _t('مسجّل في $count جلسات', 'Enrolled in $count sessions');
  String get shareLabel => _t('مشاركة', 'Share');
  String get shareContact => _t('مشاركة جهة اتصال', 'Share contact');
  String get shareMyProfile => _t('مشاركة ملفي', 'Share my profile');
  String get shareCalendar => _t('مشاركة جدولي', 'Share my calendar');
  String get shareFailed =>
      _t('تعذّرت المشاركة. حاول مرة أخرى.', 'Could not share. Try again.');
  String get avatarChangeTooltip => _t('تغيير الصورة', 'Change photo');
  String get avatarUploadFailed => _t(
        'تعذّر تحديث الصورة. حاول مرة أخرى.',
        'Could not update the photo. Try again.',
      );
  // التحقق من الهوية — the guided face-capture / liveness flow (D-404, frames
  // 758:4180 / 758:4248 / 758:4316).
  String get identityVerificationTitle =>
      _t('التحقق من الهوية', 'Identity verification');
  String get stepSmilePrompt => _t('ابتسم', 'Smile');
  String get stepTurnRightPrompt =>
      _t('ادر راسك لليمين', 'Turn your head right');
  String get stepTurnLeftPrompt => _t('ادر راسك لليسار', 'Turn your head left');
  String get identityCameraUnavailable => _t(
        'الكاميرا غير متاحة. اختر صورة من المعرض بدلاً من ذلك.',
        'The camera is unavailable. Choose a photo from the gallery instead.',
      );
  String get chooseFromGallery => _t('اختر من المعرض', 'Choose from gallery');

  // Moderator (محاور) per-session Q&A desk (Figma 758:5307, D-405).
  String get moderatorDeskTitle => _t('أسئلة الجلسة', 'Session questions');
  String get moderatorBadge => _t('محاوِر', 'Moderator');
  String get moderatorManageQuestions =>
      _t('إدارة الأسئلة', 'Manage questions');
  String get moderatorChipAll => _t('الكل', 'All');
  String get moderatorChipNew => _t('جديد', 'New');
  String get moderatorChipOnStage => _t('يتم الإجابة', 'Being answered');
  String get moderatorActionOnStage => _t('يتم الإجابة', 'Being answered');
  String get moderatorActionReject => _t('مرفوض', 'Reject');
  String get moderatorToHost => _t('إلى المضيف', 'To host');
  String get moderatorEmpty =>
      _t('لا توجد أسئلة معتمدة بعد.', 'No approved questions yet.');
  String get moderatorForbidden => _t(
        'لست محاوِرًا لهذه الجلسة.',
        'You are not a moderator for this session.',
      );
  String get moderatorError => _t(
        'تعذّر تحميل الأسئلة. حاول مرة أخرى.',
        'Could not load the questions. Try again.',
      );
  String get moderatorActionFailed =>
      _t('تعذّر تنفيذ الإجراء. حاول مرة أخرى.', 'Action failed. Try again.');

  // Staff gate-operator console (Figma 758:4380/4651/4735/4819/4886, D-406).
  String get gateScannerEntry => _t('مسح البوابة', 'Gate scanner');
  String get gateSelectGate => _t('اختر البوابة', 'Select gate');
  String get gateScanHint =>
      _t('وجّه الكاميرا إلى رمز QR', 'Point the camera at the QR code');
  String get gateManualHint => _t('أدخل الرمز يدويًا', 'Enter the code manually');
  String get gateManualSubmit => _t('تحقّق', 'Check');
  String get gateHold => _t('إيقاف مؤقت', 'Hold');
  String get gateResume => _t('استئناف', 'Resume');
  String get gateAllowed => _t('مسموح', 'Allowed');
  String get gateAllowedSub =>
      _t('مرحباً بك في الفعالية', 'Welcome to the event');
  String get gateDenied => _t('ممنوع', 'Denied');
  String get gateDeniedSub =>
      _t('غير مصرح بالدخول', 'Entry not authorised');
  String get gateFieldName => _t('الاسم', 'Name');
  String get gateFieldReference => _t('الرقم المرجعي', 'Reference');
  String get gateFieldType => _t('النوع', 'Type');
  String get gateFieldGate => _t('البوابة', 'Gate');
  String get gateFieldDirection => _t('الحركة', 'Direction');
  String get gateScanAgain => _t('سكان مرة أخرى', 'Scan again');
  String get gateNone => _t('لا يوجد', 'None');
  String get gateDirectionIn => _t('دخول', 'Entry');
  String get gateDirectionOut => _t('خروج', 'Exit');
  String get gateNotAssigned => _t(
        'لست مشغّلاً لأي بوابة.',
        'You are not assigned to any gate.',
      );
  String get gateForbidden => _t(
        'لا تملك صلاحية تشغيل البوابات.',
        'You are not authorised to operate gates.',
      );
  String get gateError => _t(
        'تعذّر الاتصال بالبوابة. حاول مرة أخرى.',
        'Could not reach the gate. Try again.',
      );
  String get gateRateLimited =>
      _t('محاولات كثيرة. انتظر قليلاً.', 'Too many attempts. Wait a moment.');
  String get statBookedSessions => _t('جلسات محفوظة', 'Booked sessions');
  String get statMeetings => _t('مقابلات مؤكدة', 'Confirmed meetings');
  String get statisticsTitle => _t('الإحصائيات', 'Statistics');
  String get todayScheduleTitle => _t('جدولي اليوم', "Today's schedule");
  String get scheduleEmpty =>
      _t('لا يوجد لديك مواعيد اليوم', 'No items today');
  String get smartBadgeLink => _t('بطاقتي الذكية', 'My smart badge');
  String get accountSettingsLink => _t('إعدادات الحساب', 'Account settings');
  String get myAreaPendingNote => _t(
        'حسابك قيد المراجعة. ستظهر بطاقتك وجدولك بعد الاعتماد.',
        'Your account is under review. Your badge and schedule appear once approved.',
      );
  String get myAreaError =>
      _t('تعذّر تحميل منطقتك.', 'Could not load your area.');

  // Venue map (Page 015).
  String get venueMapTitle => _t('الخريطة', 'Venue map');
  String get venueMapError =>
      _t('تعذّر تحميل الخريطة.', 'Could not load the map.');
  String get venueMapEmpty =>
      _t('لا توجد عناصر على الخريطة بعد', 'No map items yet');
  String get legendHall => _t('قاعة', 'Hall');
  String get legendZone => _t('منطقة', 'Zone');
  String get legendBooth => _t('جناح', 'Booth');
  String get legendPoi => _t('نقطة اهتمام', 'Point of interest');
  // KSA Wave-2 frame 215:562 copy (the selected-node info card).
  String get venueMapDirectMe => _t('أرشدني', 'Guide me');
  String get venueMapViewDetails => _t('عرض التفاصيل', 'View details');

  // Sessions — daily schedule (Page 016). The two pills + the day strip + the
  // search box all filter the cached programme client-side (Page_016 L-1).
  String get sessionsTitle => _t('الجلسات', 'Sessions');
  String get sessionsViewUpcoming => _t('الأجندة القادمة', 'Upcoming agenda');
  String get sessionsViewForum => _t('أجندة الفعالية', 'Event agenda');
  String get sessionsAllDays => _t('كل الأيام', 'All days');
  String get sessionsSearchHint => _t('البحث', 'Search');
  String get sessionsScheduleSection => _t('المواعيد', 'Schedule');
  String get sessionsEmpty => _t('لا توجد جلسات', 'No sessions');
  String get sessionsError =>
      _t('تعذّر تحميل الجلسات.', 'Could not load the sessions.');

  // Session detail (Page 017).
  String get sessionDetailTitle => _t('تفاصيل الجلسة', 'Session detail');
  String get sessionDetailError =>
      _t('تعذّر تحميل الجلسة.', 'Could not load the session.');
  String get sessionNotFound =>
      _t('الجلسة غير موجودة أو تمت إزالتها', 'This session was not found');
  String get descriptionHeading => _t('وصف الجلسة', 'Description');
  String get speakersHeading => _t('المتحدثون', 'Speakers');
  String get hostLabel => _t('المضيف', 'Host');
  String get mySeatHeading => _t('مقعدي', 'My seat');
  String seatLocation(String row, int seat) =>
      _t('الصف $row · مقعد $seat', 'Row $row · Seat $seat');
  String get seatBadgeHint => _t(
        'تأكد من إبراز بطاقتك عند الدخول',
        'Show your badge at entry',
      );
  String get seatViewLink => _t('عرض', 'View');
  String get addToCalendar => _t('أضف إلى تقويمي', 'Add to calendar');
  String get reminder => _t('تذكير', 'Reminder');
  String get calendarAdded =>
      _t('تمت إضافة الجلسة إلى تقويمك', 'Added to your calendar');
  String get calendarFailed =>
      _t('تعذّرت إضافة الجلسة إلى التقويم', 'Could not add to calendar');
  String get reminderDeferred => _t(
        'ستتوفر التذكيرات مع إعداد الإشعارات.',
        'Reminders arrive with notifications setup.',
      );

  // My Seat (Page 018 — Figma 898-2873, D-432).
  String get mySeatTitle => _t('مقعدي', 'My seat');
  String get sessionLabel => _t('الجلسة', 'Session');
  String get seatChipLabel => _t('مقعد', 'Seat');
  String get rowChipLabel => _t('الصف', 'Row');
  String get stageLabelBilingual => _t('المسرح · STAGE', 'Stage · STAGE');

  // My Seat map (Page 018).
  String get mySeatMapTitle => _t('مقعدي · خريطة الجلوس', 'My seat map');
  String get seatMapError =>
      _t('تعذّر تحميل خريطة المقاعد.', 'Could not load the seat map.');
  String get seatMapUnavailable =>
      _t('خريطة المقاعد غير متاحة بعد', 'Seat map not available yet');
  String get stageLabel => _t('المسرح', 'Stage');
  String get noSeatYet => _t('لا يوجد لديك مقعد بعد', 'You have no seat yet');
  String get legendMine => _t('مقعدك', 'Your seat');
  String get legendAvailable => _t('متاح', 'Available');
  String get legendReserved => _t('محجوز', 'Reserved');
  String seatCapacity(int reserved, int total) =>
      _t('محجوز $reserved من $total', '$reserved of $total reserved');
  String get navigateToSeat => _t('إرشادي إلى مقعدي', 'Guide me to my seat');
  String get shareLocation => _t('مشاركة الموقع', 'Share location');
  String seatShareText(String row, int seat) => _t(
        'مقعدي في الملتقى: صف $row · مقعد $seat',
        'My SIMF seat: Row $row · Seat $seat',
      );

  // Speakers list (Page 019).
  String get speakersTitle => _t('المتحدثون', 'Speakers');
  String get speakersError =>
      _t('تعذّر تحميل المتحدثين.', 'Could not load the speakers.');
  String get speakersEmpty => _t('لا يوجد متحدثون', 'No speakers');

  // Speaker profile (Page 020).
  String get speakerProfileTitle => _t('ملف المتحدث', 'Speaker profile');
  String get speakerProfileError =>
      _t('تعذّر تحميل ملف المتحدث.', 'Could not load the speaker profile.');
  String get speakerNotFound =>
      _t('المتحدث غير موجود', 'This speaker was not found');
  String get cvBio => _t('نبذة عنه', 'Biography');
  String get cvQualifications => _t('المؤهلات العلمية', 'Qualifications');
  String get cvTraining => _t('الخبرات التدريبية', 'Training experience');
  String get cvAwards => _t('الجوائز', 'Awards');
  String get speakerSessionsHeading => _t('جلسات المتحدث', "Speaker's sessions");
  String get copyLinkLabel => _t('نسخ الرابط', 'Copy link');
  String get linkCopied => _t('تم نسخ الرابط', 'Link copied');
  String get requestMeeting => _t('طلب مقابلة', 'Request meeting');
  String get meetingNameLabel => _t('الاسم', 'Your name');
  String get meetingSubjectLabel => _t('الموضوع', 'Subject');
  String get meetingSendButton => _t('إرسال الطلب', 'Send request');
  String get meetingRequestSent =>
      _t('تم إرسال طلب المقابلة', 'Meeting request sent');
  String get meetingRequestInvalid => _t(
        'يرجى إدخال الاسم والموضوع',
        'Please enter your name and a subject',
      );
  String get meetingRequestNotAllowed => _t(
        'هذا المتحدث لا يستقبل طلبات المقابلة',
        'This speaker is not accepting meeting requests',
      );
  String get meetingRequestFailed => _t(
        'تعذّر إرسال الطلب. حاول مرة أخرى.',
        'Could not send the request. Try again.',
      );

  // Booths (Page 022).
  String get boothsTitle => _t('الأجنحة', 'Booths');
  String get boothsError =>
      _t('تعذّر تحميل الأجنحة.', 'Could not load the booths.');
  String get boothsEmpty => _t('لا توجد أجنحة', 'No booths');
  // Booths list re-skin (Figma 922-2458, D-432).
  String get boothsSearchHint =>
      _t('ابحث عن جناح أو شركة', 'Search for a booth or company');
  String get boothsNoMatch => _t('لا توجد أجنحة مطابقة', 'No matching booths');
  String get boothsHallFallback => _t('قاعة المعرض', 'Exhibition hall');
  String boothsGuideMe(String code) =>
      _t('أرشدني إلى الجناح · $code', 'Guide me to the booth · $code');
  String get boothsOfficerRole => _t('المسؤول في الجناح', 'Booth officer');

  // Sponsors (Page 023).
  String get sponsorsTitle => _t('الرعاة', 'Sponsors');
  String get sponsorsError =>
      _t('تعذّر تحميل الرعاة.', 'Could not load the sponsors.');
  String get sponsorsEmpty => _t('لا يوجد رعاة', 'No sponsors');

  // Archive (Page 024).
  String get archiveTitle => _t('الأرشيف', 'Archive');
  String get archiveError =>
      _t('تعذّر تحميل الأرشيف.', 'Could not load the archive.');
  String get archiveEmpty => _t('لا توجد نسخ سابقة', 'No past editions');
  // Archive detail re-skin (Figma 925-3079, D-432).
  String get archiveNotice => _t(
        'تُعرض نسخة 2026 في الأرشيف بعد انتهاء الملتقى.',
        'Edition 2026 appears in the archive after the forum ends.',
      );
  String get archivePickEdition => _t('اختار ملتقى', 'Choose a forum edition');
  String get archiveTitleLabel => _t('عنوان الملتقى', 'Forum title');
  String get archiveSummaryLabel => _t('نبذة', 'Overview');
  String get archivePlaceLabel => _t('المكان', 'Place');
  String get archiveTimeLabel => _t('الزمن', 'Time');
  String get archiveStatSpeakers => _t('المتحدثون', 'Speakers');
  String get archiveStatAttendees => _t('الحضور', 'Attendees');
  String get archiveStatSessions => _t('الفعاليات', 'Activities');
  String archiveEditionPill(int year) => _t('ملتقى $year', 'Edition $year');
  // D-432 — the rich archive-detail sections (Figma 925-3079 / 24-01).
  String get archiveGalleryLabel => _t('الصور والفيديو', 'Photos & videos');
  String get archiveSessionsLabel => _t('عناوين الجلسات', 'Session titles');
  String get archivePastSpeakersLabel =>
      _t('المتحدثون السابقون', 'Past speakers');
  String archiveMoreCount(int count) => _t('+$count آخرون', '+$count more');
  String archiveStats(int attendees, int sessions, int speakers) => _t(
        '$attendees حضور · $sessions جلسة · $speakers متحدث',
        '$attendees attendees · $sessions sessions · $speakers speakers',
      );

  // News (Page 029).
  String get newsTitle => _t('الأخبار', 'News');
  String get newsError => _t('تعذّر تحميل الأخبار.', 'Could not load the news.');
  String get newsEmpty => _t('لا توجد أخبار', 'No news');
  String get newsNotFound => _t('الخبر غير موجود', 'This article was not found');

  // Media gallery (Page 030).
  String get galleryTitle =>
      _t('معرض الصور والفيديوهات', 'Media gallery');
  String get galleryError =>
      _t('تعذّر تحميل الوسائط.', 'Could not load the media.');
  String get galleryEmpty => _t('لا توجد وسائط', 'No media yet');

  // About the forum (Page 037).
  String get aboutTitle => _t('عن الملتقى', 'About the forum');
  String get aboutError =>
      _t('تعذّر تحميل المحتوى.', 'Could not load the content.');
  String get aboutEmpty =>
      _t('المحتوى قيد الإعداد', 'Content coming soon');

  // Rate / feedback (Page 040).
  String get rateTitle => _t('تقييم', 'Rate');
  String get rateLead =>
      _t('كيف كانت تجربتك في الملتقى؟', 'How was your forum experience?');
  String get rateStarsRequired =>
      _t('يرجى اختيار عدد النجوم', 'Please pick a star rating');
  String get rateCommentLabel => _t('ملاحظاتك (اختياري)', 'Your comments (optional)');
  String get rateSubmit => _t('إرسال التقييم', 'Submit rating');
  String get rateThanks => _t('شكراً لتقييمك', 'Thanks for your rating');
  String get rateFailed =>
      _t('تعذّر إرسال التقييم. حاول مرة أخرى.', 'Could not submit. Try again.');

  // Media partners (Page 031).
  String get mediaPartnersTitle => _t('الشركاء الإعلاميون', 'Media partners');
  String get mediaPartnersError =>
      _t('تعذّر تحميل الشركاء الإعلاميين.', 'Could not load the media partners.');
  String get mediaPartnersEmpty =>
      _t('لا يوجد شركاء إعلاميون', 'No media partners');

  // Notifications (Page 033).
  String get notificationsTitle => _t('الإشعارات', 'Notifications');
  String get notificationsEmpty =>
      _t('لا توجد إشعارات بعد', 'No notifications yet');
  String get notificationsError =>
      _t('تعذّر تحميل إشعاراتك.', 'Could not load your notifications.');
  String get notificationsMarkAll => _t('تعليم الكل كمقروء', 'Mark all read');
  String get notificationsMarkAllFailed =>
      _t('تعذّر تعليم الإشعارات كمقروءة.', 'Could not mark the notifications read.');
  String get notificationsSearchHint => _t('البحث', 'Search');
  String get notificationsFilterAll => _t('الكل', 'All');
  String get notificationsFilterSessions => _t('جلسات', 'Sessions');
  String get notificationsFilterVip => _t('VIP', 'VIP');
  String get notificationsNoMatches =>
      _t('لا توجد إشعارات مطابقة', 'No matching notifications');
  String get dayToday => _t('اليوم', 'Today');
  String get dayYesterday => _t('أمس', 'Yesterday');

  // Meet people (Page 035).
  String get meetPeopleTitle => _t('قابل أشخاص مثلك', 'Meet people');
  String get meetPeopleEmpty => _t('لا توجد تطابقات بعد', 'No matches yet');
  String get meetPeopleError =>
      _t('تعذّر تحميل التطابقات الخاصة بك.', 'Could not load your matches.');
  String meetPeopleSharedInterests(int count) =>
      _t('$count اهتمامات مشتركة', '$count shared interests');

  // Accessibility (Page 038 — client-local settings, no API).
  String get accessibilityTitle => _t('إمكانية الوصول', 'Accessibility');
  String get accessibilityIntro => _t(
        'اضبط تجربة العرض بما يناسبك. هذه الإعدادات محلية على جهازك.',
        'Adjust the display to suit you. These settings are local to your device.',
      );
  String get accessibilityTextSizeLabel => _t('حجم النص', 'Text size');
  String get accessibilityTextSizeSmall => _t('صغير', 'Small');
  String get accessibilityTextSizeDefault => _t('افتراضي', 'Default');
  String get accessibilityTextSizeLarge => _t('كبير', 'Large');
  String get accessibilityHighContrastTitle =>
      _t('تباين عالٍ', 'High contrast');
  String get accessibilityHighContrastSubtitle => _t(
        'يزيد التباين بين النص والخلفية لتسهيل القراءة.',
        'Increases the contrast between text and background for easier reading.',
      );
  String get accessibilityReduceMotionTitle =>
      _t('تقليل الحركة', 'Reduce motion');
  String get accessibilityReduceMotionSubtitle => _t(
        'يقلل الرسوم المتحركة والانتقالات في التطبيق.',
        'Reduces animations and transitions across the app.',
      );

  // More hub (Page 041) — navigation tiles + static version line.
  String get moreTitle => _t('المزيد', 'More');
  String get moreAbout => _t('عن الملتقى', 'About the forum');
  String get moreAccessibility => _t('إمكانية الوصول', 'Accessibility');
  String get moreTerms => _t('الشروط والأحكام', 'Terms & conditions');
  String get moreRate => _t('تقييم', 'Rate');
  String get moreNotifications => _t('الإشعارات', 'Notifications');
  String get moreMediaPartners => _t('الشركاء الإعلاميون', 'Media partners');
  String get moreVersion => _t('الملتقى البحري v0.1.0', 'SIMF v0.1.0');

  // Guest mode (Page 012 — informational entry).
  String get guestModeTitle => _t('وضع الضيف', 'Guest mode');
  String get guestModeHeadline => _t('التصفح كضيف', 'Browsing as guest');
  String get guestModeBrowseBody => _t(
        'يمكنك كضيف تصفّح الجلسات والمتحدثين والخريطة التفاعلية والوسائط.',
        'As a guest you can browse the sessions, speakers, the venue map and the media.',
      );
  String get guestModeSignInBody => _t(
        'سجّل الدخول للحصول على بطاقتك الذكية والإشعارات الشخصية وحجز المقاعد.',
        'Sign in to get your smart badge, personal notifications and booking.',
      );
  String get guestModeContinueButton =>
      _t('المتابعة كضيف', 'Continue as guest');
  String get guestModeSignInButton => _t('تسجيل الدخول', 'Sign in');

  // AI session summary (Page 034).
  String get aiSummaryTitle => _t('ملخص الجلسة', 'AI session summary');
  String get aiSummaryOpenFromSession => _t(
        'افتح ملخص جلسة من صفحة الجلسة.',
        'Open a session summary from a session.',
      );
  String get aiSummaryNone =>
      _t('لا يوجد ملخص منشور بعد.', 'No published summary yet.');
  String get aiSummaryError =>
      _t('تعذر تحميل الملخص.', 'Could not load the summary.');
  String get aiSummaryGeneratedBanner =>
      _t('تم إنشاؤه بواسطة الذكاء الاصطناعي', 'Generated by AI');
  String get aiSummaryKeyPointsHeading => _t('أبرز النقاط', 'Key points');
  String get aiSummaryRecommendationsHeading =>
      _t('التوصيات', 'Recommendations');
  String get aiSummarySpeakersHeading => _t('المتحدثون', 'Speakers');
  String get aiSummaryFullTextHeading => _t('النص الكامل', 'Full text');

  // Send a question (Page 026 — live Q&A composer).
  String get sendQuestionTitle => _t('إرسال سؤال', 'Send a question');
  String get sendQuestionNoSession => _t(
        'افتح هذه الشاشة من جلسة مباشرة لإرسال سؤال.',
        'Open this from a live session to send a question.',
      );
  String get sendQuestionRecipientLabel => _t('إلى من؟', 'Send to');
  String get sendQuestionToSpeaker => _t('المتحدث', 'Speaker');
  String get sendQuestionToHost => _t('المضيف', 'Host');
  String get sendQuestionFieldLabel => _t('سؤالك', 'Your question');
  String get sendQuestionHint =>
      _t('اكتب سؤالك هنا…', 'Type your question here…');
  String get sendQuestionEmpty =>
      _t('اكتب سؤالك أولاً', 'Type your question first');
  String get sendQuestionSubmit => _t('إرسال السؤال', 'Send question');
  String get sendQuestionSent => _t('تم إرسال سؤالك', 'Your question was sent');
  String get sendQuestionNotOpen => _t(
        'الأسئلة مفتوحة فقط من 5 دقائق قبل بدء الجلسة حتى انتهائها.',
        'Questions are only open from 5 minutes before the session until it ends.',
      );
  String get sendQuestionFailed => _t(
        'تعذر إرسال سؤالك. حاول مرة أخرى.',
        'Could not send your question. Try again.',
      );
  String get sendQuestionWindowHint => _t(
        'تتم مراجعة الأسئلة قبل عرضها على الهواء.',
        'Questions are reviewed before going on air.',
      );

  // Audience comments (Page 028).
  String get commentsTitle => _t('تعليقات الجمهور', 'Audience comments');
  String get commentsNoSession =>
      _t('افتح هذه الشاشة من جلسة مباشرة.', 'Open this from a live session.');
  String get commentsError =>
      _t('تعذّر تحميل التعليقات.', 'Could not load the comments.');
  String get commentsEmpty => _t('لا توجد تعليقات بعد', 'No comments yet');
  String get commentBodyHint => _t('اكتب تعليقك…', 'Write your comment…');
  String get commentSend => _t('إرسال', 'Send');
  String get commentSubmitted =>
      _t('تم إرسال تعليقك', 'Your comment was submitted');
  String get commentSubmittedPending => _t(
        'تم إرسال تعليقك وهو قيد المراجعة.',
        'Your comment was submitted and is awaiting moderation.',
      );
  String get commentSubmitFailed => _t(
        'تعذّر إرسال التعليق. حاول مرة أخرى.',
        'Could not submit the comment. Try again.',
      );

  // Entry badge (Page 032).
  String get badgeTitle => _t('بطاقة الدخول', 'Entry badge');
  String get badgeShowAtEntry =>
      _t('أبرز هذه البطاقة عند الدخول', 'Show this at entry');
  String get badgePendingBody => _t(
        'ستتوفر بطاقتك بعد اعتماد حسابك.',
        'Your badge is available once your account is approved.',
      );
  String get badgeError => _t('تعذّر تحميل بطاقتك.', 'Could not load your badge.');
  String get badgeNotApprovedBody => _t(
        'حسابك غير معتمد بعد. ستتوفر بطاقة الدخول بعد اعتماد حسابك.',
        'Your account is not approved yet. Your entry badge will be available '
            'once your account is approved.',
      );
  // KSA Wave-2 frame 221:769 copy.
  String get badgeScanToEnter => _t('امسح للدخول', 'Scan to enter');
  String get badgeAddPerson => _t('امسح لإضافة شخص', 'Scan to add a contact');

  // D-426 — QR-page role actions + exhibitor lead capture.
  String get badgeScanVisitor =>
      _t('مسح بطاقة زائر', 'Scan visitor badge');
  String get myVisitorsTitle => _t('زواري', 'My Visitors');
  String get myVisitorsEmpty => _t(
        'لم تقم بمسح أي زائر بعد. امسح بطاقة زائر لإضافته هنا.',
        'No visitors yet. Scan a visitor badge to capture them here.',
      );
  String get scanVisitorTitle => _t('مسح بطاقة زائر', 'Scan visitor badge');
  String get scanVisitorCaptured =>
      _t('تمت إضافة الزائر إلى زواري', 'Visitor added to My Visitors');
  String get scanVisitorNotFound =>
      _t('لا توجد بطاقة زائر مطابقة', 'No matching visitor badge');
  String get scanVisitorForbidden => _t(
        'مسح بطاقات الزوار متاح لحسابات العارضين فقط.',
        'Only exhibitor accounts can scan visitor badges.',
      );
  String get scanVisitorError =>
      _t('تعذر مسح البطاقة. حاول مرة أخرى.', 'Could not scan the badge. Try again.');

  // Live broadcast (Page 025). liveNowLabel already exists (reused for the badge).
  String get liveBroadcastTitle => _t('البث المباشر', 'Live broadcast');
  // D-433 — live broadcast + ask-question + media-coverage re-skins
  // (Figma 934-3450 / 934-3636 / 947-3764 / 958-2246).
  String get liveNowBroadcasting => _t('يُبث الآن', 'Now broadcasting');
  String get liveSessionLabel => _t('الجلسة', 'Session');
  String get liveCaptionHint => _t(
        'الترجمة الفورية للنص المنطوق تظهر هنا...',
        'Live captions of the spoken word appear here…',
      );
  String get liveRegionNoticeLabel => _t('إشعار:', 'Notice:');
  String get liveRegionNoticeBody => _t(
        'البث المباشر متاح داخل منطقة الرياض فقط حسب لوائح التنظيم.',
        'Live broadcasting is available only inside the Riyadh region per the '
            'organising regulations.',
      );
  String get liveAskQuestion => _t('اطرح سؤالاً', 'Ask a question');
  String get sendQuestionSectionLabel => _t('الاسئلة', 'Questions');
  String get sendQuestionNoteLabel => _t('ملاحظة', 'Note');
  String get mediaCoverageTitle => _t('التغطية الإعلامية', 'Media coverage');
  String get galleryImagesSection => _t('الصور', 'Images');
  String get galleryVideosSection => _t('الفيديوهات', 'Videos');
  String get liveNoSessionSelected => _t(
        'لا توجد جلسة بث محددة — افتح جلسة لمشاهدتها.',
        'No live session selected — open a session to watch.',
      );
  String get liveBroadcastError =>
      _t('تعذّر تحميل البث المباشر.', 'Could not load the live broadcast.');
  String get liveRecordingAvailable => _t(
        'يتوفر تسجيل لهذه الجلسة.',
        'A recording of this session is available.',
      );
  String get liveNotLiveYet => _t(
        'هذه الجلسة لا تُبَث حالياً.',
        'This session is not broadcasting right now.',
      );
  String get liveSignLanguageAvailable => _t(
        'تتوفر ترجمة بلغة الإشارة.',
        'Sign-language interpretation is available.',
      );
  // Live feed toggle (Page 025, D-349) — swaps the player between the main feed
  // and the sign-language feed when the session carries both.
  String get liveFeedMain => _t('البث', 'Main feed');
  String get liveFeedSignLanguage => _t('لغة الإشارة', 'Sign language');
  String get liveFeedError => _t(
        'تعذّر تشغيل هذا البث. حاول مرة أخرى.',
        'Could not play this feed. Try again.',
      );

  // AI assistant (Page 036) — interim shell; no backend chatbot endpoint.
  String get chatbotTitle => _t('المساعد الذكي', 'AI assistant');
  String get chatbotPreviewBanner => _t(
        'المساعد الذكي في وضع المعاينة — الردود مؤقتة.',
        'The AI assistant is in preview — replies are interim.',
      );
  String get chatbotEmpty =>
      _t('اسأل المساعد للبدء.', 'Ask the assistant to get started.');
  String get chatbotInputHint => _t('اكتب رسالتك…', 'Type your message…');
  String get chatbotSendTooltip => _t('إرسال', 'Send');

  // Visitor contact sharing — FDS-014 (Share my contact / Scan / My Contacts).
  String get shareMyContactTitle => _t('شارك جهة اتصالي', 'Share my contact');
  String get shareMyContactHint => _t(
        'اعرض رمز QR لزائر آخر ليحفظ بطاقتك، أو شاركها كملف vCard.',
        'Show this QR to another visitor to save your card, or share it as a vCard.',
      );
  String get shareMyContactRotate => _t('تدوير الرمز', 'Rotate code');
  String get shareMyContactRotateConfirmTitle =>
      _t('تدوير رمز المشاركة؟', 'Rotate share code?');
  String get shareMyContactRotateConfirmBody => _t(
        'سيتوقف الرمز السابق عن العمل ولن يتمكن أحد من حفظه بعد ذلك.',
        'The previous code will stop working and can no longer be saved by anyone.',
      );
  String get shareMyContactRotated =>
      _t('تم إنشاء رمز جديد', 'A new code was generated');
  String get shareMyContactError =>
      _t('تعذر تحميل رمز المشاركة.', 'Could not load your share code.');

  String get scanContactTitle => _t('مسح رمز QR', 'Scan QR');
  String get scanContactManualLabel =>
      _t('أو أدخل الرمز يدوياً', 'Or enter the code manually');
  String get scanContactManualField => _t('رمز المشاركة', 'Share code');
  String get scanContactResolve => _t('بحث', 'Look up');
  String get scanContactNotFound =>
      _t('رمز غير صالح أو لم يعد متاحاً.', 'Code not found or no longer valid.');
  String get scanContactError =>
      _t('تعذر قراءة جهة الاتصال.', 'Could not read the contact.');
  String get scanContactCameraUnavailable =>
      _t('الكاميرا غير متاحة', 'Camera unavailable');
  // Shared by all QR scanners: the camera starts only on tap so the on-screen
  // back/cancel stays usable on devices where the live camera grabs taps (D-426).
  String get scanStartCamera =>
      _t('اضغط لمسح الرمز بالكاميرا', 'Tap to scan with the camera');
  String get scanStopCamera => _t('إيقاف الكاميرا', 'Stop camera');

  String get contactPreviewTitle => _t('معاينة جهة الاتصال', 'Contact preview');
  String get saveContactLabel =>
      _t('حفظ في جهات اتصالي', 'Save to My Contacts');
  String get saveContactNoteHint => _t('ملاحظة (اختياري)', 'Note (optional)');
  String get saveContactSaved => _t('تم حفظ جهة الاتصال', 'Contact saved');
  String get saveContactSelf =>
      _t('لا يمكنك حفظ بطاقتك أنت.', 'You can’t save your own card.');
  String get saveContactError =>
      _t('تعذر حفظ جهة الاتصال.', 'Could not save the contact.');

  String get myContactsTitle => _t('جهات اتصالي', 'My Contacts');
  String get myContactsEmpty =>
      _t('لا توجد جهات اتصال محفوظة بعد', 'No saved contacts yet');
  String get myContactsEmptyHint => _t(
        'امسح رمز QR لزائر آخر لحفظ بطاقته.',
        'Scan another visitor’s QR to save their card.',
      );
  String get myContactsError =>
      _t('تعذر تحميل جهات الاتصال.', 'Could not load your contacts.');
  String get myContactsRemove => _t('إزالة', 'Remove');
  String get myContactsRemoveConfirmTitle =>
      _t('إزالة جهة الاتصال؟', 'Remove contact?');
  String get myContactsRemoveConfirmBody => _t(
        'ستتم إزالة جهة الاتصال هذه من قائمتك.',
        'This contact will be removed from your list.',
      );
  String get myContactsRemoved => _t('تمت الإزالة', 'Removed');
  String get myContactsExportVcard => _t('تصدير vCard', 'Export vCard');
  String get contactScanAdd => _t('مسح للإضافة', 'Scan to add');
  String get contactUnavailable =>
      _t('هذه الجهة لم تعد متاحة', 'This contact is no longer available');
  String get contactNoteLabel => _t('ملاحظة', 'Note');
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
