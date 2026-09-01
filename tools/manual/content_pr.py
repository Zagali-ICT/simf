"""The public-relations chapter: invitations, VIPs, announcements, inquiries.

The four pages differ in reach by three orders of magnitude, and the Control
Panel does not make that obvious. An invitation reaches one person. A VIP notice
reaches at most five hundred and never shows you who it skipped. An announcement
has no ceiling at all, can include accounts nobody has approved yet, and cannot
be recalled once the worker has claimed it.

That asymmetry - the page that looks like the careful one is the bounded one,
and the page that looks routine is the unbounded one - is the chapter's spine.
Every number in it was read out of the validator, the service or the enum that
enforces it.
"""

from blocks import bullets, figure, h2, h3, note, p, t, table


def chapter_public_relations():
    return {
        "id": "public-relations",
        "title": t("Reaching people: invitations, VIPs and announcements",
                   "الوصول إلى الناس: الدعوات وكبار الشخصيات والإعلانات"),
        "blocks": [
            p("Four pages send things to people, and they are not variations on "
              "one another. Read this table before using the third one.",
              "أربع صفحات تُرسل أشياء إلى الناس، وهي ليست صيغًا مختلفة لشيء واحد. "
              "اقرأ هذا الجدول قبل استخدام الثالثة منها."),
            table(
                ["Page", "Reaches", "Ceiling", "Channels"],
                ["الصفحة", "تصل إلى", "الحد الأقصى", "القنوات"],
                [
                    ["Invitations", "One named person, one row per send",
                     "One at a time", "In-app notification only — no email"],
                    ["VIPs — notify", "The VIP profiles you tick",
                     "500 per send, refused above that",
                     "In-app notification and email"],
                    ["Announcements", "One session's seat-holders, or a whole "
                     "audience",
                     "None anywhere",
                     "In-app notification and email, one of each per recipient"],
                    ["Contact inquiries", "Nobody — it is an inbox, not an "
                     "outbox", "—", "Incoming from the app's contact form"],
                ],
                [
                    ["الدعوات", "شخص واحد محدَّد، وسجل لكل إرسال",
                     "واحد في المرة", "إشعار داخل التطبيق فقط — بلا بريد"],
                    ["كبار الشخصيات — إشعار", "ملفات كبار الشخصيات التي تحدّدها",
                     "500 في الإرسالة الواحدة، ويُرفض ما زاد",
                     "إشعار داخل التطبيق وبريد إلكتروني"],
                    ["الإعلانات", "أصحاب مقاعد جلسة واحدة، أو جمهور كامل",
                     "لا حد في أي موضع",
                     "إشعار داخل التطبيق وبريد إلكتروني، واحد من كلٍّ لكل مستلم"],
                    ["رسائل التواصل", "لا أحد — فهي صندوق وارد لا صادر", "—",
                     "واردة من نموذج التواصل في التطبيق"],
                ]),

            h2("Invitations", "الدعوات"),
            p("An invitation is a record of one send to one person. Inviting the "
              "same person again does not update the first invitation — it adds a "
              "second row, and the first one stays as the record that they were "
              "invited before. The row carries who sent it, who it was addressed "
              "to, its state, free-text notes, and when the recipient "
              "responded.",
              "الدعوة سجلٌّ لإرسال واحد إلى شخص واحد. ودعوة الشخص نفسه مرة أخرى لا "
              "تحدّث الدعوة الأولى — بل تضيف سجلًّا ثانيًا، ويبقى الأول شاهدًا على "
              "أنه دُعي من قبل. ويحمل السجل من أرسلها، ومن وُجّهت إليه، وحالتها، "
              "وملاحظات نصية حرّة، ووقت رد المستلم."),
            p("Issuing one sends an in-app notification and nothing else. There is "
              "no invitation email, so a recipient who does not open the app will "
              "not learn about it from the system.",
              "إصدار الدعوة يرسل إشعارًا داخل التطبيق ولا شيء غيره. فلا يوجد بريد "
              "دعوة، ومن لا يفتح التطبيق من المستلمين لن يعلم بها من النظام."),
            bullets(
                ["The invitation is addressed to the profile, not to a sign-in "
                 "account. Somebody with no account can be invited; they simply "
                 "have no inbox to receive the notification in, and the "
                 "invitation still stands as a record.",
                 "The notification is best-effort. If notifications are down the "
                 "invitation is still created and still succeeds — it does not "
                 "fail on the way out.",
                 "A state can move from pending to a settled answer. It cannot be "
                 "pushed back to pending afterwards, because the recipient has "
                 "already answered and unsaying that is not something an "
                 "administrator should be able to do."],
                ["الدعوة موجَّهة إلى الملف لا إلى حساب دخول. فمن لا حساب له يمكن "
                 "دعوته؛ غير أنه لا يملك صندوقًا يتلقى فيه الإشعار، وتبقى الدعوة "
                 "قائمة كسجل.",
                 "الإشعار على أساس بذل الوسع. فإن كانت الإشعارات متوقفة تُنشأ "
                 "الدعوة وتنجح رغم ذلك — ولا تفشل في طريقها للخروج.",
                 "يمكن أن تنتقل الحالة من قيد الانتظار إلى جواب نهائي. ولا يمكن "
                 "إعادتها إلى قيد الانتظار بعد ذلك، لأن المستلم قد أجاب فعلًا "
                 "وسحب ذلك ليس مما ينبغي أن يقدر عليه مسؤول."]),
            figure("cp-admin-invitations-default",
                   "The invitations list. One row per send, so a re-invitation "
                   "appears alongside the earlier one rather than replacing it.",
                   "قائمة الدعوات. سجل لكل إرسال، فتظهر إعادة الدعوة إلى جانب "
                   "السابقة لا بدلًا منها."),

            h2("VIPs are a profile type, not a list you edit",
               "كبار الشخصيات نوع ملف، لا قائمة تحرّرها"),
            p("There is no VIP table, and nothing on the VIPs page itself "
              "changes who is on it. The page shows every profile whose profile "
              "type is named VVIP, VIP or Gold — those three names, exactly. "
              "Somebody becomes a VIP by being given one of those profile types, "
              "and stops being one by being given a different type. The \"VIP "
              "tier\" checkbox on the visitor profile-type form is a different "
              "switch: it decides who may reserve a VIP-tier seat for themselves "
              "and whether the app shows the VIP marker, and it puts nobody on "
              "this page. The two do not line up out of the box — Gold is on this "
              "page, but only VVIP and VIP start out marked as the VIP tier, so a "
              "Gold guest is listed here and still cannot reserve a VIP seat for "
              "themselves.",
              "لا يوجد جدول لكبار الشخصيات، ولا شيء في صفحة كبار الشخصيات "
              "نفسها يغيّر من عليها. فالصفحة تعرض كل ملف يكون نوعه مسمًّى VVIP "
              "أو VIP أو Gold — هذه الأسماء الثلاثة تحديدًا. ويصير المرء من كبار "
              "الشخصيات بمنحه أحد هذه الأنواع، ويكفّ عن ذلك بمنحه نوعًا آخر. أمّا "
              "خانة «فئة كبار الشخصيات» في نموذج نوع ملف الزائر فمفتاح آخر: هي "
              "التي تقرّر من يحجز لنفسه مقعدًا من فئة كبار الشخصيات، وهل يُظهر "
              "التطبيق علامة كبار الشخصيات، ولا تُدخل أحدًا إلى هذه الصفحة. "
              "والاثنان لا يتطابقان ابتداءً — فـ Gold على هذه الصفحة، لكن المعلَّم بفئة كبار "
              "الشخصيات في الأصل هو VVIP وVIP فقط، فيظهر ضيف Gold هنا ولا يستطيع مع ذلك أن "
              "يحجز لنفسه مقعد كبار الشخصيات."),
            note("Guests with no sign-in account are listed here without an email "
                 "address rather than left out. They are real invitees and hiding "
                 "them would make the list disagree with the guest list.",
                 "يُدرج الضيوف الذين لا حساب دخول لهم هنا بلا بريد إلكتروني بدل "
                 "استبعادهم. فهم مدعوّون حقيقيون، وإخفاؤهم يجعل القائمة تخالف "
                 "قائمة الضيوف."),
            p("The notify action on this page sends to the profiles you have "
              "selected. It refuses more than five hundred in one send, and it "
              "quietly skips anybody in the selection who is not actually a VIP "
              "or who has no account to notify. The result tells you how many "
              "VIPs it reached and how many emails were queued, and nothing "
              "more: there is no skipped count on the screen, and the message "
              "stays a green success even when part of your selection was "
              "dropped. So compare the number it reports against the number you "
              "ticked — that is the difference between believing a hundred "
              "people were told and knowing that eighty-three were. How many it "
              "skipped goes to the audit log, not to you.",
              "يرسل إجراء الإشعار في هذه الصفحة إلى الملفات التي حدّدتها. ويرفض ما "
              "زاد على خمسمائة في الإرسالة الواحدة، ويتخطى بهدوء كل من في التحديد "
              "ممن ليس فعلًا من كبار الشخصيات أو ليس له حساب يُشعَر. وتخبرك النتيجة "
              "بعدد من بلغتهم من كبار الشخصيات وبعدد رسائل البريد في الطابور، ولا "
              "شيء غير ذلك: فلا يظهر على الشاشة عدد من تخطاهم، وتبقى الرسالة نجاحًا "
              "أخضر وإن سقط جزء من تحديدك. فقابِل الرقم الذي تعرضه بعدد من "
              "أشّرت عليهم — فهو الفرق بين اعتقاد أن مئةً قد أُبلغوا ومعرفة أن "
              "ثلاثةً وثمانين أُبلغوا. أمّا عدد من تخطاهم فيُكتب في سجل التدقيق "
              "ولا يُعرض عليك."),
            figure("cp-admin-vips-default",
                   "The VIPs page. Everybody on it is here because of their "
                   "profile type, and that is the only way on or off.",
                   "صفحة كبار الشخصيات. كل من فيها موجود بسبب نوع ملفه، وذلك هو "
                   "السبيل الوحيد للدخول إليها أو الخروج منها."),

            h2("Announcements", "الإعلانات"),
            p("An announcement is the broadcast desk. Write a bilingual title and "
              "message, choose an importance, choose who it goes to, and submit. "
              "Each recipient gets one notification inside the app and one queued "
              "email. There is no text-message or messaging-app channel — "
              "FDS-009 lists them; they were never built.",
              "الإعلان هو مكتب البث. اكتب عنوانًا ورسالة بلغتين، واختر الأهمية، "
              "واختر من تصل إليه، ثم أرسل. فيتلقى كل مستلم إشعارًا واحدًا داخل "
              "التطبيق وبريدًا واحدًا في الطابور. ولا توجد قناة رسائل نصية ولا "
              "تطبيقات مراسلة — يذكرها المستند FDS-009؛ ولم تُبنَ قط."),
            h3("Choosing who it goes to", "اختيار من تصل إليه"),
            table(
                ["Target", "Who that is"],
                ["الهدف", "من هم"],
                [
                    ["A session", "Everybody with a sign-in account who holds a "
                     "live seat reservation in that session. A seat-holder with "
                     "no account — a walk-in, say — has no inbox, and drops out "
                     "of both the send and the estimate"],
                    ["Approved app users", "Every approved non-administrator "
                     "account"],
                    ["Event attendees", "Everybody with a sign-in account who "
                     "holds a live seat reservation in at least one session, "
                     "with the same exclusion"],
                    ["Everyone including pending", "Every non-administrator "
                     "account whatever its state — including sign-ups nobody has "
                     "approved yet"],
                ],
                [
                    ["جلسة", "كل من له حساب دخول ويحمل حجز مقعد ساريًا في تلك "
                     "الجلسة. ومن يحمل مقعدًا بلا حساب — كالحاضر المباشر — لا "
                     "صندوق له، فيسقط من الإرسال ومن التقدير معًا"],
                    ["مستخدمو التطبيق المعتمدون", "كل حساب معتمد ليس مسؤولًا"],
                    ["حضور الفعالية", "كل من له حساب دخول ويحمل حجز مقعد ساريًا "
                     "في جلسة واحدة على الأقل، بالاستثناء نفسه"],
                    ["الجميع بمن فيهم قيد الانتظار", "كل حساب ليس مسؤولًا أيًّا "
                     "كانت حالته — بمن فيهم المسجَّلون الذين لم يعتمدهم أحد بعد"],
                ]),

            note("Nothing anywhere limits how many people an announcement "
                 "reaches. The composer accepts the target, checks the title and "
                 "message lengths, and queues it. The VIP page's five-hundred "
                 "ceiling does not exist here, and \"everyone including pending\" "
                 "means exactly that. This is the one page in the Control Panel "
                 "where a wrong click is measured in tens of thousands of "
                 "messages.",
                 "لا شيء في أي موضع يحدّ عدد من يصلهم الإعلان. فالمحرّر يقبل "
                 "الهدف، ويتحقق من طولي العنوان والرسالة، ثم يضعه في الطابور. "
                 "وسقف الخمسمائة في صفحة كبار الشخصيات لا وجود له هنا، و«الجميع "
                 "بمن فيهم قيد الانتظار» تعني ذلك حرفيًّا. وهذه هي الصفحة الوحيدة "
                 "في لوحة التحكم التي تُقاس فيها النقرة الخاطئة بعشرات الآلاف من "
                 "الرسائل."),

            h3("What happens after you submit",
               "ماذا يحدث بعد الإرسال"),
            p("Submitting does not send. It writes one pending job and hands back "
              "an estimate of how many people it will reach; a background worker "
              "picks the job up shortly afterwards and does the sending. Two "
              "consequences follow, and both surprise people.",
              "الإرسال لا يبعث الرسائل. بل يكتب مهمة واحدة قيد الانتظار ويعيد "
              "تقديرًا لعدد من ستصلهم؛ ثم يلتقط عاملٌ في الخلفية المهمةَ بعد قليل "
              "ويتولى البث. ويترتب على ذلك أمران، وكلاهما يفاجئ الناس."),
            bullets(
                ["**The number you were shown is an estimate, computed when you "
                 "submitted.** The recipients are worked out again at send time, "
                 "so the count on the history is legitimately different from the "
                 "one the composer promised — somebody signed up, or was "
                 "approved, in between.",
                 "**It cannot be recalled.** The worker claims the job before it "
                 "starts dispatching, precisely so a restart mid-send never sends "
                 "everything twice. The same claim means there is no way to stop "
                 "it once it has started. A job left claimed for a quarter of an "
                 "hour is marked failed rather than retried, for the same "
                 "reason."],
                ["**الرقم الذي عُرض عليك تقدير حُسب لحظة الإرسال.** فالمستلمون "
                 "يُستخرجون من جديد وقت البث، ولذلك يختلف العدد في السجل اختلافًا "
                 "مشروعًا عمّا وعد به المحرّر — إذ سجّل أحدهم أو اعتُمد في ما بين "
                 "ذلك.",
                 "**ولا يمكن استرجاعه.** فالعامل يحجز المهمة قبل أن يبدأ البث، "
                 "وذلك تحديدًا حتى لا يؤدي إعادة التشغيل في منتصف البث إلى "
                 "بعث كل شيء مرتين. وهذا الحجز نفسه يعني أنه لا سبيل إلى إيقافه "
                 "بعد أن يبدأ. وتُعلَّم المهمة المحجوزة ربع ساعة بالفشل بدل إعادة "
                 "محاولتها، للسبب نفسه."]),
            p("Sending is paced rather than instantaneous: recipients go out in "
              "batches of a hundred, and the worker waits when the email queue is "
              "already carrying seven hundred messages, half a second at a time. "
              "It will wait up to two minutes; beyond that it stops holding back "
              "and lets the queue drop what it cannot take, rather than stalling "
              "every later announcement behind this one. A very large "
              "announcement is therefore delivered over minutes, and its emails "
              "are the part that can be shed under load.",
              "والبث متدرّج لا فوري: إذ يخرج المستلمون على دفعات من مئة، وينتظر "
              "العامل حين يكون طابور البريد يحمل سبعمائة رسالة أصلًا، نصف ثانية "
              "في كل مرة. وينتظر حتى دقيقتين؛ فإذا تجاوزهما كفّ عن التمهّل وترك "
              "الطابور يُسقط ما لا يسعه، بدل تعطيل كل إعلان لاحق خلف هذا الإعلان. "
              "ولذلك يُسلَّم الإعلان الضخم على مدى دقائق، ورسائل بريده هي الجزء "
              "الذي قد يسقط عند الضغط."),
            figure("cp-admin-announcements-default",
                   "The announcements desk with its history. The recipient count "
                   "on a past row is what was actually resolved at send time.",
                   "مكتب الإعلانات وسجله. عدد المستلمين في السجل السابق هو ما "
                   "استُخرج فعلًا وقت البث."),

            h2("Contact inquiries", "رسائل التواصل"),
            p("This is an inbox. The messages come from the contact form in the "
              "mobile app, which anybody may use — no sign-in is required — so a "
              "row may carry a name, an email address and a message with no "
              "account behind it at all. The public website has no form wired to "
              "this page.",
              "هذه صندوق وارد. تأتي الرسائل من نموذج التواصل في تطبيق الجوال، "
              "ويجوز لأي أحد استخدامه — إذ لا يلزم تسجيل دخول — فقد يحمل السجل "
              "اسمًا وبريدًا ورسالة بلا حساب خلفها إطلاقًا. وليس في الموقع العام "
              "نموذج موصول بهذه الصفحة."),
            p("Open inquiries are listed first, newest first. Marking one handled "
              "records who dealt with it and when; the same control reopens it if "
              "it was closed too early.",
              "تُعرض الرسائل المفتوحة أولًا والأحدث أولًا. ووسمها بأنها عولجت يسجّل "
              "من عالجها ومتى؛ والزر نفسه يعيد فتحها إن أُغلقت قبل أوانها."),
            figure("cp-admin-contact-inquiries-default",
                   "The contact inquiries inbox. Open messages come first.",
                   "صندوق رسائل التواصل. الرسائل المفتوحة أولًا."),
        ],
    }
