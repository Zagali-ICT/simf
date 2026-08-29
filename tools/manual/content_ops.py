"""The deployment and configuration chapters of the operations manual.

Kept apart from make_book.py because this half of the book is a different job:
make_book.py describes screens a person uses, this describes a server an
engineer builds.

NO SECRET VALUE APPEARS IN THIS FILE. The deploy scripts on a provisioned
machine hold real connection strings, the JWT signing key, both encryption keys,
the SMTP password and the AI key. What the manual carries is the NAME of each
variable, what it controls, what it defaults to, what it should be set to, and
what breaks when it is missing. A value that is a secret is named as one and the
command that generates it is given instead.

Every default below was read from the options class or the shipped
appsettings.json that defines it, never from a prose document. That is not
pedantry: SIMF-OPS-001 had drifted on three of these - one shared SIMF_ prefix
where there are four, two storage paths listed as required that no longer
exist, and a token lifetime of 30 where the code says 5 - and every one of them
would have been copied into this manual by a writer who trusted it. Those
three are corrected in the document itself as part of this change; the rule
that produced the correction is the one to keep.
"""

from blocks import bullets, code, figure, h2, h3, note, p, t, table


SECRET_EN = "A secret — generate one per environment"
SECRET_AR = "سر — يُولَّد واحد لكل بيئة"
REQUIRED_EN = "Required"
REQUIRED_AR = "مطلوب"


def env_table(rows):
    """rows: (name, purpose_en, purpose_ar, default_en, default_ar,
              recommended_en, recommended_ar)"""
    rows_en = [[r[0], r[1], r[3], r[5]] for r in rows]
    rows_ar = [[r[0], r[2], r[4], r[6]] for r in rows]
    return table(
        ["Variable", "What it controls", "Ships as", "Set it to"],
        ["المتغيّر", "ما يتحكم فيه", "القيمة المُشحونة", "اضبطه على"],
        rows_en, rows_ar)


# ------------------------------------------------------------- deployment --

