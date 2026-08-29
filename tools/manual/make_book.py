"""Author the manual's content and write docs/manuals/source/book.json.

Two rules govern everything in here.

1. **Every user-interface word comes from the Control Panel's own resource
   files.** `L("Admin.CreateUser.Email")` resolves to the English and Arabic
   strings the running application shows. Nothing in this file re-translates a
   label by hand, so a button renamed in the product is renamed in both volumes
   on the next build, and the Arabic volume says what the Arabic screen says.

2. **Every constraint is a fact read out of the code**, with the file that
   states it named in the manual, so a reader can check it and a maintainer can
   find it. The numbers below were taken from the FluentValidation validators,
   the options classes and the permission catalogue.

Run:  python tools/manual/make_book.py
"""

import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from blocks import bullets, figure, h2, h3, note, p, t, table
from content_accounts import chapter_changing_accounts, chapter_roles
from content_ai import chapter_knowledge_ai
from content_exhibition import chapter_exhibition
from content_modules import chapter_reference_data
from content_eventday import chapter_event_day
from content_pr import chapter_public_relations
from content_programme import chapter_programme
from content_publishing import chapter_content
from content_reports import chapter_reports
from content_system import chapter_system
from content_ops import (chapter_configuration, chapter_deployment,
                         chapter_observations)

REPO = Path(__file__).resolve().parents[2]
CP = REPO / "src/ControlPanel/SIMF.ControlPanel"
SOURCE = REPO / "docs/manuals/source"


def _read_resx(path):
    values = {}
    for data in ET.parse(path).getroot().findall("data"):
        name = data.get("name")
        value = data.find("value")
        if name and value is not None and value.text is not None:
            values[name] = value.text
    return values


EN = _read_resx(CP / "Resources/Strings.resx")
AR = _read_resx(CP / "Resources/Strings.ar.resx")


def L(key):
    """The application's own words for one resource key, in both languages."""
    if key not in EN:
        raise KeyError(f"resource key not found in Strings.resx: {key}")
    if key not in AR:
        raise KeyError(f"resource key has no Arabic value: {key}")
    return {"en": EN[key], "ar": AR[key]}


def field_table(rows):
    """A form's fields. `rows` are (label_key_or_pair, required, limit, rule)."""
    rows_en, rows_ar = [], []
    for label, required, limit, rule in rows:
        label_pair = L(label) if isinstance(label, str) else label
        rows_en.append([label_pair["en"], required["en"], limit["en"], rule["en"]])
        rows_ar.append([label_pair["ar"], required["ar"], limit["ar"], rule["ar"]])
    return table(
        ["Field", "Required", "Limit", "Rule the server enforces"],
        ["الحقل", "مطلوب", "الحد", "القاعدة التي يطبّقها الخادم"],
        rows_en, rows_ar)


YES = t("Yes", "نعم")
NO = t("No", "لا")
DASH = t("—", "—")


# ---------------------------------------------------------------- chapters --

