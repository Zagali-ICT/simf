// The 80-column rule does not apply to a translation table: you cannot shorten
// a sentence to fit a column, and the alternative - adjacent-string splitting
// like `'... and ' 'try again.'` - would permanently break exact-match grep on
// the very strings the E2E catalogues assert on screen. Every row over the
// limit in this file is a user-facing sentence, checked before this was added.
// ignore_for_file: lines_longer_than_80_chars

import 'package:flutter/widgets.dart';

/// Hand-rolled localisation lookup — every user-facing string in the app.
///
/// SIMF-MAA-001 §10 specifies `intl` + ARB files as the long-term path, and
/// the `.arb` files in `l10n/` remain the source of truth for translations.
/// The strings are mirrored here so the build does not depend on the
/// `flutter gen-l10n` step. When the project moves to generated localisation,
/// the call sites (`AppL10n.of(context).xxx`) stay; only the implementation
/// switches.
///
/// This header used to describe a "WS3 skeleton" holding only the strings that
/// skeleton needed, and to promise per-screen strings would not be added here
/// because the `mkp_*` screens carried their own copy. Both claims are now
/// false: the `mkp_*` screens are gone, and this file carries roughly 1,000
/// per-screen getters. Corrected 2026-08-13 — the file IS the app's string
/// surface, and its size follows from that; it is not a defect to be split.
class AppL10n {
  const AppL10n(this.locale);

  final Locale locale;

  /// The nearest [AppL10n], or the Arabic default when none is installed.
  // Deliberately a static method, not the factory constructor the analyzer
  // asks for: `X.of(context)` is the Flutter lookup idiom (`Theme.of`,
  // `MediaQuery.of`, `Localizations.of`), and a lookup is not construction -
  // it usually returns an instance somebody else already built.
  // ignore: prefer_constructors_over_static_methods
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

  /// The current UI language's own name, for the language-toggle chip.
  ///
  /// A language switcher shows the AUTONYM: Arabic is labelled "العربية" and
  /// English "English" whatever the active locale, which is why both sides are
  /// spelled out here rather than translated.
  String get currentLanguageAutonym => _t('العربية', 'English');

  /// The gold "AI" badge on an assistant chat bubble. Kept as the Latin
  /// initialism in both languages, as the design frame renders it.
  String get aiBadgeLabel => _t('AI', 'AI');
  String get comingSoonTitle => _t('قريباً', 'Coming soon');
  String get comingSoonBody => _t(
        'هذه الشاشة قيد التطوير. سيتم استبدالها بنسخة UI/UX النهائية لاحقاً.',
        'This screen is under construction. It will be replaced by the final UI/UX shortly.',
      );
  String get backLabel => _t('رجوع', 'Back');
  String get continueLabel => _t('متابعة', 'Continue');
  String get cancelLabel => _t('إلغاء', 'Cancel');
  // External-link confirmation (owner 2026-06-27) — every external link asks
  // before it leaves the app.
  String get externalLinkTitle => _t('فتح رابط خارجي', 'Open external link');
  String get externalLinkBody => _t(
        'سيتم نقلك خارج التطبيق إلى موقع خارجي. هل تريد المتابعة؟',
        'This will take you out of the app to an external site. Continue?',
      );
  String get externalLinkOpen => _t('فتح', 'Open');
  String get homePendingApprovalNote => _t(
        'حسابك قيد المراجعة. ستُفعَّل كل الميزات بعد الموافقة على تسجيلك.',
        'Your account is awaiting approval. Full features unlock once your '
            'registration is approved.',
      );
  String get retryLabel => _t('إعادة المحاولة', 'Retry');
  // Full-size image viewer (owner 2026-07-26) — opened by tapping any logo /
  // photo box rendered through SimfLogoImage.
  String get imageViewerCloseLabel => _t('إغلاق الصورة', 'Close image');
  String get imageViewerLoadFailed =>
      _t('تعذّر تحميل الصورة.', 'Could not load the image.');
  String get loadingLabel => _t('جارٍ التحميل…', 'Loading…');
  String get lookupLoadError =>
      _t('تعذر تحميل القائمة.', 'Could not load the list.');
  String get errorTitle => _t('حدث خطأ', 'Something went wrong');
  String get networkErrorBody => _t(
        'تعذر الاتصال بالخادم. تحقق من الاتصال بالإنترنت وحاول مرة أخرى.',
        'Could not reach the server. Check your internet connection and try again.',
      );
  // Shown for a client-synthesized API failure (no backend envelope): a
  // malformed / non-JSON response (proxy or server outage) and any unexpected
  // client-side error — so an Arabic user never sees a raw English dev string.
  String get errorServerUnavailable => _t(
        'تعذّر الوصول إلى الخادم. حاول مرة أخرى لاحقًا.',
        'Could not reach the server. Please try again later.',
      );
  String get errorGenericBody => _t(
        'حدث خطأ غير متوقع. حاول مرة أخرى.',
        'Something went wrong. Please try again.',
      );

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

  String get onboardingSkip => _t('تخطي', 'Skip');
  String get onboardingNext => _t('التالي', 'Next');
  String get onboardingGetStarted => _t('ابدأ', 'Get started');
  String get onboardingTitle1 => _t(
        'مرحباً بك في تطبيق الملتقى',
        'Welcome to the SIMF app',
      );
  // Step titles + bodies — the KSA-Project onboarding copy (Figma 148:22 /
  // 159:942 / 159:1052, D-362). One title per step (DEF-ONB-006).
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
  String get signUpTitle => _t('إنشاء حساب', 'Sign up');
  String get signUpButton => _t('إنشاء حساب', 'Create account');
  String get invalidEmail => _t('بريد إلكتروني غير صالح', 'Invalid email');
  String get passwordPolicyError => _t(
        'يجب أن تحقّق كلمة المرور الشروط التالية:\n'
            '• من ٨ إلى ١٢٨ حرفًا\n'
            '• حرف كبير واحد على الأقل\n'
            '• حرف صغير واحد على الأقل\n'
            '• رقم واحد على الأقل\n'
            '• رمز خاص واحد على الأقل',
        'Your password must meet all of the following:\n'
            '• 8 to 128 characters\n'
            '• at least one upper-case letter\n'
            '• at least one lower-case letter\n'
            '• at least one digit\n'
            '• at least one special character',
      );
  String get passwordLength => _t(
        'من ٨ إلى ١٢٨ حرفًا',
        '8 to 128 characters',
      );
  String get passwordUppercase => _t(
        'حرف كبير واحد على الأقل',
        'at least one upper-case letter',
      );
  String get passwordLowercase => _t(
        'حرف صغير واحد على الأقل',
        'at least one lower-case letter',
      );
  String get passwordDigit => _t(
        'رقم واحد على الأقل',
        'at least one digit',
      );
  String get passwordSpecial => _t(
        'رمز خاص واحد على الأقل',
        'at least one special character',
      );
  String get signUpCheckEmail =>
      _t('تحقق من بريدك الإلكتروني', 'Check your email');

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

  String get haveAccountQuestion => _t('هل لديك حساب ؟', 'Have an account?');