def chapter_deployment():
    return {
        "id": "deployment",
        "title": t("Deploying to a server", "النشر على الخادم"),
        "blocks": [
            p("SIMF is installed on premises, on Windows Server with IIS and "
              "SQL Server. A deployment is three separate things, done in this "
              "order: the pipeline copies the built files onto the server, an "
              "operator sets that server's configuration, and the application "
              "creates or updates its own database the first time it starts. "
              "Nothing else creates the database, and nothing in the pipeline "
              "sets a single configuration value.",
              "يُثبَّت نظام SIMF محليًا على خادم ويندوز مع IIS وSQL Server. وعملية "
              "النشر ثلاثة أمور منفصلة تُنفَّذ بهذا الترتيب: يَنسخ خط الإنتاج الملفات "
              "المبنية إلى الخادم، ثم يضبط المشغّل تهيئة ذلك الخادم، ثم ينشئ "
              "التطبيق قاعدة بياناته أو يحدّثها عند أول تشغيل. لا شيء آخر ينشئ "
              "قاعدة البيانات، ولا شيء في خط الإنتاج يضبط أي قيمة تهيئة."),

            h2("The estate", "منظومة الخوادم"),
            p("There are two servers, and each runs all four applications. The "
              "single most important thing to know before running a deployment "
              "is that the environment names are misleading:",
              "يوجد خادمان، ويشغّل كل منهما التطبيقات الأربعة جميعها. وأهم ما يجب "
              "معرفته قبل تنفيذ أي نشر أن أسماء البيئات مضلِّلة:"),
            table(
                ["Environment name", "What it actually is", "Address"],
                ["اسم البيئة", "ما هي في الحقيقة", "العنوان"],
                [["SIMF-Prod", "PRE-PRODUCTION", "simf.zagali-ict.com"],
                 ["SIM-RNSF", "PRODUCTION", "web / cp / api.simrsnf.com"]],
                [["SIMF-Prod", "ما قبل الإنتاج", "simf.zagali-ict.com"],
                 ["SIM-RNSF", "الإنتاج", "web / cp / api.simrsnf.com"]]),
            note("This is not a typo to be corrected. Those are the names as "
                 "registered, and the one that reads like production is not. "
                 "Anyone who \"fixes\" the mapping on sight deploys straight to "
                 "production while believing they are rehearsing. Trust the job "
                 "names in the pipeline, never the environment string.",
                 "ليس هذا خطأً مطبعيًا يُصحَّح. فهذه هي الأسماء كما هي مسجّلة، والاسم "
                 "الذي يبدو وكأنه الإنتاج ليس كذلك. ومن «يصحّح» هذا الربط بمجرد "
                 "رؤيته سينشر مباشرة إلى الإنتاج وهو يظن أنه يتدرّب. فاعتمد على "
                 "أسماء المهام في خط الإنتاج، لا على اسم البيئة أبدًا."),

            h2("The four applications", "التطبيقات الأربعة"),
            table(
                ["Application", "IIS site", "Folder on the server", "Public address"],
                ["التطبيق", "موقع IIS", "المجلد على الخادم", "العنوان العام"],
                [["API", "SIMF.API", "D:\\System\\v1.0.1\\api", "api.simrsnf.com (internal)"],
                 ["Control Panel", "SIMF.CP", "D:\\System\\v1.0.1\\cp", "cp.simrsnf.com"],
                 ["Website", "SIMF.WEB", "D:\\System\\v1.0.1\\web", "web.simrsnf.com"],
                 ["Mobile edge", "SIMF.EDGE", "D:\\System\\v1.0.1\\edge", "edge.simrsnf.com"]],
                [["واجهة البرمجة", "SIMF.API", "D:\\System\\v1.0.1\\api", "api.simrsnf.com (داخلي)"],
                 ["لوحة التحكم", "SIMF.CP", "D:\\System\\v1.0.1\\cp", "cp.simrsnf.com"],
                 ["الموقع", "SIMF.WEB", "D:\\System\\v1.0.1\\web", "web.simrsnf.com"],
                 ["حافة الجوال", "SIMF.EDGE", "D:\\System\\v1.0.1\\edge", "edge.simrsnf.com"]]),
            note("The IIS sites and application pools must already exist. The "
                 "deployment copies files into them; it does not create them. "
                 "Create them by hand in IIS Manager before the first "
                 "deployment, with the .NET CLR version set to No Managed Code.",
                 "يجب أن تكون مواقع IIS ومجمّعات التطبيقات موجودة مسبقًا. فالنشر "
                 "ينسخ الملفات إليها ولا ينشئها. أنشئها يدويًا في مدير IIS قبل أول "
                 "نشر، مع ضبط إصدار CLR على «بدون تعليمات برمجية مُدارة»."),

            h2("Before the first deployment", "قبل أول عملية نشر"),
            bullets([
                "Windows Server with IIS and the ASP.NET Core hosting bundle "
                "installed — without the bundle the application pool starts and "
                "immediately stops.",
                "SQL Server, reachable from the application server on port 1433.",
                "A TLS certificate for each public host name. The one for the "
                "API is the one that gets forgotten, because it is internal.",
                "The host time zone set to Arab Standard Time.",
                "Write access, for each application pool identity, to the log "
                "directory and to the file-store directory.",
                "For the Flutter web bundle only: the IIS URL Rewrite and "
                "Application Request Routing modules, with proxying enabled.",
            ], [
                "خادم ويندوز مع IIS وحزمة استضافة ASP.NET Core مثبّتة — فبدون "
                "الحزمة يبدأ مجمّع التطبيقات ثم يتوقف فورًا.",
                "خادم SQL Server يمكن الوصول إليه من خادم التطبيق على المنفذ 1433.",
                "شهادة TLS لكل اسم مضيف عام. وشهادة واجهة البرمجة هي التي "
                "تُنسى عادة لأنها داخلية.",
                "ضبط المنطقة الزمنية للخادم على التوقيت العربي القياسي.",
                "صلاحية الكتابة، لهوية كل مجمّع تطبيقات، على مجلد السجلات ومجلد "
                "مخزن الملفات.",
                "لحزمة الويب المبنية بـFlutter فقط: وحدتا URL Rewrite وApplication "
                "Request Routing في IIS مع تفعيل الوكالة.",
            ]),

            h2("The order of a deployment", "ترتيب عملية النشر"),
            bullets([
                "The pipeline builds all four applications and publishes them as "
                "one artefact.",
                "It confirms the target machine, and refuses to continue if the "
                "machine is the one named as forbidden for that job — this is "
                "what stops a pre-production run reaching production.",
                "For each application in turn — API first, edge last — it stops "
                "the site and its pool, mirrors the files into place, and starts "
                "them again, proving both reached the started state.",
                "The operator then runs that server's configuration scripts as "
                "an administrator.",
                "The operator restarts IIS. Recycling the pool is not enough: "
                "the worker process inherits its environment from the service "
                "that launched it, so a recycled pool keeps the old values.",
                "The API is started; it creates or migrates both databases and "
                "seeds the first administrator.",
            ], [
                "يبني خط الإنتاج التطبيقات الأربعة وينشرها كحزمة واحدة.",
                "يتأكد من الجهاز الهدف، ويرفض المتابعة إذا كان الجهاز هو المحظور "
                "لتلك المهمة — وهذا ما يمنع تشغيل ما قبل الإنتاج من الوصول إلى "
                "الإنتاج.",
                "ثم لكل تطبيق بالتتابع — واجهة البرمجة أولًا والحافة أخيرًا — "
                "يوقف الموقع ومجمّعه، وينسخ الملفات، ثم يعيد تشغيلهما ويتحقق من "
                "بلوغهما حالة التشغيل.",
                "بعد ذلك يشغّل المشغّل نصوص تهيئة ذلك الخادم بصلاحيات المسؤول.",
                "ثم يعيد المشغّل تشغيل IIS. ولا تكفي إعادة تدوير المجمّع: فعملية "
                "العامل ترث بيئتها من الخدمة التي شغّلتها، فيحتفظ المجمّع المُعاد "
                "تدويره بالقيم القديمة.",
                "ثم تُشغَّل واجهة البرمجة، فتنشئ قاعدتَي البيانات أو تُرقّيهما وتزرع "
                "أول مسؤول.",
            ]),

            h2("The databases", "قواعد البيانات"),
            p("There are two, deliberately kept apart: one holds accounts, "
              "roles and permissions, the other holds everything else. There is "
              "no manual migration step — the API applies its own migrations at "
              "startup, and it applies them to the second database before the "
              "first. That order is required, not incidental.",
              "هناك قاعدتان مفصولتان عن عمد: إحداهما للحسابات والأدوار والصلاحيات، "
              "والأخرى لكل ما عداها. ولا توجد خطوة ترحيل يدوية — فواجهة البرمجة "
              "تطبّق ترحيلاتها عند الإقلاع، وتطبّقها على القاعدة الثانية قبل "
              "الأولى. وهذا الترتيب لازم وليس عرَضيًا."),
            p("Booting the API against an empty database creates it. Content — "
              "the programme, the speakers, the news, the sponsors and the "
              "archive — is applied separately, by hand, from the seed scripts. "
              "One step in that process is copying the speaker photographs into "
              "the file store; without it every photograph returns not-found "
              "while the database rows look perfectly healthy.",
              "يؤدي تشغيل واجهة البرمجة على قاعدة فارغة إلى إنشائها. أما المحتوى — "
              "البرنامج والمتحدثون والأخبار والرعاة والأرشيف — فيُطبَّق منفصلًا "
              "ويدويًا من نصوص البذر. وإحدى خطوات تلك العملية نسخ صور المتحدثين "
              "إلى مخزن الملفات؛ وبدونها تُرجع كل صورة «غير موجودة» بينما تبدو "
              "صفوف قاعدة البيانات سليمة تمامًا."),

            h2("The first administrator", "أول مسؤول"),
            p("The system creates exactly one administrator on first boot, from "
              "configuration. Its email, its starting password and its "
              "authenticator secret are all configuration values. Once it has "
              "signed in, change the password, pair a fresh authenticator, and "
              "then change those configuration values to new throwaway ones. "
              "That last step is the line between deployed and secure.",
              "ينشئ النظام مسؤولًا واحدًا فقط عند أول إقلاع، انطلاقًا من التهيئة. "
              "فبريده وكلمة مروره الأولى وسرّ تطبيق المصادقة كلها قيم تهيئة. وبعد "
              "تسجيل دخوله، غيّر كلمة المرور، وأقرِن تطبيق مصادقة جديدًا، ثم غيّر "
              "قيم التهيئة تلك إلى قيم مؤقتة جديدة. وهذه الخطوة الأخيرة هي الفارق "
              "بين «منشور» و«آمن»."),
            note("Two traps. If the configured password or email is blank the "
                 "seeder writes no administrator, logs the reason, and the "
                 "system starts up perfectly healthy — every sign-in then fails "
                 "with nothing visibly wrong. And a password containing a run "
                 "such as 12345 is rejected by the password rules, with exactly "
                 "the same silent outcome.",
                 "مِزلقان اثنان. إذا كانت كلمة المرور أو البريد المهيّأ فارغًا فلن "
                 "يكتب البذّار أي مسؤول، وسيسجّل السبب، وسيقلع النظام سليمًا "
                 "تمامًا — ثم يفشل كل تسجيل دخول دون أي خلل ظاهر. وكذلك كلمة مرور "
                 "تحوي تسلسلًا مثل 12345 ترفضها قواعد كلمات المرور، بالنتيجة "
                 "الصامتة نفسها."),
        ],
    }


# ----------------------------------------------------- environment values --