def chapter_getting_in():
    return {
        "id": "getting-in",
        "title": t("Signing in for the first time",
                   "تسجيل الدخول لأول مرة"),
        "blocks": [
            p("The Control Panel is the operations console for the Saudi "
              "International Maritime Forum. Everything in this manual is done "
              "from it, so this chapter comes first: it covers the sign-in "
              "screen, the password change the system forces on a new account, "
              "the second factor, and the recovery codes you are shown exactly "
              "once.",
              "لوحة التحكم هي وحدة تشغيل الملتقى البحري السعودي الدولي. كل ما "
              "يرد في هذا الدليل يُنفَّذ من خلالها، ولذلك يأتي هذا الفصل أولًا: "
              "فهو يغطي شاشة تسجيل الدخول، وتغيير كلمة المرور الذي يفرضه النظام "
              "على الحساب الجديد، والعامل الثاني، ورموز الاسترداد التي تُعرض "
              "مرة واحدة فقط."),

            h2("The sign-in screen", "شاشة تسجيل الدخول"),
            p("Open the Control Panel address in a browser. The sign-in page "
              "asks for an email address and a password, and offers a language "
              "switch and a light/dark switch in the top corner. The language "
              "switch changes the whole application, not just this page.",
              "افتح عنوان لوحة التحكم في المتصفح. تطلب صفحة تسجيل الدخول البريد "
              "الإلكتروني وكلمة المرور، وتتيح في الزاوية العليا مبدّل اللغة "
              "ومبدّل المظهر الفاتح والداكن. يغيّر مبدّل اللغة التطبيق بأكمله، "
              "لا هذه الصفحة وحدها."),
            figure("cp-login-default",
                   "The sign-in page as it opens.",
                   "صفحة تسجيل الدخول عند فتحها."),

            h2("The forced password change", "تغيير كلمة المرور الإلزامي"),
            p("A newly created account is issued a bootstrap password that the "
              "system insists is replaced. On the first sign-in the Control "
              "Panel therefore does not go to the dashboard: it opens a dialog "
              "over the sign-in page asking for a new password twice. Until "
              "that is done the account cannot get in.",
              "يُمنح الحساب المُنشأ حديثًا كلمة مرور أولية يُصرّ النظام على "
              "استبدالها. لذلك لا تنتقل لوحة التحكم عند أول تسجيل دخول إلى لوحة "
              "المعلومات، بل تفتح نافذة فوق صفحة الدخول تطلب كلمة مرور جديدة "
              "مرتين. ولا يمكن للحساب الدخول قبل إتمام ذلك."),
            figure("cp-login-password-change-empty",
                   "The password change is a dialog over the sign-in page, not a separate address.",
                   "تغيير كلمة المرور نافذة فوق صفحة الدخول، وليس عنوانًا منفصلًا."),
            p("The new password must satisfy the rules the server applies to "
              "every password in the system. They are stricter than the length "
              "rule most people expect, and the two that surprise operators are "
              "the last two:",
              "يجب أن تستوفي كلمة المرور الجديدة القواعد التي يطبّقها الخادم على "
              "كل كلمات المرور في النظام. وهي أكثر صرامة من قاعدة الطول التي "
              "يتوقعها معظم الناس، وأكثر ما يفاجئ المشغّلين هما القاعدتان "
              "الأخيرتان:"),
            bullets([
                "Between 8 and 128 characters.",
                "At least one capital letter, one small letter, one digit and "
                "one symbol.",
                "No character repeated three times in a row — \"aa\" is allowed, "
                "\"aaa\" is not.",
                "No run of three characters in sequence within one class — "
                "\"abc\", \"123\", \"cba\" and \"987\" are all rejected.",
                "Not the account's own email address, and not a commonly used "
                "password (leet-speak spellings such as p@ssw0rd are folded back "
                "and rejected too).",
            ], [
                "بين 8 و128 حرفًا.",
                "حرف كبير واحد على الأقل، وحرف صغير، ورقم، ورمز.",
                "لا يتكرر أي حرف ثلاث مرات متتالية — «aa» مقبولة و«aaa» مرفوضة.",
                "لا تسلسل من ثلاثة أحرف متتابعة داخل الفئة نفسها — «abc» و«123» "
                "و«cba» و«987» جميعها مرفوضة.",
                "ألا تكون البريد الإلكتروني للحساب نفسه، وألا تكون من كلمات "
                "المرور الشائعة (وتُردّ الكتابة المموّهة مثل p@ssw0rd إلى أصلها "
                "وتُرفض كذلك).",
            ]),
            note("These rules are enforced in PasswordPolicy.cs and applied by "
                 "SimfPasswordValidator on every path that sets a password.",
                 "تُطبَّق هذه القواعد في PasswordPolicy.cs عبر SimfPasswordValidator "
                 "في كل مسار يضبط كلمة مرور."),

            h2("The second factor", "العامل الثاني"),
            p("The Control Panel requires two-factor authentication. After the "
              "password the system asks for the six-digit code from an "
              "authenticator application. If the account has never paired one, "
              "the system shows the pairing screen instead and will not let the "
              "account past it — enrolment is mandatory, not optional.",
              "تشترط لوحة التحكم المصادقة الثنائية. بعد كلمة المرور يطلب النظام "
              "الرمز المكوّن من ستة أرقام من تطبيق المصادقة. وإذا لم يسبق للحساب "
              "إقران تطبيق، يعرض النظام شاشة الإقران ولا يسمح بتجاوزها — "
              "فالتسجيل إلزامي وليس اختياريًا."),
            figure("cp-login-totp-empty",
                   "The second-factor step, after the password has been accepted.",
                   "خطوة العامل الثاني بعد قبول كلمة المرور."),
            figure("cp-login-enrol-2fa-qr",
                   "The pairing screen an account meets when it has no authenticator "
                   "yet. The QR code and the key beneath it are obscured here: they "
                   "are a real credential, and a manual should never print one.",
                   "شاشة الإقران التي يواجهها الحساب الذي لا يملك تطبيق مصادقة بعد. "
                   "وقد حُجب رمز الاستجابة السريعة والمفتاح أسفله هنا: فهما وثيقة "
                   "حقيقية، ولا ينبغي لدليل أن يطبع واحدة."),
            note("The requirement is controlled by "
                 "IdentityLifecycle:RequireControlPanelTwoFactorEnrolment, which "
                 "ships as true. It applies to the Control Panel only — the "
                 "mobile application and the public website are not affected.",
                 "يتحكم في هذا الاشتراط المفتاح "
                 "IdentityLifecycle:RequireControlPanelTwoFactorEnrolment، وقيمته "
                 "الافتراضية true. وهو يخص لوحة التحكم وحدها — ولا يؤثر على "
                 "تطبيق الجوال ولا على الموقع العام."),

            h2("Recovery codes", "رموز الاسترداد"),
            figure("cp-login-enrol-2fa-recovery-codes",
                   "The ten recovery codes, shown once and never again. They are "
                   "obscured here because they were real when this was taken — a "
                   "manual that printed a working set would be handing them out.",
                   "رموز الاسترداد العشرة، تُعرض مرة واحدة ولا تظهر بعدها أبدًا. وقد "
                   "حُجبت هنا لأنها كانت حقيقية وقت التقاط الصورة — فالدليل الذي "
                   "يطبع مجموعة صالحة إنما يوزّعها."),
            p("When an account pairs its authenticator the system displays ten "
              "single-use recovery codes. They are shown once and never again. "
              "Print them or store them somewhere safe: they are what gets the "
              "account back in when the phone is lost. If they are lost as well, "
              "an administrator must reset the account's second factor from "
              "Reset user 2FA, which is covered later in this manual.",
              "عند إقران الحساب لتطبيق المصادقة يعرض النظام عشرة رموز استرداد "
              "يُستخدم كل منها مرة واحدة. تُعرض مرة واحدة فقط ولا تظهر بعدها "
              "أبدًا. اطبعها أو احفظها في مكان آمن، فهي وسيلة استعادة الحساب عند "
              "فقدان الهاتف. وإذا فُقدت أيضًا، فيجب على مسؤول إعادة تعيين العامل "
              "الثاني للحساب من صفحة إعادة تعيين المصادقة الثنائية، وهو ما يتناوله "
              "هذا الدليل لاحقًا."),

            h2("The dashboard", "لوحة المعلومات"),
            p("A completed sign-in lands on the dashboard: a summary of the "
              "event and, down the left, the menu that this manual follows "
              "chapter by chapter.",
              "ينتهي تسجيل الدخول الناجح إلى لوحة المعلومات: ملخّص للفعالية، "
              "وعلى الجانب القائمة التي يتبعها هذا الدليل فصلًا بفصل."),
            figure("cp-dashboard-default",
                   "The dashboard after signing in.",
                   "لوحة المعلومات بعد تسجيل الدخول."),
        ],
    }


