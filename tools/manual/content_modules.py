"""The module chapters: how the Control Panel's areas are actually worked.

The generated reference already answers "what is this page, who may open it, what
can be done on it". What it cannot answer is the question an operator actually
has - which page comes first, what depends on what, and what breaks if you skip a
step. That is what these chapters are for, and they are grouped by the JOB rather
than by the menu, because the job is what somebody arrives with.

Every dependency and rule stated here was read out of the entity, the validator
or the controlled document that enforces it. Where the system does not enforce
something, this says so rather than implying an order that is only convention.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_reference_data():
    return {
        "id": "reference-data",
        "title": t("The lists everything else chooses from",
                   "القوائم التي يختار منها كل شيء آخر"),
        "blocks": [
            p("Six pages hold lists that nothing else works without. They look "
              "like the least interesting screens in the Control Panel and they "
              "are the ones that stop a registration dead when they are empty: a "
              "visitor cannot finish signing up without an organisation to belong "
              "to, and cannot be registered at the walk-in desk — nor given a "
              "bulk-minted badge — without a profile type. Approving a visitor "
              "still issues the badge whether or not a tier is picked. Fill these "
              "before an event opens, not during it.",
              "ست صفحات تحمل قوائم لا يعمل شيء آخر بدونها. تبدو أقل شاشات لوحة "
              "التحكم إثارة، وهي التي توقف التسجيل تمامًا حين تكون فارغة: فالزائر "
              "لا يستطيع إتمام تسجيله بلا جهة ينتمي إليها، ولا يمكن تسجيله عند "
              "المكتب — ولا منحه شارة ضمن دفعة — بلا نوع ملف. أما اعتماد الزائر "
              "فيصدر شارته سواء اختيرت له فئة أم لا. املأ هذه قبل افتتاح "
              "الفعالية، لا أثناءها."),

            h2("What each list feeds", "ما تغذّيه كل قائمة"),
            table(
                ["List", "Read by", "What an empty list does"],
                ["القائمة", "يقرأها", "ماذا يحدث حين تكون فارغة"],
                [
                    ["Interests", "The mobile app's interests step, and the "
                     "\"meet people like you\" matching",
                     "The picker offers nothing; a visitor cannot pick the one "
                     "to ten interests the app asks for"],
                    ["Organisations", "The organisation picker on every "
                     "registration, in the app and at the desk",
                     "Registration cannot be completed — an organisation is "
                     "required. This is not hypothetical: it happened on the "
                     "first production install"],
                    ["Countries", "Nationality on every profile, and the "
                     "calling-code picker",
                     "No visitor can state a nationality — Saudi Arabia is a row "
                     "in this list like any other. App sign-up and the desk's "
                     "full registration are both refused, for Saudis as much as "
                     "foreigners; only a quick walk-in taken with no nationality "
                     "at all still completes"],
                    ["Regions", "Place of birth for a Saudi visitor",
                     "Nothing — the picker falls back to the thirteen official "
                     "Saudi regions built into both the app and the desk. Place "
                     "of birth itself is required on every app sign-up, though "
                     "the desk may leave it blank"],
                    ["Visitor profile types", "Which tier a visitor is approved "
                     "into, and what the badge says",
                     "The account is still approved and still gets a working "
                     "badge, but in no tier — the badge shows no tier line and "
                     "falls back to the default gold strip, and any gate with an "
                     "allow-list refuses it"],
                    ["Other profile types", "The same, for partner, exhibitor, "
                     "media and staff accounts",
                     "A partner account cannot be saved — its profile type is "
                     "required"],
                ],
                [
                    ["الاهتمامات", "خطوة الاهتمامات في تطبيق الجوال، ومطابقة "
                     "«قابل أشخاصًا مثلك»",
                     "لا تعرض القائمة شيئًا، فلا يستطيع الزائر اختيار الاهتمامات "
                     "من واحد إلى عشرة التي يطلبها التطبيق"],
                    ["الجهات", "قائمة اختيار الجهة في كل تسجيل، في التطبيق وعند "
                     "المكتب",
                     "لا يمكن إتمام التسجيل — فالجهة مطلوبة. وهذا ليس افتراضًا: "
                     "فقد وقع فعلًا عند أول تثبيت إنتاجي"],
                    ["البلدان", "الجنسية في كل ملف، وقائمة رموز الاتصال",
                     "لا يستطيع أي زائر تحديد جنسيته — فالسعودية سجل في هذه "
                     "القائمة كسائر السجلات. فيُرفض التسجيل في التطبيق ويُرفض "
                     "التسجيل الكامل عند المكتب، للسعوديين كما لغيرهم؛ ولا "
                     "يكتمل إلا تسجيل سريع عند المكتب بلا جنسية أصلًا"],
                    ["المناطق", "مكان الميلاد للزائر السعودي",
                     "لا شيء — إذ ترجع القائمة إلى مناطق السعودية الرسمية الثلاث "
                     "عشرة المضمَّنة في التطبيق وفي المكتب معًا. ومكان الميلاد "
                     "نفسه مطلوب في كل تسجيل عبر التطبيق، وإن جاز تركه فارغًا "
                     "عند المكتب"],
                    ["أنواع ملفات الزوار", "الفئة التي يُعتمد فيها الزائر، وما "
                     "تعرضه الشارة",
                     "يُعتمد الحساب وتصدر له شارة عاملة، لكن بلا فئة — فلا يظهر "
                     "على الشارة سطر الفئة ويعود شريطها إلى الذهبي الافتراضي، "
                     "وترفضه أي بوابة لها قائمة سماح"],
                    ["أنواع الملفات الأخرى", "الشيء نفسه لحسابات الشركاء "
                     "والعارضين والإعلاميين والموظفين",
                     "لا يمكن حفظ حساب الشريك — فنوع ملفه مطلوب"],
                ]),

            note("The organisation list is a curated government import matched on "
                 "commercial registration, which is why the Control Panel is the "
                 "only place it is edited and why the app cannot add to it. When "
                 "somebody's employer is genuinely absent they pick the \"Other\" "
                 "entry and type the name; that text is read later by a person, "
                 "not merged automatically.",
                 "قائمة الجهات استيراد حكومي مُنسَّق يُطابَق على السجل التجاري، ولذلك "
                 "فإن لوحة التحكم هي الموضع الوحيد لتحريرها، ولذلك لا يستطيع "
                 "التطبيق الإضافة إليها. وحين تكون جهة عمل الشخص غائبة فعلًا يختار "
                 "«أخرى» ويكتب الاسم؛ ويقرأ ذلك النص شخصٌ لاحقًا ولا يُدمج تلقائيًا."),

            h2("Deleting is not deleting", "الحذف ليس حذفًا"),
            p("Removing an entry from any of these lists deactivates it. It stops "
              "being offered to anybody choosing from now on, and every profile "
              "that already points at it keeps pointing at it — a visitor who "
              "chose an interest does not lose it because the interest was "
              "retired. Re-activating puts it back in the picker. This is why "
              "four of these lists — interests, organisations, countries and "
              "regions — can be tidied during an event without breaking the "
              "people already registered. A profile type is stricter: it refuses "
              "to be removed at all while any account is still assigned to it, "
              "and the Control Panel shows you that refusal, so retiring one "
              "means re-assigning its holders first.",
              "إزالة أي مدخل من هذه القوائم تعطّله. فيتوقف عرضه على من يختار من "
              "الآن فصاعدًا، ويظل كل ملف يشير إليه مشيرًا إليه — فالزائر الذي اختار "
              "اهتمامًا لا يفقده لأن الاهتمام قد أُوقف. وإعادة تفعيله تعيده إلى "
              "القائمة. ولهذا يمكن ترتيب أربع من هذه القوائم — الاهتمامات "
              "والجهات والبلدان والمناطق — أثناء الفعالية دون الإضرار بمن سجّلوا "
              "بالفعل. أما نوع الملف فأشدّ: يرفض أن يُزال ما دام مُسندًا إلى أي "
              "حساب، وتعرض لوحة التحكم هذا الرفض، فإيقاف نوعٍ يقتضي نقل حامليه "
              "إلى غيره أولًا."),

            h2("Profile types are not just labels",
               "أنواع الملفات ليست مجرد تسميات"),
            p("A profile type decides more than a word on a badge: whether the "
              "holder counts as a visitor, whether they appear in the app's "
              "meet-people list, whether they are a VIP tier, and — through the "
              "gate's allow-list — which entrances they may use. Creating one is "
              "quick; deciding what it means is the part worth doing carefully, "
              "because gates and badges are configured against it afterwards.",
              "يحدّد نوع الملف أكثر من كلمة على الشارة: هل يُحتسب حامله زائرًا، وهل "
              "يظهر في قائمة «قابل أشخاصًا» في التطبيق، وهل هو من فئات كبار "
              "الشخصيات، ومن خلال قائمة السماح في البوابة — أي المداخل يجوز له "
              "استخدامها. وإنشاؤه سريع، لكن تحديد معناه هو الجزء الجدير بالعناية، "
              "لأن البوابات والشارات تُهيَّأ بناءً عليه بعد ذلك."),
            figure("cp-admin-profile-types-visitor-default",
                   "The visitor profile types. Each one is a tier an account can "
                   "be approved into.",
                   "أنواع ملفات الزوار. كل نوع فئة يمكن اعتماد الحساب فيها."),
        ],
    }