def chapter_configuration():
    return {
        "id": "configuration",
        "title": t("Every configuration value",
                   "كل قيم التهيئة"),
        "blocks": [
            p("Configuration reaches the applications as machine-wide "
              "environment variables set on the server. The files shipped inside "
              "each application carry only non-secret defaults, and they are "
              "overwritten by every deployment, so nothing that matters may be "
              "edited there.",
              "تصل التهيئة إلى التطبيقات عبر متغيّرات بيئة على مستوى الجهاز تُضبط "
              "على الخادم. أما الملفات المشحونة داخل كل تطبيق فلا تحمل سوى القيم "
              "الافتراضية غير السرية، ويُعاد كتابتها مع كل نشر، فلا يجوز تعديل أي "
              "شيء مهم فيها."),

            h2("How a variable name is built", "كيف يُبنى اسم المتغيّر"),
            p("Each application reads its own prefix, and a colon in the setting "
              "name becomes a double underscore in the variable name.",
              "يقرأ كل تطبيق بادئته الخاصة، وتتحوّل النقطتان الرأسيتان في اسم "
              "الإعداد إلى شرطتين سفليتين في اسم المتغيّر."),
            table(
                ["Application", "Prefix", "Example"],
                ["التطبيق", "البادئة", "مثال"],
                [["API", "SIMF_API_", "SIMF_API_ConnectionStrings__SimfAppDb"],
                 ["Control Panel", "SIMF_CP_", "SIMF_CP_Api__BaseUrl"],
                 ["Website", "SIMF_WEB_", "SIMF_WEB_Api__BaseUrl"],
                 ["Mobile edge", "SIMF_EDGE_", "SIMF_EDGE_ReverseProxy__KnownProxies__0"]],
                [["واجهة البرمجة", "SIMF_API_", "SIMF_API_ConnectionStrings__SimfAppDb"],
                 ["لوحة التحكم", "SIMF_CP_", "SIMF_CP_Api__BaseUrl"],
                 ["الموقع", "SIMF_WEB_", "SIMF_WEB_Api__BaseUrl"],
                 ["حافة الجوال", "SIMF_EDGE_", "SIMF_EDGE_ReverseProxy__KnownProxies__0"]]),
            note("A bare SIMF_ prefix binds to nothing. Worse, each application "
                 "refuses to start if it finds variables using that retired "
                 "prefix, naming them in the error — so an upgraded server must "
                 "have the new variables written first and the old ones cleared "
                 "afterwards, never the other way round. The one exception is "
                 "ASPNETCORE_ENVIRONMENT, which is read before any prefix "
                 "applies and stays unprefixed on every host.",
                 "البادئة SIMF_ المجرّدة لا ترتبط بشيء. والأسوأ أن كل تطبيق يرفض "
                 "الإقلاع إذا وجد متغيّرات تستخدم تلك البادئة المتقاعدة، ويذكرها في "
                 "رسالة الخطأ — لذا يجب على الخادم المُرقّى أن تُكتب فيه المتغيّرات "
                 "الجديدة أولًا ثم تُمحى القديمة بعدها، لا العكس أبدًا. والاستثناء "
                 "الوحيد هو ASPNETCORE_ENVIRONMENT الذي يُقرأ قبل تطبيق أي بادئة "
                 "ويبقى بلا بادئة على كل الخوادم."),

            h2("How to set one", "كيف يُضبط المتغيّر"),
            p("From an elevated PowerShell prompt on the server, machine scope, "
              "then restart IIS so the worker process picks up the new "
              "environment block:",
              "من موجّه PowerShell بصلاحيات مرتفعة على الخادم، على نطاق الجهاز، ثم "
              "أعد تشغيل IIS لتلتقط عملية العامل كتلة البيئة الجديدة:"),
            code("[Environment]::SetEnvironmentVariable(\n"
                 "    'SIMF_API_ConnectionStrings__SimfAppDb',\n"
                 "    'Server=...;Database=SIMF_App;...',\n"
                 "    [EnvironmentVariableTarget]::Machine)\n\n"
                 "iisreset"),
            note("Recycling the application pool is not enough, and this is the "
                 "most common reason a correctly set value appears to have no "
                 "effect.",
                 "لا تكفي إعادة تدوير مجمّع التطبيقات، وهذا أشيع سبب يجعل قيمة "
                 "مضبوطة بشكل صحيح تبدو بلا أثر."),

            h2("The values that stop a server starting",
               "القيم التي تمنع الخادم من الإقلاع"),
            p("Seven values are boot gates: in production, the application "
              "refuses to start without its own. This is deliberate — each one "
              "protects something that would otherwise fail silently and much "
              "later.",
              "سبع قيم تمثّل بوابات إقلاع: ففي الإنتاج يرفض التطبيق البدء بدون "
              "القيمة الخاصة به. وهذا مقصود — إذ تحمي كل واحدة منها أمرًا كان "
              "سيفشل بصمت وفي وقت متأخر جدًا."),
            table(
                ["Variable", "Application"],
                ["المتغيّر", "التطبيق"],
                [["SIMF_API_FileStorage__EncryptionKey", "API"],
                 ["SIMF_API_Storage__UserIdDocumentEncryptionKey", "API"],
                 ["SIMF_API_Ai__PromptHash__Secret", "API"],
                 ["SIMF_CP_DataProtection__KeyRingPath", "Control Panel"],
                 ["SIMF_WEB_DataProtection__KeyRingPath", "Website"],
                 ["SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address", "Mobile edge"],
                 ["SIMF_EDGE_ReverseProxy__KnownProxies__0", "Mobile edge"]],
                [["SIMF_API_FileStorage__EncryptionKey", "واجهة البرمجة"],
                 ["SIMF_API_Storage__UserIdDocumentEncryptionKey", "واجهة البرمجة"],
                 ["SIMF_API_Ai__PromptHash__Secret", "واجهة البرمجة"],
                 ["SIMF_CP_DataProtection__KeyRingPath", "لوحة التحكم"],
                 ["SIMF_WEB_DataProtection__KeyRingPath", "الموقع"],
                 ["SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address", "حافة الجوال"],
                 ["SIMF_EDGE_ReverseProxy__KnownProxies__0", "حافة الجوال"]]),
            p("Two of the API's three are encryption keys, and they behave "
              "differently under change. The file-store key wraps a per-file "
              "key, so it has a rotation path in principle — but the job that "
              "finishes a rotation has never been built, so treat it as set "
              "once. The identity-document key has no such path at all: "
              "changing it strands every encrypted column permanently, with no "
              "way back. Escrow both, with the version number stamped alongside "
              "the file-store key, before the system carries any real data.",
              "اثنان من مفاتيح واجهة البرمجة الثلاثة مفتاحا تشفير، ويسلكان سلوكًا "
              "مختلفًا عند التغيير. فمفتاح مخزن الملفات يغلّف مفتاحًا لكل ملف، فله "
              "من حيث المبدأ مسار تدوير — لكن المهمة التي تُتِمّ التدوير لم تُبنَ "
              "قط، فعامِله على أنه يُضبط مرة واحدة. أما مفتاح وثائق الهوية فلا "
              "مسار له إطلاقًا: وتغييره يعزل كل عمود مشفَّر نهائيًا بلا رجعة. "
              "فاحفظ نسخة موثوقة من المفتاحين، مع رقم إصدار مفتاح مخزن الملفات، "
              "قبل أن يحمل النظام أي بيانات حقيقية."),
            code("# Generate a 32-byte key, base64-encoded:\n"
                 "openssl rand -base64 32\n\n"
                 "# The JWT signing key wants more:\n"
                 "openssl rand -base64 48"),
            {"t": "pagebreak"},
        ] + api_variable_blocks() + other_host_blocks(),
    }


