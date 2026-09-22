import type { Messages } from './en';

/**
 * Persian (فارسی) messages.
 *
 * Typed as {@link Messages}, so this file cannot drift from the English source: adding a key there
 * and forgetting it here fails the type check. Latin product names (Jev, TypeSafe, .NET, API) are
 * kept in Latin script, which is how they are written in Persian technical prose.
 */
export const fa: Messages = {
  app: {
    name: 'JevTicketRouter',
    tagline: 'تفکیک ساختاریافتهٔ تیکت با TypeSafe Jev',
    footer: 'فراخوانی‌های ساختاریافته با Jev انجام می‌شود؛ تصمیم نهایی با قوانین قطعی .NET است.',
    apiDocs: 'مستندات API',
  },

  header: {
    github: 'مشاهدهٔ کد روی GitHub',
    language: 'زبان',
    modeLive: 'Jev زنده',
    modeSelfHosted: 'خودمیزبان',
    modeLocal: 'هوش مصنوعی محلی',
    modeMock: 'حالت آزمایشی',
    modeUnknown: 'API در دسترس نیست',
    modeLiveHint: 'فراخوانی API سرویس TypeSafe با مدل {{model}}.',
    modeSelfHostedHint:
      'مدلی از نوع System One که روی سخت‌افزار خودتان اجرا می‌شود و به همان سؤال‌های تایپ‌شدهٔ API ابری ' +
      'پاسخ می‌دهد. هیچ درخواستی از سازمان خارج نمی‌شود.',
    modeLocalHint:
      'تصمیم‌ها را مدلی درون شبکهٔ خودتان می‌گیرد. هیچ درخواستی از سازمان خارج نمی‌شود.',
    modeMockHint:
      'هیچ ارائه‌دهندهٔ هوش مصنوعی تنظیم نشده است، بنابراین پاسخ‌های نمونهٔ قطعی برگردانده می‌شود.',
    modeUnknownHint: 'ارتباط با API برقرار نشد، بنابراین ارائه‌دهندهٔ آن نامشخص است.',
  },

  form: {
    heading: 'ثبت تیکت',
    subheading: 'فارسی یا انگلیسی — هر دو یکسان پردازش می‌شوند.',
    title: 'عنوان',
    titlePlaceholder: 'خلاصهٔ کوتاهی از مشکل',
    description: 'شرح',
    descriptionPlaceholder: 'چه اتفاقی افتاد، چه انتظاری داشتید، و چه کارهایی را امتحان کرده‌اید',
    descriptionHint: 'از وارد کردن رمز، شماره حساب یا اطلاعات محرمانهٔ واقعی خودداری کنید.',
    requesterRole: 'نقش درخواست‌کننده',
    submit: 'تحلیل تیکت',
    submitting: 'در حال تحلیل…',
    reset: 'پاک کردن فرم',
    samplesDivider: 'یا یک نمونه را امتحان کنید',
  },

  roles: {
    BranchEmployee: 'کارمند شعبه',
    Customer: 'مشتری',
    InternalSupport: 'پشتیبانی داخلی',
  },

  demos: {
    'persian-technical': {
      label: 'فارسی · مشکل فنی',
      hint: 'گزارش یک باجه‌دار شعبه دربارهٔ خطای نرم‌افزار، به فارسی.',
    },
    'english-access': {
      label: 'انگلیسی · درخواست دسترسی',
      hint: 'یک درخواست دسترسی روتین و از پیش تأییدشده.',
    },
    'security-review': {
      label: 'امنیتی · نیازمند بازبینی',
      hint: 'گزارش فیشینگ که قوانین آن را ارجاع می‌دهند و متنش را حذف می‌کنند.',
    },
  },

  result: {
    heading: 'تصمیم مسیریابی',
    ticketId: 'تیکت {{id}}',
    escalated: 'در انتظار بازبینی انسانی',
    autoRouted: 'آمادهٔ مسیریابی خودکار',
    summaryAuto: 'به‌صورت خودکار به {{team}} با اولویت {{priority}} ارجاع شد.',
    summaryEscalated:
      'برای {{team}} با اولویت {{priority}} در صف قرار گرفت و تا بازبینی انسانی نگه داشته شد.',
    category: 'دسته',
    targetTeam: 'تیم مقصد',
    priority: 'اولویت',
    sensitiveData: 'دادهٔ حساس',
    humanReview: 'بازبینی انسانی',
    sensitiveDetected: 'شناسایی شد',
    sensitiveNone: 'شناسایی نشد',
    reviewRequired: 'لازم است',
    reviewNotRequired: 'لازم نیست',
    confidenceHeading: 'میزان اطمینان Jev',
    sensitiveWarning:
      'دادهٔ حساس شناسایی شد. متن تیکت از لاگ‌های ساختاریافته و از بخش جزئیات فنی پایین حذف شده است.',
    loadingHeading: 'در حال تحلیل تیکت…',
    loadingSubheading: 'پرسیدن پنج سؤال از Jev در قالب یک فراخوانی واحد',
    loadingAria: 'در حال تحلیل تیکت',
    errorHeading: 'تحلیل ناموفق بود',
    errorTitle: 'تحلیل این تیکت ممکن نشد',
    emptyHeading: 'هنوز تیکتی تحلیل نشده است',
    emptyBody:
      'یک تیکت ثبت کنید یا یکی از نمونه‌ها را انتخاب کنید. Jev در یک فراخوانی به پنج سؤال مشخص پاسخ می‌دهد و سپس قوانین قطعی .NET مسیر نهایی را تعیین می‌کنند.',
  },

  decision: {
    byJev: 'Jev',
    byRule: 'قانون',
    jevHint: 'این مقدار دقیقاً همان چیزی است که Jev برگردانده است.',
    ruleOverrodeHint:
      'یک قانون کسب‌وکار این مقدار را {{value}} کرد. پیشنهاد Jev {{modelValue}} بود.',
    ruleConfirmedHint:
      'یک قانون کسب‌وکار این مقدار را تأیید کرد. پیشنهاد Jev هم {{modelValue}} بود.',
  },

  confidence: {
    notReported: 'گزارش نشده',
    belowThreshold: '{{percent}} · زیر آستانه',
    noneAria: '{{label}}: میزان اطمینان گزارش نشده است',
    noulHint: 'پاسخ‌های بله/خیر (noul) فقط احتمال برمی‌گردانند و میزان اطمینان ندارند.',
    meterHint: 'میزان اطمینان Jev برابر {{percent}} است. آستانهٔ ارجاع {{threshold}} است.',
    meterAria: 'میزان اطمینان {{label}}',
  },

  devDetails: {
    heading: 'جزئیات فنی',
    redactedBanner:
      'این تیکت به‌عنوان حاوی دادهٔ حساس علامت خورد، بنابراین سرور پیش از ارسال پاسخ، متن تیکت را حذف کرد. شرح خام در اینجا در دسترس نیست.',
    jevResponse: 'پاسخ ساختاریافتهٔ Jev (پاک‌سازی‌شده)',
    provider: 'ارائه‌دهنده: {{value}}',
    model: 'مدل: {{value}}',
    latency: 'زمان پاسخ: {{value}} میلی‌ثانیه',
    rulesApplied: 'قوانین قطعی اعمال‌شده ({{count}})',
    noRules: 'هیچ قانونی پیشنهاد Jev را تغییر نداد. مدل مطمئن بود و چیزی نیاز به ارجاع نداشت.',
    finalResponse: 'پاسخ نهایی API',
  },

  categories: {
    TechnicalIssue: 'مشکل فنی',
    ServiceInquiry: 'استعلام خدمات',
    AccessRequest: 'درخواست دسترسی',
    SecurityConcern: 'موضوع امنیتی',
    GeneralQuestion: 'پرسش عمومی',
  },

  teams: {
    ApplicationSupport: 'پشتیبانی نرم‌افزار',
    Infrastructure: 'زیرساخت',
    IdentityAccess: 'هویت و دسترسی',
    Security: 'امنیت',
    BusinessOperations: 'عملیات کسب‌وکار',
  },

  priorities: {
    Low: 'کم',
    Medium: 'متوسط',
    High: 'زیاد',
    Critical: 'بحرانی',
  },

  rules: {
    SECURITY_OR_CRITICAL_ESCALATION:
      'موضوعات امنیتی و تیکت‌های بحرانی همیشه نیازمند بازبینی انسانی هستند.',
    LOW_CONFIDENCE_ESCALATION:
      'اطمینان Jev کمتر از آستانه در هر یک از فیلدهای مسیریابی، بازبینی انسانی را الزامی می‌کند.',
    SENSITIVE_DATA_REDACTION: 'شرح تیکت‌هایی که حاوی دادهٔ حساس تشخیص داده می‌شوند حذف می‌شود.',
    MODEL_REQUESTED_REVIEW: 'Jev احتمال بالایی برآورد کرد که این تیکت به بازبینی انسانی نیاز دارد.',
  },

  validation: {
    titleMin: 'عنوان باید حداقل {{count}} نویسه باشد.',
    titleMax: 'عنوان باید حداکثر {{count}} نویسه باشد.',
    descriptionMin: 'شرح باید حداقل {{count}} نویسه باشد.',
    descriptionMax: 'شرح باید حداکثر {{count}} نویسه باشد.',
    roleInvalid: 'یک نقش درخواست‌کننده انتخاب کنید.',
  },

  toast: {
    routed: 'تحلیل شد. به {{team}} ارجاع داده شد.',
    needsReview: 'تحلیل شد. این تیکت به بازبینی انسانی نیاز دارد.',
    failed: 'تحلیل ناموفق بود. جزئیات را در سمت دیگر ببینید.',
  },

  errors: {
    unreachable:
      'ارتباط با API تحلیل برقرار نشد. مطمئن شوید بک‌اند در حال اجراست و دوباره تلاش کنید.',
    unexpected: 'API تحلیل خطای غیرمنتظره‌ای برگرداند ({{status}}).',
  },
};