def chapter_creating_users():
    return {
        "id": "creating-users",
        "title": t("Creating a user by hand", "إنشاء مستخدم يدويًا"),
        "blocks": [
            p("The Control Panel creates three kinds of account, and they are "
              "not variations of one form — they are three separate pipelines "
              "with different screens, different fields, different rules and "
              "different outcomes. Choosing the wrong one is the most common "
              "mistake, so this chapter starts with how to choose.",
              "تنشئ لوحة التحكم ثلاثة أنواع من الحسابات، وهي ليست صيغًا مختلفة "
              "لنموذج واحد، بل ثلاثة مسارات منفصلة بشاشات وحقول وقواعد ونتائج "
              "مختلفة. واختيار المسار الخاطئ هو أكثر الأخطاء شيوعًا، ولذلك يبدأ "
              "هذا الفصل ببيان كيفية الاختيار."),

            table(
                ["To create", "Use", "Address", "The account ends up"],
                ["لإنشاء", "استخدم", "العنوان", "ينتهي الحساب إلى"],
                [
                    ["An operator of this Control Panel",
                     EN["Module.AdminAdmins"], "/admin/admins",
                     "Awaiting approval, with a 7-day emailed invitation"],
                    ["A visitor or guest attending the forum",
                     EN["Module.AdminVisitors"], "/admin/visitors",
                     "Awaiting approval, with no password and no email"],
                    ["A partner, exhibitor, media or staff account",
                     EN["Module.AdminOthers"], "/admin/others",
                     "Awaiting approval, always — never auto-approved"],
                    ["A VIP or VVIP guest",
                     EN["Module.AdminVisitorsVip"], "/admin/visitors/vip",
                     "As a visitor, plus the VIP protocol fields"],
                ],
                [
                    ["مشغّل للوحة التحكم هذه",
                     AR["Module.AdminAdmins"], "/admin/admins",
                     "بانتظار الموافقة، مع دعوة بالبريد صالحة 7 أيام"],
                    ["زائر أو ضيف يحضر الملتقى",
                     AR["Module.AdminVisitors"], "/admin/visitors",
                     "بانتظار الموافقة، بلا كلمة مرور وبلا بريد"],
                    ["حساب شريك أو عارض أو إعلامي أو موظف",
                     AR["Module.AdminOthers"], "/admin/others",
                     "بانتظار الموافقة دائمًا — ولا يُعتمد تلقائيًا أبدًا"],
                    ["ضيف من كبار الشخصيات",
                     AR["Module.AdminVisitorsVip"], "/admin/visitors/vip",
                     "كالزائر، مع إضافة حقول مراسم كبار الشخصيات"],
                ]),

            note("The distinction that matters: an administrator receives an "
                 "emailed invitation and sets a password. A visitor or partner "
                 "receives neither — their credential is the QR badge, and the "
                 "badge is only minted when the account is approved.",
                 "الفارق الجوهري: المسؤول يتلقى دعوة بالبريد ويضبط كلمة مرور. "
                 "أما الزائر أو الشريك فلا يتلقى شيئًا من ذلك — إذ إن وثيقته هي "
                 "شارة الاستجابة السريعة، ولا تُصدر الشارة إلا عند اعتماد الحساب."),

            {"t": "pagebreak"},

            h2("Creating an administrator", "إنشاء مسؤول"),
            p("An administrator is somebody who signs in to this Control Panel. "
              "Open the menu group " + EN["Nav.AccessControl"] + ", choose "
              + EN["Module.AdminAdmins"] + ", and use the Add button on the grid "
              "toolbar. The form is deliberately short — an administrator has no "
              "visitor profile, no badge and no identity documents.",
              "المسؤول هو من يسجّل الدخول إلى لوحة التحكم هذه. افتح مجموعة "
              + AR["Nav.AccessControl"] + " واختر " + AR["Module.AdminAdmins"] +
              " ثم استخدم زر الإضافة في شريط أدوات الجدول. والنموذج قصير عن قصد "
              "— فالمسؤول ليس له ملف زائر ولا شارة ولا وثائق هوية."),
            figure("cp-admin-admins-default",
                   "The administrators list. The Add button opens the create form.",
                   "قائمة المسؤولين. يفتح زر الإضافة نموذج الإنشاء."),
            figure("cp-admin-admins-add-empty",
                   "The create form: an email address, a display name, and one tick "
                   "box per role.",
                   "نموذج الإنشاء: بريد إلكتروني، واسم معروض، ومربع اختيار لكل دور."),

            field_table([
                ("Admin.CreateUser.Email", YES, t("256 characters", "256 حرفًا"),
                 t("Must be a valid email address, and must not already belong "
                   "to an account.",
                   "يجب أن يكون بريدًا إلكترونيًا صالحًا وألا يكون مستخدمًا في حساب آخر.")),
                ("Admin.CreateUser.DisplayName", YES, t("2 to 128 characters", "من 2 إلى 128 حرفًا"),
                 t("The name shown throughout the Control Panel.",
                   "الاسم الذي يظهر في أنحاء لوحة التحكم.")),
                ("Admin.CreateUser.RolesLabel", NO, DASH,
                 t("One tick box per role. Assigning a role here requires the "
                   "role-assignment permission as well as the create permission.",
                   "مربع اختيار لكل دور. ويتطلب إسناد دور هنا صلاحية إسناد الأدوار "
                   "إضافة إلى صلاحية الإنشاء.")),
            ]),

            figure("cp-admin-admins-add-validation",
                   "A refused submission. The email box is a typed field, so the "
                   "browser catches a malformed address before the form is sent "
                   "at all; the form's own checks report a short display name and "
                   "a missing address the same way, and nothing reaches the "
                   "server until both are right.",
                   "محاولة إرسال مرفوضة. حقل البريد حقل مُصنّف، فيلتقط المتصفح "
                   "العنوان المشوّه قبل إرسال النموذج أصلًا؛ وتُبلغ فحوص النموذج "
                   "نفسها عن الاسم المعروض القصير والعنوان الناقص بالطريقة ذاتها، "
                   "ولا يصل شيء إلى الخادم حتى يصحّ كلاهما."),
            figure("cp-admin-admins-add-filled",
                   "The same form filled in and ready to submit.",
                   "النموذج نفسه بعد تعبئته وجاهزًا للإرسال."),

            p("On save the account is created in the awaiting-approval state, "
              "an invitation valid for seven days is emailed to the address, "
              "and every other approved administrator is notified that an "
              "account is waiting. Two-factor is armed on the account from the "
              "moment it is created, so the new administrator will be taken "
              "through the pairing screen on their first sign-in.",
              "عند الحفظ يُنشأ الحساب في حالة انتظار الموافقة، وتُرسل إلى العنوان "
              "دعوة صالحة سبعة أيام، ويُشعَر كل مسؤول معتمد آخر بوجود حساب ينتظر. "
              "وتُفعَّل المصادقة الثنائية على الحساب منذ لحظة إنشائه، فيمر المسؤول "
              "الجديد بشاشة الإقران عند أول تسجيل دخول له."),
            figure("cp-admin-admins-add-result",
                   "The new account in the list, awaiting approval, with the "
                   "confirmation message.",
                   "الحساب الجديد في القائمة بانتظار الموافقة، مع رسالة التأكيد."),
            note("A duplicate email is refused with the error "
                 "ADMIN_EMAIL_ALREADY_REGISTERED and the form stays open. "
                 "If the mail server is not configured the account is still "
                 "created — only the invitation fails to arrive, and it fails "
                 "quietly, in the background.",
                 "يُرفض البريد المكرر برمز الخطأ ADMIN_EMAIL_ALREADY_REGISTERED "
                 "ويبقى النموذج مفتوحًا. وإذا لم يكن خادم البريد مهيّأً فسيُنشأ "
                 "الحساب على أي حال — ولن تصل الدعوة فحسب، ويحدث ذلك بصمت في "
                 "الخلفية."),

            {"t": "pagebreak"},

            h2("Creating a visitor", "إنشاء زائر"),
            p("A visitor is an attendee. The form is the on-site registration "
              "desk wizard, and it is long because it captures the identity the "
              "gate will check. It is organised into numbered sections; the "
              "fields that appear depend on two choices — the profile type, and "
              "whether the visitor is Saudi.",
              "الزائر هو أحد الحضور. والنموذج هو معالج مكتب التسجيل في الموقع، "
              "وهو طويل لأنه يلتقط الهوية التي ستتحقق منها البوابة. وهو مقسّم إلى "
              "أقسام مرقّمة، وتتوقف الحقول الظاهرة على اختيارين: نوع الملف، وما إذا "
              "كان الزائر سعوديًا."),
            figure("cp-admin-visitors-default",
                   "The visitors list. Add opens the registration wizard.",
                   "قائمة الزوار. يفتح زر الإضافة معالج التسجيل."),
            figure("cp-admin-visitors-add-top",
                   "The wizard opens on the profile type, which decides much of "
                   "what follows.",
                   "يفتح المعالج على نوع الملف، وهو ما يحدّد كثيرًا مما يليه."),
            figure("cp-admin-visitors-add-middle",
                   "The identity section.",
                   "قسم الهوية."),

            h3("Identity", "الهوية"),
            field_table([
                ("Admin.WalkIn.Field.DisplayName", YES, t("128 characters", "128 حرفًا"), DASH),
                ("Admin.WalkIn.Field.ArabicName", NO, t("50 characters", "50 حرفًا"),
                 t("The form allows 128 characters; the server rejects anything "
                   "over 50. Keep to 50.",
                   "يسمح النموذج بـ128 حرفًا، لكن الخادم يرفض ما يتجاوز 50. فالتزم بـ50.")),
                ("Admin.WalkIn.Field.EnglishName", NO, t("50 characters", "50 حرفًا"),
                 t("Same 128-versus-50 mismatch as the Arabic name.",
                   "الاختلاف نفسه بين 128 و50 كما في الاسم العربي.")),
                ("Admin.WalkIn.Field.JobTitle", NO, t("100 characters", "100 حرف"), DASH),
                ("Admin.WalkIn.Field.JobTitleArabic", NO, t("100 characters", "100 حرف"), DASH),
                ("Admin.WalkIn.Field.PlaceOfBirth", NO, t("128 characters", "128 حرفًا"),
                 t("A region picker for a Saudi visitor; free text otherwise.",
                   "قائمة مناطق للزائر السعودي، ونص حر لغيره.")),
            ]),

            h3("Nationality and identity document", "الجنسية ووثيقة الهوية"),
            field_table([
                ("Admin.WalkIn.Field.NationalId", NO, t("10 digits", "10 أرقام"),
                 t("Saudi visitors. Must start with 1 and pass the check-digit "
                   "test; a number that fails it is refused.",
                   "للزوار السعوديين. يجب أن يبدأ بالرقم 1 وأن يجتاز اختبار رقم "
                   "التحقق، ويُرفض الرقم الذي لا يجتازه.")),
                ("Admin.WalkIn.Field.IqamaNumber", NO, t("10 digits", "10 أرقام"),
                 t("Non-Saudi residents. Must start with 2 and pass the same "
                   "check-digit test.",
                   "للمقيمين غير السعوديين. يجب أن يبدأ بالرقم 2 وأن يجتاز اختبار "
                   "رقم التحقق نفسه.")),
                ("Admin.WalkIn.Field.PassportNumber", NO, t("20 characters", "20 حرفًا"),
                 t("Length only — the desk applies no format check, so a "
                   "passport the mobile application would reject is accepted here.",
                   "الطول فقط — لا يطبّق المكتب أي تحقق من الصيغة، فيُقبل هنا جواز "
                   "سفر كان تطبيق الجوال ليرفضه.")),
            ]),
            note("One profile may hold one document of each kind. The same "
                 "number may appear on more than one profile — the constraint "
                 "that once made a number unique across all profiles was removed "
                 "deliberately, because it blocked legitimate registrations.",
                 "يمكن أن يحمل الملف الواحد وثيقة واحدة من كل نوع. وقد يظهر الرقم "
                 "نفسه في أكثر من ملف — إذ أُزيل عن قصد القيد الذي كان يجعل الرقم "
                 "فريدًا عبر كل الملفات، لأنه كان يمنع تسجيلات مشروعة."),

            figure("cp-admin-visitors-add-lower",
                   "The nationality section, which decides whether a national ID, "
                   "an Iqama or a passport is asked for.",
                   "قسم الجنسية، وهو الذي يحدّد ما إذا كان المطلوب هوية وطنية أم "
                   "إقامة أم جواز سفر."),

            h3("Contact and organisation", "التواصل والجهة"),
            field_table([
                ("Admin.WalkIn.Field.SaudiMobile", NO, t("32 characters", "32 حرفًا"),
                 t("Must be 05XXXXXXXX or +9665XXXXXXXX.",
                   "يجب أن يكون بالصيغة 05XXXXXXXX أو ‎+9665XXXXXXXX.")),
                ("Admin.WalkIn.Field.InternationalMobile", NO, t("32 characters", "32 حرفًا"),
                 t("Must be the international format: a plus sign, the country "
                   "code, then the number.",
                   "يجب أن يكون بالصيغة الدولية: علامة زائد ثم رمز الدولة ثم الرقم.")),
                ("Admin.WalkIn.Field.Email", NO, t("256 characters", "256 حرفًا"), DASH),
                (t("Organisation", "الجهة"), YES, t("150 characters", "150 حرفًا"),
                 t("Chosen from the organisation list. If the employer is not "
                   "listed, pick the \"Other\" entry and type the name — this is "
                   "why that entry exists.",
                   "تُختار من قائمة الجهات. وإذا لم تكن جهة العمل مدرجة فاختر "
                   "«أخرى» واكتب الاسم — ولهذا وُجد هذا الخيار.")),
            ]),
            figure("cp-admin-visitors-add-bottom",
                   "The lower sections: contact, the identity document and the "
                   "profile photo.",
                   "الأقسام السفلى: التواصل ووثيقة الهوية وصورة الملف."),
            note("Interests are offered for visitors only, and at most ten may "
                 "be chosen.",
                 "تُعرض الاهتمامات للزوار فقط، ويمكن اختيار عشرة منها كحد أقصى."),

            p("On save the visitor is created awaiting approval, with no "
              "password and no badge. The badge is minted when the account is "
              "approved from the pending queue. There is a configuration option "
              "that approves a walk-in visitor immediately and prints the badge "
              "at the desk, but it ships switched off, and it never applies to "
              "partner accounts.",
              "عند الحفظ يُنشأ الزائر بانتظار الموافقة، بلا كلمة مرور وبلا شارة. "
              "وتُصدر الشارة عند اعتماد الحساب من قائمة الانتظار. وثمة خيار "
              "إعدادات يعتمد الزائر الحاضر فورًا ويطبع الشارة عند المكتب، لكنه "
              "يُشحن مُعطّلًا، ولا ينطبق أبدًا على حسابات الشركاء."),

            {"t": "pagebreak"},

            h2("Creating a partner or staff account", "إنشاء حساب شريك أو موظف"),
            p("The " + EN["Module.AdminOthers"] + " page uses the same wizard as "
              "the visitor page, with two differences: there is no interests "
              "section, and the account is never approved automatically. Every "
              "account created here waits for a person to approve it.",
              "تستخدم صفحة " + AR["Module.AdminOthers"] + " المعالج نفسه المستخدم "
              "في صفحة الزوار، مع فارقين: لا يوجد قسم اهتمامات، ولا يُعتمد الحساب "
              "تلقائيًا أبدًا. فكل حساب يُنشأ هنا ينتظر اعتماد شخص له."),
            figure("cp-admin-others-default",
                   "The partner and staff accounts list.",
                   "قائمة حسابات الشركاء والموظفين."),
            figure("cp-admin-others-add-top",
                   "The same wizard, without the interests section.",
                   "المعالج نفسه، بلا قسم الاهتمامات."),

            h2("Registering a VIP", "تسجيل أحد كبار الشخصيات"),
            p("The VIP page is the visitor wizard with a protocol section added "
              "at the top, and a welcome photo alongside the profile photo.",
              "صفحة كبار الشخصيات هي معالج الزائر نفسه مع إضافة قسم المراسم في "
              "أعلاه، وصورة ترحيب إلى جانب صورة الملف."),
            field_table([
                ("Admin.WalkIn.Field.MawjId", NO, t("64 characters", "64 حرفًا"), DASH),
                ("Admin.WalkIn.Field.Honorific", NO, t("64 characters", "64 حرفًا"), DASH),
                ("Admin.WalkIn.Field.HonorificArabic", NO, t("64 characters", "64 حرفًا"), DASH),
                ("Admin.WalkIn.Field.PreferredLanguage", NO, t("16 characters", "16 حرفًا"),
                 t("Arabic or English.", "العربية أو الإنجليزية.")),
            ]),
            figure("cp-admin-visitors-vip-default",
                   "The VIP registration page.",
                   "صفحة تسجيل كبار الشخصيات."),
            figure("cp-admin-visitors-vip-add-top",
                   "The VIP wizard, with the protocol section above the identity "
                   "section.",
                   "معالج كبار الشخصيات، مع قسم المراسم فوق قسم الهوية."),
        ],
    }


