/// Renders a country's flag emoji from its ISO 3166-1 **numeric** code.
///
/// The backend carries a speaker's nationality as the ISO 3166-1 numeric code
/// (`PublicSessionSpeaker.CountryId`); the KSA-Project Figma frames (e.g.
/// session detail 889:2450 "المتحدثون") show the matching flag (🇸🇦) beside
/// the name. A flag emoji is the two **regional indicator symbols** for the
/// country's ISO 3166-1 alpha-2 code, so the render is a two-step lookup:
/// numeric → alpha-2 → the regional-indicator pair. [countryFlagEmoji] returns
/// `null` for a null / unknown code so callers simply omit the flag (never a
/// tofu box or a wrong flag).
library;

/// The flag emoji for [numericCode] (ISO 3166-1 numeric), or `null` when the
/// code is null or not a recognised country. Pure — no allocation beyond the
/// two-character result string.
String? countryFlagEmoji(int? numericCode) {
  if (numericCode == null) {
    return null;
  }
  final alpha2 = _iso3166NumericToAlpha2[numericCode];
  if (alpha2 == null) {
    return null;
  }
  return _alpha2ToFlag(alpha2);
}

/// Turns a two-letter ISO 3166-1 alpha-2 code (e.g. `SA`) into its flag emoji
/// by mapping each ASCII letter to its regional-indicator symbol (U+1F1E6 is
/// the indicator for `A`, offset 0x1F1E6 - 'A'). Returns the literal code in
/// the (unreachable, for our static table) case of a malformed entry.
String _alpha2ToFlag(String alpha2) {
  const base = 0x1F1E6; // 🇦 — regional indicator symbol letter A
  const a = 0x41; // 'A'
  final codeUnits = alpha2.codeUnits;
  if (codeUnits.length != 2) {
    return alpha2;
  }
  final first = codeUnits[0];
  final second = codeUnits[1];
  if (first < a || first > a + 25 || second < a || second > a + 25) {
    return alpha2;
  }
  return String.fromCharCodes(<int>[base + (first - a), base + (second - a)]);
}

/// ISO 3166-1 numeric → alpha-2. The officially assigned set (the same codes
/// the CP country lookup seeds). Stable reference data — additions to ISO are
/// rare; an unmapped code simply yields no flag.
const Map<int, String> _iso3166NumericToAlpha2 = <int, String>{
  4: 'AF', 8: 'AL', 10: 'AQ', 12: 'DZ', 16: 'AS', 20: 'AD', 24: 'AO', 28: 'AG',
  31: 'AZ', 32: 'AR', 36: 'AU', 40: 'AT', 44: 'BS', 48: 'BH', 50: 'BD',
  51: 'AM', 52: 'BB', 56: 'BE', 60: 'BM', 64: 'BT', 68: 'BO', 70: 'BA',
  72: 'BW', 74: 'BV', 76: 'BR', 84: 'BZ', 86: 'IO', 90: 'SB', 92: 'VG',
  96: 'BN', 100: 'BG', 104: 'MM', 108: 'BI', 112: 'BY', 116: 'KH', 120: 'CM',
  124: 'CA', 132: 'CV', 136: 'KY', 140: 'CF', 144: 'LK', 148: 'TD', 152: 'CL',
  156: 'CN', 158: 'TW', 162: 'CX', 166: 'CC', 170: 'CO', 174: 'KM', 175: 'YT',
  178: 'CG', 180: 'CD', 184: 'CK', 188: 'CR', 191: 'HR', 192: 'CU', 196: 'CY',
  203: 'CZ', 204: 'BJ', 208: 'DK', 212: 'DM', 214: 'DO', 218: 'EC', 222: 'SV',
  226: 'GQ', 231: 'ET', 232: 'ER', 233: 'EE', 234: 'FO', 238: 'FK', 242: 'FJ',
  246: 'FI', 248: 'AX', 250: 'FR', 254: 'GF', 258: 'PF', 260: 'TF', 262: 'DJ',
  266: 'GA', 268: 'GE', 270: 'GM', 275: 'PS', 276: 'DE', 288: 'GH', 292: 'GI',
  296: 'KI', 300: 'GR', 304: 'GL', 308: 'GD', 312: 'GP', 316: 'GU', 320: 'GT',
  324: 'GN', 328: 'GY', 332: 'HT', 334: 'HM', 336: 'VA', 340: 'HN', 344: 'HK',
  348: 'HU', 352: 'IS', 356: 'IN', 360: 'ID', 364: 'IR', 368: 'IQ', 372: 'IE',
  376: 'IL', 380: 'IT', 384: 'CI', 388: 'JM', 392: 'JP', 398: 'KZ', 400: 'JO',
  404: 'KE', 408: 'KP', 410: 'KR', 414: 'KW', 417: 'KG', 418: 'LA', 422: 'LB',
  426: 'LS', 428: 'LV', 430: 'LR', 434: 'LY', 438: 'LI', 440: 'LT', 442: 'LU',
  446: 'MO', 450: 'MG', 454: 'MW', 458: 'MY', 462: 'MV', 466: 'ML', 470: 'MT',
  474: 'MQ', 478: 'MR', 480: 'MU', 484: 'MX', 492: 'MC', 496: 'MN', 498: 'MD',
  499: 'ME', 500: 'MS', 504: 'MA', 508: 'MZ', 512: 'OM', 516: 'NA', 520: 'NR',
  524: 'NP', 528: 'NL', 531: 'CW', 533: 'AW', 534: 'SX', 535: 'BQ', 540: 'NC',
  548: 'VU', 554: 'NZ', 558: 'NI', 562: 'NE', 566: 'NG', 570: 'NU', 574: 'NF',
  580: 'MP', 581: 'UM', 583: 'FM', 584: 'MH', 585: 'PW', 586: 'PK', 591: 'PA',
  598: 'PG', 600: 'PY', 604: 'PE', 608: 'PH', 612: 'PN', 616: 'PL', 620: 'PT',
  624: 'GW', 626: 'TL', 630: 'PR', 634: 'QA', 638: 'RE', 642: 'RO', 643: 'RU',
  646: 'RW', 652: 'BL', 654: 'SH', 659: 'KN', 660: 'AI', 662: 'LC', 663: 'MF',
  666: 'PM', 670: 'VC', 674: 'SM', 678: 'ST', 682: 'SA', 686: 'SN', 688: 'RS',
  690: 'SC', 694: 'SL', 702: 'SG', 703: 'SK', 704: 'VN', 705: 'SI', 706: 'SO',
  710: 'ZA', 716: 'ZW', 724: 'ES', 728: 'SS', 729: 'SD', 732: 'EH', 740: 'SR',
  744: 'SJ', 748: 'SZ', 752: 'SE', 756: 'CH', 760: 'SY', 762: 'TJ', 764: 'TH',
  768: 'TG', 772: 'TK', 776: 'TO', 780: 'TT', 784: 'AE', 788: 'TN', 792: 'TR',
  795: 'TM', 796: 'TC', 798: 'TV', 800: 'UG', 804: 'UA', 807: 'MK', 818: 'EG',
  826: 'GB', 831: 'GG', 832: 'JE', 833: 'IM', 834: 'TZ', 840: 'US', 850: 'VI',
  854: 'BF', 858: 'UY', 860: 'UZ', 862: 'VE', 876: 'WF', 882: 'WS', 887: 'YE',
  894: 'ZM',
};