def api_variable_blocks():
    R_EN, R_AR = REQUIRED_EN, REQUIRED_AR
    S_EN, S_AR = SECRET_EN, SECRET_AR
    return [
        h2("The API", "واجهة البرمجة"),
        p("The API reads by far the most configuration — the connection "
          "strings, every key, the mail server, the bootstrap administrator and "
          "the AI provider. Its script on a provisioned server carries 82 "
          "variables.",
          "تقرأ واجهة البرمجة أكبر قدر من التهيئة بفارق كبير — سلاسل الاتصال، "
          "وكل المفاتيح، وخادم البريد، والمسؤول الأول، ومزوّد الذكاء الاصطناعي. "
          "ويحمل نصّها على خادم مهيّأ 82 متغيّرًا."),

        h3("Databases", "قواعد البيانات"),
        env_table([
            ("SIMF_API_ConnectionStrings__SimfIdentityDb",
             "The accounts, roles and permissions database.",
             "قاعدة بيانات الحسابات والأدوار والصلاحيات.",
             "empty", "فارغ", R_EN, R_AR),
            ("SIMF_API_ConnectionStrings__SimfAppDb",
             "Everything else — profiles, programme, gates, meetings.",
             "كل ما عدا ذلك — الملفات والبرنامج والبوابات والاجتماعات.",
             "empty", "فارغ", R_EN, R_AR),
        ]),
        note("Both are required. The application throws on the first database "
             "access if either is missing — it does not start and then "
             "misbehave.",
             "كلاهما مطلوب. ويُخفق التطبيق عند أول وصول لقاعدة البيانات إذا غاب "
             "أحدهما — فهو لا يقلع ثم يسيء التصرف."),

        h3("Keys and tokens", "المفاتيح والرموز"),
        env_table([
            ("SIMF_API_Jwt__SigningKey",
             "Signs every access token. Also derives the one-time-code and "
             "meeting-link hashes, so changing it invalidates those too.",
             "يوقّع كل رمز وصول. ومنه تُشتق تجزئات الرموز لمرة واحدة وروابط "
             "الاجتماعات، فتغييره يُبطلها أيضًا.",
             "empty", "فارغ",
             "At least 32 bytes; 48 recommended", "32 بايت على الأقل، ويُفضَّل 48"),
            ("SIMF_API_Jwt__Issuer", "The token issuer name.",
             "اسم جهة إصدار الرمز.", "SIMF", "SIMF", "SIMF", "SIMF"),
            ("SIMF_API_Jwt__Audience", "The token audience name.",
             "اسم الجمهور المستهدف للرمز.", "SIMF", "SIMF", "SIMF", "SIMF"),
            ("SIMF_API_Jwt__AccessTokenMinutes",
             "How long an access token stays valid.",
             "مدة صلاحية رمز الوصول.", "5", "5", "5", "5"),
            ("SIMF_API_Jwt__SessionLifetimeHours",
             "How long a signed-in session may be refreshed for.",
             "المدة التي يمكن خلالها تجديد جلسة مسجّلة الدخول.",
             "24", "24", "24", "24"),
            ("SIMF_API_Storage__UserIdDocumentEncryptionKey",
             "Encrypts the national ID, Iqama, passport and mobile columns. "
             "Cannot be changed once data exists.",
             "يشفّر أعمدة الهوية الوطنية والإقامة وجواز السفر والجوال. ولا يمكن "
             "تغييره بعد وجود بيانات.",
             "empty", "فارغ", S_EN + " (32 bytes, base64)", S_AR + " (32 بايت، base64)"),
            ("SIMF_API_FileStorage__EncryptionKey",
             "Wraps the key of every encrypted stored file.",
             "يغلّف مفتاح كل ملف مخزَّن مشفَّر.",
             "empty", "فارغ", S_EN + " (32 bytes, base64)", S_AR + " (32 بايت، base64)"),
            ("SIMF_API_FileStorage__KekVersion",
             "The version stamp written into every encrypted file. On a restore "
             "the right key under the wrong version fails every read.",
             "ختم الإصدار المكتوب في كل ملف مشفَّر. وعند الاستعادة يُخفق كل قراءة "
             "إذا كان المفتاح صحيحًا والإصدار خاطئًا.",
             "1", "1", "1", "1"),
            ("SIMF_API_FileStorage__PreviousEncryptionKey",
             "The superseded key, during a rotation only.",
             "المفتاح السابق، أثناء التدوير فقط.",
             "empty", "فارغ", "Leave empty", "اتركه فارغًا"),
            ("SIMF_API_Ai__PromptHash__Secret",
             "Signs the AI prompt fingerprints. Without it the application "
             "falls back to a publicly derivable key.",
             "يوقّع بصمات محفّزات الذكاء الاصطناعي. وبدونه يرجع التطبيق إلى مفتاح "
             "يمكن اشتقاقه علنًا.",
             "empty", "فارغ", S_EN, S_AR),
        ]),

        h3("The first administrator", "أول مسؤول"),
        env_table([
            ("SIMF_API_SuperAdmin__Email", "The bootstrap administrator's address.",
             "بريد المسؤول الأول.",
             "superadmin@simrsnf.com", "superadmin@simrsnf.com",
             "The real address", "العنوان الحقيقي"),
            ("SIMF_API_SuperAdmin__TempPassword",
             "Its starting password. Must satisfy the password rules, including "
             "the no-sequential-run rule.",
             "كلمة مروره الأولى. ويجب أن تستوفي قواعد كلمات المرور، ومنها قاعدة "
             "منع التسلسل.",
             "empty", "فارغ", S_EN, S_AR),
            ("SIMF_API_SuperAdmin__TotpSecret",
             "Its authenticator secret. Without it the most privileged account "
             "in the system is protected by a password alone.",
             "سرّ تطبيق المصادقة الخاص به. وبدونه يكون أعلى حساب صلاحية في النظام "
             "محميًا بكلمة مرور وحدها.",
             "empty", "فارغ",
             S_EN + " (base32)", S_AR + " (base32)"),
            ("SIMF_API_SuperAdmin__PasswordChangeRequired",
             "Force a password change at the first sign-in.",
             "فرض تغيير كلمة المرور عند أول تسجيل دخول.",
             "true", "true", "true", "true"),
            ("SIMF_API_Seed__DemoPassword",
             "The shared password of the sample accounts.",
             "كلمة المرور المشتركة للحسابات التجريبية.",
             "empty", "فارغ",
             "Leave empty in production", "اتركه فارغًا في الإنتاج"),
            ("SIMF_API_Seed__EnableDemoAccounts",
             "Create the sample accounts outside development.",
             "إنشاء الحسابات التجريبية خارج بيئة التطوير.",
             "false", "false", "false", "false"),
        ]),
        note("The sample accounts must not exist on a production system. They "
             "are created only in development, or when the switch above is "
             "turned on deliberately.",
             "يجب ألا توجد الحسابات التجريبية على نظام إنتاجي. فهي لا تُنشأ إلا في "
             "بيئة التطوير، أو عند تفعيل المفتاح أعلاه عمدًا."),

        h3("Mail", "البريد"),
        env_table([
            ("SIMF_API_Email__Host", "The mail server.", "خادم البريد.",
             "empty", "فارغ", R_EN, R_AR),
            ("SIMF_API_Email__Port", "Its port.", "منفذه.", "587", "587", "587", "587"),
            ("SIMF_API_Email__User", "The mail account.", "حساب البريد.",
             "empty", "فارغ", R_EN, R_AR),
            ("SIMF_API_Email__Password", "Its password.", "كلمة مروره.",
             "empty", "فارغ", S_EN, S_AR),
            ("SIMF_API_Email__FromAddress", "The address mail is sent from.",
             "العنوان الذي تُرسل منه الرسائل.",
             "no-reply@ammn.com.sa", "no-reply@ammn.com.sa",
             "The verified sender", "المرسل الموثّق"),
            ("SIMF_API_Email__FromName", "The display name on sent mail.",
             "الاسم الظاهر على الرسائل المرسلة.", "SIMF", "SIMF", "SIMF", "SIMF"),
            ("SIMF_API_Email__FailureAlertRecipients",
             "Who is told when mail delivery starts failing.",
             "من يُبلَّغ عند بدء فشل تسليم البريد.",
             "empty", "فارغ",
             "An operations address", "عنوان فريق التشغيل"),
        ]),
        note("Mail failure is invisible from the interface. Sending is queued in "
             "the background, so a wrong password does not throw, does not "
             "appear on screen, and does not stop an account being created — "
             "the messages simply never arrive. Set the alert recipients, and "
             "test delivery after every change.",
             "فشل البريد غير مرئي من الواجهة. فالإرسال يجري في الخلفية عبر طابور، "
             "فكلمة مرور خاطئة لا تُطلق خطأً ولا تظهر على الشاشة ولا تمنع إنشاء "
             "حساب — بل لا تصل الرسائل فحسب. فاضبط عناوين التنبيه، واختبر التسليم "
             "بعد كل تغيير."),
        note("An EMPTY mail host is worse than a wrong one, and this was "
             "observed rather than reasoned about. With no host configured the "
             "first message to be sent fails in a way the retry logic does not "
             "catch, the background sender stops for good, and the health check "
             "reports the API as unhealthy from that moment — while the site "
             "keeps serving pages normally. If a load balancer is watching that "
             "endpoint it will take a working server out of rotation because "
             "nobody filled in an SMTP host.",
             "أما مضيف البريد الفارغ فأسوأ من الخاطئ، وهذا أمر لوحظ عمليًا لا "
             "استُنتج. فمع عدم تهيئة أي مضيف تفشل أول رسالة بطريقة لا يلتقطها "
             "منطق إعادة المحاولة، فيتوقف المرسل الخلفي نهائيًا، ويُبلغ فحص "
             "السلامة عن اعتلال واجهة البرمجة منذ تلك اللحظة — بينما يواصل الموقع "
             "تقديم صفحاته بشكل طبيعي. وإذا كان موزّع الأحمال يراقب تلك النقطة "
             "فسيُخرج خادمًا سليمًا من الخدمة لأن أحدًا لم يملأ مضيف SMTP."),

        h3("Network and limits", "الشبكة والحدود"),
        env_table([
            ("SIMF_API_ReverseProxy__KnownProxies__0",
             "The trusted proxy, so a visitor's real address is recovered.",
             "الوكيل الموثوق، لاستعادة عنوان الزائر الحقيقي.",
             "empty", "فارغ",
             "The proxy's IP ADDRESS", "عنوان IP للوكيل"),
            ("SIMF_API_Cors__WebAppOrigins__0",
             "A browser origin allowed to call the API. Numbered from zero.",
             "أصل متصفح مسموح له باستدعاء واجهة البرمجة. مرقّم من الصفر.",
             "empty", "فارغ",
             "Each public site address", "عنوان كل موقع عام"),
            ("SIMF_API_RateLimit__PermitLimit",
             "Requests allowed per address in the window.",
             "عدد الطلبات المسموح بها لكل عنوان في النافذة.",
             "20", "20", "20", "20"),
            ("SIMF_API_RateLimit__WindowSeconds", "The window, in seconds.",
             "النافذة بالثواني.", "60", "60", "60", "60"),
            ("SIMF_API_Swagger__AllowSwagger",
             "Publish the interactive API documentation in production.",
             "نشر وثائق واجهة البرمجة التفاعلية في الإنتاج.",
             "false", "false", "false", "false"),
            ("SIMF_API_FileStorage__RootPath",
             "Where uploaded files are written.",
             "المكان الذي تُكتب فيه الملفات المرفوعة.",
             "empty", "فارغ",
             "An explicit path that is backed up",
             "مسار صريح مشمول بالنسخ الاحتياطي"),
            ("SIMF_API_Storage__LogDirectory", "Where the log files are written.",
             "المكان الذي تُكتب فيه ملفات السجل.",
             "logs", "logs", "An explicit path", "مسار صريح"),
        ]),
        note("The trusted-proxy entry must be an IP address. A host name is "
             "read, fails to parse, and is dropped without a word — the setting "
             "then looks configured while every visitor shares one rate-limit "
             "bucket. If the file-store path is left empty the files land in a "
             "default location under ProgramData that nobody chose and that a "
             "backup may not cover.",
             "يجب أن يكون مدخل الوكيل الموثوق عنوان IP. أما اسم المضيف فيُقرأ "
             "ويفشل تحليله ويُسقَط دون أي إشعار — فيبدو الإعداد مضبوطًا بينما يتشارك "
             "كل الزوار حصة واحدة من حد المعدل. وإذا تُرك مسار مخزن الملفات فارغًا "
             "فستقع الملفات في موقع افتراضي ضمن ProgramData لم يختره أحد وقد لا "
             "يشمله النسخ الاحتياطي."),

        h3("Walk-in desk and offline badges", "مكتب الحضور والشارات دون اتصال"),
        p("These govern the on-site registration desk. Every switch ships off, "
          "so the desk behaves conservatively until somebody deliberately opens "
          "it up for an event day.",
          "تحكم هذه المفاتيح مكتب التسجيل في الموقع. وكلها مُشحونة مُعطّلة، فيتصرف "
          "المكتب بتحفّظ حتى يفتحه أحد عمدًا ليوم الفعالية."),
        env_table([
            ("SIMF_API_WalkInMode__Enabled", "The desk features as a whole.",
             "خصائص المكتب ككل.", "false", "false",
             "true on event days", "true في أيام الفعالية"),
            ("SIMF_API_WalkInMode__ExpiresAt",
             "When the desk switches itself off again.",
             "متى يُغلق المكتب نفسه من جديد.",
             "empty", "فارغ", "Blank means never", "الفراغ يعني بلا انتهاء"),
            ("SIMF_API_WalkInMode__QuickRegister", "The shortened desk form.",
             "نموذج المكتب المختصر.", "false", "false", "false", "false"),
            ("SIMF_API_WalkInMode__QuickRegisterRequiresIdentityDocument",
             "Whether the short form still demands an identity document.",
             "هل يظل النموذج المختصر يشترط وثيقة هوية.",
             "true", "true", "true", "true"),
            ("SIMF_API_WalkInMode__AutoApprove",
             "Approve a desk visitor immediately and print the badge. Never "
             "applies to partner accounts.",
             "اعتماد زائر المكتب فورًا وطباعة الشارة. ولا ينطبق أبدًا على حسابات "
             "الشركاء.",
             "false", "false", "Only on an event day", "في يوم الفعالية فقط"),
            ("SIMF_API_WalkInMode__SessionWalkIn",
             "Admission to a session at the door.",
             "الدخول إلى جلسة عند الباب.", "false", "false", "false", "false"),
            ("SIMF_API_WalkInMode__ArrivalGraceMinutes",
             "How late an arrival still counts. Capped at 240.",
             "إلى أي حد يظل الوصول المتأخر محتسبًا. والحد الأقصى 240.",
             "15", "15", "15", "15"),
            ("SIMF_API_WalkInMode__AcceptOfflineBadges",
             "Honour badges issued by a desk with no network.",
             "قبول الشارات الصادرة من مكتب بلا شبكة.",
             "false", "false", "false", "false"),
            ("SIMF_API_WalkInMode__OfflineUpload",
             "Accept a batch uploaded from an offline desk.",
             "قبول دفعة مرفوعة من مكتب غير متصل.",
             "false", "false", "false", "false"),
            ("SIMF_API_WalkInMode__AllowBadgeActivation",
             "Let a badge be activated at the desk.",
             "السماح بتفعيل شارة عند المكتب.", "false", "false", "false", "false"),
            ("SIMF_API_WalkInMode__BadgeKey",
             "Signs offline badges. Only meaningful when offline badges are "
             "accepted.",
             "يوقّع الشارات دون اتصال. ولا معنى له إلا عند قبول تلك الشارات.",
             "empty", "فارغ", SECRET_EN, SECRET_AR),
            ("SIMF_API_WalkInMode__BadgeKeyVersion", "Its version stamp.",
             "ختم إصداره.", "0", "0", "0", "0"),
            ("SIMF_API_WalkInMode__PreviousBadgeKey",
             "The superseded badge key, during a rotation only.",
             "مفتاح الشارة السابق، أثناء التدوير فقط.",
             "empty", "فارغ", "Leave empty", "اتركه فارغًا"),
            ("SIMF_API_WalkInMode__PreviousBadgeKeyVersion",
             "Its version stamp.", "ختم إصداره.", "0", "0", "0", "0"),
        ]),
        note("The offline badge desk carries its own copy of the badge key and "
             "its version. The two must match the API values exactly, or every "
             "badge the desk issues is refused at the gate.",
             "يحمل مكتب الشارات غير المتصل نسخته الخاصة من مفتاح الشارة وإصداره. "
             "ويجب أن يطابقا قيم واجهة البرمجة تمامًا، وإلا رُفضت عند البوابة كل "
             "شارة يصدرها المكتب."),

        h3("Account lifecycle and devices", "دورة حياة الحساب والأجهزة"),
        env_table([
            ("SIMF_API_IdentityLifecycle__RequireControlPanelTwoFactorEnrolment",
             "Force every Control Panel account through authenticator enrolment.",
             "إلزام كل حساب في لوحة التحكم بإقران تطبيق مصادقة.",
             "true", "true", "true", "true"),
            ("SIMF_API_IdentityLifecycle__PasswordMaxAgeDays",
             "Expire a password after this many days. Zero means never.",
             "انتهاء صلاحية كلمة المرور بعد هذا العدد من الأيام. والصفر يعني أبدًا.",
             "0", "0", "0", "0"),
            ("SIMF_API_IdentityLifecycle__PasswordHistoryCount",
             "How many previous passwords may not be reused. Zero disables it.",
             "عدد كلمات المرور السابقة التي لا يجوز إعادة استخدامها. والصفر يعطّلها.",
             "0", "0", "0", "0"),
            ("SIMF_API_IdentityLifecycle__DormantAccountDisableDays",
             "Disable an account unused for this long. Zero means never.",
             "تعطيل الحساب غير المستخدم هذه المدة. والصفر يعني أبدًا.",
             "0", "0", "0", "0"),
            ("SIMF_API_DeviceKey__RequireStepUpForEnrol",
             "Demand a second factor before a device key is enrolled.",
             "اشتراط عامل ثانٍ قبل تسجيل مفتاح جهاز.",
             "true", "true", "true", "true"),
            ("SIMF_API_DeviceKey__MaxActiveKeysPerUser",
             "How many devices one account may keep enrolled.",
             "عدد الأجهزة التي يمكن للحساب الواحد إبقاؤها مسجّلة.",
             "5", "5", "5", "5"),
            ("SIMF_API_Jwt__StreamAudience",
             "The audience of the short-lived token that authorises a video stream.",
             "جمهور الرمز قصير الأجل الذي يصرّح ببث فيديو.",
             "simf-stream", "simf-stream", "simf-stream", "simf-stream"),
            ("SIMF_API_Jwt__StreamTokenMinutes", "How long that token lasts.",
             "مدة صلاحية ذلك الرمز.", "180", "180", "180", "180"),
        ]),

        h3("Artificial intelligence", "الذكاء الاصطناعي"),
        p("The AI features answer with a harmless echo until a provider is "
          "configured, so an unconfigured deployment degrades quietly rather "
          "than failing.",
          "تجيب خصائص الذكاء الاصطناعي بصدى غير ضار حتى يُهيَّأ مزوّد، فتتراجع "
          "البيئة غير المهيّأة بهدوء بدل أن تفشل."),
        env_table([
            ("SIMF_API_Ai__DefaultProvider",
             "Which provider answers: Echo, Anthropic, Gemini or OpenAi.",
             "أي مزوّد يجيب: Echo أو Anthropic أو Gemini أو OpenAi.",
             "Echo", "Echo", "Anthropic", "Anthropic"),
            ("SIMF_API_Ai__Anthropic__ApiKey", "The Anthropic key.",
             "مفتاح Anthropic.", "empty", "فارغ", SECRET_EN, SECRET_AR),
            ("SIMF_API_Ai__Anthropic__DefaultModel", "The model used by default.",
             "النموذج المستخدم افتراضيًا.",
             "claude-haiku-4-5-20251001", "claude-haiku-4-5-20251001",
             "claude-haiku-4-5-20251001", "claude-haiku-4-5-20251001"),
            ("SIMF_API_Ai__Anthropic__BaseUrl", "The provider address.",
             "عنوان المزوّد.", "https://api.anthropic.com",
             "https://api.anthropic.com",
             "https://api.anthropic.com", "https://api.anthropic.com"),
            ("SIMF_API_Ai__Anthropic__DefaultMaxTokens",
             "The reply length ceiling.", "الحد الأقصى لطول الإجابة.",
             "2048", "2048", "2048", "2048"),
            ("SIMF_API_Ai__Gemini__ApiKey", "The Gemini key, if used.",
             "مفتاح Gemini إن استُخدم.", "empty", "فارغ",
             "Leave empty unless used", "اتركه فارغًا ما لم يُستخدم"),
            ("SIMF_API_Ai__OpenAi__ApiKey", "The OpenAI key, if used.",
             "مفتاح OpenAI إن استُخدم.", "empty", "فارغ",
             "Leave empty unless used", "اتركه فارغًا ما لم يُستخدم"),
        ]),

        h3("Uploads, media and links", "الرفع والوسائط والروابط"),
        env_table([
            ("SIMF_API_UploadScanning__Enabled",
             "Scan every upload before it is stored.",
             "فحص كل ملف مرفوع قبل تخزينه.", "true", "true", "true", "true"),
            ("SIMF_API_FaceDetection__Enabled",
             "Check that an identity photograph contains a face.",
             "التحقق من احتواء صورة الهوية على وجه.",
             "true", "true", "true", "true"),
            ("SIMF_API_FaceDetection__MinConfidence",
             "How sure that check must be.",
             "درجة اليقين المطلوبة في ذلك التحقق.", "0.5", "0.5", "0.5", "0.5"),
            ("SIMF_API_SessionRecordingStorage__MaxUploadBytes",
             "The largest session recording accepted (1 GB).",
             "أكبر تسجيل جلسة مقبول (1 جيجابايت).",
             "1073741824", "1073741824", "1073741824", "1073741824"),
            ("SIMF_API_OrganizationHeroVideo__MaxUploadBytes",
             "The largest banner video accepted (200 MB).",
             "أكبر فيديو واجهة مقبول (200 ميجابايت).",
             "209715200", "209715200", "209715200", "209715200"),
            ("SIMF_API_OrganizationHeroVideo__PublicApiBaseUrl",
             "The address that video is served from publicly.",
             "العنوان الذي يُقدَّم منه ذلك الفيديو علنًا.",
             "empty", "فارغ", "The edge address", "عنوان الحافة"),
            ("SIMF_API_MeetingLinks__PublicWebBaseUrl",
             "The site a meeting confirmation link points at. Without it the "
             "links in those emails go nowhere.",
             "الموقع الذي يشير إليه رابط تأكيد الاجتماع. وبدونه لا تؤدي روابط تلك "
             "الرسائل إلى شيء.",
             "empty", "فارغ", "https://web.simrsnf.com", "https://web.simrsnf.com"),
            ("SIMF_API_MeetingLinks__TokenTtlHours",
             "How long such a link stays valid.", "مدة صلاحية ذلك الرابط.",
             "72", "72", "72", "72"),
            ("SIMF_API_Swagger__Username",
             "Guards the interactive documentation when it is published.",
             "يحرس الوثائق التفاعلية عند نشرها.", "empty", "فارغ",
             "Required only if the documentation is published",
             "مطلوب فقط إذا نُشرت الوثائق"),
            ("SIMF_API_Swagger__Password", "Its password.", "كلمة مروره.",
             "empty", "فارغ", SECRET_EN, SECRET_AR),
            ("SIMF_API_Serilog__MinimumLevel__Default", "How much is logged.",
             "مقدار ما يُسجَّل.", "Information", "Information",
             "Information", "Information"),
            ("SIMF_API_AllowedHosts", "The host names the API answers for.",
             "أسماء المضيفين التي تستجيب لها واجهة البرمجة.", "*", "*",
             "api.simrsnf.com", "api.simrsnf.com"),
        ]),
        {"t": "pagebreak"},
    ]