def chapter_profile_image():
    return {
        "id": "profile-image",
        "title": t("The profile picture", "صورة الملف الشخصي"),
        "blocks": [
            p("A profile picture is stored in the system's central file store, "
              "encrypted, and served back only to the account itself or to an "
              "administrator holding the right permission. It is not a file "
              "sitting in a public folder, and it cannot be reached by guessing "
              "an address.",
              "تُحفظ صورة الملف الشخصي في مخزن الملفات المركزي للنظام، مشفَّرة، "
              "ولا تُقدَّم إلا لصاحب الحساب نفسه أو لمسؤول يملك الصلاحية المناسبة. "
              "وهي ليست ملفًا في مجلد عام، ولا يمكن الوصول إليها بتخمين العنوان."),

            h2("Where a picture is set", "أين تُضبط الصورة"),
            bullets([
                "By the account owner, from My profile — this is the only place "
                "with a cropping tool.",
                "By an administrator, on the edit form of a visitor or partner "
                "account.",
                "By the registration desk, as part of the create wizard, "
                "alongside the identity document.",
            ], [
                "من صاحب الحساب، عبر صفحة ملفي الشخصي — وهي الموضع الوحيد الذي "
                "يتضمّن أداة القصّ.",
                "من مسؤول، في نموذج تعديل حساب زائر أو شريك.",
                "من مكتب التسجيل، ضمن معالج الإنشاء، إلى جانب وثيقة الهوية.",
            ]),
            figure("cp-account-profile-avatar-empty",
                   "The avatar card before a picture is set: a placeholder, the "
                   "accepted formats and the size ceiling, and the file picker.",
                   "بطاقة الصورة قبل ضبط أي صورة: عنصر نائب، والصيغ المقبولة والحد "
                   "الأقصى للحجم، وأداة اختيار الملف."),
            figure("cp-account-profile-avatar-cropper",
                   "Choosing a file opens the cropper straight away. The frame is "
                   "square because the stored picture is square; there is no "
                   "separate upload button, and nothing is sent until this is saved.",
                   "يفتح اختيار الملف أداة القص فورًا. والإطار مربّع لأن الصورة "
                   "المحفوظة مربّعة؛ ولا يوجد زر رفع منفصل، ولا يُرسل شيء حتى يُحفظ هذا."),
            figure("cp-account-profile-avatar-set",
                   "The picture in place, with the option to remove it.",
                   "الصورة بعد ضبطها، مع خيار إزالتها."),

            h2("What the system accepts", "ما يقبله النظام"),
            table(
                ["Rule", "Value"],
                ["القاعدة", "القيمة"],
                [
                    ["File types", "PNG, JPEG or WebP — nothing else"],
                    ["Maximum size", "2 MB for a profile picture; 5 MB for an identity document"],
                    ["Checked how", "The file's declared type must match its actual content, so renaming a file to .png does not get it past the check"],
                    ["Cropping", "Square, produced at 400 by 400 pixels"],
                    ["Stored", "Encrypted, one active picture per account"],
                ],
                [
                    ["أنواع الملفات", "PNG أو JPEG أو WebP — لا غير"],
                    ["الحجم الأقصى", "2 ميجابايت لصورة الملف، و5 ميجابايت لوثيقة الهوية"],
                    ["كيفية التحقق", "يجب أن يطابق نوع الملف المعلن محتواه الفعلي، فلا يجدي تغيير الامتداد إلى ‎.png"],
                    ["القصّ", "مربّع، بمقاس 400 × 400 بكسل"],
                    ["التخزين", "مشفَّرة، وصورة واحدة نشطة لكل حساب"],
                ]),
            note("There is no size check in the browser: an oversized file is "
                 "uploaded and then refused by the server, so the failure "
                 "appears after the wait rather than before it.",
                 "لا يوجد تحقق من الحجم في المتصفح: فالملف الكبير يُرفع ثم يرفضه "
                 "الخادم، فيظهر الفشل بعد الانتظار لا قبله."),

            h2("Replacing and removing", "الاستبدال والحذف"),
            figure("cp-admin-admins-avatar-thumbnail",
                   "The difference in a list: an account with a picture shows a "
                   "thumbnail, one without shows a tile of its initials. An empty "
                   "cell therefore means no picture was ever set, not a picture "
                   "that failed to load.",
                   "الفارق كما يظهر في القائمة: الحساب الذي له صورة يعرض صورة "
                   "مصغّرة، والذي لا صورة له يعرض مربعًا بالأحرف الأولى من اسمه. "
                   "فالخانة الفارغة تعني أنه لم تُضبط صورة قط، لا أن صورة أخفقت "
                   "في التحميل."),

            p("Uploading a new picture retires the previous one in the same "
              "step; there is never more than one active picture on an account. "
              "Where an account has no picture the Control Panel shows a tile "
              "with the person's initials rather than a broken image, so an "
              "empty grid cell means no picture was ever set.",
              "يؤدي رفع صورة جديدة إلى سحب السابقة في الخطوة نفسها؛ فلا توجد أبدًا "
              "أكثر من صورة نشطة واحدة للحساب. وحين لا يكون للحساب صورة تعرض لوحة "
              "التحكم مربعًا بالأحرف الأولى من الاسم بدلًا من صورة معطوبة، فالخانة "
              "الفارغة تعني أنه لم تُضبط صورة قط."),
        ],
    }