  String get signUpVisitorTitle => _t('إنشاء حساب · زائر', 'Sign up — visitor');
  String get profileSectionPersonal => _t('البيانات الشخصية', 'Personal');
  String get profileSectionAffiliation => _t('الجهة والفئة', 'Affiliation');
  String get profileSectionInterests => _t('الاهتمامات', 'Interests');
  String get profileLoadError =>
      _t('تعذر تحميل النموذج.', 'Could not load the form.');
  String get arabicNameLabel =>
      _t('الاسم الكامل (بالعربية)', 'Full name (Arabic)');
  String get englishNameLabel =>
      _t('الاسم الكامل (بالإنجليزية)', 'Full name (English)');
  String get jobTitleLabel =>
      _t('المسمى الوظيفي (بالإنجليزية)', 'Job title (English)');
  String get jobTitleArabicLabel =>
      _t('المسمى الوظيفي (بالعربية)', 'Job title (Arabic)');
  String get nationalityLabel => _t('الجنسية', 'Nationality');
  String get isSaudiLabel => _t('سعودي الجنسية', 'Saudi national');
  String get nationalIdLabel => _t('رقم الهوية الوطنية', 'National ID');
  String get documentTypeLabel => _t('نوع الوثيقة', 'Document type');
  String get iqamaSegment => _t('الإقامة', 'Iqama');
  String get passportSegment => _t('جواز السفر', 'Passport');
  String get iqamaNumberLabel => _t('رقم الإقامة', 'Iqama number');
  String get passportNumberLabel => _t('رقم جواز السفر', 'Passport number');
  String get saudiMobileLabel => _t('رقم الجوال', 'Mobile');
  String get internationalMobileLabel =>
      _t('رقم الجوال الدولي', 'International mobile');
  String get dateOfBirthLabel => _t('تاريخ الميلاد', 'Date of birth');
  String get placeOfBirthLabel => _t('مكان الميلاد', 'Place of birth');
  // D-469 — Saudi → region dropdown; others → free text "as in passport".
  String get placeOfBirthRegionHint => _t('اختر المنطقة', 'Select region');
  String get placeOfBirthPassportHint =>
      _t('كما في جواز السفر', 'As in your passport');
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
  String get profileTypeLabel => _t('الفئة', 'Profile type');
  // D-471 — hint for the profile-type searchable picker sheet.
  String get profileTypeSearchHint =>
      _t('ابحث عن الفئة', 'Search profile type');
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
  // #14 — edit-mode success toast (My interests screen opened from My-Area).
  String get interestsUpdatedToast =>
      _t('تم تحديث اهتماماتك', 'Your interests were updated');
  // Owner 2026-07-26 — add / edit the mobile number from the profile. Validation
  // only: there is deliberately NO OTP / verification step on this screen.
  String get myMobileTitle => _t('رقم الجوال', 'Mobile number');
  String get myMobileHelper => _t(
        'أضف أو عدّل رقم جوالك. يُستخدم للتواصل معك بخصوص الملتقى.',
        'Add or edit your mobile number. It is used to contact you about the '
            'forum.',
      );
  String get myMobileCurrentLabel => _t('الرقم الحالي', 'Current number');
  String get myMobileNoneYet => _t('لم يُضف بعد', 'Not added yet');
  String get myMobileUpdatedToast =>
      _t('تم تحديث رقم الجوال', 'Your mobile number was updated');
  String get attachIdImageLabel => _t('إرفاق صورة الهوية', 'Attach ID image');
  // KSA-Project profile frame copy (Figma 168:2972, D-368).
  String get createProfileTitle => _t('إنشاء ملف شخصى', 'Create profile');
  String get documentNumberLabel => _t('رقم الوثيقة', 'Document number');
  String get attachmentsLabel => _t(
        'المرفقات (صورة الهوية / الإقامة / الجواز)',
        'Attachments (ID / Iqama / passport image)',
      );
  String get attachFileLabel => _t('إرفاق ملف', 'Attach file');
  // BUG-019 / 19f — a walk-in desk has to be able to shoot the document on the
  // spot, so an attachment offers the camera as well as a file pick.
  String get attachSourceTitle => _t('إضافة صورة', 'Add an image');
  String get attachFromCamera => _t('التقاط بالكاميرا', 'Take a photo');
  String get attachFromFile => _t('اختيار ملف', 'Choose a file');
  String get termsAgreeQuestion => _t(
        'الموافقة على الشروط والأحكام؟',
        'Agree to the terms & conditions?',
      );
  String get idImageAttachedLabel => _t('تم إرفاق الصورة', 'Image attached');
  String get removeLabel => _t('إزالة', 'Remove');
  String get clearLabel => _t('مسح', 'Clear');
  String get saveLabel => _t('حفظ', 'Save');
  // Page 007 advances to the interests screen with Next (D-332); Page 007‑01
  // title.
  String get nextLabel => _t('التالي', 'Next');
  String get interestsTitle => _t('اهتماماتي', 'My interests');
  String get profileSavedToast => _t('تم حفظ الملف الشخصى', 'Profile saved');
  String get idImageUploadFailed => _t(
        'تم حفظ الملف الشخصى، لكن تعذر رفع الصورة. حاول لاحقًا.',
        'Profile saved, but the image upload failed. Try again later.',
      );
  String get requiredField => _t('هذا الحقل مطلوب', 'This field is required');
  // D-434 — shown as a banner when the user is routed back to complete their
  // profile, and as a toast when Next is blocked, so the missing required
  // items get clear attention instead of failing silently.
  String get completeProfilePrompt => _t(
        'يرجى إكمال الحقول المطلوبة أدناه لإنهاء ملفك الشخصي.',
        'Please complete the required fields below to finish your profile.',
      );
  String get nationalityRequired =>
      _t('الجنسية مطلوبة', 'Nationality is required');
  // D-723 — create-profile now requires every field except the plate number.
  String get jobTitleRequired =>
      _t('المسمى الوظيفي مطلوب', 'Job title is required');
  String get placeOfBirthRequired =>
      _t('مكان الميلاد مطلوب', 'Place of birth is required');
  String get mobileRequired =>
      _t('رقم الجوال مطلوب', 'Mobile number is required');
  // BUG-019 / 19m — the validator also applies the Luhn mod-10 check digit
  // (`isValidNationalId`), so a number that matches the "10 digits starting
  // with 1" shape can still be rejected. Say so, or the message reads as a lie.
  String get nationalIdInvalid => _t(
        'رقم الهوية الوطنية غير صحيح (10 أرقام تبدأ بـ 1 مع رقم تحقق صحيح)',
        'Invalid national ID (10 digits starting with 1, with a valid check '
            'digit)',
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
  String get searchCountryHint => _t('ابحث عن الجنسية', 'Search for a country');
  // C7 (D-371) — the male-mandatory camera photo + face check.
  String get idImageRequiredForMen => _t(
        'الصورة الشخصية مطلوبة — التقطها بالكاميرا',
        'A photo is required — capture it with the camera',
      );
  String get noFaceDetectedError => _t(
        'لم يتم التعرف على وجه في الصورة — أعد التقاط صورة واضحة للوجه',
        'No face was detected in the photo — retake a clear photo of the face',
      );
  // Two-photo split (D-431-follow-up) — the ID document (gallery, all) + the
  // face photo (live capture → avatar; men-required, women-optional).
  String get idImageRequired =>
      _t('صورة الهوية مطلوبة', 'An ID image is required');
  String get facePhotoLabel => _t('الصورة الشخصية (الوجه)', 'Face photo');
  String get facePhotoRequiredForMen => _t(
        'الصورة الشخصية مطلوبة — التقطها بالكاميرا',
        'A face photo is required — capture it with the camera',
      );
  String get facePhotoOptionalForWomen =>
      _t('الصورة الشخصية اختيارية', 'Face photo (optional)');
  String get facePhotoCaptureLabel =>
      _t('التقاط صورة الوجه', 'Capture face photo');
  String get facePhotoCaptured =>
      _t('تم التقاط الصورة الشخصية', 'Face photo captured');
  String get retakeLabel => _t('إعادة الالتقاط', 'Retake');
  String get facePhotoUploadFailed => _t(
        'تعذّر رفع الصورة الشخصية. حاول مرة أخرى.',
        "Couldn't upload the face photo. Try again.",
      );
  // Name rules — Arabic-only / English-only, full name of 2 to 4 parts (D-459).
  String get arabicNameLettersOnly => _t(
        'يجب أن يحتوي الاسم بالعربية على حروف عربية فقط',
        'The Arabic name must contain Arabic letters only',
      );
  String get englishNameLettersOnly => _t(
        'يجب أن يحتوي الاسم بالإنجليزية على حروف إنجليزية فقط',
        'The English name must contain English letters only',
      );
  String get fullNameParts => _t(
        'أدخل الاسم الكامل (مقطعان على الأقل)',
        'Enter your full name (at least 2 parts)',
      );
  // C6 (D-371/D-459) — رقم اللوحة, optional; Saudi 17-letter set when filled.
  String get plateNumberLabel =>
      _t('رقم اللوحة (اختياري)', 'Plate number (optional)');
  String get plateNumberInvalid => _t(
        'أدخل رقم لوحة صحيح: حروف لوحات سعودية و/أو أرقام',
        'Enter a valid plate: Saudi plate letters and/or digits',
      );
  // C6 (D-459) — the plate letter dropdowns + the digits field.
  String get plateLetterHint => _t('حرف', 'Letter');
  String get plateDigitsLabel => _t('الأرقام', 'Digits');
  String get plateDigitsHint => _t('١-٤ أرقام', '1–4 digits');
  // C5 (D-371) — under "Other" the profile-type pick is required.
  String get profileTypeRequired =>
      _t('يجب اختيار الفئة', 'A profile type selection is required');
  // C4 (D-371) — the standard phone shapes, mirrored client/server.
  String get saudiMobileInvalid => _t(
        'أدخل الرقم بصيغة 05XXXXXXXX أو +9665XXXXXXXX أو 009665XXXXXXXX',
        'Enter as 05XXXXXXXX or +9665XXXXXXXX or 009665XXXXXXXX',
      );
  String get internationalMobileInvalid => _t(
        'أدخل الرقم بصيغة دولية: 00 أو + ثم رمز الدولة والرقم، مثل 00966XXXXXXXXX أو +966XXXXXXXXX',
        'Use international format: 00 or + then country code and number, e.g. 00966XXXXXXXXX or +966XXXXXXXXX',
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

  String get termsTitle => _t('الشروط والأحكام', 'Terms & conditions');
  String termsLastUpdated(String date) =>
      _t('آخر تحديث · $date', 'Last updated · $date');
  String get termsEmpty => _t('لا يوجد محتوى', 'No content');
  String get termsAcceptCheckbox => _t(
        'أوافق على الشروط والأحكام',
        'I accept the terms and conditions',
      );
  // Sign-up mandatory-accept checkbox (D-719): the lead text before the
  // tappable الشروط والأحكام link, and the error when it is left unchecked.
  String get termsAcceptLead => _t('أوافق على', 'I accept the');
  String get termsMustAccept => _t(
        'يجب الموافقة على الشروط والأحكام',
        'You must accept the terms and conditions',
      );
  // KSA-Project terms frame copy (Figma 505:1553, D-367).
  String get termsImportantInfoTitle => _t(
        'معلومات هامة لزوار الملتقى',
        'Important information for forum visitors',
      );
  String get termsAcceptButton => _t('موافق', 'Agree');
  String get declineLabel => _t('رفض', 'Decline');

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
  String get contactUsTitle => _t('تواصل معنا', 'Contact us');
  // تواصل معنا — Contact us screen (Figma 1388-7711; POST /app/contact-inquiry).
  String get contactSendTitle => _t('أرسل رسالة', 'Send a message');
  String get contactNameLabel => _t('الاسم', 'Name');
  String get contactNameHint => _t('أدخل اسمك الكامل', 'Enter your full name');
  String get contactNameRequired => _t('الاسم مطلوب', 'Name is required');
  String get contactEmailLabel => _t('البريد الإلكتروني', 'Email');
  String get contactEmailHint => _t('example@email.com', 'example@email.com');
  String get contactEmailInvalid =>
      _t('بريد إلكتروني صالح مطلوب', 'A valid email is required');
  String get contactMessageLabel => _t('الرسالة', 'Message');
  String get contactMessageHint =>
      _t('اكتب رسالتك هنا...', 'Write your message here…');
  String get contactMessageRequired =>
      _t('الرسالة مطلوبة', 'Message is required');
  String get contactSendButton => _t('إرسال', 'Send');
  String get contactInfoTitle => _t('معلومات التواصل', 'Contact information');
  String get contactHotlineLabel => _t('الخط الساخن', 'Hotline');
  String get contactLocationLabel => _t('الموقع', 'Location');
  String get contactSocialTitle =>
      _t('وسائل التواصل الاجتماعي', 'Social media');
  String get contactSentToast => _t(
        'تم إرسال رسالتك. شكراً لتواصلك معنا.',
        'Your message has been sent. Thank you for contacting us.',
      );
  String get contactSendFailed => _t(
        'تعذّر إرسال رسالتك. حاول مرة أخرى.',
        'Could not send your message. Please try again.',
      );
  String get simfSocialFooter => _t(
        '@SIMF_RSNF · الملتقى البحري السعودي الدولي',
        '@SIMF_RSNF · Saudi International Maritime Forum',
      );

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
  // D-726 (item 11) — the idle session-timeout warning (SessionGuard).
  String get sessionExpiryTitle => _t('هل ما زلت هنا؟', 'Are you still there?');
  String sessionExpiryCountdown(int seconds) => _t(
        'ستُنهى جلستك خلال $seconds ثانية بسبب عدم النشاط.',
        'Your session will end in $seconds seconds due to inactivity.',
      );
  String get sessionStaySignedIn => _t('البقاء مسجّلاً', 'Stay signed in');

  String get signInTitle => _t('تسجيل الدخول', 'Sign in');
  String get emailLabel => _t('البريد الإلكتروني', 'Email');

  /// Placeholder shown inside an empty email field. The example address stays
  /// Latin in both languages, as an address itself would be.
  String get emailHintExample => _t('example@email.com', 'example@email.com');
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
  String get guestSignInLink => _t('الدخول كضيف', 'Enter as guest');
  String get signInForumTitle => _t('الملتقى الدولى البحرى', 'SIMF');
  String get rememberMeLabel => _t('تذكرنى', 'Remember me');
  String get orDividerLabel => _t('او', 'or');
  String get faceIdSignInButton =>
      _t('التسجيل ببصمة الوجه', 'Sign in with Face ID');

  String get biometricSignInTooltip =>
      _t('الدخول بالبصمة / الوجه', 'Sign in with biometrics');

  /// The SIGN-IN caller's copy for "the device can't do this" (D-422): no OS
  /// face/fingerprint is enrolled, or the OS sheet failed unexpectedly. It
  /// names the password form because that form is on the same screen as the
  /// Face-ID button — the enrol caller, which has no such form, passes
  /// [biometricUnavailableEnrol] instead.
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

  // Face-ID activation — the side-menu toggle + the one-time post-sign-in
  // prompt (D-441).
  String get biometricEnableToggle =>
      _t('الدخول ببصمة الوجه', 'Face ID sign-in');
  String get biometricPromptBody => _t(
        'استخدم بصمتك للدخول في المرة القادمة دون كلمة المرور.',
        'Use your face or fingerprint to sign in next time — no password needed.',
      );
  String get biometricPromptEnable => _t('تفعيل', 'Enable');

  // My Devices (S10) — every enrolled biometric credential on the account, with
  // a per-row revoke. A device key outlives a session revoke, so the owner needs
  // somewhere to see one they did not enrol.
  String get myDevicesTitle => _t('أجهزتي', 'My devices');
  String get myDevicesManage =>
      _t('إدارة الأجهزة المسجّلة', 'Manage enrolled devices');
  String get myDevicesEmpty => _t(
        'لا توجد أجهزة مسجّلة للدخول ببصمة الوجه.',
        'No devices are enrolled for biometric sign-in.',
      );
  String get myDevicesThisDevice => _t('هذا الجهاز', 'This device');
  String get myDevicesUnnamed => _t('جهاز غير مسمّى', 'Unnamed device');
  String get myDevicesAdded => _t('أُضيف في', 'Added');
  String get myDevicesLastUsed => _t('آخر استخدام', 'Last used');
  String get myDevicesNeverUsed => _t('لم يُستخدم بعد', 'Never used');
  String get myDevicesRevoked => _t('ملغى', 'Revoked');
  String get myDevicesRevokeTitle =>
      _t('إلغاء هذا الجهاز؟', 'Remove this device?');
  String get myDevicesRevokeBody => _t(
        'لن يتمكن هذا الجهاز من الدخول ببصمة الوجه بعد الآن.',
        'This device will no longer be able to sign in with biometrics.',
      );
  String get myDevicesRevokeThisDeviceBody => _t(
        'هذا هو جهازك الحالي. سيتم إيقاف الدخول ببصمة الوجه عليه، وستحتاج إلى كلمة المرور في المرة القادمة.',
        'This is the device you are using. Biometric sign-in will be turned off here and you will need your password next time.',
      );
  String get myDevicesRevokeConfirm => _t('إلغاء الجهاز', 'Remove device');
  String get myDevicesRevokedToast =>
      _t('تم إلغاء الجهاز', 'Device removed');
  String get biometricEnabledToast =>
      _t('تم تفعيل الدخول بالبصمة', 'Face ID sign-in enabled');
  String get biometricDisabledToast =>
      _t('تم إيقاف الدخول بالبصمة', 'Face ID sign-in turned off');
  // Disabling revokes the device key and wipes the local biometric credential —
  // confirm the destructive action first (owner 2026-06-21).
  String get biometricDisableConfirmTitle =>
      _t('إيقاف الدخول بالبصمة', 'Turn off Face ID sign-in');
  String get biometricDisableConfirmBody => _t(
        'سيتم حذف بيانات الدخول بالبصمة من هذا الجهاز نهائياً.',
        'Your Face ID sign-in data will be permanently deleted from this device.',
      );
  String get biometricDisableConfirmAction => _t('حذف', 'Delete');
  // #7a — enabling first confirms intent, then verifies an emailed step-up code
  // before the device key is enrolled.
  String get biometricEnableConfirmTitle =>
      _t('تفعيل الدخول ببصمة الوجه؟', 'Enable Face ID sign-in?');
  String get biometricEnableConfirmBody => _t(
        'سنرسل رمز تأكيد إلى بريدك الإلكتروني للتحقق من هويتك.',
        "We'll email you a confirmation code to verify it's you.",
      );
  String get biometricEnableConfirmAction => _t('متابعة', 'Continue');
  String get biometricStepUpTitle => _t('تأكيد بصمة الوجه', 'Confirm Face ID');
  String get biometricStepUpHeading =>
      _t('أدخل رمز التأكيد', 'Enter the confirmation code');
  String get biometricStepUpSendFailed => _t(
      'تعذّر إرسال الرمز. حاول مرة أخرى.',
      "Couldn't send the code. Try again.",);
  // D-738 — the OS device-credential confirm step at enrolment (banking flow:
  // emailed OTP → device PIN/biometric → enable) and its failure states.
  String get biometricLocalConfirmReason => _t(
        'أكّد قفل الشاشة أو بصمتك لتفعيل الدخول ببصمة الوجه',
        'Confirm your device PIN or biometric to enable Face ID sign-in',
      );
  String get biometricLocalConfirmCancelled => _t(
        'أُلغي التأكيد — لم يتم تفعيل الدخول ببصمة الوجه.',
        'Confirmation cancelled — Face ID sign-in was not enabled.',
      );
  String get biometricNoDeviceCredential => _t(
        'فعّل قفل الشاشة (رمز PIN أو نمط أو كلمة مرور) على جهازك أولاً ثم حاول مجدداً.',
        'Set a device screen lock (PIN, pattern or password) first, then try again.',
      );
  // The SIGN-IN caller's lockout copy: it names the password form because that
  // form is on the same screen the biometric button sits on. It must not name
  // the device PIN — `confirmDeviceIdentity` passes `biometricOnly: true`, so
  // the OS sheet never offers one, and telling a locked-out user to use it is a
  // dead end.
  String get biometricLockedOut => _t(
        'محاولات كثيرة خاطئة. المصادقة مقفلة مؤقتاً — حاول لاحقاً أو سجّل الدخول بكلمة المرور.',
        'Too many attempts. Authentication is temporarily locked — try again shortly, or sign in with your password.',
      );

  // The ENROLMENT caller's lockout copy. The user is already signed in on the
  // step-up screen and there is no password form to send them to; the OS
  // lockout clears on its own, so waiting is the whole recovery.
  String get biometricLockedOutEnrol => _t(
        'محاولات كثيرة خاطئة. المصادقة مقفلة مؤقتاً — حاول لاحقاً.',
        'Too many attempts. Authentication is temporarily locked — try again shortly.',
      );

  // The ENROLMENT caller's copy for the same "device can't do this" outcome.
  // Same reason as the lockout above: the user is already signed in, so the
  // sign-in copy's "sign in with your password" is advice they cannot act on.
  // It also does not send them to the device settings — the step-up is only
  // reachable when an enrolled biometric was found, so what lands here is an
  // unexpected OS failure rather than a missing face/fingerprint.
  String get biometricUnavailableEnrol => _t(
        'تعذّر التحقق بالبصمة على هذا الجهاز. حاول مرة أخرى.',
        "Biometric confirmation couldn't run on this device. Try again.",
      );

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
  String get otpDidntReceive => _t('لم يصلك الرمز؟', "Didn't get the code?");
  String get otpResendAction => _t('إعادة الإرسال', 'Resend');
  // #12 — confirmation that a fresh sign-in code was emailed in place.
  String get otpResentToast =>
      _t('تم إرسال رمز جديد إلى بريدك', 'A new code was sent to your email');
  String get forgotPasswordTitle => _t('نسيت كلمة المرور', 'Forgot password');
  String get forgotPasswordBody => _t(
        'أدخل بريدك الإلكتروني المسجّل وسنرسل لك رمز إعادة تعيين كلمة المرور.',
        'Enter your registered email and we will send you a password reset '
            'code.',
      );
  String get rememberedPasswordQuestion =>
      _t('تذكرت كلمة المرور؟', 'Remembered your password?');
  String get sendCodeButton => _t('إرسال الرمز', 'Send code');
  // The forgot-password submit label (owner 2026-07-07, D-674). "Code", not
  // "link" — the flow emails a code the user types on the reset screen.
  String get sendRecoveryCodeButton =>
      _t('إرسال رمز الاستعادة', 'Send recovery code');
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
  // The badge sign-in header title (owner 2026-07-07, D-674).
  String get badgePortalSignInTitle =>
      _t('البوابة الرئيسية • دخول', 'Main portal • Sign in');
  String get badgeScanHint => _t(
        'وجّه الكاميرا نحو رمز QR المطبوع على شارتك.',
        'Point the camera at the QR code on your badge.',
      );
  String get badgeManualLabel =>
      _t('أو أدخل رمز الشارة يدويًا', 'Or enter the badge code manually');
  String get badgeManualField => _t('رمز الشارة', 'Badge code');
  String get badgeResolveButton => _t('متابعة', 'Continue');
  // The scanner viewfinder's "actively searching" caption (Figma 758:4596).
  String get scanningCode => _t('جارٍ فحص الرمز...', 'Scanning the code…');
  // Shared QR-scanner chrome (used by the badge, contact and exhibitor
  // scanners).
  String get qrStopCamera => _t('إيقاف الكاميرا', 'Stop camera');
  String get qrBack => _t('رجوع', 'Back');
  String get qrManualLabel =>
      _t('أو أدخل الرمز يدويًا', 'Or enter the code manually');
  // Camera-error / permission-denied state on any scanner (D-737).
  String get scannerCameraError => _t(
        'تعذّر تشغيل الكاميرا. فعّل إذن الكاميرا من إعدادات النظام، أو أدخل الرمز يدويًا بالأسفل.',
        'Camera unavailable. Enable camera permission in system settings, or type the code below.',
      );
  String get scannerCameraRetry => _t('إعادة المحاولة', 'Try again');
  // A scanned contact QR that carries no SIMF share token (a foreign phone's
  // vCard, or an old QR) can't resolve to a live card — offer to add it to the
  // phone's own contacts instead (D-744).
  String get scanContactSaveToPhoneTitle =>
      _t('حفظ في جهات اتصال الهاتف؟', 'Save to phone contacts?');
  String get scanContactSaveToPhoneBody => _t(
        'هذه البطاقة ليست لعضو في الملتقى. أضِفها إلى جهات اتصال هاتفك مباشرة.',
        'This card isn’t a SIMF member. Add it straight to your phone’s contacts.',
      );
  String get scanContactSaveToPhoneConfirm => _t('إضافة', 'Add');
  String get scanContactSaveToPhoneFailed =>
      _t('تعذّر فتح إضافة جهة الاتصال.', 'Couldn’t open add-to-contacts.');
  String get badgeNotRecognised =>
      _t('تعذّر التعرّف على الشارة.', 'The badge was not recognised.');
  String get badgeScanError => _t('تعذّرت قراءة الشارة. حاول مجددًا.',
      'Could not read the badge. Try again.',);
  String get badgeActivateTitle => _t('تفعيل حسابك', 'Activate your account');
  String get badgeActivateEmailIntro => _t(
        'أدخل بريدك الإلكتروني لإرسال رمز التحقق.',
        'Enter your email so we can send a verification code.',
      );
  String badgeActivateCodeSent(String maskedEmail) => _t(
        'أرسلنا رمز التحقق إلى $maskedEmail.',
        'We sent a verification code to $maskedEmail.',
      );
  String get badgeSendCodeButton => _t('إرسال الرمز', 'Send code');
  String get badgeActivateButton =>
      _t('تفعيل وتعيين كلمة المرور', 'Activate & set password');
  // D-738 — the password step after a has-password badge resolves.
  String get badgePasswordTitle =>
      _t('أدخل كلمة المرور', 'Enter your password');
  String badgeWelcomeName(String name) => _t('مرحبًا $name', 'Welcome, $name');
  String badgeSignInAccountLine(String masked) => _t(
        'تسجيل الدخول إلى الحساب $masked',
        'Signing in to $masked',
      );
  String get badgeActivatedDone => _t(
        'تم تفعيل حسابك. سجّل الدخول الآن.',
        'Your account is activated. Sign in now.',
      );
  String get emailLabelGeneric => _t('البريد الإلكتروني', 'Email');

  String get splashTagline => 'SAUDI · MOD · RSNF';
  String get splashTitle => _t(
        'الملتقى البحري السعودي الدولي',
        'Saudi International Maritime Forum',
      );
  // Two lines per the KSA-Project splash frame (Figma 159:573, D-361). The
  // frame shows Western digits in the date/year, so the Arabic line matches it.
  String get splashEventLine => _t(
        'النسخة الرابعة\n23–25 نوفمبر 2026 · الرياض',
        '4th Edition\n23–25 Nov 2026 · Riyadh',
      );

  // Onboarding intro videos (Page 002 — interim placeholder frames; the real
  // YouTube clips introd_001..003 land with SIMF-VID-001).
  String get onboardingVideoLabel => _t('مقطع تعريفي', 'Intro video');
  String get onboardingMutedTooltip => _t('الصوت مكتوم', 'Sound muted');

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
  // Signed-in home "عن الملتقى" group (frame 758:1134, node 1052:12856) — the
  // full-width tile that opens the send-a-question (ask the moderator) screen.
  String get tileAskModerator => _t('اسأل المحاور', 'Ask the moderator');

  // Bottom navigation (KSA Wave-2 shell — frames 512:1492 / 213:963). The
  // sessions tab reuses [sessionsTitle] ("الجلسات", Figma 206:1732).
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
  // BUG-014 — the locked guest badge tile stays intentionally inert; the hint
  // is the only cue a screen-reader user gets that it is locked and why.
  String get guestBadgeLockedHint => _t(
        'مقفل — سجّل الدخول لفتح بطاقتك الذكية',
        'Locked — sign in to unlock your smart badge',
      );
  String get homeOpenInfoSection =>
      _t('معلومات مفتوحة للجميع', 'Open to everyone');
  String get faqRowTitle => _t('الأسئلة الشائعة', 'FAQ');
  String get faqRowSubtitle =>
      _t('FAQ • معلومات الموقع والفعالية', 'FAQ • Venue & event info');
  // الأسئلة الشائعة — FAQ accordion (Figma 1388-7567; GET /app/faq).
  String get faqEmpty =>
      _t('لا توجد أسئلة شائعة بعد.', 'No frequently asked questions yet.');
  String get faqError => _t(
        'تعذّر تحميل الأسئلة الشائعة.',
        'Could not load the FAQ.',
      );
  String get discoverSaudiTitle => _t('روح السعودية', 'Spirit of Saudi');
  String get discoverSaudiSubtitle =>
      _t('زر السعودية · استكشف الرياض', 'Visit Saudi · Discover Riyadh');
  // The signed-in home's filled gold discover badge (frame 758:1280); the guest
  // home keeps the outlined "KSA" badge (frame 758:2910).
  String get discoverSaudiBadge => _t('السعودية', 'Saudi');

  /// The GUEST variant of the badge above (Figma 758:2910), which the frame
  /// renders as the Latin abbreviation in both languages. Deliberately not
  /// translated: the signed-in variant [discoverSaudiBadge] is the localised
  /// one.
  String get discoverSaudiBadgeShort => _t('KSA', 'KSA');
  String get greetingMorning => _t('صباح الخير', 'Good morning');
  String get greetingEvening => _t('مساء الخير', 'Good evening');

  /// The home greeting word (owner 2026-07-21) — a friendly, time-independent
  /// "مرحبًا" shown above the user's first name, replacing the time-of-day
  /// word.
  String get greetingWelcome => _t('مرحبًا', 'Welcome');
  String get homeLiveTitle => _t(
        'الجلسة الافتتاحية تُبث الآن',
        'The opening session is live now',
      );
  String get homeLiveSubtitle =>
      _t('شاهد البث المباشر', 'Watch the live stream');
  // The signed-in home's "عن الملتقى" section bar (frame 758:1207) — a bordered
  // nav row that opens the About-the-forum page.
  String get homeAboutSection => _t('عن الملتقى', 'About the forum');
  String get homeSmartSection => _t('الميزات الذكية', 'Smart features');
  String get tileBilateralMeetings =>
      _t('اللقاءات الثنائية', 'Bilateral meetings');
  String get tileSessionSummary => _t('ملخص الجلسات', 'Session summaries');
  String get followUsSection => _t('تابعنا', 'Follow us');
  // The official handle line — a proper noun, identical in both languages.
  String get followUsHandle => '@SIMF_RSNF · الملتقى البحري السعودي الدولي';
  String get discoverSection => _t('اكتشف السعودية', 'Discover Saudi');
  // The top discovery hero banner on the signed-in home (frame 758:1134 node
  // 758:1203): the gold "اكتشف السعودية" title reuses [discoverSection]; this
  // is the white sub-line over the event photo.
  String get discoverBannerSubtitle =>
      _t('تعال واكتشف جديدك المفضل', 'Come discover your favourites');
  // ابرز الاحداث — the highlights / latest-news teaser card on the signed-in
  // home (frame 758:1134 node 758:1239). The post image rides the D-357
  // NewsImage asset route (Phase 1); the engagement counts (758:1252) are
  // admin-entered data landing in Phase 2 — the row stays hidden until the wire
  // carries them.
  String get featuredEventsSection => _t('ابرز الاحداث', 'Highlights');
  // The post-card source name + handle (frame 758:1246 / 758:1244).
  String get postSourceName => _t('الملتقى البحري', 'The Maritime Forum');
  String get postSourceHandle => '@SIMF';
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
  // Camera security rules (owner 2026-07-06, D-662): the identity photo must be
  // a LIVE, human-verified capture — there is no gallery path, so a static
  // image is never used. The step labels + directional cue + progress bar match
  // Figma 758:4180 / 4248 / 4316 (D-663). Owner 2026-07-07 (D-683) — clear,
  // human-friendly commands in big font so the visitor knows exactly what to do
  // at each liveness step (the terse Front/Right/Left labels were unclear).
  String get livenessHumanCheckTitle =>
      _t('للتأكد من أنك شخص حقيقي', "To confirm you're a real person");
  String get livenessSmilePrompt => _t('ابتسم من فضلك', 'Please smile');
  String get livenessTurnRightPrompt =>
      _t('أدر رأسك إلى اليمين', 'Please turn your head right');
  String get livenessTurnLeftPrompt =>
      _t('أدر رأسك إلى اليسار', 'Please turn your head left');
  String get identityCameraUnavailable => _t(
        'الكاميرا مطلوبة للتحقق من الهوية بصورة حية. فعّل الكاميرا وحاول مجددًا.',
        'The camera is required for a live identity check. Enable it and retry.',
      );
  String get identityRetry => _t('إعادة المحاولة', 'Retry');

  // Moderator (محاور) per-session Q&A desk (Figma 758:5307, D-405).
  String get moderatorDeskTitle => _t('أسئلة الجلسة', 'Session questions');
  String get moderatorBadge => _t('محاوِر', 'Moderator');
  String get moderatorManageQuestions =>
      _t('إدارة الأسئلة', 'Manage questions');
  String get moderatorChipAll => _t('الكل', 'All');
  String get moderatorChipNew => _t('جديد', 'New');
  // Figma 1461:12227 — the five filter chips + the three per-question actions.
  String get moderatorChipAccepted => _t('الأسئلة المقبولة', 'Accepted');
  String get moderatorChipAnswered => _t('تمت الإجابة', 'Answered');
  String get moderatorChipRejected => _t('مرفوض', 'Rejected');
  // DEF-MOD-007 — the duplicate `moderatorChipOnStage` was removed: it was a
  // byte-identical copy of the action label below and was never rendered.
  String get moderatorActionOnStage => _t('يتم الإجابة', 'Being answered');
  String get moderatorActionAnswered => _t('تمت الإجابة', 'Answered');
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

  // FR-MOD-001 — the moderator's own sessions (GET /app/sessions/moderated), so
  // the desk is offered only where the per-session grant exists instead of
  // being discovered as a 403 after the tap.
  String get moderatorMySessions => _t('جلساتي', 'My sessions');
  String get moderatorMySessionsEmpty => _t(
        'لم يتم إسنادك إلى أي جلسة بعد.',
        'You are not assigned to any session yet.',
      );
  String get moderatorMySessionsError => _t(
        'تعذّر تحميل جلساتك. حاول مرة أخرى.',
        'Could not load your sessions. Try again.',
      );

  // FR-MOD-003 — drag-to-reorder on the desk queue.
  String get moderatorReorderHandle =>
      _t('إعادة ترتيب السؤال', 'Reorder question');
  String get moderatorReorderFailed => _t(
        'تعذّر حفظ الترتيب. حاول مرة أخرى.',
        'Could not save the order. Try again.',
      );

  // Staff gate-operator console (Figma 758:4380/4651/4735/4819/4886, D-406/D-509).
  String get gateScannerEntry => _t('مسح البوابة', 'Gate scanner');
  String get gateScanTitle => _t('فحص رمز QR — موظف', 'QR scan — staff');
  String get gateSelectGate => _t('اختر البوابة', 'Select gate');
  String get gateMovementType => _t('نوع الحركة', 'Movement type');
  String get gateChooseDirectionFirst => _t(
        'اختر نوع الحركة أولاً لتفعيل السكان',
        'Choose the movement type first to enable scanning',
      );
  String get gateScanCode => _t('سكان الرمز', 'Scan code');
  String get gateUnregistered => _t('غير مسجّل', 'Unregistered');
  String get gateScanHint =>
      _t('وجّه الكاميرا إلى رمز QR', 'Point the camera at the QR code');
  String get gateManualHint =>
      _t('أدخل الرمز يدويًا', 'Enter the code manually');
  String get gateManualSubmit => _t('تحقّق', 'Check');
  String get gateHold => _t('إيقاف مؤقت', 'Hold');
  String get gateResume => _t('استئناف', 'Resume');
  String get gateAllowed => _t('مسموح', 'Allowed');
  String get gateAllowedSub =>
      _t('مرحباً بك في الفعالية', 'Welcome to the event');
  String get gateDenied => _t('ممنوع', 'Denied');
  String get gateDeniedSub => _t('غير مصرح بالدخول', 'Entry not authorised');
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
  // DEF-STF-006 — an inactive gate denies EVERY scan, so it must be marked in
  // the picker; the operator was left reading red denials with no hint that the
  // GATE, not the badge, was the problem.
  String get gateInactiveTag => _t('غير نشطة', 'inactive');
  String get gateInactiveWarning => _t(
        'هذه البوابة غير نشطة — سيُرفض كل مسح عليها. اختر بوابة أخرى أو اطلب '
            'تفعيلها من لوحة التحكم.',
        'This gate is inactive — every scan on it will be denied. Pick another '
            'gate, or ask the Control Panel to activate it.',
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
  String get gateSavedOffline => _t(
        'تعذّر الاتصال — حُفظ المسح وسيُعاد إرساله تلقائيًا.',
        'No connection — the scan was saved and will retry automatically.',
      );
  String gatePendingSync(int count) => _t(
        'بانتظار المزامنة: $count',
        '$count scan(s) waiting to sync',
      );

  // D-821 — the verdict the device reached with no network. Deliberately worded
  // as provisional: the scan is queued and the server re-decides it on upload,
  // so the operator must not read these as the final answer.
  String get gateOfflineAllowed => _t(
        'مسموح (دون اتصال) — حُفظ المسح للتأكيد لاحقًا.',
        'Allowed (offline) — saved for confirmation.',
      );
  String get gateOfflineDeniedBadge => _t(
        'بطاقة غير صالحة — لم يتم التحقق منها.',
        'Invalid badge — it did not verify.',
      );
  String get gateOfflineDeniedProfileType => _t(
        'هذا النوع غير مسموح له بالدخول من هذه البوابة.',
        'This badge type is not allowed at this gate.',
      );
  String get gateOfflineDeniedGateInactive => _t(
        'هذه البوابة موقوفة في آخر إعدادات مُزامنة.',
        'This gate was switched off in the last synced rules.',
      );

  // Staff walk-in registration — "add a visitor at the exhibition" (Figma
  // 1467:12357, D-509).
  String get staffRegisterVisitorTitle =>
      _t('إنشاء ملف زائر', 'Create visitor profile');
  String get staffRegisterVisitorEntry =>
      _t('تسجيل زائر', 'Register a visitor');
  String get staffEmailLabel => _t('البريد الالكتروني', 'Email');
  String get staffPhoneLabel => _t('رقم الجوال', 'Mobile number');
  String get staffOrganisationLabel => _t('الجهة / المنظمة', 'Organisation');
  // BUG-019 / 19k — the long parenthetical wrapped these two captions onto a
  // second line while every sibling label was one line. The caption is now
  // short; the detail moved to the field hint below it.
  String get staffAttachIdLabel => _t('صورة الهوية', 'ID document');
  String get staffAttachIdHint => _t(
        'الهوية الوطنية أو الإقامة أو جواز السفر',
        'National ID, Iqama or passport',
      );
  String get staffAttachPhotoLabel => _t('الصورة الشخصية', 'Personal photo');
  String get staffAttachOptionalHint => _t('اختياري', 'Optional');
  String get staffAttachFile => _t('إرفاق ملف', 'Attach file');
  // BUG-019 / 19k — "Attach personal photo" overflowed the fixed attach box on
  // a phone-width column; the field caption above it already says which photo.
  String get staffAttachPhoto => _t('إرفاق صورة', 'Attach photo');
  String get staffCompletePrompt => _t(
        'أكمل بيانات الزائر المطلوبة.',
        "Complete the visitor's required details.",
      );
  String get staffRegisterSuccess => _t(
        'تم تسجيل الزائر — بانتظار الاعتماد',
        'Visitor registered — pending approval',
      );
  String get staffRegisterError => _t(
        'تعذّر تسجيل الزائر. حاول مرة أخرى.',
        'Could not register the visitor. Try again.',
      );
  String get staffProfileTypeUnavailable => _t(
        'تعذّر تحميل تصنيف الزائر.',
        'Could not load the visitor classification.',
      );
  // DEF-STF-007 — the classification lookup came back EMPTY, so the operator
  // has nothing to pick and submit can never pass. Say what to do about it.
  String get staffProfileTypeEmptyHelp => _t(
        'لا توجد تصنيفات زوار مفعّلة. اطلب من مسؤول لوحة التحكم إضافة تصنيف زائر ثم أعد المحاولة.',
        'No active visitor classifications exist. Ask a Control Panel administrator to add one, then retry.',
      );
  // DEF-STF-004 — an attachment upload that fails AFTER the visitor was
  // created. The account exists; only the file is missing, so the operator
  // retries the UPLOAD instead of registering the person a second time.
  String get staffUploadFailedTitle => _t(
        'تم تسجيل الزائر — تعذّر رفع المرفقات',
        'Visitor registered — attachments not uploaded',
      );
  String get staffUploadFailedIntro => _t(
        'تم إنشاء حساب الزائر (بانتظار الاعتماد)، لكن تعذّر رفع ما يلي:',
        'The visitor account was created (pending approval), but these could '
            'not be uploaded:',
      );
  String get staffUploadRetryLabel => _t('إعادة رفع المرفقات', 'Retry upload');
  String get staffUploadSkipLabel =>
      _t('المتابعة بدون المرفقات', 'Continue without them');
  String get staffUploadRetrySuccess => _t(
        'تم رفع المرفقات.',
        'The attachments were uploaded.',
      );
  String get staffRegisterAnother => _t('تسجيل زائر آخر', 'Register another');
  // The My-Area "الجلسات المحفوظة" counter (D-584) — shows the SAVED
  // (favourited) count and opens الجلسات المحفوظة (1701:8928); Arabic already
  // read "محفوظة".
  String get statBookedSessions => _t('جلسات محفوظة', 'Saved sessions');
  String get statMeetings => _t('مقابلات', 'Meetings');
  String get statisticsTitle => _t('الإحصائيات', 'Statistics');
  String get todayScheduleTitle => _t('جدولي اليوم', "Today's schedule");
  // جدولي اليوم sub-group headers (frame 758:1283, nodes 1041:2042 / 1041:2044)
  // — gold, above the session rows and the meeting rows respectively.
  String get scheduleSessionsGroup => _t('جلسات', 'Sessions');
  String get scheduleMeetingsGroup => _t('مقابلات', 'Meetings');
  String get scheduleEmpty => _t('لا يوجد لديك مواعيد اليوم', 'No items today');
  String get smartBadgeLink => _t('بطاقتي الذكية', 'My smart badge');
  String get accountSettingsLink => _t('إعدادات الحساب', 'Account settings');
  String get myAreaPendingNote => _t(
        'حسابك قيد المراجعة. ستظهر بطاقتك وجدولك بعد الاعتماد.',
        'Your account is under review. Your badge and schedule appear once approved.',
      );
  // BUG-013 — the TRUE-guest copy (no account at all); see [badgeGuestBody].
  String get myAreaGuestNote => _t(
        'سجّل الدخول أو أنشئ حساباً لعرض ملفك الشخصي وجدولك.',
        'Sign in or create an account to see your profile and schedule.',
      );
  String get myAreaError =>
      _t('تعذّر تحميل منطقتك.', 'Could not load your area.');

  String get venueMapTitle => _t('الخريطة', 'Venue map');
  String get venueMapError =>
      _t('تعذّر تحميل الخريطة.', 'Could not load the map.');
  String get venueMapEmpty =>
      _t('لا توجد عناصر على الخريطة بعد', 'No map items yet');
  // The floating map controls are icon-only, so they carried no accessible name
  // at all (BUG-012) — a screen-reader user could not zoom or recentre the map.
  String get venueMapZoomIn => _t('تكبير الخريطة', 'Zoom in');
  String get venueMapZoomOut => _t('تصغير الخريطة', 'Zoom out');
  String get venueMapResetView =>
      _t('إعادة ضبط عرض الخريطة', 'Reset the map view');
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
  // D-750 — the bottom-nav program/agenda tab label (owner 2026-07-20). Distinct
  // from [sessionsTitle], which titles the Sessions screen and other surfaces.
  String get agendaTitle => _t('الأجندة', 'Agenda');
  String get sessionsViewUpcoming => _t('الأجندة القادمة', 'Upcoming agenda');
  String get sessionsViewForum => _t('أجندة الفعالية', 'Event agenda');
  String get sessionsAllDays => _t('كل الأيام', 'All days');
  String get sessionsSearchHint => _t('البحث', 'Search');
  String get sessionsScheduleSection => _t('المواعيد', 'Schedule');
  // The Sessions screen header (frame 883:2308 node 883:2314 "برنامج الملتقي" —
  // corrected spelling الملتقى); distinct from the bottom-nav label
  // ([sessionsTitle] "الجلسات", nav component 206:1732).
  String get sessionsProgrammeTitle => _t('برنامج الملتقى', 'Forum programme');
  // D-452 (Figma 883:2320) — the session type tabs (احداث dropped to match
  // the 3-tab frame, owner 2026-07-03).
  String get sessionTypeAll => _t('الكل', 'All');
  String get sessionTypeWorkshop => _t('ورش العمل', 'Workshops');
  String get sessionTypeSession => _t('جلسات', 'Sessions');
  String get sessionsEmpty => _t('لا توجد جلسات', 'No sessions');
  String get sessionsEmptyWorkshops => _t('لا توجد ورش عمل', 'No workshops');
  // Shown when the whole selected day is empty — the الكل / All tab (or a
  // tab-less event-typed day), so the message is about the day, not "sessions".
  String get sessionsEmptyDay =>
      _t('لا يوجد برنامج في هذا اليوم', 'No programme for this day');
  String get sessionsError =>
      _t('تعذّر تحميل الجلسات.', 'Could not load the sessions.');

  String get sessionDetailTitle => _t('تفاصيل الجلسة', 'Session detail');
  String get sessionDetailError =>
      _t('تعذّر تحميل الجلسة.', 'Could not load the session.');
  String get sessionNotFound =>
      _t('الجلسة غير موجودة أو تمت إزالتها', 'This session was not found');
  // Header-card action buttons + the ask-the-host card (Figma 889:2450).
  String get sessionLink => _t('رابط الجلسة', 'Session link');
  String get sessionSummary => _t('ملخص الجلسة', 'Session summary');
  String get askHost => _t('اسأل المحاور', 'Ask the host');
  // D-714 (item 12, FDS-007 §B.4 GAP-2) — before a session goes live the ask
  // entry reads as the distinct pre-session ("mode B", Phase=Pre) question, so
  // the two ask modes are visibly separate on the one detail screen.
  String get askHostPreSession =>
      _t('اطرح سؤالاً قبل الجلسة', 'Ask a question before it starts');
  // #3 — pre-ask is gated on having JOINED the session (a booking), not on
  // physical check-in; the ask card is disabled with this hint until then.
  String get askHostJoinFirst =>
      _t('انضم إلى الجلسة لطرح سؤال', 'Join the session to ask a question');
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
  // D-485 — the in-app session join flow (Join CTA / seat picker / hub).
  String get joinSectionHeading => _t('الانضمام للجلسة', 'Join this session');
  String get joinSeatCta => _t('اختر مقعدي', 'Select my seat');
  String get joinOpenCta => _t('انضم إلى الجلسة', 'Join this session');
  // The single gold join button on the session detail (Figma 889:2450, owner
  // 2026-06-30): one label for both seating modes — open-seating joins in
  // place, assigned-seat opens the seat picker.
  String get joinSessionCta => _t('الانضمام إلى الجلسة', 'Join the session');
  // D-750 (owner 2026-07-20) — case-1 (open-seating) join CTA: the button reads
  // "register to attend" instead of the generic join label, because an
  // open-seating join is a registration, not a seat reservation.
  String get joinOpenRegisterCta =>
      _t('سجل لحضور الجلسة', 'Register to attend the session');
  // A8 (2026-07-27) — bookings auto-confirm: the owner removed the Control
  // Panel approval step on 2026-07-18, so the seat is HELD the moment it is
  // picked. The old copy ("then await approval") described a dead workflow.
  String get joinSeatHint => _t(
        'اختر مقعدك ويُحجز لك فوراً',
        'Pick your seat — it is held for you straight away',
      );
  String get joinOpenHint =>
      _t('دخول عام — بدون مقعد محدد', 'General admission — no specific seat');
  String get joinConfirmTitle => _t('تأكيد الانضمام', 'Join this session?');
  // A8 — the open-seating join confirmation. There is no approval request:
  // the registration is recorded immediately, it does not reserve a specific
  // seat, and entry is confirmed at check-in (matches joinOpenSuccessBody).
  String get joinConfirmBody => _t(
        'سيتم تسجيلك لحضور هذه الجلسة فوراً دون الحاجة إلى موافقة. '
            'التسجيل لا يحجز مقعداً محدداً، '
            'وسيتم تأكيد دخولك عند تسجيل الدخول للجلسة.',
        'You will be registered for this session right away — no approval '
            'needed. This does not reserve a specific seat; your entry is '
            'confirmed at session check-in.',
      );
  String get joinConfirmAction => _t('انضمام', 'Join');
  String get joinPendingToast => _t('تم إرسال طلبك — بانتظار موافقة الإدارة',
      'Request sent — pending approval',);
  // D-750 — case-1 (open-seating) post-join success alert body (replaces the
  // joinPendingToast snackbar): registering is not a seat reservation and does
  // not guarantee entry; entry is confirmed at session check-in.
  String get joinOpenSuccessBody => _t(
        'تم تسجيلك لحضور هذه الجلسة بنجاح. هذا التسجيل لا يعني حجز مقعد أو ضمان الدخول للجلسة، سيتم تأكيد دخولك عند تسجيل الدخول للجلسة',
        'You have successfully registered to attend this session. This registration does not reserve a seat or guarantee entry; your entry will be confirmed at session check-in.',
      );
  String get joinFailed =>
      _t('تعذّر إرسال الطلب', "Couldn't send your request");
  String get joinSessionFull => _t('لا توجد أماكن متبقية', 'No places remain');
  String get generalAdmissionLabel => _t('دخول عام', 'General admission');
  String get reservationPendingHint =>
      _t('بانتظار موافقة الإدارة', 'Pending approval');
  // D-572 — the approved-booking hint (Figma 889:2766): once the Control Panel
  // approves the seat the card swaps the pending line for this.
  String get seatShowBadgeHint =>
      _t('تأكد من إبراز بطاقتك عند الدخول', 'Show your badge at entry');
  String get cancelBookingCta => _t('إلغاء الحجز', 'Cancel booking');
  String get cancelBookingConfirmTitle => _t('إلغاء الحجز', 'Cancel booking?');
  String get cancelBookingConfirmBody => _t(
        'سيتم إلغاء حجزك لهذه الجلسة.',
        'Your booking for this session will be cancelled.',
      );
  String get bookingCancelledToast => _t('تم إلغاء الحجز', 'Booking cancelled');
  String get bookingCancelFailed =>
      _t('تعذّر إلغاء الحجز', "Couldn't cancel the booking");
  String get seatPickerTitle => _t('اختر مقعدك', 'Select your seat');
  String get seatPickerHint =>
      _t('اضغط على مقعد متاح لحجزه', 'Tap an available seat to reserve it');
  String get seatPickerRandomCta => _t('اختيار تلقائي', 'Auto-pick a seat');
  String get seatReservedToast =>
      _t('تم الحجز — بانتظار الموافقة', 'Reserved — pending approval');
  // D-750 — case-2 (assigned-seat) post-reserve success alert body (replaces
  // the seatReservedToast snackbar): the hold is released if the visitor does
  // not check in by 3 minutes before the session starts, to free the seat.
  String get seatReservedAlertBody => _t(
        'تم حجز المقعد بنجاح سيتم الغاء الحجز في حالة عدم تسجيل الدخول للجلسة قبل 3 دقائق قبل بدء الجلسة لاتاحة المقعد لأشخاص اخرين',
        'Seat reserved successfully. The reservation will be cancelled if you do not check in by 3 minutes before the session starts, to free the seat for others.',
      );
  String get seatReserveFailed =>
      _t('تعذّر حجز المقعد', "Couldn't reserve that seat");
  // Seat picker — tap→select→confirm (owner 2026-07-25): the chip above the
  // auto-pick button confirms the tapped seat, and the CTA commits the hold.
  String seatPickerSelectedChip(String row, int seat) => _t(
      'المقعد المختار: الصف $row · مقعد $seat',
      'Selected: Row $row · Seat $seat',);
  String get seatPickerConfirmCta => _t('تأكيد المقعد', 'Confirm my seat');
  // B1 (owner "change seat") — moving an already-held seat. The picker doubles
  // as the destination chooser: when the visitor already holds a seat it opens
  // in CHANGE mode (its own title/hint/CTA) and the confirm step names both
  // seats so nobody swaps by accident.
  String get seatChangeCta => _t('تغيير المقعد', 'Change seat');
  String get seatChangeTitle => _t('تغيير مقعدك', 'Change your seat');
  String get seatChangeHint => _t(
        'اضغط على مقعد متاح لنقل حجزك إليه — سيبقى مقعدك الحالي محجوزاً حتى ينجح النقل.',
        'Tap an available seat to move your booking to it — you keep your current seat unless the move succeeds.',
      );
  String get seatChangeConfirmCta => _t('تأكيد التغيير', 'Confirm the change');
  String get seatChangeConfirmTitle => _t('تغيير المقعد؟', 'Change your seat?');
  String seatChangeConfirmBody(
    String fromRow,
    int fromSeat,
    String toRow,
    int toSeat,
  ) =>
      _t(
        'سيتم نقل حجزك من الصف $fromRow · مقعد $fromSeat إلى الصف $toRow · مقعد $toSeat.',
        'Your booking moves from Row $fromRow · Seat $fromSeat to Row $toRow · Seat $toSeat.',
      );
  String seatChangedAlertBody(String row, int seat) => _t(
        'تم نقل حجزك إلى الصف $row · مقعد $seat.',
        'Your booking has moved to Row $row · Seat $seat.',
      );
  String get seatChangeFailed =>
      _t('تعذّر تغيير المقعد', "Couldn't change your seat");
  String get seatChangeTaken => _t(
        'تم حجز هذا المقعد للتو — لا يزال مقعدك الحالي محجوزاً لك.',
        'That seat was just taken — you still have your current seat.',
      );
  String get joinHubTitle => _t('احجز مقعداً', 'Book a seat');
  String get joinHubHint =>
      _t('اختر جلسة للانضمام إليها', 'Choose a session to join');
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
  // A12 — the fourth seat state: the holder has scanned in at the hall gate,
  // so the seat is confirmed rather than merely held. Same wording as the
  // Control Panel's live-hall map (Admin.SessionLiveHall.Seat.Confirmed).
  String get legendConfirmed => _t('تم التأكيد', 'Confirmed');
  String seatCapacity(int reserved, int total) =>
      _t('محجوز $reserved من $total', '$reserved of $total reserved');
  String get navigateToSeat => _t('إرشادي إلى مقعدي', 'Guide me to my seat');
  String get shareLocation => _t('مشاركة الموقع', 'Share location');
  String seatShareText(String row, int seat) => _t(
        'مقعدي في الملتقى: صف $row · مقعد $seat',
        'My SIMF seat: Row $row · Seat $seat',
      );

  // D-771 (owner 2026-07-26) — seat TIERS. The tier is real data on the hall
  // layout: a VVIP row is protocol seating nobody may book, a VIP row is for
  // VIP guests only, a Normal row is open to every visitor.
  String get seatTierVvip => _t('شخصيات بالغة الأهمية', 'VVIP');
  String get seatTierVip => _t('كبار الشخصيات', 'VIP');
  String get seatTierNormal => _t('عادي', 'Normal');
  String get seatTierVvipLocked => _t(
        'مقعد محجوز لكبار الضيوف — لا يمكن حجزه',
        'Reserved for protocol guests — cannot be booked',
      );
  String get seatTierVipLocked => _t(
        'مقعد مخصص لكبار الشخصيات',
        'Reserved for VIP guests',
      );
  String get seatTierPickerHint => _t(
        'المقاعد المقفلة غير متاحة لك: مقاعد الشخصيات بالغة الأهمية يخصّصها المنظّم، ومقاعد كبار الشخصيات لكبار الشخصيات فقط.',
        'Locked seats are not available to you: VVIP seats are assigned by the organiser, and VIP seats are for VIP guests only.',
      );

  // D-771 — the staff seating desk (tablet, Staff role only).
  String get staffSeatingTitle =>
      _t('إرشاد الضيوف للمقاعد', 'Guest seating desk');
  String get staffSeatingIntro => _t(
        'امسح بطاقة الضيف لمعرفة مقعده، أو اضغط على أي مقعد لمعرفة صاحبه.',
        "Scan a guest's badge to find their seat, or tap a seat to see who it belongs to.",
      );
  String get staffSeatingScanLabel => _t('رمز البطاقة', 'Badge code');
  String get staffSeatingScanCta => _t('بحث', 'Look up');
  String get staffSeatingScanHint => _t(
        'وجّه الكاميرا نحو رمز QR على بطاقة الضيف',
        "Point the camera at the QR code on the guest's badge",
      );
  String get staffSeatingSessionLabel => _t('الجلسة', 'Session');
  String get staffSeatingPickSession =>
      _t('اختر الجلسة أولاً', 'Choose a session first');
  String get staffSeatingNoSeat => _t('لا يوجد مقعد لهذا الضيف في هذه الجلسة',
      'This guest has no seat in this session',);
  String get staffSeatingSeatEmpty =>
      _t('هذا المقعد شاغر', 'This seat is empty');
  String get staffSeatingReference => _t('الرقم المرجعي', 'Reference');
  String get staffSeatingGuest => _t('الضيف', 'Guest');
  String get staffSeatingSeat => _t('المقعد', 'Seat');
  String staffSeatingSeatValue(String row, int seat) =>
      _t('صف $row · مقعد $seat', 'Row $row · Seat $seat');
  String get staffSeatingCheckedIn => _t('سجّل الدخول', 'Checked in');
  String get staffSeatingNotCheckedIn =>
      _t('لم يسجّل الدخول بعد', 'Not checked in yet');
  String get staffSeatingLookupFailed =>
      _t('تعذّر تنفيذ البحث.', 'The lookup failed.');
  String get staffSeatingUnknownBadge =>
      _t('لم يتم التعرف على هذه البطاقة.', 'That badge was not recognised.');
  String get staffSeatingGuestPhoto => _t('صورة الضيف', 'Guest photo');
  String get staffSeatingClear => _t('مسح النتيجة', 'Clear result');

  String get speakersTitle => _t('المتحدثون', 'Speakers');
  String get speakersError =>
      _t('تعذّر تحميل المتحدثين.', 'Could not load the speakers.');
  String get speakersEmpty => _t('لا يوجد متحدثون', 'No speakers');
  // Frame 908:1744 — the search box + sort control above the speaker list.
  String get speakersSearchHint => _t('ما الذي تبحث عنه', 'What are you after');
  String get speakersSortAlpha =>
      _t('ترتيب حسب الابجدية', 'Sort alphabetically');
  String get speakersNoMatches => _t('لا نتائج مطابقة', 'No matching speakers');

  String get speakerProfileTitle => _t('ملف المتحدث', 'Speaker profile');
  String get speakerProfileError =>
      _t('تعذّر تحميل ملف المتحدث.', 'Could not load the speaker profile.');
  String get speakerNotFound =>
      _t('المتحدث غير موجود', 'This speaker was not found');
  String get cvBio => _t('نبذة عنه', 'Biography');
  String get cvQualifications => _t('المؤهلات العلمية', 'Qualifications');
  String get cvTraining => _t('الخبرات التدريبية', 'Training experience');
  String get cvAwards => _t('الجوائز', 'Awards');
  String get speakerSessionsHeading =>
      _t('جلسات المتحدث', "Speaker's sessions");
  String get copyLinkLabel => _t('نسخ الرابط', 'Copy link');
  String get linkCopied => _t('تم نسخ الرابط', 'Link copied');
  String get requestMeeting => _t('طلب مقابلة', 'Request meeting');
  String get meetingNameLabel => _t('الاسم', 'Your name');
  String get meetingSubjectLabel => _t('الموضوع', 'Subject');
  // Bilateral-meeting entry (owner: VIP اللقاءات الثنائية) — pick a speaker.
  String get meetingSelectSpeakerLabel => _t('اختر المتحدث', 'Select speaker');
  String get meetingSelectSpeakerHint =>
      _t('اختر المتحدث…', 'Choose a speaker…');
  String get meetingSelectSpeakerFirst =>
      _t('اختر متحدثاً أولاً', 'Select a speaker first');
  String get meetingSendButton => _t('إرسال الطلب', 'Send request');
  // Figma 1776:4958 / 1776:5036 — the light "طلب مقابلة" sheet: a subject field,
  // a row of day cards, then that day's time-slot chips.
  String get meetingSubjectHint => _t('اكتب الموضوع', 'Write the subject');
  String get meetingChooseDateLabel => _t('اختر التاريخ', 'Choose the date');
  String get meetingChooseTimeLabel => _t('اختر الوقت', 'Choose the time');
  String get meetingChooseDateFirst =>
      _t('الرجاء اختيار التاريخ أولاً', 'Please choose a date first');
  String get meetingPickDateTime =>
      _t('الرجاء اختيار التاريخ والوقت', 'Please choose a date and time');
  String get meetingSlotNone =>
      _t('لا توجد فترات متاحة حالياً', 'No meeting slots available right now');
  // QA A28 — the old copy ("for VIP guests only") described a rule that no
  // longer exists: eligibility to request a speaker meeting moved off the VIP
  // tier onto the per-user, admin-assigned UserProfile.AllowsSpeakerMeeting
  // flag (bi-meeting rework). This states the real rule and what to do next.
  String get meetingNotEnabled => _t(
        'طلب مقابلة المتحدّث غير مُفعَّل لحسابك. '
            'تواصل مع فريق الملتقى لتفعيله.',
        'Requesting a speaker meeting is not enabled for your account. '
            'Contact the SIMF team to enable it.',
      );
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

  // Bi-Meeting rework — the اللقاءات الثنائية page's two request buttons + the
  // no-access state, and the delegation-meeting request sheet + confirm screen.
  String get requestSpeakerMeeting =>
      _t('طلب مقابلة متحدث', 'Request a speaker meeting');
  String get requestDelegationMeeting =>
      _t('طلب اجتماع وفد', 'Request a delegation meeting');
  String get meetingAccessRequired => _t(
        'اللقاءات الثنائية متاحة للحسابات المصرَّح لها فقط',
        'Bilateral meetings are available to authorised accounts only',
      );
  String get delegationRequestTitle =>
      _t('طلب اجتماع وفد', 'Delegation meeting request');
  String get delegationSelectCountryLabel =>
      _t('اختر الوفد', 'Select the delegation');
  String get delegationSelectCountryFirst =>
      _t('اختر الوفد أولاً', 'Select a delegation first');
  String get delegationNoneAvailable =>
      _t('لا توجد وفود متاحة', 'No delegations available');
  String get delegationAttendeeCountLabel =>
      _t('عدد الحضور', 'Number of attendees');
  String get delegationAttendeeCountHint => _t('مثال: 5', 'e.g. 5');
  String get delegationAttendeeCountInvalid =>
      _t('أدخل عدد حضور صحيحاً', 'Enter a valid number of attendees');
  String get delegationNotAllowed => _t(
        'غير مصرَّح لك بطلب اجتماعات الوفود',
        'You are not permitted to request delegation meetings',
      );
  String get delegationTargetNotInvited => _t(
        'هذا الوفد غير متاح للاجتماعات',
        'This delegation is not available for meetings',
      );
  // A30 — this in-app screen answers a DELEGATION meeting only
  // (`/meeting-confirm` with a requestId). The website's
  // `/meeting/confirm?token=` page is the separate emailed-link surface. They
  // used to share the title "تأكيد الاجتماع / Confirm meeting", so a tester
  // driving this screen for a SPEAKER meeting hit a spurious 403/409; the copy
  // now names the delegation explicitly.
  String get meetingConfirmTitle =>
      _t('تأكيد اجتماع الوفد', 'Confirm delegation meeting');
  String get meetingConfirmIntro => _t(
        'اضغط لتأكيد اجتماع وفدكم مع الوفد الآخر، '
            'أو ارفضه إذا تعذّر عقده.',
        'Confirm your delegation meeting with the other delegation, '
            'or decline it if it cannot go ahead.',
      );
  String get meetingConfirmButton =>
      _t('تأكيد الاجتماع', 'Confirm the meeting');
  String get meetingConfirmDone => _t('تم تأكيد الاجتماع', 'Meeting confirmed');
  String get meetingConfirmNotAwaiting => _t(
        'هذا الاجتماع ليس بانتظار التأكيد',
        'This meeting is not awaiting confirmation',
      );
  String get meetingConfirmFailed => _t(
        'تعذّر تأكيد الاجتماع. حاول مرة أخرى.',
        'Could not confirm the meeting. Try again.',
      );
  String get meetingConfirmMissing =>
      _t('لم يتم العثور على الاجتماع', 'Meeting not found');

  // B8 — the delegation target's DECLINE action on the same screen. Before
  // this the only exit from an approved meeting they could not attend was an
  // admin cancel.
  String get meetingDeclineButton => _t('رفض الاجتماع', 'Decline the meeting');
  String get meetingDeclineDone => _t('تم رفض الاجتماع', 'Meeting declined');
  String get meetingDeclineIntro => _t(
        'تم إبلاغ الوفد الطالب بالرفض وتحرير فترة القاعة.',
        'The requesting delegation has been told, '
            'and the hall slot is released.',
      );
  String get meetingDeclineFailed => _t(
        'تعذّر رفض الاجتماع. حاول مرة أخرى.',
        'Could not decline the meeting. Try again.',
      );

  // D-500 (Wave 5, الطلبات 1408:9726) — the unified requests feed (supersedes
  // the D-479 read-only My-meetings screen). D-745 (owner 2026-07-11): the
  // requests feed became the history page ("طلباتي") once the VIP
  // bilateral-meetings page ([meetingsTitle]) split off; the frame header
  // "اللقاءات الثنائية" (1408:9726) now belongs to that new page.
  String get requestsTitle => _t('طلباتي', 'My requests');

  /// The VIP bilateral-meetings page title (اللقاءات الثنائية, Figma 1408:9726)
  /// — matches the Home tile label [tileBilateralMeetings].
  String get meetingsTitle => _t('اللقاءات الثنائية', 'Bilateral meetings');
  String get requestsLink => _t('الطلبات', 'Requests');
  String get requestsEmpty =>
      _t('لا توجد طلبات بعد', 'You have no requests yet');
  String get requestsNoResults =>
      _t('لا توجد طلبات بهذه الحالة', 'No requests with this status');
  String get requestsError =>
      _t('تعذّر تحميل طلباتك', 'Could not load your requests');

  String get requestKindSpeaker =>
      _t('طلب لقاء مع متحدث', 'Speaker meeting request');
  String get requestKindDelegation =>
      _t('طلب اجتماع وفد', 'Delegation meeting request');
  String get requestKindSession =>
      _t('طلب حضور جلسة', 'Session attendance request');
  String get requestKindDocument =>
      _t('طلب وثيقة المشاركة', 'Participation document request');
  String get requestKindBadge => _t('طلب تحديث البادج', 'Badge update request');

  // Status chips. (السجل serves "all"; there is no standalone "All" chip.)
  String get requestStatusPending => _t('قيد المراجعة', 'Under review');
  String get requestStatusAccepted => _t('مقبول', 'Accepted');
  // QA B12 — an accepted meeting checked in at the hall by an operator.
  String get requestStatusAttended => _t('تم الحضور', 'Attended');
  String get requestStatusRejected => _t('مرفوض', 'Rejected');
  String get requestStatusCancelled => _t('ملغى', 'Cancelled');

  // الطلبات top action row (Figma 1408:9736) — السجل = all requests (default),
  // المقبولة = accepted filter shortcut.
  String get requestsTabAccepted => _t('المقبولة', 'Accepted');
  String get requestsTabLog => _t('السجل', 'Log');

  String get requestNew => _t('طلب جديد', 'New request');
  // The نوع الطلب type-picker sheet and its document / badge forms were deleted
  // with new_request_sheet.dart (D-703 flagged it orphaned on 2026-07-08; owner
  // confirmed deletion 2026-07-28), so their 14 strings went with them. The
  // feed still RENDERS existing document/badge requests via requestKindDocument
  // / requestKindBadge below — only the creation UI is gone.

  String get requestCancel => _t('إلغاء الطلب', 'Cancel request');
  String get requestCancelConfirm =>
      _t('هل تريد إلغاء هذا الطلب؟', 'Cancel this request?');
  String get requestCancelKeep => _t('تراجع', 'Keep');
  String get requestCancelled => _t('تم إلغاء الطلب', 'Request cancelled');
  String get requestCancelFailed =>
      _t('تعذّر إلغاء الطلب', 'Could not cancel the request');

  /// A request card's short date (locale-aware "12 يناير 2026" / "12 Jan 2026").
  // الطلبات card date carries the year (Figma 1408:9782 — "12 يناير 2026").
  // In Arabic an LRM (U+200E) sits before the year so the bidi algorithm does
  // not pull the year across the Arabic month name (it stays day-month-year,
  // left-to-right, as the frame shows) when the Text is pinned LTR.
  String requestDate(DateTime date) => isArabic
      ? '${_shortDate(date)} ‎${date.year}'
      : '${_shortDate(date)} ${date.year}';

  /// The card date line when the request's date is today (Figma 1408:9782):
  /// "07:45 AM · اليوم" — a 12-hour time (English AM/PM, matching the frame)
  /// then the relative "today". Non-today dates use [requestDate] instead.
  String requestTimeToday(DateTime date) => '${_time12h(date)} · $dayToday';

  /// "07:45 AM" — 12-hour clock with a zero-padded hour and an English AM/PM
  /// marker (the frame shows "AM"/"PM" literally, in both locales).
  String _time12h(DateTime date) {
    final period = date.hour < 12 ? 'AM' : 'PM';
    final hour12 = date.hour % 12 == 0 ? 12 : date.hour % 12;
    final hh = hour12.toString().padLeft(2, '0');
    final mm = date.minute.toString().padLeft(2, '0');
    return '$hh:$mm $period';
  }

  String get boothsTitle => _t('الأجنحة', 'Booths');
  // The screen header per Figma frame 922:2458 node 922:2464 ("المعرض" — the
  // exhibition); distinct from the nav-tile/route label (boothsTitle).
  String get boothsExhibitionTitle => _t('المعرض', 'Exhibition');
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

  String get sponsorsTitle => _t('الرعاة', 'Sponsors');
  String get sponsorsError =>
      _t('تعذّر تحميل الرعاة.', 'Could not load the sponsors.');
  String get sponsorsEmpty => _t('لا يوجد رعاة', 'No sponsors');
  // The three sponsor band headers (Figma 922:2824). The API returns the raw
  // English tier enum name, so the app maps the tier weight to the localized
  // band header itself (Platinum→strategic, Gold→premium, Silver→gold band).
  String get sponsorTierStrategic =>
      _t('الرعاية الاستراتيجية', 'Strategic Partner');
  String get sponsorTierPremium => _t('رعاة بريميوم', 'Premium Sponsors');
  String get sponsorTierGold => _t('رعاة ذهبيون', 'Gold Sponsors');
  String get sponsorTierBronze => _t('الرعاة', 'Sponsors');
  String sponsorTierLabel(int tier, String fallback) {
    switch (tier) {
      case 10:
        return sponsorTierStrategic;
      case 20:
        return sponsorTierPremium;
      case 30:
        return sponsorTierGold;
      case 40:
        return sponsorTierBronze;
      default:
        return fallback;
    }
  }

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
  // Frame 927:3343 — the past-speakers overflow card shows "+N" over "آخرون".
  String get archiveOthersLabel => _t('آخرون', 'Others');
  String archiveStats(int attendees, int sessions, int speakers) => _t(
        '$attendees حضور · $sessions جلسة · $speakers متحدث',
        '$attendees attendees · $sessions sessions · $speakers speakers',
      );

  String get newsTitle => _t('الأخبار', 'News');
  String get newsError =>
      _t('تعذّر تحميل الأخبار.', 'Could not load the news.');
  String get newsEmpty => _t('لا توجد أخبار', 'No news');
  String get newsNotFound =>
      _t('الخبر غير موجود', 'This article was not found');

  String get galleryTitle => _t('معرض الصور والفيديوهات', 'Media gallery');
  String get galleryError =>
      _t('تعذّر تحميل الوسائط.', 'Could not load the media.');
  String get galleryEmpty => _t('لا توجد وسائط', 'No media yet');

  // About the forum (Page 037 · عن الملتقى) — KSA frame 1116:16448
  // (restructured: header + الرسالة + الرؤية + تفاصيل الملتقى + المحاور).
  String get aboutTitle => _t('عن الملتقى', 'About the forum');
  String get aboutError =>
      _t('تعذّر تحميل المحتوى.', 'Could not load the content.');
  String get aboutEmpty => _t('المحتوى قيد الإعداد', 'Content coming soon');
  // Header (frame 1116:16448) — the anchor mark + the forum name.
  String get aboutForumName =>
      _t('الملتقى الدولي البحري', 'The International Maritime Forum');
  String get aboutMissionTitle => _t('الرسالة', 'Mission');
  String get aboutVisionTitle => _t('الرؤية', 'Vision');
  String get aboutDetailsTitle => _t('تفاصيل الملتقى', 'Forum details');
  // تفاصيل الملتقى rows. Values mirror the Figma mock (1116:16448); the exact
  // event date is an open item — confirm with the client before publish.
  String get aboutDetailYearLabel => _t('السنة', 'Year');
  String get aboutDetailYearValue => '2026';
  String get aboutDetailDateLabel => _t('الزمن', 'Date');
  String get aboutDetailDateValue => '01-2026 — 04-2026';
  String get aboutDetailLocationLabel => _t('المكان', 'Location');
  String get aboutDetailLocationValue => _t('السعودية', 'Saudi Arabia');
  // D-495 — Organization-profile additions: status badge + contact + version.
  String aboutStatus(String status) {
    switch (status) {
      case 'Soon':
        return _t('قريباً', 'Coming soon');
      case 'Archived':
        return _t('مؤرشف', 'Archived');
      default:
        return _t('مفتوح', 'Open');
    }
  }

  String get aboutContactTitle => _t('التواصل', 'Contact');
  String get aboutContactPhone => _t('الهاتف', 'Phone');
  String get aboutContactEmail => _t('البريد الإلكتروني', 'Email');
  String get aboutContactWebsite => _t('الموقع الإلكتروني', 'Website');
  String get aboutVersionTitle => _t('معلومات النظام', 'System info');
  String get aboutVersionLabel => _t('الإصدار', 'Version');
  String get aboutHeroHeading => _t(
        'منصة سعودية عالمية لدعم الحوار في قضايا الأمن البحري',
        'A Saudi global platform advancing dialogue on maritime-security issues',
      );
  // Static fallback for the intro paragraph when the CMS `about` block is
  // empty.
  String get aboutHeroBody => _t(
        'الملتقى البحري السعودي الدولي حدث دولي رفيع المستوى، يجمع القادة '
            'والمسؤولين والخبراء لتبادل التجارب وتعزيز فهم عالمي مشترك لمستقبل '
            'الأمن البحري.',
        'The Saudi International Maritime Forum is a high-level international '
            'event that brings together leaders, officials and experts to share '
            'experience and build a shared global understanding of the future of '
            'maritime security.',
      );
  // المحاور الرئيسية — the four fixed forum themes (frames 1082:15578…15620).
  String get aboutThemesTitle => _t('المحاور الرئيسية', 'Main themes');
  String get aboutTheme1Title => _t(
        'المتغيرات في البيئة الاستراتيجية العالمية',
        'Shifts in the global strategic environment',
      );
  String get aboutTheme1Body => _t(
        'وتأثيرها على أمن سلاسل الإمداد البحرية',
        'and their impact on maritime supply-chain security',
      );
  String get aboutTheme2Title => _t(
        'التهديدات على سلاسل إمداد الطاقة',
        'Threats to energy supply chains',
      );
  String get aboutTheme2Body => _t(
        'وأثرها على الاقتصاد العالمي',
        'and their impact on the global economy',
      );
  String get aboutTheme3Title =>
      _t('حماية قاع البحار', 'Protecting the seabed');
  String get aboutTheme3Body =>
      _t('وأثره على الأمن الدولي', 'and its impact on international security');
  String get aboutTheme4Title => _t(
        'الأمن السيبراني للنقل البحري',
        'Cybersecurity for maritime transport',
      );
  String get aboutTheme4Body =>
      _t('التحديات والحلول', 'Challenges and solutions');

  // Rate / feedback (Page 040; Figma 1116:16894).
  String get rateTitle => _t('تقييم الملتقى', 'Rate the forum');
  String get rateKicker => _t('شارك تجربتك', 'Share your experience');
  String get rateLead =>
      _t('كيف كانت تجربتك في الملتقى؟', 'How was your forum experience?');
  String get rateStarsRequired =>
      _t('يرجى اختيار عدد النجوم', 'Please pick a star rating');

  /// The accessible name of one star in a 1–5 star bar (BUG-012): the stars are
  /// bare glyphs, so without it a screen-reader user met five unnamed tappables
  /// and could not submit a rating at all.
  String rateStarLabel(int stars) => switch (stars) {
        1 => _t('نجمة واحدة', '1 star'),
        2 => _t('نجمتان', '2 stars'),
        _ => _t('$stars نجوم', '$stars stars'),
      };

  /// D-713 (item 8) — the "watched" context header on a per-session rating: the
  /// session title + when it was held, so a user arriving from a rate prompt
  /// (or a notification days later) knows which session they are rating. A
  /// blank [when] (unknown session time) drops the trailing separator.
  String rateWatchedAt(String session, String when) {
    final base = _t('شاهدت «$session»', 'Watched "$session"');
    return when.isEmpty ? base : '$base · $when';
  }

  /// The one-word descriptor for an overall score (Figma "جيد جداً").
  String rateScoreWord(int stars) => switch (stars) {
        1 => _t('ضعيف جداً', 'Very poor'),
        2 => _t('ضعيف', 'Poor'),
        3 => _t('متوسط', 'Average'),
        4 => _t('جيد جداً', 'Very good'),
        _ => _t('ممتاز', 'Excellent'),
      };

  /// The "{n} من 5 · {word}" summary line under the overall stars. The leading
  /// count is wrapped in a Unicode LTR isolate (FSI/PDI) so the Western digit
  /// doesn't bidi-reorder against the Arabic text.
  String rateScoreSummary(int stars) {
    // FSI (U+2066) … PDI (U+2069) isolate the Western digit so it doesn't
    // bidi-reorder against the Arabic text (built via char codes to keep the
    // source free of invisible direction marks).
    final count =
        '${String.fromCharCode(0x2066)}$stars${String.fromCharCode(0x2069)}';
    return _t(
      '$count من 5 · ${rateScoreWord(stars)}',
      '$stars of 5 · ${rateScoreWord(stars)}',
    );
  }

  // "قيّم العناصر" — the per-element scores (Figma 1116:17143).
  String get rateElementsTitle => _t('قيّم العناصر', 'Rate the elements');
  String get rateCatOrganization => _t('التنظيم', 'Organization');
  String get rateCatContent => _t('المحتوى', 'Content');
  String get rateCatApp => _t('التطبيق', 'App');
  String get rateCatVenue => _t('المكان والمرافق', 'Venue & facilities');

  String get rateCommentLabel => _t('ملاحظاتك', 'Your notes');
  String get rateCommentHint =>
      _t('اكتب ملاحظاتك هنا...', 'Write your notes here...');
  String get rateSubmit => _t('إرسال التقييم', 'Submit rating');
  String get rateThanks => _t('شكراً لتقييمك', 'Thanks for your rating');
  String get rateFailed =>
      _t('تعذّر إرسال التقييم. حاول مرة أخرى.', 'Could not submit. Try again.');
  // Owner 2026-07-19 — shown when the visitor has not attended what they are
  // trying to rate (server: 403 RATING_NOT_ATTENDED / form isEligible=false).
  String get rateAttendRequired => _t(
        'يمكنك تقييم ما حضرته فقط.',
        'You can only rate something you attended.',
      );
  String get rateRequiredQuestions => _t(
        'يرجى الإجابة على جميع الأسئلة المطلوبة',
        'Please answer all required questions',
      );
  String get rateLoadFailed =>
      _t('تعذّر تحميل نموذج التقييم.', 'Could not load the rating form.');

  String get mediaPartnersTitle => _t('الشركاء الإعلاميون', 'Media partners');
  String get mediaPartnersError => _t(
      'تعذّر تحميل الشركاء الإعلاميين.', 'Could not load the media partners.',);
  String get mediaPartnersEmpty =>
      _t('لا يوجد شركاء إعلاميون', 'No media partners');

  String get notificationsTitle => _t('الإشعارات', 'Notifications');
  String get notificationsEmpty =>
      _t('لا توجد إشعارات بعد', 'No notifications yet');
  String get notificationsError =>
      _t('تعذّر تحميل إشعاراتك.', 'Could not load your notifications.');
  String get notificationsMarkAll => _t('تعليم الكل كمقروء', 'Mark all read');
  String get notificationsMarkAllFailed => _t('تعذّر تعليم الإشعارات كمقروءة.',
      'Could not mark the notifications read.',);
  String get notificationsSearchHint => _t('البحث', 'Search');
  String get notificationsFilterAll => _t('الكل', 'All');
  String get notificationsFilterSessions => _t('جلسات', 'Sessions');
  String get notificationsFilterVip => _t('VIP', 'VIP');
  String get notificationsNoMatches =>
      _t('لا توجد إشعارات مطابقة', 'No matching notifications');
  String get dayToday => _t('اليوم', 'Today');
  String get dayYesterday => _t('أمس', 'Yesterday');

  // Meet people (Page 035 · قابل أشخاص مثلك) — Build #13 partner directory
  // (Sponsors + Speakers + Booth companies + opted-in members).
  String get meetPeopleTitle => _t('قابل أشخاص مثلك', 'Meet people');
  String get meetPeopleEmpty =>
      _t('لا يوجد أشخاص لعرضهم بعد', 'No one to show yet');
  String get meetPeopleError =>
      _t('تعذّر تحميل الدليل.', 'Could not load the directory.');

  // Accessibility (Page 038; Figma 1116:16630 — client-local settings, no API).
  String get accessibilityTitle => _t('إمكانية الوصول', 'Accessibility');
  String get accessibilityIntro => _t(
        'اضبط تجربة العرض بما يناسبك. هذه الإعدادات محلية على جهازك.',
        'Adjust the display to suit you. These settings are local to your device.',
      );
  // Section headers (Figma العرض / الصوت والقراءة).
  String get accessibilitySectionDisplay => _t('العرض', 'Display');
  String get accessibilitySectionSound =>
      _t('الصوت والقراءة', 'Sound & reading');
  String get accessibilityTextSizeLabel => _t('حجم الخط', 'Font size');
  String get accessibilityTextSizeSmall => _t('صغير', 'Small');
  String get accessibilityTextSizeDefault => _t('متوسط', 'Medium');
  String get accessibilityTextSizeLarge => _t('كبير', 'Large');
  String get accessibilityTextSizeExtraLarge => _t('أكبر', 'Extra large');
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
  String get accessibilityScreenReaderTitle =>
      _t('قارئ الشاشة', 'Screen reader');
  String get accessibilityScreenReaderSubtitle => _t(
        'يُعلن اسم كل شاشة عند الانتقال إليها لمساعدة قارئ الشاشة.',
        'Announces each screen as you navigate, to assist your screen reader.',
      );
  String get accessibilityCaptionsTitle =>
      _t('الترجمة النصية (للجلسات)', 'Captions (for sessions)');
  String get accessibilityCaptionsSubtitle => _t(
        'يعرض شريط الترجمة النصية أثناء البث المباشر للجلسات.',
        'Shows the live caption strip during session broadcasts.',
      );

  /// Announced by the screen-reader assist when a screen opens.
  String accessibilityScreenAnnouncement(String screen) =>
      _t('فتح $screen', 'Opened $screen');

  // More hub (Page 041; Figma 1129:17224) — grouped sections + version line.
  String get moreTitle => _t('المزيد', 'More');
  // BUG-017 — the side drawer (a flat list of every destination) and the
  // Profile "More" hub (My area / Forum info / Settings / Legal, the only home
  // of the language row) were BOTH labelled "المزيد" / "More". The drawer is the
  // app's navigation menu, so it takes its own name; the hub keeps [moreTitle].
  String get menuTitle => _t('القائمة', 'Menu');
  String get moreAbout => _t('عن الملتقى', 'About the forum');
  String get moreAccessibility => _t('إمكانية الوصول', 'Accessibility');
  String get moreTerms => _t('الشروط والأحكام', 'Terms & conditions');
  String get moreRate => _t('تقييم', 'Rate');
  String get moreNotifications => _t('الإشعارات', 'Notifications');
  String get moreResetPassword =>
      _t('إعادة تعيين كلمة المرور', 'Reset password');
  String get moreMediaPartners => _t('الشركاء الإعلاميون', 'Media partners');

  // Section headers (Figma 1129:17224).
  String get moreSectionForumInfo => _t('معلومات الملتقى', 'Forum information');
  String get moreSectionSettings => _t('الإعدادات', 'Settings');
  String get moreSectionLegal => _t('قانوني', 'Legal');
  String get moreForumGuide => _t('دليل الملتقى', 'Forum guide');
  String get morePresentations => _t('عروض الجلسات', 'Session presentations');
  String get moreVisitSaudi => _t('اكتشف السعودية', 'Discover Saudi');

  // دليل الملتقى — Forum guide (Figma 1388-7493). Static in-app copy (no
  // backend). The Arabic strings are reproduced verbatim from the design; the
  // Figma leaves steps 3 & 5 with placeholder/duplicate copy (owner to supply
  // final wording).
  String get forumGuideTitle => _t('دليل الملتقى', 'Forum guide');
  String get forumGuideIntro => _t(
        'مرحبًا بك في ملتقى SIMF 2026. يهدف هذا الدليل إلى مساعدتك على الاستفادة القصوى من تجربتك في الملتقى.',
        'Welcome to SIMF 2026. This guide is here to help you get the most out of your forum experience.',
      );
  String get forumGuideStep1Title =>
      _t('التسجيل والدخول', 'Registration & sign-in');
  String get forumGuideStep1Body => _t(
        'قم بتسجيل الدخول باستخدام بريدك الإلكتروني وكلمة المرور المُرسلة إليك عند التسجيل في الملتقى.',
        'Sign in with the email and password sent to you when you registered for the forum.',
      );
  String get forumGuideStep2Title =>
      _t('استكشاف الجلسات', 'Explore the sessions');
  String get forumGuideStep2Body => _t(
        'تصفّح جدول الجلسات من الصفحة الرئيسية واختر الجلسة التي تودّ حضورها وأضفها إلى مفضلتك.',
        'Browse the session schedule from the home page, pick the session you want to attend, and add it to your favourites.',
      );
  String get forumGuideStep3Title =>
      _t('التسجيل الحضور والدخول', 'On-site registration & entry');
  String get forumGuideStep3Body => _t(
        'قم بتسجيل الدخول باستخدام بريدك الإلكتروني وكلمة المرور المُرسلة إليك عند التسجيل في الملتقى.',
        'Sign in with the email and password sent to you when you registered for the forum.',
      );
  String get forumGuideStep4Title =>
      _t('التواصل مع المتحدثين', 'Reach the speakers');
  String get forumGuideStep4Body => _t(
        'يمكنك إرسال أسئلتك للمتحدثين مباشرةً من خلال قسم الأسئلة في صفحة كل جلسة.',
        'You can send your questions to the speakers directly from the questions section on each session page.',
      );
  String get forumGuideStep5Title =>
      _t('التسجيل والدخول', 'Registration & sign-in');
  String get forumGuideStep5Body => _t(
        'قم بتسجيل الدخول باستخدام بريدك الإلكتروني وكلمة المرور المُرسلة إليك عند التسجيل في الملتقى.',
        'Sign in with the email and password sent to you when you registered for the forum.',
      );
  String get moreLanguage => _t('اللغة', 'Language');
  String get moreRateApp => _t('تقييم التطبيق', 'Rate the app');
  String get moreMyAreaCardTitle => _t('منطقتي', 'My area');

  /// The display name of the currently active language (shown on the اللغة
  /// row).
  String get languageCurrentName => _t('العربية', 'English');

  /// D-736 — the More-menu footer line over the REAL installed version
  /// (package_info_plus). Empty (a bare dev/test runtime) → the edition alone.
  String moreVersionLine(String version) => version.isEmpty
      ? 'SIMF 2026'
      : _t('SIMF 2026 · الإصدار $version', 'SIMF 2026 · v$version');
  // D-668 — About-the-app screen (version / release date / organizer + links).
  // The release date is a maintained constant (no build-date source in the
  // app).
  String get aboutAppTitle => _t('عن التطبيق', 'About the app');
  String get aboutAppInfoTitle => _t('معلومات التطبيق', 'App information');
  String get aboutAppReleaseDateLabel => _t('تاريخ الإصدار', 'Release date');
  String get aboutAppReleaseDate => _t('06-07-2026', '06-07-2026');
  String get aboutAppOrganizerLabel => _t('الجهة المنظمة', 'Organizer');
  String get aboutAppOrganizerValue =>
      _t('القوات البحرية الملكية السعودية', 'Royal Saudi Naval Forces');
  String get aboutAppLinksTitle => _t('روابط', 'Links');
  // D-736 — About-the-app manual update check (server version policy).
  String get aboutCheckForUpdates =>
      _t('التحقق من التحديثات', 'Check for updates');
  String get updateCheckingLabel => _t('جارٍ التحقق…', 'Checking…');
  String get aboutUpToDateTitle =>
      _t('أنت على أحدث إصدار', "You're up to date");
  String aboutUpToDateBody(String version) => version.isEmpty
      ? _t('لا يتوفر تحديث جديد.', 'No new update is available.')
      : _t('الإصدار الحالي: $version', 'Current version: $version');
  String aboutUpdateAvailableBody(String version) => version.isEmpty
      ? updateOptionalBody
      : _t(
          'يتوفر إصدار جديد ($version). ننصح بالتحديث للحصول على أحدث التحسينات.',
          'A new version ($version) is available. We recommend updating for the latest improvements.',
        );
  String get okLabel => _t('حسناً', 'OK');

  String get guestModeTitle => _t('وضع الضيف', 'Guest mode');
  String get guestModeHeadline => _t('التصفح كضيف', 'Browsing as guest');
  String get guestModeBrowseBody => _t(
        'يمكنك كضيف تصفّح المتحدثين والخريطة التفاعلية والوسائط.',
        'As a guest you can browse the speakers, the venue map and the media.',
      );
  String get guestModeSignInBody => _t(
        'سجّل الدخول لعرض الأجندة والبث المباشر، وللحصول على بطاقتك الذكية والإشعارات الشخصية وحجز المقاعد.',
        'Sign in to view the agenda and live broadcast, and to get your smart '
            'badge, personal notifications and booking.',
      );
  String get guestModeContinueButton =>
      _t('المتابعة كضيف', 'Continue as guest');
  String get guestModeSignInButton => _t('تسجيل الدخول', 'Sign in');

  // AI session summary (Page 034 · ملخص الجلسة) — KSA frame 1072:13518.
  String get aiSummaryTitle => _t('ملخص الجلسة', 'AI session summary');
  String get aiSummaryOpenFromSession => _t(
        'افتح ملخص جلسة من صفحة الجلسة.',
        'Open a session summary from a session.',
      );
  String get aiSummaryNone =>
      _t('لا يوجد ملخص منشور بعد.', 'No published summary yet.');
  String get aiSummaryError =>
      _t('تعذر تحميل الملخص.', 'Could not load the summary.');
  String get aiSummaryKeyPointsHeading => _t('أبرز النقاط', 'Key points');
  String get aiSummaryRecommendationsHeading =>
      _t('التوصيات', 'Recommendations');
  String get aiSummarySpeakersHeading => _t('المتحدثون', 'Speakers');
  // Figma 1072:13518 — the redesigned session-summary screen.
  String get aiSummarySessionLabel => _t('الجلسة', 'Session');
  // Item #35 (2026-07-20) — labels for the two video players on the summary
  // surface: the session's FULL live recording and the team's short summary
  // cut.
  String get aiSummaryRecordingLabel => _t('التسجيل الكامل', 'Full recording');
  String get aiSummaryVideoLabel =>
      _t('ملخص الجلسة (فيديو)', 'Session summary (video)');
  String get aiSummaryGenerateButton =>
      _t('توليد ملخص للجلسة', 'Generate session summary');
  String get aiSummaryNoSessions =>
      _t('لا توجد جلسات متاحة بعد.', 'No sessions available yet.');

  // Wave 2 — session-summaries list (Figma 1388:8392): search + the three
  // الكل / جلساتي / المفضلة tabs over the cached programme.
  // The list-screen header (plural) — distinct from [aiSummaryTitle] (the
  // single-session detail header).
  String get sessionSummariesTitle => _t('ملخص الجلسات', 'Session summaries');
  String get sessionSummarySearchHint =>
      _t('ابحث عن جلسة أو متحدث...', 'Search a session or speaker...');
  String get sessionsTabAll => _t('الكل', 'All');
  String get sessionsTabMine => _t('جلساتي', 'My sessions');
  String get sessionsTabFavourites => _t('المفضلة', 'Favourites');
  String get sessionsNoFavourites =>
      _t('لا توجد جلسات مفضلة بعد.', 'No favourite sessions yet.');
  String get sessionsNoMine =>
      _t('لا توجد جلسات محجوزة بعد.', 'No booked sessions yet.');
  String get sessionsNoMatch =>
      _t('لا توجد نتائج مطابقة.', 'No matching results.');
  // Owner 2026-07-14 — the summaries list holds only sessions with a PUBLISHED
  // محضر, so a future / not-yet-summarised programme shows this, not "no
  // sessions".
  String get sessionSummariesEmpty =>
      _t('لا توجد ملخصات منشورة بعد.', 'No published summaries yet.');
  String get sessionRecordedBadge => _t('مسجّل', 'Recorded');
  // Owner 2026-07-14 — session state chips on the agenda / my-sessions / summary
  // cards (live now · a published محضر is available).
  String get sessionLiveBadge => _t('مباشر الآن', 'Live now');
  String get sessionSummaryReadyBadge => _t('الملخص متاح', 'Summary ready');
  String get favouriteToggleError =>
      _t('تعذر تحديث المفضلة.', 'Could not update favourites.');
  String sessionDurationMinutes(int minutes) =>
      _t('$minutes دقيقة', '$minutes min');

  // Wave 2 — "my sessions" list, App "تفاصيل الجلسات" (Figma 1388:9067),
  // reached from the My-Area "my sessions" counter. Four tabs partition the
  // user's booked / joined sessions. App title matches Figma 1388:9067 ("عروض
  // الجلسات"); the EN stays "My sessions" (distinct from the
  // downloadable-slides screen) for clarity.
  String get mySessionsTitle => _t('عروض الجلسات', 'My sessions');
  String get mySessionsTabUpcoming => _t('القادمة', 'Upcoming');
  String get mySessionsTabAttended => _t('حضرتها', 'Attended');
  String get mySessionsTabMissed => _t('فاتتني', 'Missed');
  String get mySessionsTabArchive => _t('الأرشيف', 'Archive');
  String get mySessionsError =>
      _t('تعذر تحميل جلساتك.', 'Could not load your sessions.');
  String get mySessionsEmpty =>
      _t('لا توجد جلسات في هذه القائمة.', 'No sessions in this list.');
  // The count subtitle, e.g. "3 جلسات قادمة" / "3 upcoming sessions".
  String mySessionsCount(int count, String tabLabel) =>
      _t('$count جلسة · $tabLabel', '$count · $tabLabel');

  // #8 — Saved sessions, App "الجلسات المحفوظة" (Figma 1701:8928), reached from
  // the My-Area saved-sessions counter. The favourited sessions (المفضلة =
  // محفوظة) with a saved-count header + category chips over the cached
  // programme.
  String get savedSessionsTitle => _t('الجلسات المحفوظة', 'Saved sessions');
  // The gold count-row unit label, rendered as "$count جلسة محفوظة".
  String get savedSessionsCountLabel => _t('جلسة محفوظة', 'saved sessions');
  String get savedSessionsEmpty =>
      _t('لا توجد جلسات محفوظة بعد.', 'No saved sessions yet.');

  // المقابلات, App "المقابلات" (Figma 1701:9406), reached from the My-Area
  // "مقابلات" counter. The caller's speaker + delegation meetings as person
  // cards over four status filter chips. Reuses the الطلبات feed (read-only).
  String get myMeetingsTitle => _t('المقابلات', 'My meetings');
  String get myMeetingsFilterCompleted => _t('مكتملة', 'Completed');
  String get myMeetingsFilterPending => _t('قيد الانتظار', 'Pending');
  String get myMeetingsFilterRejected => _t('مرفوضة', 'Rejected');
  // The neutral badge on an accepted meeting card (Figma 1701:9446).
  String get myMeetingBadgeConfirmed => _t('مؤكدة', 'Confirmed');
  String get myMeetingsEmpty => _t('لا توجد مقابلات بعد.', 'No meetings yet.');
  // The list section header, rendered as "جميع المقابلات ($count)".
  String myMeetingsAllHeader(int count) =>
      _t('جميع المقابلات ($count)', 'All meetings ($count)');

  // Wave 4 — Delegations, App "الوفود" (Figma 1426:10771): the invited
  // countries' delegations with head of delegation, date range and member
  // count.
  String get delegationsTitle => _t('الوفود', 'Delegations');
  String get delegationsSearchHint =>
      _t('ابحث عن دولة أو وفد...', 'Search for a country or delegation...');
  String get delegationsError =>
      _t('تعذر تحميل الوفود.', 'Could not load delegations.');
  String get delegationsEmpty => _t('لا توجد وفود بعد.', 'No delegations yet.');
  String get delegationsNoResults =>
      _t('لا توجد نتائج مطابقة.', 'No matching results.');
  String get delegationsCountriesStat =>
      _t('دولة مشاركة', 'Participating countries');
  String get delegationsParticipantsStat =>
      _t('إجمالي المشاركين', 'Total participants');
  String get delegationsHeadLabel => _t('رئيس الوفد', 'Head of delegation');

  /// The active-filter chip shown when a stats-strip flag isolates one country;
  /// tapping the chip clears the flag filter.
  String get delegationsClearFilter => _t('عرض كل الدول', 'Show all countries');

  /// The member count, e.g. "12 عضو" / "12 members" (with the Arabic plural).
  String delegationsMembers(int count) {
    if (!isArabic) {
      return count == 1 ? '1 member' : '$count members';
    }
    if (count == 1) {
      return 'عضو واحد';
    }
    if (count == 2) {
      return 'عضوان';
    }
    if (count >= 3 && count <= 10) {
      return '$count أعضاء';
    }
    return '$count عضواً';
  }

  /// The delegation date range, e.g. "12 يناير – 15 يناير" / "12 Jan – 15 Jan".
  /// Falls back to whichever single date is present; '' when neither is set.
  String delegationsDateRange(DateTime? start, DateTime? end) {
    final from = start == null ? null : _shortDate(start);
    final to = end == null ? null : _shortDate(end);
    if (from != null && to != null) {
      return '$from – $to';
    }
    return from ?? to ?? '';
  }

  String _shortDate(DateTime date) {
    const arabicMonths = <String>[
      'يناير',
      'فبراير',
      'مارس',
      'أبريل',
      'مايو',
      'يونيو',
      'يوليو',
      'أغسطس',
      'سبتمبر',
      'أكتوبر',
      'نوفمبر',
      'ديسمبر',
    ];
    const englishMonths = <String>[
      'Jan',
      'Feb',
      'Mar',
      'Apr',
      'May',
      'Jun',
      'Jul',
      'Aug',
      'Sep',
      'Oct',
      'Nov',
      'Dec',
    ];
    final month = (isArabic ? arabicMonths : englishMonths)[date.month - 1];
    return '${date.day} $month';
  }

  // Wave 2 — session-presentations list, App "عروض الجلسات" (Figma 1388:7621):
  // downloadable decks grouped by day, each with a تحميل button.
  // Owner 2026-07-03: the screen header matches the Home "الجلسات" tile
  // (Figma 1388:7621 header is "الجلسات"), so both read the same word.
  String get sessionPresentationsTitle => _t('الجلسات', 'Sessions');
  String get presentationsEmpty =>
      _t('لا توجد عروض متاحة بعد.', 'No presentations available yet.');
  String get presentationsError =>
      _t('تعذر تحميل العروض.', 'Could not load the presentations.');
  // The gold button label on the الجلسات cards. Kept the word "تحميل" (owner) —
  // as of D-592 it opens the session summary (34), not a file download.
  String get presentationDownload => _t('تحميل', 'Download');

  // Event day group header, 1-based ("اليوم الأول" / "Day 1") — shared by the
  // session-summaries (8392) + presentations (7621) day grouping.
  String eventDayLabel(int dayIndex) {
    const arabicOrdinals = <String>[
      'الأول',
      'الثاني',
      'الثالث',
      'الرابع',
      'الخامس',
      'السادس',
      'السابع',
    ];
    if (isArabic) {
      final ordinal = dayIndex >= 1 && dayIndex <= arabicOrdinals.length
          ? arabicOrdinals[dayIndex - 1]
          : '$dayIndex';
      return 'اليوم $ordinal';
    }
    return 'Day $dayIndex';
  }

  // Wave 3 — exhibitor (Figma 1439:11881) + sponsor (1439:11826) detail screens
  // (shared template). The tier pill prefixes a localized tier word onto the
  // role.
  String get exhibitorDetailTitle => _t('العارض', 'Exhibitor');
  String get sponsorDetailTitle => _t('الراعي', 'Sponsor');
  String get exhibitorAboutHeader =>
      _t('نبذة عن العارض', 'About the exhibitor');
  String get sponsorAboutHeader => _t('نبذة عن الراعي', 'About the sponsor');
  String get standLocationLabel =>
      _t('موقع الجناح على الخريطة', 'Booth location on the map');
  String get websiteLabel => _t('الموقع الإلكتروني', 'Website');
  String get entityDetailError =>
      _t('تعذر تحميل التفاصيل.', 'Could not load the details.');

  /// The exhibitor tier pill, e.g. "عارض بريميوم" / "Premium exhibitor".
  String exhibitorTierPill(String tierName) =>
      _t('عارض ${_tierWord(tierName)}', '${_tierWord(tierName)} exhibitor');

  /// The sponsor tier pill, e.g. "رعاية بريميوم" / "Premium sponsor".
  String sponsorTierPill(String tierName) =>
      _t('رعاية ${_tierWord(tierName)}', '${_tierWord(tierName)} sponsor');

  /// Maps the wire tier enum name (Premium/Platinum/Gold/Silver/Bronze) to its
  /// localized word; an unknown value passes through unchanged.
  String _tierWord(String tierName) {
    if (!isArabic) {
      return tierName;
    }
    switch (tierName) {
      case 'Premium':
        return 'بريميوم';
      case 'Platinum':
        return 'بلاتيني';
      case 'Gold':
        return 'ذهبي';
      case 'Silver':
        return 'فضي';
      case 'Bronze':
        return 'برونزي';
      default:
        return tierName;
    }
  }

  // Send a question (Page 026 — live Q&A composer).
  // Figma 934:3636 retitled the screen to "معلومات عن الجلسة" (Session
  // information) — the session-data block sits above the question composer.
  String get sessionInfoTitle => _t('معلومات عن الجلسة', 'Session information');
  // The session-data section header above the question composer (Figma
  // 1049:12590).
  String get sessionDataLabel => _t('بيانات الجلسة', 'Session details');
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
  // DEF-MOD-006 — the copy promised a 5-minute pre-start window the server has
  // never enforced. `SessionQuestionService.SubmitAsync` has NO lower bound (a
  // future session takes questions ahead of time) and closes them the moment
  // the session ends; once live, the hall-arrival gate applies. The string now
  // describes that behaviour instead of inventing a rule.
  String get sendQuestionNotOpen => _t(
        'الأسئلة مغلقة لهذه الجلسة.',
        'Questions are closed for this session.',
      );
  String get sendQuestionFailed => _t(
        'تعذر إرسال سؤالك. حاول مرة أخرى.',
        'Could not send your question. Try again.',
      );
  // A17 — the copy promised a review that does not happen for a LIVE question.
  // `SessionQuestionService.SubmitAsync` only screens (AI, advisory) and
  // queues for the Scientific Committee when the question is asked BEFORE the
  // session starts; once it is live the question lands Approved with no AI and
  // no committee, and the session's moderator alone decides what is pushed on
  // air. The string now names the gate that is always real — the moderator.
  String get sendQuestionWindowHint => _t(
        'يختار مشرف الجلسة الأسئلة التي تُعرض على الهواء.',
        'The session moderator picks which questions go on air.',
      );

  // (D-605/D-609: the Audience-comments (Page 028) l10n strings were removed
  // with the feature — rejected by customer.)

  String get badgeTitle => _t('بطاقة الدخول', 'Entry badge');
  String get badgeShowAtEntry =>
      _t('أبرز هذه البطاقة عند الدخول', 'Show this at entry');
  String get badgePendingBody => _t(
        'ستتوفر بطاقتك بعد اعتماد حسابك.',
        'Your badge is available once your account is approved.',
      );
  String get badgeError =>
      _t('تعذّر تحميل بطاقتك.', 'Could not load your badge.');
  String get badgeNotApprovedBody => _t(
        'حسابك غير معتمد بعد. ستتوفر بطاقة الدخول بعد اعتماد حسابك.',
        'Your account is not approved yet. Your entry badge will be available '
            'once your account is approved.',
      );
  // BUG-013 — the TRUE-guest copy (no account at all). The bottom nav switches
  // tabs inside the shell, so the router's auth redirect never runs and a
  // signed-out visitor lands on this tab; it used to show the PENDING copy
  // above, which describes a submitted registration that does not exist.
  String get badgeGuestBody => _t(
        'سجّل الدخول أو أنشئ حساباً للحصول على بطاقة الدخول الخاصة بك.',
        'Sign in or create an account to get your entry badge.',
      );
  // KSA Wave-2 frame 221:769 copy.
  String get badgeScanToEnter => _t('امسح للدخول', 'Scan to enter');
  String get badgeAddPerson => _t('امسح لإضافة شخص', 'Scan to add a contact');

  // D-426 — QR-page role actions + exhibitor lead capture.
  String get badgeScanVisitor => _t('مسح بطاقة زائر', 'Scan visitor badge');
  String get myVisitorsTitle => _t('زوار جناحي', 'My Booth Visitors');
  // BUG-025 — "My Booth Visitors" (exhibitor lead capture) and "My Contacts"
  // (visitor-to-visitor card sharing) are two separate lists. This one line
  // states which is which so the two are never confused.
  String get myVisitorsNote => _t(
        'بطاقات الزوار التي مسحتها في جناحك. قائمة منفصلة عن «جهات اتصالي».',
        'Badges you scanned at your booth. This list is separate from My Contacts.',
      );
  String get myVisitorsEmpty => _t(
        'لم تقم بمسح أي زائر بعد. امسح بطاقة زائر في جناحك لإضافته هنا.',
        'No booth visitors yet. Scan a visitor badge at your booth to capture '
            'them here.',
      );
  // FR-EXH-002 — the lead list gained the remove + vCard export My Contacts has
  // had since D-286. The wording deliberately mirrors the My-Contacts sheet so
  // the two card lists behave the same way.
  String get myVisitorsNoteLabel => _t('ملاحظة', 'Note');
  String get myVisitorsExportVcard => _t('تصدير vCard', 'Export vCard');
  String get myVisitorsRemove => _t('إزالة', 'Remove');
  String get myVisitorsRemoveConfirmTitle =>
      _t('إزالة هذا الزائر؟', 'Remove this visitor?');
  String get myVisitorsRemoveConfirmBody => _t(
        'سيتم حذف هذا الزائر من قائمة جناحي. يمكنك مسح بطاقته مرة أخرى لاحقاً.',
        "This visitor will be removed from your booth's list. You can scan their "
            'badge again later.',
      );
  String get myVisitorsRemoved => _t('تمت إزالة الزائر', 'Visitor removed');
  String get scanVisitorTitle => _t('مسح بطاقة زائر', 'Scan visitor badge');
  String get scanVisitorCaptured => _t(
      'تمت إضافة الزائر إلى زوار جناحي', 'Visitor added to My Booth Visitors',);
  String get scanVisitorNotFound =>
      _t('لا توجد بطاقة زائر مطابقة', 'No matching visitor badge');
  String get scanVisitorForbidden => _t(
        'مسح بطاقات الزوار متاح لحسابات العارضين فقط.',
        'Only exhibitor accounts can scan visitor badges.',
      );
  String get scanVisitorError => _t('تعذر مسح البطاقة. حاول مرة أخرى.',
      'Could not scan the badge. Try again.',);

  /// D-519 — the exhibitor home's lead-capture tools section header.
  String get exhibitorToolsSection => _t('أدوات العارض', 'Exhibitor tools');

  // Live broadcast (Page 025). liveNowLabel already exists (reused for the
  // badge).
  String get liveBroadcastTitle => _t('البث المباشر', 'Live broadcast');
  // Login-gate (owner, 2026-07-01): the live stream is login-only — a
  // signed-out guest sees this prompt (with the shared signInButton label)
  // instead of the player.
  String get liveNeedLogin => _t(
        'سجّل الدخول لمشاهدة البث المباشر.',
        'Sign in to watch the live stream.',
      );
  // D-433 — live broadcast + ask-question + media-coverage re-skins
  // (Figma 934-3450 / 934-3636 / 947-3764 / 958-2246).
  String get liveNowBroadcasting => _t('يُبث الآن', 'Now broadcasting');
  String get liveSessionLabel => _t('الجلسة', 'Session');
  // A15 (2026-07-26) — the caption strip renders the admin-typed
  // `Session.LiveCaptions` note, which never changes during the broadcast. The
  // old copy promised live AI translation of the spoken audio, which the app
  // does not do (there is no speech-to-text and no streaming translation), so
  // the placeholder now names what the strip actually is: an organiser note.
  String get liveCaptionHint => _t(
        'يظهر هنا النص التوضيحي الذي يكتبه المنظّم لهذه الجلسة.',
        'Caption text written by the organiser for this session appears here.',
      );
  String get liveAskQuestion => _t('اطرح سؤالاً', 'Ask a question');
  String get liveUpcomingSessions => _t('الجلسات القادمة', 'Upcoming sessions');
  String get sendQuestionSectionLabel => _t('الاسئلة', 'Questions');
  String get sendQuestionNoteLabel => _t('ملاحظة', 'Note');
  // Media-center hub header — Figma 947:3764 / 1049:12629 renamed the container
  // from "التغطية الإعلامية" to "المركز الاعلامي".
  String get mediaCoverageTitle => _t('المركز الاعلامي', 'Media center');
  // The news tab label inside the media center — Figma calls it "احدث
  // المستجدات" (Latest updates), not the bare "الأخبار" screen name.
  String get latestUpdatesTitle => _t('احدث المستجدات', 'Latest updates');
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

  // AI assistant (Page 036 · المساعد الذكي) — KSA frame 1064:13066. Backed by
  // the centralised AI (POST /app/ai/assistance, grounded on the live event
  // context); the screen opens with the greeting and answers each prompt
  // through that endpoint.
  String get chatbotTitle => _t('المساعد الذكي', 'AI assistant');
  String get chatbotInputHint => _t('اكتب رسالتك...', 'Type your message…');
  String get chatbotSendTooltip => _t('إرسال', 'Send');
  String get chatbotGreeting => _t(
        'مرحباً 🤝 أنا مساعدك الذكي. كيف يمكنني المساعدة اليوم؟',
        'Hello 🤝 I’m your smart assistant. How can I help today?',
      );
  String get chatbotError => _t(
        'تعذّر الحصول على رد الآن. حاول مرة أخرى.',
        'Couldn’t get a reply right now. Please try again.',
      );
  // The four quick-reply chips under the transcript (frame 1070:13389).
  String get chatbotChipMeeting => _t('طلب لقاء', 'Request a meeting');
  String get chatbotChipUpcoming => _t('الجلسات القادمة', 'Upcoming sessions');
  String get chatbotChipSami => _t('مكان جناح SAMI', 'SAMI booth location');
  String get chatbotChipToday => _t('جلسات اليوم', 'Today’s sessions');

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
  String get scanContactNotFound => _t(
      'رمز غير صالح أو لم يعد متاحاً.', 'Code not found or no longer valid.',);
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

  /// The splash edition line (#40-residual). Split out of [splashEventLine] so
  /// the date/location half can come from the CP-configured organization
  /// profile while the edition ordinal stays a bundled literal (the profile
  /// carries no edition ordinal).
  String get splashEditionLine => _t('النسخة الرابعة', '4th Edition');

  /// Session detail — what the hall door recorded. The five strings that drove
  /// the old GPS self check-in (the "أنا هنا / I'm here" button, the
  /// outside-the-boundary, no-boundary-configured and permission-denied
  /// outcomes, and the "Check out" toggle) went with it on 2026-07-31: arrival
  /// is established by the gate scan, so the app neither claims an arrival nor
  /// asks for a location permission.
  String get sessionArrivalCheckedIn =>
      _t('تم تسجيل حضورك في القاعة', 'Your hall arrival is recorded');
  String get sessionArrivalDeparted =>
      _t('تم تسجيل مغادرتك', 'Your departure is recorded');

  /// Session detail — the read-only hall check-in STATUS (owner 2026-07-31:
  /// arrival is established by the gate scan at the hall door, not by GPS). The
  /// "not yet" line is an instruction, not an error; the error line is only for
  /// a failed read of the status itself.
  String get sessionArrivalNotYet => _t(
        'لم يُسجَّل حضورك في القاعة بعد. أبرز بطاقتك عند باب القاعة لتسجيل '
            'الحضور.',
        'You are not checked in yet. Show your badge at the hall door to be '
            'checked in.',
      );
  String get sessionArrivalStatusError => _t(
        'تعذّر تحميل حالة حضورك في القاعة.',
        'Could not load your hall check-in status.',
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
