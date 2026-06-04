using SIMF.Application.IdentityAccess.Abstractions;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Translates ASP.NET Core Identity error descriptions into Arabic so the
/// bilingual error contract (D-030) can carry an Arabic message for every
/// validator failure raised by Identity (the password policy, the user store).
/// Unknown Identity codes fall back to the English description — the field
/// stays non-empty so the contract holds, and an unknown code surfaces clearly.
///
/// <para>H21 — D-082: takes <see cref="UserOperationError"/> instead of
/// <c>Microsoft.AspNetCore.Identity.IdentityError</c> — Application no
/// longer depends on Identity types. The <c>Code</c> string keeps the
/// Identity-compatible value (e.g. <c>"PasswordMismatch"</c>) so the
/// switch arms below are unchanged.</para>
/// </summary>
internal static class IdentityErrorTranslator
{
    /// <summary>
    /// Returns the Arabic version of an Identity error description. The mapping
    /// is by Identity's stable <c>Code</c> string.
    /// </summary>
    public static string ToArabic(UserOperationError error) =>
        error.Code switch
        {
            "PasswordTooShort" => "كلمة المرور قصيرة جدًا.",
            "PasswordRequiresNonAlphanumeric" => "يجب أن تحتوي كلمة المرور على رمز خاص.",
            "PasswordRequiresDigit" => "يجب أن تحتوي كلمة المرور على رقم.",
            "PasswordRequiresLower" => "يجب أن تحتوي كلمة المرور على حرف صغير.",
            "PasswordRequiresUpper" => "يجب أن تحتوي كلمة المرور على حرف كبير.",
            "PasswordRequiresUniqueChars" => "يجب أن تحتوي كلمة المرور على أحرف متنوعة.",
            "PasswordMismatch" => "كلمة المرور الحالية غير صحيحة.",
            "DuplicateUserName" => "اسم المستخدم مستخدم بالفعل.",
            "DuplicateEmail" => "البريد الإلكتروني مسجّل بالفعل.",
            "InvalidEmail" => "البريد الإلكتروني غير صالح.",
            "InvalidUserName" => "اسم المستخدم غير صالح.",
            "UserAlreadyHasPassword" => "للمستخدم كلمة مرور بالفعل.",
            "UserLockoutNotEnabled" => "قفل الحساب غير مفعّل لهذا المستخدم.",
            _ => error.Description,
        };
}