def reference_strings():
    return {
        "title": t("Every page in the Control Panel",
                   "كل صفحة في لوحة التحكم"),
        "intro": t(
            "This section lists every page the Control Panel serves, in the "
            "order the menu presents them. For each: the address, the file that "
            "implements it, the permission that gates the page, the permission "
            "that puts it in the menu, and a picture of the page as it renders. "
            "It is generated from the source code on every build, so it "
            "describes the system as it is rather than as it once was.",
            "يسرد هذا القسم كل صفحة تقدّمها لوحة التحكم، بالترتيب الذي تعرضها به "
            "القائمة. ولكل صفحة: العنوان، والملف الذي ينفّذها، والصلاحية التي "
            "تحرس الصفحة، والصلاحية التي تُظهرها في القائمة، وصورة للصفحة كما "
            "تُعرض. ويُولَّد هذا القسم من الشيفرة المصدرية مع كل بناء، فيصف النظام "
            "كما هو لا كما كان."),
        "headers": [t("Property", "الخاصية"), t("Value", "القيمة")],
        "rowRoute": t("Address", "العنوان"),
        "rowFile": t("Implemented by", "ينفّذها"),
        "rowCodeBehind": t("Code-behind", "ملف الشيفرة المصاحب"),
        "rowPermission": t("Page permission", "صلاحية الصفحة"),
        "rowNavPermission": t("Menu permission", "صلاحية القائمة"),
        "rowStub": t("Placeholder", "صفحة مؤقتة"),
        "stubYes": t("Yes — the module is not built yet",
                     "نعم — لم تُبنَ الوحدة بعد"),
        "nonNavTitle": t("Pages reached from inside another page",
                         "صفحات يُوصل إليها من داخل صفحة أخرى"),
        "actionsTitle": t("What you can do here", "ما يمكنك فعله هنا"),
        "actionsHeaders": [t("Action", "الإجراء"),
                           t("Permission it needs", "الصلاحية التي يتطلبها")],
        "ungated": t("No permission of its own", "بلا صلاحية خاصة به"),
        "bulk": t("on the selected rows", "على الصفوف المحددة"),
        "pageButton": t("A button the page gates itself",
                        "زر تحرسه الصفحة بنفسها"),
        "columnsLabel": t("The list shows", "يعرض الجدول"),
        "callsLabel": t("It calls", "يستدعي"),
        "noActions": t(
            "This page has no list toolbar: it is a form, a dashboard or a "
            "console rather than a list of records.",
            "لا يحتوي هذا الصفحة على شريط أدوات جدول: فهي نموذج أو لوحة معلومات "
            "أو وحدة تشغيل، لا قائمة سجلات."),
        "redirected": t(
            "No screenshot: this address cannot be opened directly by a "
            "signed-in reader. The two account-state pages redirect to the "
            "dashboard, and the three sign-in pages redirect back to the sign-in "
            "form, because each is reachable only at its own point in a flow.",
            "لا توجد صورة: لا يمكن فتح هذا العنوان مباشرة لقارئ مسجّل الدخول. "
            "فصفحتا حالة الحساب تُحوَّلان إلى لوحة المعلومات، وصفحات تسجيل الدخول "
            "الثلاث تُحوَّل إلى نموذج الدخول، لأن كلًّا منها لا تُبلغ إلا في موضعها "
            "من المسار."),
        "nonNavIntro": t(
            "These pages have an address but no menu entry. They open from a "
            "button or a row inside another page, or they belong to the "
            "sign-in flow.",
            "لهذه الصفحات عناوين لكنها بلا مدخل في القائمة. تُفتح من زر أو من صف "
            "داخل صفحة أخرى، أو تنتمي إلى مسار تسجيل الدخول."),
    }


