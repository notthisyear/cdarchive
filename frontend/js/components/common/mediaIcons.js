// 24x24 line icons drawn in currentColor, one per release format and creator type. Used by the
// release picker (confirmRelease.js), the format dropdown in the music form and the type dropdown
// in the artist form.
//
//   element.innerHTML = mediaIcons.svg("vinyl", "h-10 w-10");
//   const name = mediaIcons.forFormat("Cd");          // "cd", or null when there's no icon
//   const name = mediaIcons.forCreatorType("Group");  // "group", or null when there's no icon

const ICONS = {
    cd: `<circle cx="12" cy="12" r="10"/>
         <circle cx="12" cy="12" r="3"/>
         <path d="M12 5.5a6.5 6.5 0 0 1 6.5 6.5"/>`,
    vinyl: `<circle cx="12" cy="12" r="10"/>
            <circle cx="12" cy="12" r="7.5" stroke-opacity="0.5"/>
            <circle cx="12" cy="12" r="5.5" stroke-opacity="0.5"/>
            <circle cx="12" cy="12" r="3" fill="currentColor"/>`,
    cassette: `<rect x="2" y="5" width="20" height="14" rx="2"/>
               <circle cx="8" cy="11" r="2"/>
               <circle cx="16" cy="11" r="2"/>
               <path d="M10 11h4"/>
               <path d="M6 19l2-4h8l2 4"/>`,
    // An audio file: a page with a folded corner and a music note
    digital: `<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
              <path d="M14 2v6h6"/>
              <path d="M11 17.5V11l4-1v6"/>
              <circle cx="9.5" cy="17.5" r="1.5"/>
              <circle cx="13.5" cy="16" r="1.5"/>`,
    // A question mark in a circle, for formats we have no icon for
    other: `<circle cx="12" cy="12" r="10"/>
            <path d="M9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3"/>
            <path d="M12 17h.01"/>`,
    // Head and shoulders of one person
    person: `<circle cx="12" cy="8" r="4"/>
             <path d="M4 21v-1a6 6 0 0 1 6-6h4a6 6 0 0 1 6 6v1"/>`,
    // One person in front of two others
    group: `<circle cx="12" cy="8" r="3"/>
            <path d="M6.5 20v-1a4 4 0 0 1 4-4h3a4 4 0 0 1 4 4v1"/>
            <circle cx="5.5" cy="9.5" r="2.5"/>
            <path d="M1.5 18v-.5A3.5 3.5 0 0 1 5 14h1"/>
            <circle cx="18.5" cy="9.5" r="2.5"/>
            <path d="M22.5 18v-.5A3.5 3.5 0 0 0 19 14h-1"/>`
};

// Our ReleaseFormat names (as from musicbrainz.toReleaseFormat()) to icon names.
// "Unknown" has no icon.
const FORMAT_ICONS = {
    Cd: "cd",
    Vinyl: "vinyl",
    Cassette: "cassette",
    Digital: "digital",
    Other: "other"
};

export function forFormat(format) {
    return FORMAT_ICONS[format] ?? null;
}

// Our CreatorType names to icon names
const CREATOR_TYPE_ICONS = {
    Person: "person",
    Group: "group",
    Other: "other"
};

export function forCreatorType(type) {
    return CREATOR_TYPE_ICONS[type] ?? null;
}

// The icon as an <svg> string. The markup is our own constant, so it's safe for innerHTML.
export function svg(name, classes = "") {
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" class="${classes}"
                 fill="none" stroke="currentColor" stroke-width="1.5"
                 stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                ${ICONS[name]}
            </svg>`;
}
