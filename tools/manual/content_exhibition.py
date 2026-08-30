"""The exhibition chapter: exhibitors, booths, sponsors and the venue map.

Four pages that look like one module and are not. The chapter's job is to
separate them, because the Control Panel groups them under one menu and an
operator reasonably assumes a booth belongs to an exhibitor the way a room
belongs to a building. It does not: the link is optional, it points the other
way round from the way the menu reads, and the account that lets a person on a
stand scan a visitor is a third thing again.

Everything here was read out of the entities, the admin service and the public
projections. Where FDS-006 and the code disagree - the sponsor tiers, and the
map being 2D - both are stated and the code is named as the one in force.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_exhibition():
    return {
        "id": "exhibition",
        "title": t("The exhibition floor",
                   "أرض المعرض"),
        "blocks": [
            p("Four pages carry the exhibition, and the thing worth knowing "
              "before opening any of them is that they are not one hierarchy. "
              "An exhibitor is a company. A booth is a stand — a physical space "
              "on the floor, which is coded and numbered before anybody has "
              "signed for it. A sponsor is neither: it is a marketing row with a "
              "tier and a logo, attached to no booth and no exhibitor. The venue "
              "map is drawn from its own set of points and not from any of them.",
              "أربع صفحات تحمل المعرض، وأول ما ينبغي معرفته قبل فتح أي منها أنها "
              "ليست تسلسلًا هرميًّا واحدًا. فالعارض شركة. والجناح مساحة مادية على "
              "أرض المعرض، تُرقَّم ويُعطى لها رمز قبل أن يوقّع عليها أحد. والراعي "
              "ليس هذا ولا ذاك: بل سجل تسويقي له فئة وشعار، لا يرتبط بجناح ولا "
              "بعارض. وخريطة الموقع تُرسم من مجموعة نقاطها الخاصة، لا من أيٍّ من "
              "هؤلاء."),

            h2("Three entities and one optional link",
               "ثلاثة كيانات ورابط واحد اختياري"),
            table(
                ["Page", "What the row is", "What it links to"],
                ["الصفحة", "ما هو السجل", "بماذا يرتبط"],
                [
                    ["Exhibitors", "An exhibiting company: bilingual name, "
                     "contact email, phone and website, a tier, social links, "
                     "city, a latitude and longitude — degrees on the globe, "
                     "not the booth's venue-map coordinates — country and its "
                     "own logo",
                     "Nothing above it. Booths point at it, not the reverse"],
                    ["Booths", "The stand itself: a floor code such as A-12, a "
                     "bilingual booth name, a hall, and a booth officer — the "
                     "person standing on it, who is a different party from the "
                     "company",
                     "Optionally a hall, and optionally one exhibitor. The "
                     "hall may be left empty — the form carries a \"no hall\" "
                     "choice — and the exhibitor for as long as the stand is "
                     "unsold"],
                    ["Sponsors", "A sponsorship: tier, tagline, about text, "
                     "contact block and logo",
                     "Nothing. A sponsor is not a booth and not an exhibitor, "
                     "and giving a company a booth does not make it a sponsor"],
                    ["Venue map", "One labelled point: a hall, a zone, a booth "
                     "or a point of interest, placed at an X and Y",
                     "Optionally a hall or a booth. Tapping any point opens "
                     "its info card; only a booth point offers a way through "
                     "to the booth's record"],
                ],
                [
                    ["العارضون", "شركة عارضة: اسم بلغتين، وبريد وهاتف وموقع "
                     "للتواصل، وفئة، وروابط تواصل اجتماعي، ومدينة، وخط عرض "
                     "وخط طول — درجات على الكرة الأرضية، لا إحداثيات الجناح على "
                     "خريطة الموقع — ودولة، وشعار خاص بها",
                     "لا شيء فوقه. الأجنحة تشير إليه، لا العكس"],
                    ["الأجنحة", "الجناح نفسه: رمز أرضي مثل A-12، واسم جناح "
                     "بلغتين، وقاعة، ومسؤول جناح — وهو الشخص الواقف فيه، وطرف "
                     "مختلف عن الشركة",
                     "قاعة اختياريًّا، وعارض واحد اختياريًّا. ويجوز ترك القاعة "
                     "فارغة — ففي النموذج خيار «بلا قاعة» — كما يجوز ترك العارض "
                     "فارغًا ما دام الجناح غير مباع"],
                    ["الرعاة", "رعاية: فئة، وعبارة تعريفية، ونبذة، وبيانات "
                     "تواصل، وشعار",
                     "لا شيء. فالراعي ليس جناحًا ولا عارضًا، ومنح شركةٍ جناحًا لا "
                     "يجعلها راعيًا"],
                    ["خريطة الموقع", "نقطة معنونة واحدة: قاعة أو منطقة أو جناح "
                     "أو معلم، توضع عند إحداثي س وص",
                     "قاعة أو جناح اختياريًّا. والنقر على أي نقطة يفتح بطاقة "
                     "تعريفها؛ ونقطة الجناح وحدها تتيح المرور إلى سجل الجناح"],
                ]),

            figure("cp-admin-exhibitors-default",
                   "The exhibitors list. Each row is a company, and the account "
                   "count is the number of live memberships on it — it goes on "
                   "counting after the company is deactivated, when nobody there "
                   "can scan.",
                   "قائمة العارضين. كل سجل شركة، وعدد الحسابات هو عدد العضويات "
                   "السارية فيها — ويظل يعدّها بعد تعطيل الشركة، حين لا يستطيع "
                   "أحد فيها المسح."),

            h2("The booth is the space, and it comes first",
               "الجناح هو المساحة، وهو يأتي أولًا"),
            p("A booth is created and coded before an exhibitor is signed, which "
              "is why every exhibitor-facing field on it can be left empty. The "
              "floor code is upper-cased when saved and must be unique across "
              "every booth including the deactivated ones — a code freed by "
              "retiring a stand is not free for re-use.",
              "يُنشأ الجناح ويُعطى رمزه قبل التعاقد مع العارض، ولذلك يجوز ترك كل "
              "حقل يخص العارض فيه فارغًا. ويُحوَّل الرمز الأرضي إلى حروف كبيرة عند "
              "الحفظ، ويجب أن يكون فريدًا بين جميع الأجنحة بما فيها المعطَّلة — "
              "فالرمز الذي يُفرَج عنه بإيقاف جناح لا يصبح متاحًا لإعادة الاستخدام."),
            note("The officer fields belong to the person on the stand, not to "
                 "the booth and not to the company. The booth's own name is the "
                 "plain Name field; everything prefixed \"officer\" is that "
                 "individual's name, phone, email, city and links. It is the "
                 "single most-confused pair of fields on the page.",
                 "حقول المسؤول تخص الشخص الواقف في الجناح، لا الجناح ولا الشركة. "
                 "فاسم الجناح هو حقل الاسم المجرَّد؛ وكل ما سُبق بكلمة «المسؤول» هو "
                 "اسم ذلك الشخص وهاتفه وبريده ومدينته وروابطه. وهما أكثر حقلين "
                 "يقع فيهما اللبس في الصفحة."),
            p("One consequence catches people out. When a booth is linked to an "
              "exhibitor, the public listing reads the company name off the "
              "exhibitor — and it hides the booth entirely if that exhibitor is "
              "inactive, even though the booth's own active switch was never "
              "touched. A stand that vanishes from the app while its own row "
              "still reads active is almost always an exhibitor that was "
              "deactivated.",
              "ولهذا أثرٌ يفاجئ الناس. فحين يُربط الجناح بعارض، تقرأ القائمة "
              "العامة اسم الشركة من العارض — وتُخفي الجناح كليًّا إن كان ذلك العارض "
              "معطَّلًا، مع أن مفتاح تفعيل الجناح نفسه لم يُمس. والجناح الذي يختفي "
              "من التطبيق بينما سجله ما زال مفعَّلًا يكون في الغالب الأعم عارضًا "
              "قد عُطِّل."),
            figure("cp-admin-booths-default",
                   "The booths list. The code, the hall and the linked exhibitor "
                   "are the three columns that decide what the app shows.",
                   "قائمة الأجنحة. الرمز والقاعة والعارض المرتبط هي الأعمدة "
                   "الثلاثة التي تحدد ما يعرضه التطبيق."),

            h2("Giving somebody on a stand the ability to scan visitors",
               "منح شخص في الجناح القدرة على مسح الزوار"),
            p("This is the part of the exhibition module that is genuinely a "
              "procedure, and it is the part that goes wrong. A person can sign "
              "up in the app as an exhibitor: the sign-up screen's "
              "registration-type tab has an Other side, and the exhibitor "
              "profile type is offered there. What that sign-up cannot create is "
              "the exhibitor company itself, or the membership tying the account "
              "to it — so a self-registered account arrives carrying the profile "
              "type, no membership and no way to scan. Both of those are made "
              "here, in the Control Panel. Open an exhibitor's row and use its "
              "Accounts button; there are two ways in and they are not "
              "interchangeable.",
              "هذا هو الجزء الذي يمثل إجراءً فعليًّا في وحدة المعرض، وهو الجزء "
              "الذي يقع فيه الخطأ. ويستطيع الشخص أن يسجّل نفسه عارضًا من داخل "
              "التطبيق: ففي شاشة التسجيل تبويب «نوع التسجيل» وفي جانبه الآخر "
              "يُعرض نوع ملف العارض. لكن ذلك التسجيل لا ينشئ شركة العارض نفسها "
              "ولا العضوية التي تربط الحساب بها — فيصل الحساب المسجَّل ذاتيًّا "
              "حاملًا نوع الملف، بلا عضوية ولا قدرة على المسح. وكلتاهما تُنشأ هنا "
              "في لوحة التحكم. افتح سجل العارض واستخدم زر الحسابات؛ فأمامك "
              "طريقان وهما غير متكافئين."),
            bullets(
                ["**Create a new account.** The Control Panel provisions a "
                 "fresh identity, sets the exhibitor profile type on it, and "
                 "records the membership against this exhibitor in one step. "
                 "Use this for somebody who has no account yet.",
                 "**Link an account that already exists.** Matched on the email "
                 "address, case-insensitively. Use this for somebody who was "
                 "already created elsewhere — on the Others page, or at a desk."],
                ["**إنشاء حساب جديد.** تنشئ لوحة التحكم هويةً جديدة، وتضبط عليها "
                 "نوع ملف العارض، وتسجّل العضوية لدى هذا العارض في خطوة واحدة. "
                 "استخدم هذا لمن ليس له حساب بعد.",
                 "**ربط حساب موجود.** يُطابَق على عنوان البريد الإلكتروني دون "
                 "تمييز بين الحروف الكبيرة والصغيرة. استخدم هذا لمن أُنشئ حسابه "
                 "في مكان آخر — في صفحة الحسابات الأخرى أو عند أحد المكاتب."]),

            h3("Why Link refuses, and what to do about it",
               "لماذا يرفض الربط، وما العمل"),
            p("Linking checks three things before it agrees, and all three "
              "refusals are deliberate rather than defensive.",
              "يتحقق الربط من ثلاثة أمور قبل أن يوافق، وحالات الرفض الثلاث "
              "مقصودة لا وقائية."),
            table(
                ["The refusal", "What it means", "What to do"],
                ["الرفض", "ماذا يعني", "ما العمل"],
                [
                    ["The exhibitor is closed",
                     "Its own Active switch is off. This is checked before "
                     "anything else, and a closed exhibitor takes on no new "
                     "accounts. The Accounts button is still offered on a "
                     "deactivated row, so this is the refusal you meet first",
                     "Reactivate the exhibitor, then link. It is the one place "
                     "Link and Revoke differ: Revoke still works on a closed "
                     "exhibitor"],
                    ["The account is not eligible",
                     "The account does not carry an active profile type whose "
                     "app role is Exhibitor. The Control Panel will not quietly "
                     "change the profile type here, because that would silently "
                     "overwrite an app role another administrator assigned",
                     "Set an exhibitor profile type on the account first, on the "
                     "Others page, then link"],
                    ["The account is already linked",
                     "That person already holds a live membership with another "
                     "exhibitor. One account belongs to one exhibitor at a time, "
                     "and the database enforces it",
                     "Revoke the earlier membership, then link — or use a "
                     "different account"],
                ],
                [
                    ["العارض مغلق",
                     "مفتاح تفعيله مطفأ. ويُفحص هذا قبل كل شيء، والعارض المغلق لا "
                     "يقبل حسابات جديدة. وزر الحسابات يظل معروضًا على السجل "
                     "المعطَّل، فهذا أول رفض تلقاه",
                     "أعد تفعيل العارض ثم اربط. وهذا هو الموضع الوحيد الذي يختلف "
                     "فيه الربط عن السحب: فالسحب يعمل على عارض مغلق"],
                    ["الحساب غير مؤهل",
                     "لا يحمل الحساب نوع ملف مفعَّلًا يكون دوره في التطبيق «عارض». "
                     "ولن تغيّر لوحة التحكم نوع الملف هنا بصمت، لأن ذلك يعني "
                     "الكتابة فوق دور في التطبيق أسنده مسؤول آخر",
                     "اضبط نوع ملف عارض على الحساب أولًا من صفحة الحسابات "
                     "الأخرى، ثم اربط"],
                    ["الحساب مرتبط بالفعل",
                     "هذا الشخص يحمل عضوية سارية لدى عارض آخر. فالحساب الواحد "
                     "يتبع عارضًا واحدًا في الوقت الواحد، وقاعدة البيانات تفرض ذلك",
                     "اسحب العضوية السابقة ثم اربط — أو استخدم حسابًا آخر"],
                ]),

            note("The most common failure in this whole chapter: an account "
                 "carries an exhibitor profile type — set on the Others page, or "
                 "picked by the person at sign-up — and everybody assumes it is "
                 "ready. It is not. It has the profile type and no membership, "
                 "so the app refuses its badge scans and "
                 "its visitor list, and nothing on the Exhibitors page ties it to "
                 "a stand. The instinct is to create the account again from the "
                 "Accounts panel, which fails on the duplicate email address. "
                 "The fix is Link, not Create.",
                 "أشهر إخفاق في هذا الفصل كله: حساب يحمل نوع ملف عارض — أُسند له "
                 "في صفحة الحسابات الأخرى، أو اختاره صاحبه عند التسجيل — فيفترض "
                 "الجميع أنه جاهز. وليس كذلك. فهو يحمل نوع الملف ولا يحمل "
                 "عضوية، فيرفض التطبيق عمليات مسح الشارات وقائمة "
                 "الزوار لديه، ولا شيء في صفحة العارضين يربطه بجناح. ويكون الميل "
                 "إلى إنشاء الحساب مجددًا من لوحة الحسابات، فيفشل بسبب تكرار "
                 "البريد الإلكتروني. والحل هو الربط لا الإنشاء."),

            h3("What a live membership actually grants",
               "ماذا تمنحه العضوية السارية فعلًا"),
            p("Three things together authorise lead capture: an active account "
              "carrying the exhibitor profile type, a live membership against an "
              "exhibitor, and an exhibitor that is itself still active. With all "
              "three, that person's app can scan a visitor's entry badge and "
              "keep the resulting contact card, list the visitors they collected, "
              "delete one, and export a card as a vCard. They also become a "
              "recipient of the business-meeting notifications sent to the "
              "exhibitor. **Deactivating the exhibitor takes all of that away "
              "from every one of its officers at once**, without a single "
              "membership being touched.",
              "ثلاثة أمور معًا تمنح صلاحية التقاط العملاء: حساب مفعَّل يحمل نوع ملف "
              "العارض، وعضوية سارية لدى عارض، وعارض ما زال هو نفسه مفعَّلًا. فبها "
              "جميعًا يستطيع تطبيق ذلك الشخص مسح شارة دخول الزائر والاحتفاظ "
              "ببطاقة التواصل الناتجة، وعرض قائمة "
              "الزوار الذين جمعهم، وحذف واحدة منها، وتصدير البطاقة كملف vCard. "
              "ويصبح كذلك من متلقّي إشعارات لقاءات الأعمال المرسلة إلى العارض. "
              "**وتعطيل العارض يسلب ذلك كله من جميع مسؤوليه دفعةً واحدة**، دون "
              "المساس بأي عضوية."),

            h3("Revoking access", "سحب الصلاحية"),
            p("Revoking deactivates the membership; it never deletes it. The row "
              "is the record of who captured which visitor's details and under "
              "whose consent, so it has to survive the person losing access. "
              "Three things stop working immediately: the badge scan and the "
              "booth's collected contact cards, the business-meeting "
              "notifications, and the account count on the exhibitors grid. "
              "Revoking works on an inactive exhibitor too — withdrawing access "
              "has to remain possible after a stand closes.",
              "سحب الصلاحية يعطّل العضوية، ولا يحذفها أبدًا. فالسجل هو الدليل على "
              "من التقط بيانات أي زائر وبأي موافقة، فوجب أن يبقى بعد فقدان الشخص "
              "لصلاحيته. وتتوقف ثلاثة أمور فورًا: مسح الشارات وبطاقات التواصل "
              "المجمَّعة للجناح، وإشعارات لقاءات الأعمال، وعدّاد الحسابات في شبكة "
              "العارضين. ويعمل السحب على عارض معطَّل أيضًا — إذ يجب أن يظل سحب "
              "الصلاحية ممكنًا بعد إغلاق الجناح."),
            note("Only live memberships are listed in the Accounts panel, which "
                 "is why a revoked person simply disappears from it rather than "
                 "appearing greyed out. Revoking one that is already revoked is "
                 "refused rather than silently accepted.",
                 "لا تُعرض في لوحة الحسابات إلا العضويات السارية، ولذلك يختفي "
                 "الشخص الذي سُحبت صلاحيته منها بدل أن يظهر باهتًا. ويُرفض سحب "
                 "صلاحية مسحوبة أصلًا بدل قبوله بصمت."),

            h2("Sponsors and their tiers", "الرعاة وفئاتهم"),
            p("Sponsors carry four tiers — Platinum, Gold, Silver and Bronze — "
              "and the tier is always chosen, never inherited: a blank Add form "
              "opens on Platinum, the API refuses anything that is not one of the "
              "four, and an Excel import requires a Tier column and rejects any "
              "row that leaves it blank. The public surface groups them "
              "by tier with Platinum first, and within a tier orders by the "
              "display-order number and then by Arabic name.",
              "للرعاة أربع فئات — بلاتيني وذهبي وفضي وبرونزي — والفئة تُختار دائمًا "
              "ولا تُورَّث: فنموذج الإضافة الفارغ يفتح على البلاتيني، وترفض الواجهة "
              "البرمجية أي قيمة خارج الأربع، ويشترط الاستيراد من Excel عمود Tier "
              "ويرفض أي سجل يتركه فارغًا. وتجمعهم الواجهة العامة بحسب الفئة "
              "والبلاتيني أولًا، وترتبهم داخل الفئة الواحدة بحسب رقم ترتيب "
              "العرض ثم بالاسم العربي."),
            note("FDS-006 describes the sponsor tiers as Strategic, Premium and "
                 "Gold. The system does not use those; the four above are what it "
                 "offers and what the public listing groups by. Separately, "
                 "exhibitors have their own distinct tier list — Premium, Gold, "
                 "Silver, Bronze — which is not the sponsor list even though two "
                 "of the words are shared.",
                 "يصف المستند FDS-006 فئات الرعاة بأنها: استراتيجي وبريميوم "
                 "وذهبي. والنظام لا يستخدم تلك؛ فالفئات الأربع أعلاه هي ما يوفره "
                 "وما تُجمَّع القائمة العامة على أساسه. وللعارضين — بشكل منفصل — "
                 "قائمة فئات خاصة بهم: بريميوم وذهبي وفضي وبرونزي، وهي ليست قائمة "
                 "الرعاة وإن تشاركتا كلمتين."),
            figure("cp-admin-sponsors-default",
                   "The sponsors list. Tier and display order together decide "
                   "the order the public surfaces show.",
                   "قائمة الرعاة. الفئة وترتيب العرض معًا يحددان الترتيب الذي "
                   "تعرضه الواجهات العامة."),

            h2("The venue map", "خريطة الموقع"),
            p("The map is a set of labelled points, each one a hall, a zone, a "
              "booth or a point of interest, placed at an X and Y. Nought to a "
              "thousand is the convention the field documents, but nothing "
              "enforces it and the app never reads the absolute numbers: on each "
              "load it takes the smallest and largest X and Y among the points it "
              "received and stretches that spread across its canvas. Only the "
              "positions of the points relative to one another matter, **so "
              "moving one outlying point shifts where every other point lands.** "
              "Every point is tappable and opens an info card, whatever it "
              "carries; only a booth point offers a way through to the booth's "
              "own record, and it needs the booth link to have anything to show "
              "there. A hall link reaches the app, but nothing there opens a "
              "hall. It is a two-dimensional map of points, not the "
              "three-dimensional isometric floor plan FDS-006 describes.",
              "الخريطة مجموعة نقاط معنونة، كل نقطة قاعة أو منطقة أو جناح أو معلم، "
              "توضع عند إحداثي س وص. ومن صفر إلى ألف هو العُرف الذي يوثّقه الحقل، "
              "لكن لا شيء يفرضه، والتطبيق لا يقرأ القيم المطلقة أصلًا: فهو في كل "
              "تحميل يأخذ أصغر وأكبر قيمتَي س وص بين النقاط التي وصلته، ويمدّ ذلك "
              "المدى ليملأ مساحته. فلا يهم إلا موضع النقاط بعضها من بعض، **ولذلك "
              "فتحريك نقطة واحدة شاذة يزيح موضع كل نقطة أخرى.** وكل نقطة قابلة "
              "للنقر وتفتح بطاقة تعريفها مهما حملت؛ ونقطة الجناح وحدها تتيح "
              "المرور إلى سجل الجناح، وتحتاج إلى رابط الجناح كي يكون لديها ما "
              "تعرضه هناك. أما رابط القاعة فيصل إلى التطبيق، لكن لا شيء فيه يفتح "
              "قاعة. وهي خريطة نقاط ثنائية الأبعاد، لا المخطط المجسَّم "
              "ثلاثي الأبعاد الذي يصفه المستند FDS-006."),
            note("A booth also has its own map coordinates on the booths page. "
                 "They are stored and published, and no map is drawn from them — "
                 "the app's map is built from the venue-map points. Placing a "
                 "booth by editing its coordinates therefore moves nothing on "
                 "screen; add or move its point on the venue map instead.",
                 "للجناح كذلك إحداثيات خريطة خاصة به في صفحة الأجنحة. وهي تُحفظ "
                 "وتُنشر، ولا تُرسم منها أي خريطة — فخريطة التطبيق مبنية من نقاط "
                 "خريطة الموقع. ولذلك فإن تحديد موضع جناح بتحرير إحداثياته لا "
                 "يحرّك شيئًا على الشاشة؛ بل أضف نقطته على خريطة الموقع أو "
                 "حرّكها."),
            p("One anonymous read serves the map, cached for forty-five seconds, "
              "and the mobile app is its only consumer. The public website does "
              "not use it: its exhibition page shows an exported floor-plan image "
              "instead, which is static and is not produced from these points. "
              "Moving a point therefore changes the app and leaves the website "
              "picture exactly as it was.",
              "تخدم الخريطةَ قراءةٌ واحدة مجهولة، مخزَّنة مؤقتًا خمسًا وأربعين "
              "ثانية، وتطبيق الجوال هو مستهلكها الوحيد. أما الموقع العام فلا "
              "يستخدمها: إذ تعرض صفحة المعرض فيه صورة مخطط مصدَّرة، وهي ثابتة "
              "ولا تُنتج من هذه النقاط. ولذلك فتحريك نقطةٍ يغيّر التطبيق ويترك "
              "صورة الموقع كما هي تمامًا."),
            figure("cp-admin-venue-map-default",
                   "The venue map points. Each row is one point on the map the "
                   "mobile app draws.",
                   "نقاط خريطة الموقع. كل سجل نقطة على الخريطة التي يرسمها تطبيق "
                   "الجوال."),
        ],
    }
