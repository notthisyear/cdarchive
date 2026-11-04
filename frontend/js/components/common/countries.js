// The countries an artist can be from, as { code, name } sorted by name. Codes are ISO 3166-1
// alpha-2 (as MusicBrainz uses them), plus MusicBrainz's own XE (Europe) and XW (Worldwide).
// Names come from the browser in English.
//
//   const sweden = countries.byCode("se");         // { code: "SE", name: "Sweden" }, or null
//   const matches = countries.startingWith("sw");  // Sweden, Switzerland

const CODES = `
    AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS
    BT BV BW BY BZ CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE
    EG EH ER ES ET FI FJ FK FM FO FR GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM
    HN HR HT HU ID IE IL IM IN IO IQ IR IS IT JE JM JO JP KE KG KH KI KM KN KP KR KW KY KZ LA LB LC
    LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR MS MT MU MV MW MX MY MZ NA
    NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW
    SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TF TG TH TJ TK TL TM TN TO
    TR TT TV TW TZ UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS XK YE YT ZA ZM ZW
`.trim().split(/\s+/);

const SPECIAL_NAMES = {
    XE: "Europe",
    XW: "Worldwide"
};

const displayNames = new Intl.DisplayNames(["en"], { type: "region" });

export const ALL = [...CODES, ...Object.keys(SPECIAL_NAMES)]
    .map(code => ({ code, name: SPECIAL_NAMES[code] ?? displayNames.of(code) ?? code }))
    .sort((a, b) => a.name.localeCompare(b.name, "en"));

const BY_CODE = new Map(ALL.map(country => [country.code, country]));

export function byCode(code) {
    return BY_CODE.get(code?.trim().toUpperCase() ?? "") ?? null;
}

// The country whose name is exactly this (ignoring case), or null
export function byName(name) {
    const wanted = name?.trim().toLowerCase() ?? "";
    return ALL.find(country => country.name.toLowerCase() === wanted) ?? null;
}

// The countries whose name starts with the text (ignoring case). All of them for empty text.
export function startingWith(text) {
    const prefix = text?.trim().toLowerCase() ?? "";
    return ALL.filter(country => country.name.toLowerCase().startsWith(prefix));
}