def build():
    return {
        "meta": {
            "title": t("SIMF Control Panel", "لوحة تحكم الملتقى البحري السعودي الدولي"),
            "subtitle": t("Operations manual — creating users, configuring the "
                          "system, and deploying it",
                          "دليل التشغيل — إنشاء المستخدمين وتهيئة النظام ونشره"),
            "contentsTitle": t("Contents", "المحتويات"),
            "factHeaders": [t("", ""), t("", "")],
            "facts": [
                [t("Audience", "الفئة المستهدفة"),
                 t("Control Panel administrators and the team deploying the system",
                   "مسؤولو لوحة التحكم والفريق القائم على نشر النظام")],
                [t("Covers", "يغطي"),
                 t("Every Control Panel page, account creation end to end, and "
                   "the server deployment with its configuration",
                   "كل صفحات لوحة التحكم، وإنشاء الحسابات من أوله إلى آخره، ونشر "
                   "الخادم مع تهيئته")],
                [t("Screenshots", "الصور"),
                 t("Captured from the running application, not drawn",
                   "ملتقطة من التطبيق أثناء تشغيله، وليست مرسومة")],
                [t("Volumes", "المجلدات"),
                 t("English and Arabic, published separately",
                   "الإنجليزية والعربية، يصدران منفصلين")],
            ],
        },
        "chapters": [
            chapter_getting_in(),
            chapter_creating_users(),
            chapter_profile_image(),
            chapter_changing_accounts(),
            chapter_roles(),
            chapter_programme(),
            chapter_event_day(),
            chapter_exhibition(),
            chapter_content(),
            chapter_public_relations(),
            chapter_knowledge_ai(),
            chapter_system(),
            chapter_reports(),
            chapter_reference_data(),
            chapter_deployment(),
            chapter_configuration(),
            chapter_observations(),
        ],
        "reference": reference_strings(),
    }


if __name__ == "__main__":
    SOURCE.mkdir(parents=True, exist_ok=True)
    book = build()
    out = SOURCE / "book.json"
    out.write_text(json.dumps(book, indent=2, ensure_ascii=False), encoding="utf-8")
    blocks = sum(len(c["blocks"]) for c in book["chapters"])
    print(f"resx: {len(EN)} EN / {len(AR)} AR entries")
    print(f"chapters: {len(book['chapters'])}, blocks: {blocks}")
    print(f"written: {out.relative_to(REPO)}")