def other_host_blocks():
    return [
        h2("The Control Panel", "لوحة التحكم"),
        p("The Control Panel holds no database of its own. It talks to the API, "
          "and it needs somewhere shared to keep the keys that protect its "
          "session cookies.",
          "لا تملك لوحة التحكم قاعدة بيانات خاصة بها. فهي تتحدث إلى واجهة البرمجة، "
          "وتحتاج إلى موضع مشترك تحفظ فيه المفاتيح التي تحمي ملفات تعريف ارتباط "
          "الجلسة."),
        env_table([
            ("SIMF_CP_Api__BaseUrl", "The API address, with a trailing slash.",
             "عنوان واجهة البرمجة، مع شرطة مائلة في آخره.",
             "http://localhost:5175/", "http://localhost:5175/",
             "https://api.simrsnf.com/", "https://api.simrsnf.com/"),
            ("SIMF_CP_DataProtection__KeyRingPath",
             "Where the cookie-protection keys live.",
             "موضع مفاتيح حماية ملفات تعريف الارتباط.",
             "empty", "فارغ", "A shared, backed-up path",
             "مسار مشترك مشمول بالنسخ الاحتياطي"),
            ("SIMF_CP_Session__LifetimeHours", "How long a session lasts.",
             "مدة بقاء الجلسة.", "8", "8", "8", "8"),
            ("SIMF_CP_Storage__LogDirectory", "Where its logs are written.",
             "موضع كتابة سجلاتها.", "logs", "logs",
             "An explicit path", "مسار صريح"),
            ("SIMF_CP_AllowedHosts", "The host names it will answer for.",
             "أسماء المضيفين التي تستجيب لها.", "*", "*",
             "cp.simrsnf.com", "cp.simrsnf.com"),
        ]),
        note("The Control Panel and the Website must be given the SAME API "
             "address and the SAME key-ring path. A build-time test fails if the "
             "two disagree.",
             "يجب أن تُمنح لوحة التحكم والموقع العنوان نفسه لواجهة البرمجة والمسار "
             "نفسه لحلقة المفاتيح. ويُخفق اختبار عند البناء إذا اختلفا."),

        h2("The Website", "الموقع"),
        env_table([
            ("SIMF_WEB_Api__BaseUrl", "The API address; must match the Control Panel's.",
             "عنوان واجهة البرمجة؛ ويجب أن يطابق عنوان لوحة التحكم.",
             "http://localhost:5175/", "http://localhost:5175/",
             "https://api.simrsnf.com/", "https://api.simrsnf.com/"),
            ("SIMF_WEB_DataProtection__KeyRingPath",
             "Its key ring; must match the Control Panel's path.",
             "حلقة مفاتيحه؛ ويجب أن تطابق مسار لوحة التحكم.",
             "empty", "فارغ", "The same shared path", "المسار المشترك نفسه"),
            ("SIMF_WEB_AllowedHosts", "The host names it will answer for.",
             "أسماء المضيفين التي يستجيب لها.", "*", "*",
             "web.simrsnf.com", "web.simrsnf.com"),
        ]),

        h2("The mobile edge", "حافة الجوال"),
        p("The edge is the only application the mobile app talks to. It is a "
          "reverse proxy in front of the API, and it exists so the API itself "
          "never has to be published to the internet.",
          "الحافة هي التطبيق الوحيد الذي يتحدث إليه تطبيق الجوال. وهي وكيل عكسي "
          "أمام واجهة البرمجة، ووُجدت كي لا يُضطر إلى نشر واجهة البرمجة نفسها على "
          "الإنترنت."),
        env_table([
            ("SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address",
             "Where the edge forwards to.", "الوجهة التي تحوّل إليها الحافة.",
             "empty", "فارغ", "https://api.simrsnf.com/", "https://api.simrsnf.com/"),
            ("SIMF_EDGE_ReverseProxy__KnownProxies__0",
             "The trusted hop in front of the edge.",
             "الوسيط الموثوق أمام الحافة.",
             "empty", "فارغ", "The load balancer's IP ADDRESS",
             "عنوان IP لموزّع الأحمال"),
            ("SIMF_EDGE_AllowedHosts", "The host names it will answer for.",
             "أسماء المضيفين التي تستجيب لها.", "*", "*",
             "edge.simrsnf.com", "edge.simrsnf.com"),
        ]),


        h2("The public web application", "تطبيق الويب العام"),
        p("The Flutter web bundle is different in kind: its settings are "
          "COMPILED INTO the build rather than read from the server, so changing "
          "one means rebuilding and redeploying the bundle, not setting a "
          "variable and restarting.",
          "حزمة الويب المبنية بـFlutter مختلفة في طبيعتها: فإعداداتها تُدمج داخل "
          "البناء ولا تُقرأ من الخادم، فتغيير أحدها يعني إعادة بناء الحزمة ونشرها، "
          "لا ضبط متغيّر وإعادة التشغيل."),
        env_table([
            ("ApiBase",
             "The address the application calls. Must be https and must end in "
             "the API version path; the build refuses otherwise.",
             "العنوان الذي يستدعيه التطبيق. ويجب أن يكون https وأن ينتهي بمسار "
             "إصدار واجهة البرمجة، وإلا رفض البناء.",
             "empty", "فارغ",
             "https://edge.simrsnf.com/api/v1", "https://edge.simrsnf.com/api/v1"),
            ("OutDir", "Where the built bundle is written.",
             "المكان الذي تُكتب فيه الحزمة المبنية.",
             "empty", "فارغ", "The site folder", "مجلد الموقع"),
            ("AppKey", "The application key, if the gate is in use.",
             "مفتاح التطبيق إن كانت البوابة مستخدمة.",
             "empty", "فارغ", SECRET_EN, SECRET_AR),
            ("SupportPhone", "The support number shown in the application.",
             "رقم الدعم الظاهر في التطبيق.", "empty", "فارغ",
             "The real number", "الرقم الحقيقي"),
            ("SupportEmail", "The support address.", "عنوان الدعم.",
             "empty", "فارغ", "The real address", "العنوان الحقيقي"),
            ("SocialX, SocialInstagram, SocialLinkedIn, SocialYouTube, SocialTikTok",
             "The social links in the footer. Any left empty is hidden.",
             "روابط التواصل في التذييل. ويُخفى ما يُترك منها فارغًا.",
             "empty", "فارغ", "The real profiles", "الحسابات الحقيقية"),
            ("VisitSaudiUrl", "The tourism link.", "رابط السياحة.",
             "https://www.visitsaudi.com", "https://www.visitsaudi.com",
             "https://www.visitsaudi.com", "https://www.visitsaudi.com"),
        ]),
        h2("Every host", "كل الخوادم"),
        env_table([
            ("ASPNETCORE_ENVIRONMENT",
             "Which environment the application believes it is in. Unprefixed, "
             "and identical on every server.",
             "البيئة التي يظن التطبيق أنه يعمل فيها. بلا بادئة، وموحّدة على كل "
             "الخوادم.",
             "not set", "غير مضبوط", "Production", "Production"),
        ]),
        note("Setting this to anything other than Production disables the boot "
             "gates, turns on the sample-data seeding, and publishes the "
             "interactive API documentation. It is the single most consequential "
             "value on the server.",
             "ضبط هذه القيمة على غير Production يعطّل بوابات الإقلاع، ويفعّل بذر "
             "البيانات التجريبية، وينشر وثائق واجهة البرمجة التفاعلية. وهي أكثر "
             "قيمة على الخادم أثرًا على الإطلاق."),
    ]


