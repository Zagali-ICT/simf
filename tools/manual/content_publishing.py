"""The content chapter: what is published, and where it actually appears.

The Control Panel's Content menu holds seven pages that all look like "text and
pictures the public will see". They differ in two ways that matter far more than
their fields: WHO reads each one - the mobile app, the public website, or
neither - and WHAT makes an item visible, which is a different mechanism on
almost every page.

The consumer table below was built by following each admin page to its public
endpoint and then finding the client that calls it, rather than by assuming that
publishing something publishes it everywhere. Three of the pages are read by the
app alone, and the manual says so because an operator who checks the website
instead concludes the save failed.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_content():
    return {
        "id": "content",
        "title": t("Publishing content, and where it shows up",
                   "نشر المحتوى، وأين يظهر"),
        "blocks": [
            p("Before the fields, the map. These pages do not all feed the same "
              "audience, and the difference is not visible from the Control "
              "Panel: the page looks identical whether a hundred thousand app "
              "users read it or nothing does. Publishing is only half the job — "
              "knowing where to go and look is the other half.",
              "قبل الحقول، الخريطة. فهذه الصفحات لا تغذّي جمهورًا واحدًا، والفرق "
              "غير ظاهر من لوحة التحكم: إذ تبدو الصفحة كما هي سواء قرأها مئة ألف "
              "مستخدم للتطبيق أو لم يقرأها أحد. والنشر نصف العمل — ونصفه الآخر "
              "معرفة أين تذهب لتنظر."),

            h2("Who reads what", "من يقرأ ماذا"),
            table(
                ["Page", "Mobile app", "Public website"],
                ["الصفحة", "تطبيق الجوال", "الموقع العام"],
                [
                    ["Content blocks", "Yes — the About and Terms screens read "
                     "their text from here", "No"],
                    ["Banners", "Yes — the rotating hero on the app's home "
                     "screen", "No"],
                    ["Media Center", "Yes — the gallery", "No"],
                    ["News", "Yes — the news list and each article", "No — the "
                     "website's news cards are fixed in the site itself"],
                    ["Media partners", "Yes", "No"],
                    ["Previous editions", "Yes", "Yes — the Archive page and the "
                     "Archive menu"],
                    ["Sponsors", "Yes", "Yes — the logo strip on the landing "
                     "page and the Partners page"],
                ],
                [
                    ["كتل المحتوى", "نعم — تقرأ شاشتا «عن» و«الشروط» نصّهما من "
                     "هنا", "لا"],
                    ["اللافتات", "نعم — الشريط المتحرك في الشاشة الرئيسية "
                     "للتطبيق", "لا"],
                    ["المركز الإعلامي", "نعم — معرض الصور والفيديوهات", "لا"],
                    ["الأخبار", "نعم — قائمة الأخبار وكل مقال", "لا — بطاقات "
                     "أخبار الموقع مثبَّتة داخل الموقع نفسه"],
                    ["الشركاء الإعلاميون", "نعم", "لا"],
                    ["الدورات السابقة", "نعم", "نعم — صفحة الأرشيف وقائمة "
                     "الأرشيف"],
                    ["الرعاة", "نعم", "نعم — شريط الشعارات في الصفحة الرئيسية "
                     "وصفحة الشركاء"],
                ]),

            note("This is the single most useful fact in the chapter. A news "
                 "article, a banner, a gallery item, a media partner or a content "
                 "block is published for the mobile app. Of the pages on this "
                 "menu the website reads only sponsors and previous editions "
                 "live; beyond the menu it also reads the programme sessions, "
                 "the speakers, and the organisation profile — the landing "
                 "hero's background video and the forum dates that the landing, "
                 "Speakers and Venue pages show. Nothing else on this menu "
                 "reaches it — the aggregating endpoint that once fed the rest "
                 "of the landing page is still there and no "
                 "shipped page calls it, and the landing's news cards are written "
                 "into the site. Check the app, not the website, and the save you "
                 "were about to repeat will turn out to have worked.",
                 "هذه أنفع حقيقة في الفصل. فالمقال الإخباري واللافتة وعنصر المعرض "
                 "والشريك الإعلامي وكتلة المحتوى تُنشر لتطبيق الجوال. ومن صفحات "
                 "هذه القائمة لا يقرأ الموقع مباشرةً إلا الرعاة والدورات "
                 "السابقة؛ وخارجها يقرأ أيضًا جلسات البرنامج والمتحدثين وملف "
                 "الجهة — مقطع الخلفية في واجهة الصفحة الرئيسية وتواريخ الملتقى "
                 "التي تعرضها الصفحة الرئيسية وصفحتا المتحدثين والمقر. ولا يصله "
                 "شيء آخر من هذه القائمة — فنقطة القراءة المجمِّعة التي كانت "
                 "تغذّي بقية الصفحة الرئيسية ما زالت موجودة ولا تستدعيها أي صفحة "
                 "منشورة، وبطاقات الأخبار في الصفحة الرئيسية مكتوبة داخل الموقع. "
                 "تحقّق من التطبيق لا من الموقع، وسيتبيّن أن الحفظ الذي كدت تعيده "
                 "قد نجح."),

            h2("Three shapes, not three lists of text",
               "ثلاثة أشكال، لا ثلاث قوائم نصوص"),
            p("Content blocks, banners and news look alike on the menu and behave "
              "nothing alike. Each has its own idea of what makes an item "
              "appear.",
              "تتشابه كتل المحتوى واللافتات والأخبار في القائمة ولا تتشابه في "
              "السلوك إطلاقًا. ولكلٍّ منها مفهومه الخاص لما يجعل العنصر يظهر."),
            table(
                ["", "Content block", "Banner", "News item"],
                ["", "كتلة محتوى", "لافتة", "خبر"],
                [
                    ["Addressed by", "A key, such as home.welcome.title",
                     "Its display order", "Its publish date and order"],
                    ["What makes it visible", "Being active",
                     "Being active and the present moment falling inside its "
                     "start and end",
                     "Being active and its publish date having passed"],
                    ["Ordering", "None — clients ask for the key they want",
                     "Display order, zero first; ties break on the start date",
                     "Publish date and display order"],
                    ["Carries a picture", "No", "Yes", "Yes"],
                ],
                [
                    ["يُطلب بواسطة", "مفتاح، مثل home.welcome.title",
                     "ترتيب عرضه", "تاريخ نشره وترتيبه"],
                    ["ما يجعله ظاهرًا", "أن يكون مفعَّلًا",
                     "أن يكون مفعَّلًا وأن تقع اللحظة الحالية بين بدايته ونهايته",
                     "أن يكون مفعَّلًا وأن يكون تاريخ نشره قد مضى"],
                    ["الترتيب", "لا يوجد — يطلب العملاء المفتاح الذي يريدونه",
                     "ترتيب العرض والصفر أولًا، ويُفصل التعادل بتاريخ البداية",
                     "تاريخ النشر ثم ترتيب العرض"],
                    ["يحمل صورة", "لا", "نعم", "نعم"],
                ]),

            h3("Content blocks: the key is the contract",
               "كتل المحتوى: المفتاح هو العقد"),
            p("A content block is a named slot. The app asks for a key and shows "
              "whatever text is in it, so the key is what the app was written "
              "against — renaming one does not move the text, it breaks the "
              "screen that asked for the old name. Add a block, edit a block, "
              "deactivate a block; do not rename its key unless the app is being "
              "changed with it.",
              "كتلة المحتوى خانة مسمّاة. يطلب التطبيق مفتاحًا ويعرض ما فيه من نص، "
              "فالمفتاح هو ما كُتب التطبيق عليه — وإعادة تسميته لا تنقل النص، بل "
              "تُعطّل الشاشة التي طلبت الاسم القديم. أضِف كتلة، وحرّر كتلة، وعطّل "
              "كتلة؛ ولا تُعِد تسمية مفتاحها ما لم يكن التطبيق يتغيّر معها."),
            note("The value is shown as plain text. Typing HTML into it puts the "
                 "tags on the screen; it does not format anything.",
                 "تُعرض القيمة نصًّا صِرفًا. فكتابة HTML فيها تضع الوسوم على الشاشة "
                 "ولا تنسّق شيئًا."),
            p("The app re-reads a block every time the screen that uses it is "
              "opened, and again on pull-to-refresh, and keeps nothing between "
              "visits. So an edit shows up the next time a reader opens that "
              "screen, not on every phone the moment you save. There is no cache "
              "to wait out.",
              "يعيد التطبيق قراءة الكتلة كلما فُتحت الشاشة التي تستخدمها، ومرةً "
              "أخرى عند السحب للتحديث، ولا يحتفظ بشيء بين الزيارتين. فيظهر "
              "التعديل حين يفتح القارئ تلك الشاشة في المرة التالية، لا على كل "
              "هاتف لحظة الحفظ. وليس هناك تخزين مؤقت تنتظر انقضاءه."),
            figure("cp-admin-content-blocks-default",
                   "The content blocks. The key column is what the app is coded "
                   "against; the text beside it is what it shows.",
                   "كتل المحتوى. عمود المفتاح هو ما بُرمج التطبيق عليه؛ والنص "
                   "المجاور له هو ما يعرضه."),

            h3("Banners: a window, not a switch",
               "اللافتات: نافذة زمنية لا مفتاح"),
            p("A banner has a start and an end, and the public read returns only "
              "the active banners whose window contains right now. A banner that "
              "will not appear is far more often outside its dates than "
              "deactivated. Display order decides the sequence, with zero at the "
              "top, and two banners sharing an order are separated by their start "
              "date.",
              "للافتة بداية ونهاية، ولا تُرجع القراءة العامة إلا اللافتات "
              "المفعَّلة التي تقع اللحظة الحالية داخل نافذتها. واللافتة التي لا "
              "تظهر تكون خارج تواريخها أكثر بكثير مما تكون معطَّلة. ويحدّد ترتيب "
              "العرض التسلسل والصفر في الأعلى، ويُفصل بين لافتتين لهما الترتيب "
              "نفسه بتاريخ البداية."),
            note("The link on a banner is stored as a plain address rather than "
                 "an uploaded file, and it is optional. Nothing acts on it "
                 "today: tapping the banner always opens the news list, whatever "
                 "the link holds. Filling it in changes nothing on screen.",
                 "يُحفظ رابط اللافتة عنوانًا صِرفًا لا ملفًّا مرفوعًا، وهو اختياري. "
                 "ولا شيء يعمل به اليوم: فالنقر على اللافتة يفتح قائمة الأخبار "
                 "دائمًا مهما كان الرابط. وكتابته لا تغيّر شيئًا على الشاشة."),
            figure("cp-admin-banners-default",
                   "The banners. Being active, and the present moment falling "
                   "inside the start and end, are what decide whether one is on "
                   "screen right now; display order decides the sequence.",
                   "اللافتات. كون اللافتة مفعَّلة ووقوع اللحظة الحالية بين "
                   "بدايتها ونهايتها هما ما يحدد إن كانت على الشاشة الآن؛ وترتيب "
                   "العرض يحدد التسلسل."),

            h3("News: the publish date is the gate",
               "الأخبار: تاريخ النشر هو البوابة"),
            p("An article becomes public when it is active and its publish date "
              "has passed. Dating one ahead is how an article is written today "
              "and released on "
              "Sunday — and it is why an article can be plainly visible on this "
              "page and absent from the app. The admin grid shows every article "
              "regardless of its date, on purpose.",
              "يصبح المقال عامًّا حين يكون مفعَّلًا ويمضي تاريخ نشره. وتأريخه إلى "
              "الأمام هو الطريقة التي يُكتب بها المقال اليوم ويصدر يوم الأحد "
              "— وهو سبب أن "
              "يكون المقال ظاهرًا تمامًا في هذه الصفحة وغائبًا عن التطبيق. وتعرض "
              "شبكة الإدارة كل المقالات بصرف النظر عن تاريخها، وذلك مقصود."),
            bullets(
                ["The excerpt is optional, and nothing shows it today. Nothing "
                 "is derived from the body; the app's news card carries only the "
                 "picture, the date and the title, and the article screen has no "
                 "excerpt at all. Leaving it empty changes nothing on screen.",
                 "The category is the small kicker printed above the title. It is "
                 "free text typed on the article, not a list to choose from, so "
                 "spelling it differently on two articles produces two different "
                 "kickers. FDS-010 describes it as a managed list; it is not one.",
                 "Both title and body are bilingual, and both languages are "
                 "wanted: the app shows the reader whichever matches their "
                 "language."],
                ["المقتطف اختياري، ولا شيء يعرضه اليوم. فلا يُشتقّ شيء من المتن، "
                 "وبطاقة الخبر في التطبيق لا تحمل إلا الصورة والتاريخ والعنوان، "
                 "وشاشة المقال لا تحمل مقتطفًا إطلاقًا. وتركه فارغًا لا يغيّر "
                 "شيئًا على الشاشة.",
                 "التصنيف هو العنوان الصغير المطبوع فوق العنوان. وهو نص حر يُكتب "
                 "على المقال، لا قائمة يُختار منها، فكتابته بصيغتين على مقالين "
                 "تُنتج عنوانين صغيرين مختلفين. ويصفه المستند FDS-010 قائمةً "
                 "مُدارة؛ وليس كذلك.",
                 "العنوان والمتن كلاهما بلغتين، وكلتا اللغتين مطلوبة: إذ يعرض "
                 "التطبيق للقارئ ما يوافق لغته."]),
            figure("cp-admin-news-default",
                   "The news list. Every article appears here, including the ones "
                   "dated ahead that the app is not showing yet.",
                   "قائمة الأخبار. تظهر هنا كل المقالات، بما فيها المؤرَّخة إلى "
                   "الأمام التي لا يعرضها التطبيق بعد."),

            h2("Media library and Media Center are different things",
               "مكتبة الوسائط والمركز الإعلامي شيئان مختلفان"),
            p("The Media Center is the public gallery: each item is either a "
              "picture uploaded to it or a video held as a link to where it "
              "plays, grouped into albums and ordered. That is content the "
              "audience browses.",
              "المركز الإعلامي هو المعرض العام: كل عنصر فيه إما صورة رُفعت إليه "
              "أو مقطع مرئي محفوظ كرابط إلى موضع تشغيله، مجموعةً في ألبومات "
              "ومرتَّبة. وهذا محتوى يتصفحه الجمهور."),
            p("The Media library is not content at all. It is a view across the "
              "images that belong to a page — speaker photographs, media-partner "
              "and sponsor and exhibitor and booth logos, archive covers, "
              "archive past-speaker and archive gallery photographs, news "
              "pictures, programme-day images, the organisation logo, banner "
              "images. It does not reach personal files — account photographs, "
              "identity documents, VIP photos — and it does not reach the Media "
              "Center's own gallery pictures, which are managed on their own "
              "page. It exists so somebody can find a "
              "file, look at it, and deactivate or restore it without hunting "
              "through the page that owns it. Deactivating a file here removes it "
              "from wherever it was being shown.",
              "أما مكتبة الوسائط فليست محتوى إطلاقًا. بل هي عرض للصور التي تتبع "
              "صفحةً من الصفحات — صور المتحدثين، وشعارات الشركاء الإعلاميين "
              "والرعاة والعارضين والأجنحة، وأغلفة الأرشيف، وصور متحدثي الدورات "
              "السابقة وصور معارضها، وصور الأخبار، وصور أيام البرنامج، وشعار "
              "الجهة، وصور اللافتات. وهي لا تصل إلى الملفات الشخصية — صور "
              "الحسابات ووثائق الهوية وصور كبار الشخصيات — ولا تصل إلى صور معرض "
              "المركز الإعلامي التي تُدار في صفحتها الخاصة. وهي موجودة "
              "ليتمكن أحدهم من إيجاد ملف والنظر إليه وتعطيله أو استعادته دون "
              "التنقيب في الصفحة التي تملكه. وتعطيل ملف هنا يزيله من حيث كان "
              "يُعرض."),
            figure("cp-admin-media-library-default",
                   "The media library: every picture a page owns, whatever page "
                   "that is — not personal files and not the Media Center's "
                   "gallery.",
                   "مكتبة الوسائط: كل صورة تملكها صفحة، أيًّا كانت تلك الصفحة — "
                   "لا الملفات الشخصية ولا معرض المركز الإعلامي."),

            h2("Previous editions are not the event edition",
               "الدورات السابقة ليست دورة الفعالية"),
            p("Two pages carry a year and they have nothing to do with each "
              "other. Previous editions is hand-written history: a year, a "
              "bilingual title and summary, and reported totals for attendees, "
              "sessions and speakers that an administrator types in. They are not "
              "counted from anything. Alongside them sit that edition's gallery "
              "pictures, its session titles and its past speakers.",
              "صفحتان تحملان سنة ولا علاقة لإحداهما بالأخرى. فالدورات السابقة "
              "تاريخ مكتوب باليد: سنة، وعنوان وملخص بلغتين، وإجماليات مُبلَّغة "
              "للحضور والجلسات والمتحدثين يكتبها مسؤول. وهي لا تُحتسب من أي شيء. "
              "ويصحبها صور معرض تلك الدورة وعناوين جلساتها ومتحدثوها السابقون."),
            p("The event edition page, in the System menu, records which year the "
              "forum is currently running. Closing this year there does not "
              "produce an archive card for it: somebody writes that card here, by "
              "hand, with the totals they want reported.",
              "أما صفحة دورة الفعالية في قائمة النظام فتسجّل السنة التي يُقام فيها "
              "الملتقى حاليًّا. وإغلاق هذه السنة هناك لا يُنتج بطاقة أرشيف لها: بل "
              "يكتب أحدهم تلك البطاقة هنا يدويًّا بالإجماليات التي يريد الإبلاغ "
              "عنها."),
            note("Whether the archive is publicly visible at all is not a setting "
                 "on an edition. It is one switch for the whole archive, on the "
                 "Operations page: turn it off and the public list comes back "
                 "empty — the app's archive screen shows its empty state and "
                 "each edition's page reports not found. The website does not go "
                 "blank: its Archive page and Archive menu fall back to a fixed "
                 "set of edition cards built into the site, with the site's own "
                 "totals, so check the switch in the app rather than on the "
                 "website. FDS-010 describes a visibility flag per edition; the "
                 "system has a "
                 "single global one.",
                 "وكون الأرشيف ظاهرًا للعموم أصلًا ليس إعدادًا على الدورة. بل هو "
                 "مفتاح واحد للأرشيف كله في صفحة العمليات: أوقفه فتعود القائمة "
                 "العامة فارغة — فتعرض شاشة الأرشيف في التطبيق حالتها الفارغة "
                 "وتُبلِّغ صفحة كل دورة بعدم الوجود. أما الموقع فلا يفرغ: إذ ترجع "
                 "صفحة الأرشيف وقائمة الأرشيف فيه إلى مجموعة ثابتة من بطاقات "
                 "الدورات مبنيّة داخل الموقع بإجمالياته الخاصة، فتحقّق من المفتاح "
                 "في التطبيق لا في الموقع. ويصف المستند "
                 "FDS-010 راية ظهور لكل دورة؛ وفي النظام مفتاح عام واحد."),
            figure("cp-admin-archive-default",
                   "Previous editions. The attendee, session and speaker totals "
                   "on each row are typed, not counted.",
                   "الدورات السابقة. إجماليات الحضور والجلسات والمتحدثين في كل "
                   "سجل مكتوبة لا محتسَبة."),
        ],
    }
