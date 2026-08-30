"""The chapters about changing an account after it exists, and about roles.

Separated from make_book.py for the same reason content_ops.py is: creating an
account and governing one are different jobs done by different people.

As everywhere in this manual, the constraints are read from the code - the
approve and reject permissions from the permission catalogue, the reason length
from the validator, the last-administrator and baseline-role guards from the
service that enforces them.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_changing_accounts():
    return {
        "id": "changing-accounts",
        "title": t("Changing an account after it exists",
                   "تعديل الحساب بعد إنشائه"),
        "blocks": [
            p("A new account is not usable yet. It waits in a queue until "
              "somebody approves it, and only then does it get its badge. This "
              "chapter covers the approval queues, editing an account, moving it "
              "between types, resetting a lost second factor, and withdrawing "
              "access.",
              "الحساب الجديد ليس قابلًا للاستخدام بعد. فهو ينتظر في قائمة حتى "
              "يعتمده شخص، وعندها فقط يحصل على شارته. ويتناول هذا الفصل قوائم "
              "الاعتماد، وتعديل الحساب، ونقله بين الأنواع، وإعادة تعيين العامل "
              "الثاني المفقود، وسحب الوصول."),

            h2("The approval queues", "قوائم الاعتماد"),
            p("There is one queue per kind of account, and each is a separate "
              "page with its own permissions. Approving is what mints the QR "
              "badge, so until it happens the person cannot pass a gate.",
              "توجد قائمة لكل نوع من الحسابات، وكل منها صفحة مستقلة بصلاحياتها "
              "الخاصة. والاعتماد هو ما يُصدر شارة الاستجابة السريعة، فحتى يتم لا "
              "يستطيع الشخص العبور من أي بوابة."),
            table(
                ["Queue", "Address", "Approve needs", "Reject needs"],
                ["القائمة", "العنوان", "يتطلب الاعتماد", "يتطلب الرفض"],
                [["Pending visitors", "/admin/visitors/pending", "Visitors.Approve", "Visitors.Reject"],
                 ["Pending others", "/admin/others/pending", "Others.Approve", "Others.Reject"],
                 ["Pending admins", "/admin/admins/pending", "Admins.Approve", "Admins.Reject"]],
                [["الزوار بانتظار الموافقة", "/admin/visitors/pending", "Visitors.Approve", "Visitors.Reject"],
                 ["مستخدمون آخرون بانتظار الموافقة", "/admin/others/pending", "Others.Approve", "Others.Reject"],
                 ["المسؤولون بانتظار الموافقة", "/admin/admins/pending", "Admins.Approve", "Admins.Reject"]]),
            figure("cp-admin-visitors-pending-default",
                   "The pending visitors queue.",
                   "قائمة الزوار بانتظار الموافقة."),
            p("Rows can be handled one at a time or in bulk. Rejecting requires "
              "a written reason of between 10 and 500 characters, and the "
              "button stays disabled until the reason is long enough. The reason "
              "is audited, so write something a colleague reading it in six "
              "months can act on.",
              "يمكن معالجة الصفوف واحدًا واحدًا أو دفعة واحدة. ويتطلب الرفض سببًا "
              "مكتوبًا يتراوح بين 10 و500 حرف، ويبقى الزر معطّلًا حتى يبلغ السبب "
              "الطول الكافي. ويُدوَّن السبب في سجل التدقيق، فاكتب ما يستطيع زميل "
              "يقرؤه بعد ستة أشهر أن يتصرف بناءً عليه."),
            note("When approving a visitor you may also set the tier at the same "
                 "time, which saves editing the account afterwards.",
                 "عند اعتماد زائر يمكنك أيضًا ضبط فئته في الوقت نفسه، وهو ما يوفّر "
                 "تعديل الحساب لاحقًا."),

            h2("Editing an account", "تعديل الحساب"),
            p("The edit form is reached from the row actions on the visitor and "
              "partner account lists. It changes the email address, the display "
              "name, the profile type, the nationality, the two mobile numbers, "
              "the meeting preferences and the pictures. It does not change the "
              "password — nobody can set another person's password; the account "
              "resets its own from the sign-in page. On the administrators list "
              "the Edit action manages that administrator's roles instead — an "
              "administrator's details cannot be edited from the account lists "
              "at all.",
              "يُفتح نموذج التعديل من إجراءات الصف في قائمتَي الزوار والشركاء. وهو "
              "يغيّر البريد الإلكتروني والاسم المعروض ونوع الملف والجنسية ورقمَي "
              "الجوال وتفضيلات الاجتماعات والصور. ولا يغيّر كلمة المرور — إذ لا "
              "يستطيع أحد ضبط كلمة مرور شخص آخر؛ فالحساب يعيد ضبطها بنفسه من صفحة "
              "تسجيل الدخول. أما في قائمة المسؤولين فيدير إجراء التعديل أدوار ذلك "
              "المسؤول بدلًا من ذلك — فبيانات المسؤول لا تُعدَّل من قوائم الحسابات "
              "إطلاقًا."),
            note("For a partner or staff account the profile type is required, "
                 "and the form will not save without one. For a visitor it is "
                 "optional.",
                 "بالنسبة لحساب شريك أو موظف يكون نوع الملف مطلوبًا، ولن يُحفظ "
                 "النموذج بدونه. أما للزائر فهو اختياري."),

            h2("Moving an account between types", "نقل الحساب بين الأنواع"),
            p("An account created in the wrong pipeline does not have to be "
              "deleted and rebuilt. The account details view carries a change "
              "type control, gated by its own permission, which moves the "
              "account between the visitor and partner sides and asks for the "
              "profile type it should land on.",
              "الحساب المُنشأ في المسار الخاطئ لا يلزم حذفه وإعادة بنائه. إذ "
              "تتضمّن صفحة تفاصيل الحساب أداة لتغيير النوع، محروسة بصلاحية خاصة "
              "بها، تنقل الحساب بين جانب الزوار وجانب الشركاء وتسأل عن نوع الملف "
              "الذي سينتقل إليه."),

            h2("Resetting a lost second factor", "إعادة تعيين عامل ثانٍ مفقود"),
            p("When somebody loses the phone holding their authenticator, and "
              "has no recovery code left, an administrator clears the second "
              "factor from Reset user 2FA. A Control Panel account is then held "
              "at its next sign-in until it pairs a new authenticator. A visitor "
              "or partner signs in on their password alone, and pairs a new "
              "authenticator from their own profile page whenever they choose.",
              "عند فقدان شخص للهاتف الذي يحمل تطبيق المصادقة، ولم يبقَ لديه رمز "
              "استرداد، يمسح المسؤول العامل الثاني من صفحة إعادة تعيين المصادقة "
              "الثنائية. ثم يُوقَف حساب لوحة التحكم عند تسجيل دخوله التالي حتى يقرن "
              "تطبيق مصادقة جديدًا. أما الزائر أو الشريك فيسجّل دخوله بكلمة المرور "
              "وحدها، ويقرن تطبيق مصادقة جديدًا من صفحة ملفه الشخصي متى شاء."),
            figure("cp-admin-reset-2fa-default",
                   "Resetting a user's second factor.",
                   "إعادة تعيين العامل الثاني لمستخدم."),
            bullets([
                "A reason is required, 10 to 500 characters, and it is audited.",
                "You cannot reset your own second factor here — use a recovery "
                "code, or ask another administrator.",
                "You cannot reset another administrator's second factor. The "
                "most privileged accounts are re-paired through configuration on "
                "the server, deliberately, so that no single administrator can "
                "take over another.",
            ], [
                "يلزم إدخال سبب من 10 إلى 500 حرف، ويُدوَّن في سجل التدقيق.",
                "لا يمكنك إعادة تعيين عاملك الثاني من هنا — استخدم رمز استرداد أو "
                "اطلب من مسؤول آخر.",
                "ولا يمكنك إعادة تعيين العامل الثاني لمسؤول آخر. فأعلى الحسابات "
                "صلاحية يُعاد إقرانها عبر التهيئة على الخادم، عن قصد، كي لا يستطيع "
                "مسؤول واحد الاستيلاء على حساب آخر.",
            ]),

            h2("Withdrawing access", "سحب الوصول"),
            p("Delete on an account list is not a deletion. It disables the "
              "account, revokes its sessions and withdraws its admission. "
              "The row action and the bulk action open the same dialog, which "
              "asks for an audited reason of 10 to 500 characters. **The dialog "
              "says the people are notified by email; no such email is sent, so "
              "do not promise one.**",
              "زر الحذف في قوائم الحسابات ليس حذفًا. فهو يعطّل الحساب ويُبطل جلساته "
              "ويسحب اعتماده. ويفتح إجراء الصف والإجراء الجماعي النافذة نفسها "
              "التي تطلب سببًا مُدوَّنًا من 10 إلى 500 حرف. **وتقول النافذة إنه يجري "
              "إشعار الأشخاص بالبريد الإلكتروني، ولا يُرسَل أي بريد، فلا تَعِد به.**"),
            note("Your own row is skipped if you include it, and the result "
                 "reports it as skipped rather than failing — so a bulk action "
                 "that reports one fewer than you selected is usually this.",
                 "يُتخطّى صفّك أنت إن أدرجته، وتُبلغ النتيجة عنه بأنه متخطّى لا "
                 "فاشل — فإذا أبلغ إجراء جماعي عن عدد أقل بواحد مما حدّدت فهذا هو "
                 "السبب غالبًا."),
        ],
    }


def chapter_roles():
    return {
        "id": "roles",
        "title": t("Roles and permissions", "الأدوار والصلاحيات"),
        "blocks": [
            p("What an administrator can see and do is decided by permissions, "
              "and permissions are granted to roles, never to a person. To give "
              "somebody an ability you put them in a role that holds it. This is "
              "the whole model, and it has one deliberate exception: the "
              "Administrator role holds a wildcard that grants everything.",
              "يُحدَّد ما يستطيع المسؤول رؤيته وفعله بالصلاحيات، وتُمنح الصلاحيات "
              "للأدوار لا للأشخاص أبدًا. ولمنح شخص قدرةً ما تضعه في دور يملكها. "
              "هذا هو النموذج كله، وله استثناء واحد مقصود: دور المسؤول العام يحمل "
              "رمزًا شاملًا يمنح كل شيء."),

            h2("Permission names", "أسماء الصلاحيات"),
            p("A permission is written as the page it governs, a full stop, and "
              "the action — Visitors.Approve, Roles.AssignPermissions, "
              "Admins.ResetTwoFactor. Two permissions guard every page: one lets "
              "the page open at all, and one puts it in the menu. They are "
              "usually the same, and where they differ the manual's page "
              "reference lists both.",
              "تُكتب الصلاحية على هيئة الصفحة التي تحكمها، ثم نقطة، ثم الإجراء — "
              "مثل Visitors.Approve وRoles.AssignPermissions وAdmins.ResetTwoFactor. "
              "وتحرس كل صفحة صلاحيتان: واحدة تسمح بفتح الصفحة أصلًا، وأخرى تُظهرها "
              "في القائمة. وهما متطابقتان غالبًا، وحين تختلفان يذكرهما مرجع "
              "الصفحات في هذا الدليل معًا."),
            note("A button is gated by the permission of the endpoint it calls, "
                 "which is not always the page's own. On a page holding two "
                 "grids over two different things they differ, so read the "
                 "permission off the action, not off the page.",
                 "يُحرس الزر بصلاحية الخدمة التي يستدعيها، وهي ليست دائمًا صلاحية "
                 "الصفحة نفسها. ففي صفحة تحوي جدولين لشيئين مختلفين تختلفان، "
                 "فاقرأ الصلاحية من الإجراء لا من الصفحة."),

            h2("Creating a role", "إنشاء دور"),
            p("Open Roles and permissions and use Add. A role needs only a name, "
              "between 1 and 64 characters, and the name must be unique. A new "
              "role starts with no permissions at all, so it grants nothing "
              "until permissions are assigned to it.",
              "افتح الأدوار والصلاحيات واستخدم الإضافة. لا يحتاج الدور إلا إلى "
              "اسم، بين حرف واحد و64 حرفًا، ويجب أن يكون الاسم فريدًا. ويبدأ الدور "
              "الجديد بلا أي صلاحيات، فلا يمنح شيئًا حتى تُسند إليه صلاحيات."),
            figure("cp-admin-roles-default",
                   "The roles list.",
                   "قائمة الأدوار."),

            h2("Granting permissions to a role", "منح الصلاحيات لدور"),
            p("The permissions editor lists every permission in the system, "
              "grouped by the page it governs, with the ones the role already "
              "holds ticked. Tick, untick, and save.",
              "يسرد محرّر الصلاحيات كل صلاحية في النظام، مجمّعة حسب الصفحة التي "
              "تحكمها، مع تحديد ما يملكه الدور منها. حدّد وألغِ التحديد ثم احفظ."),
            figure("cp-admin-roles-x-permissions-default",
                   "The permissions editor for one role.",
                   "محرّر الصلاحيات لدور واحد."),
            note("The baseline roles that ship with the system are read-only "
                 "here. They cannot be edited, renamed or deleted, because the "
                 "system's own behaviour depends on them.",
                 "الأدوار الأساسية المشحونة مع النظام للقراءة فقط هنا. فلا يمكن "
                 "تعديلها ولا إعادة تسميتها ولا حذفها، لأن سلوك النظام نفسه يعتمد "
                 "عليها."),

            h2("Giving a person a role", "إسناد دور إلى شخص"),
            p("Roles are assigned to administrators only — a visitor or partner "
              "account carries none, because it does not sign in to this Control "
              "Panel. Assign them either when creating the administrator, or "
              "afterwards from the Edit action on the administrators list.",
              "تُسند الأدوار إلى المسؤولين فقط — فحساب الزائر أو الشريك لا يحمل "
              "أي دور، لأنه لا يسجّل الدخول إلى لوحة التحكم هذه. وتُسند إما عند "
              "إنشاء المسؤول، أو بعد ذلك من إجراء التعديل في قائمة المسؤولين."),
            figure("cp-admin-admins-default",
                   "The administrators list, where roles are assigned.",
                   "قائمة المسؤولين، حيث تُسند الأدوار."),

            h2("The guards", "الضمانات"),
            bullets([
                "The last administrator cannot have the Administrator role taken "
                "away. The system refuses, rather than leaving itself with "
                "nobody who can administer it.",
                "A role still held by somebody cannot be deleted; the error says "
                "how many people hold it.",
                "Assigning a role while creating an account needs the "
                "role-assignment permission on top of the create permission, so "
                "somebody who may add administrators cannot silently make one "
                "powerful.",
            ], [
                "لا يمكن نزع دور المسؤول العام عن آخر مسؤول. فالنظام يرفض ذلك "
                "بدلًا من أن يترك نفسه بلا من يديره.",
                "لا يمكن حذف دور لا يزال أحد يحمله؛ وتذكر رسالة الخطأ عدد من "
                "يحملونه.",
                "ويتطلب إسناد دور أثناء إنشاء حساب صلاحيةَ إسناد الأدوار فوق "
                "صلاحية الإنشاء، فلا يستطيع من يملك إضافة المسؤولين أن يجعل "
                "أحدهم واسع الصلاحية بصمت.",
            ]),
        ],
    }
