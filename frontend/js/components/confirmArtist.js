import * as styles from "../styles.js"
import * as Modal from "./modal.js";
import * as mediaIcons from "./common/mediaIcons.js";
import { createFlagElement } from "./common/countryFlag.js";

// Lets the user pick one of several MusicBrainz artists sharing a name (e.g. "Prince").
// Artists are as returned by musicbrainz.findArtists().
//
//   const artist = await confirmArtist.choose(artists, "Prince");    // the chosen artist, or null if skipped

function describeArtist(artist) {
    return {
        heading: artist.name?.trim() || "Name missing",
        typeName: artist.type?.trim() ?? "",
        note: artist.disambiguation?.trim() ?? ""
    };
}

// number is the option's position in the list, starting at 1
function createArtistButton(artist, number, onChosen) {
    const { heading, typeName, note } = describeArtist(artist);

    const button = document.createElement("button");
    button.type = "button";
    button.className = `w-full text-left px-4 py-3 rounded-lg border
                        flex items-center gap-3
                        border-slate-200 dark:border-slate-700
                        hover:bg-slate-100 dark:hover:bg-slate-700
                        focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500
                        transition-colors`;

    // Number on the left, then the text, and the type icon (if any) on the right spanning all lines
    const numberElement = document.createElement("span");
    numberElement.className = "shrink-0 min-w-6 text-right font-medium tabular-nums text-slate-400 dark:text-slate-500";
    numberElement.textContent = `${number}.`;
    button.append(numberElement);

    const text = document.createElement("div");
    text.className = "min-w-0 flex-1";
    button.append(text);

    const icon = mediaIcons.forCreatorType(typeName);
    if (icon) {
        const iconElement = document.createElement("div");
        iconElement.className = "shrink-0 text-slate-400 dark:text-slate-500";
        iconElement.title = typeName;
        iconElement.setAttribute("role", "img");
        iconElement.setAttribute("aria-label", typeName);
        iconElement.innerHTML = mediaIcons.svg(icon, "h-10 w-10");
        button.append(iconElement);
    }

    // The flag goes before the name. The type is shown only by the icon (and its tooltip).
    const headingElement = document.createElement("div");
    headingElement.className = "font-medium";
    const flagElement = createFlagElement(artist.country);
    if (flagElement)
        headingElement.append(flagElement);
    headingElement.append(heading);
    text.append(headingElement);

    if (note) {
        const noteElement = document.createElement("div");
        noteElement.className = "text-sm text-slate-500 dark:text-slate-400";
        noteElement.append("Details: ");

        const noteText = document.createElement("span");
        noteText.className = "italic";
        noteText.textContent = note;
        noteElement.append(noteText);

        text.append(noteElement);
    }

    button.addEventListener("click", () => onChosen(artist));
    return button;
}

export function choose(artists, name) {
    return new Promise(resolve => {
        let resolved = false;
        function resolveOnce(value) {
            if (resolved)
                return;
            resolved = true;
            resolve(value);
        }

        const root = document.createElement("div");
        root.className = "flex flex-col gap-4";

        const intro = document.createElement("p");
        intro.className = "text-slate-600 dark:text-slate-300";
        intro.textContent = `MusicBrainz has ${artists.length} artists called "${name}". Which one do you mean?`;
        root.append(intro);

        const list = document.createElement("div");
        list.className = "flex flex-col gap-2 max-h-[50vh] overflow-y-auto";
        root.append(list);

        let modal = null;
        for (const [index, artist] of artists.entries()) {
            list.append(createArtistButton(artist, index + 1, chosen => {
                resolveOnce(chosen);
                modal.close();
            }));
        }

        modal = Modal.show({
            title: "Choose artist",
            content: root,
            maxWidthClass: "max-w-xl",
            buttons: [
                {
                    "text": "Skip",
                    "className": styles.buttonSecondary,
                    "action": () => {
                        resolveOnce(null);
                        return true;
                    }
                }
            ],
            showCloseIcon: false,
            closeOnClickOutside: false,
            closeOnEscape: false
        });

        list.querySelector("button")?.focus();
    });
}