# --------------------------------------------------------------- appendix --

def chapter_observations():
    """Things found while writing this manual, recorded rather than fixed.

    Writing a manual against a running system finds defects, because it is the
    first time somebody reads every screen and compares it with what the code
    does. None of these were changed - the manual's job is to describe the
    system, not to alter it - but a finding nobody writes down is a finding
    made twice.
    """
    return {
        "id": "observations",
        "title": t("Appendix — what writing this manual found",
                   "ملحق — ما كشفه إعداد هذا الدليل"),
        "blocks": [
            p("This appendix records what was found while every page was opened "
              "and every constraint checked against the code. The first two "
              "groups were CORRECTED in the same change that produced this "
              "manual, and are kept here because the pattern matters more than "
              "any single line: a document and the code drift apart silently, "
              "and the drift is only visible to somebody reading both. The "
              "third group is behaviour, not error - nothing to fix, but worth "
              "knowing before it surprises somebody.",
              "يسجّل هذا الملحق ما وُجد أثناء فتح كل صفحة ومقارنة كل قيد بالشيفرة. "
              "وقد صُحّحت المجموعتان الأوليان في التغيير نفسه الذي أنتج هذا الدليل، "
              "وأُبقيتا هنا لأن النمط أهم من أي سطر بعينه: فالوثيقة والشيفرة "
              "تتباعدان بصمت، ولا يرى هذا التباعد إلا من يقرأ الاثنتين معًا. أما "
              "المجموعة الثالثة فسلوك لا خطأ — لا شيء يُصلَح فيها، لكنها جديرة "
              "بالمعرفة قبل أن تفاجئ أحدًا."),

            h2("Documents that disagreed with the code — corrected",
               "وثائق كانت تخالف الشيفرة — صُحّحت"),
            table(
                ["What a document says", "What the code does"],
                ["ما تقوله الوثيقة", "ما تفعله الشيفرة"],
                [
                    ["Thirteen page references described the page guard as a "
                     "role check on Administrator — because the TEMPLATE they "
                     "were copied from did, which is why one error appeared "
                     "thirteen times",
                     "Pages are guarded by a named permission, not by a role. "
                     "The template now says so, and says why"],
                    ["The administrator manual says the Edit action on the "
                     "administrators list is an unbuilt placeholder",
                     "Edit opens a working role editor and saves through a live "
                     "endpoint"],
                    ["The administrator manual gives the Control Panel address "
                     "as cp.simf.local",
                     "The repository's own index gives localhost:5158 for a "
                     "local run"],
                    ["The operations document describes one shared configuration "
                     "prefix",
                     "There have been four, one per application, since August 2026"],
                    ["The operations document lists two storage paths as required",
                     "Both were removed; setting them does nothing"],
                    ["The operations document gives the access-token lifetime as "
                     "30 minutes",
                     "It is 5"],
                    ["The deployment guide says the API configuration script "
                     "carries 62 values",
                     "It declares 82"],
                    ["The recovery procedure says the seeder re-applies the "
                     "configured authenticator secret on every start",
                     "It re-applies it only while two-factor is still switched "
                     "on for that account"],
                ],
                [
                    ["عدة مراجع صفحات تصف حارس الصفحة بأنه تحقق من دور المسؤول",
                     "الصفحات محروسة بصلاحية مسمّاة لا بدور"],
                    ["دليل المسؤول يقول إن إجراء التعديل في قائمة المسؤولين "
                     "عنصر نائب لم يُبنَ",
                     "التعديل يفتح محرّر أدوار عاملًا ويحفظ عبر خدمة حيّة"],
                    ["دليل المسؤول يعطي عنوان لوحة التحكم على أنه cp.simf.local",
                     "فهرس المستودع نفسه يعطي localhost:5158 للتشغيل المحلي"],
                    ["وثيقة التشغيل تصف بادئة تهيئة مشتركة واحدة",
                     "هناك أربع بادئات، واحدة لكل تطبيق، منذ أغسطس 2026"],
                    ["وثيقة التشغيل تُدرج مسارَي تخزين على أنهما مطلوبان",
                     "أُزيل كلاهما، وضبطهما لا يفعل شيئًا"],
                    ["وثيقة التشغيل تعطي عمر رمز الوصول بثلاثين دقيقة",
                     "هو خمس دقائق"],
                    ["دليل النشر يقول إن نص تهيئة واجهة البرمجة يحمل 62 قيمة",
                     "وهو يعلن 82"],
                    ["إجراء الاسترداد يقول إن البذّار يعيد تطبيق سرّ المصادقة "
                     "المهيّأ عند كل تشغيل",
                     "وهو يعيد تطبيقه فقط ما دامت المصادقة الثنائية مفعّلة لذلك "
                     "الحساب"],
                ]),

            h2("Wording in the product that no longer matched its behaviour — corrected",
               "صياغات في المنتج لم تعد تطابق سلوكه — صُحّحت"),
            bullets([
                "Saving a new visitor said an invitation had been sent. None is "
                "sent on that path — the wording predated the registration "
                "wizard that replaced it. It now says the visitor was registered.",
                "Saving a partner account showed the ADMINISTRATORS' message, "
                "which promises an invitation email that this path never "
                "queues. Partner accounts now have their own message, and it "
                "says the account is waiting for approval.",
                "The partner page's supporting line described a seven-day "
                "password invitation, which belongs to a create endpoint no "
                "page calls. It now describes what the page actually does.",
            ], [
                "كان حفظ زائر جديد يقول إن دعوة أُرسلت. ولا تُرسل أي دعوة في ذلك "
                "المسار — فالصياغة سبقت معالج التسجيل الذي حلّ محلها. وصار النص "
                "الآن يفيد بأن الزائر سُجّل.",
                "وكان حفظ حساب شريك يعرض رسالة المسؤولين، التي تَعِد ببريد دعوة "
                "لا يضعه هذا المسار في الطابور أصلًا. ولحسابات الشركاء الآن "
                "رسالتها الخاصة، وهي تفيد بأن الحساب ينتظر الموافقة.",
                "وكان السطر التوضيحي في صفحة الشركاء يصف دعوةً لتعيين كلمة مرور "
                "صالحة سبعة أيام، وهي تخص خدمة إنشاء لا تستدعيها أي صفحة. وصار "
                "يصف ما تفعله الصفحة فعلًا.",
            ]),

            h2("Behaviour worth knowing before it surprises somebody",
               "سلوك يجدر معرفته قبل أن يفاجئ أحدًا"),
            bullets([
                "With no mail host configured, the background sender stops for "
                "good the first time it is asked to send, and the health check "
                "reports the whole API as unhealthy from then on — while every "
                "page keeps working. Seen during the writing of this manual, "
                "after creating an account triggered its first notification.",
                "On a fresh installation the organisation logo is absent, so its "
                "page requests an image that is not there. The page is unharmed; "
                "the logo simply has not been uploaded yet.",
                "The registration desk accepts a passport format that the mobile "
                "application would reject, because only the desk skips the format "
                "check.",
                "The Arabic and English name fields accept 128 characters in the "
                "browser and are refused above 50 by the server.",
                "Walking quickly from page to page exhausts the per-address "
                "request allowance, because every page fetches the signed-in "
                "profile for its header. Nothing breaks; the header request is "
                "refused until the window rolls over.",
            ], [
                "مع عدم تهيئة مضيف بريد، يتوقف المرسل الخلفي نهائيًا عند أول "
                "طلب إرسال، ويُبلغ فحص السلامة عن اعتلال واجهة البرمجة كلها منذ "
                "تلك اللحظة — بينما تواصل كل الصفحات عملها. لوحظ ذلك أثناء إعداد "
                "هذا الدليل، بعد أن أطلق إنشاءُ حساب أولَ إشعار له.",
                "في التثبيت الجديد لا يوجد شعار للجهة، فتطلب صفحته صورة غير "
                "موجودة. ولا ضرر على الصفحة، فالشعار لم يُرفع بعد فحسب.",
                "يقبل مكتب التسجيل صيغة جواز سفر كان تطبيق الجوال ليرفضها، لأن "
                "المكتب وحده هو الذي يتخطى التحقق من الصيغة.",
                "يقبل حقلا الاسم العربي والإنجليزي 128 حرفًا في المتصفح ويرفضهما "
                "الخادم فوق 50.",
                "يستنفد التنقل السريع بين الصفحات حصة الطلبات لكل عنوان، لأن كل "
                "صفحة تجلب ملف المستخدم المسجّل لترويستها. ولا ينكسر شيء، إذ "
                "يُرفض طلب الترويسة حتى تتجدد النافذة.",
            ]),
        ],
    }
