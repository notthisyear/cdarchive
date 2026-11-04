import * as styles from "../styles.js"
import * as Modal from "./modal.js";
import * as musicbrainz from "../api/musicbrainz.js";
import * as mediaIcons from "./common/mediaIcons.js";
import { createFlagElement } from "./common/countryFlag.js";

// Lets the user pick one of several MusicBrainz releases of the same album (typically the CD,
// LP and digital release). Releases are as returned by musicbrainz.findReleases().
//
//   const release = await confirmRelease.choose(releases);    // the chosen release, or null if skipped

function valueOrMissing(value, fieldName) {
    const trimmed = value?.trim() ?? "";
    return trimmed !== "" ? trimmed : `${fieldName} missing`;
}

// Which icon a MusicBrainz media type gets ("CD", '12" Vinyl', "Cassette"...), and for vinyl
// its size as a caption. Uses the same mapping as the format dropdown in the music form.
// Null for "Other" and "Unknown", which are then written out as text instead.
function mediaIconFor(mediaType) {
    switch (musicbrainz.toReleaseFormat(mediaType)) {
        case "Cd":
            return { icon: "cd", caption: "" };
        case "Cassette":
            return { icon: "cassette", caption: "" };
        case "Digital":
            return { icon: "digital", caption: "" };
        case "Vinyl": {
            const size = /(\d+)"/.exec(mediaType);
            return { icon: "vinyl", caption: size ? `${size[1]}"` : "" };
        }
        default:
            return null;
    }
}

function createMediaIconElement(mediaType, { icon, caption }) {
    const container = document.createElement("div");
    container.className = "shrink-0 flex flex-col items-center gap-0.5 text-slate-400 dark:text-slate-500";
    container.title = mediaType.trim();
    container.setAttribute("role", "img");
    container.setAttribute("aria-label", mediaType.trim());

    container.innerHTML = mediaIcons.svg(icon, "h-10 w-10");

    if (caption) {
        const captionElement = document.createElement("span");
        captionElement.className = "text-xs leading-none";
        captionElement.textContent = caption;
        container.append(captionElement);
    }

    return container;
}

function describeRelease(release) {
    const tracks = release.trackCount > 0
        ? `${release.trackCount} ${release.trackCount === 1 ? "track" : "tracks"}`
        : "Track count missing";
    const mediaIcon = mediaIconFor(release.mediaType);

    return {
        heading: `${valueOrMissing(release.title, "Title")} – ${valueOrMissing(release.artistCombined, "Artist")}`,
        details: [
            valueOrMissing(release.date, "Date"),
            tracks,
            valueOrMissing(release.releaseType, "Release type"),
            mediaIcon ? null : valueOrMissing(release.mediaType, "Media type")
        ].filter(x => x).join(" · "),
        mediaIcon,
        note: release.disambiguation?.trim() ?? ""
    };
}

// number is the option's position in the list, starting at 1
function createReleaseButton(release, number, onChosen) {
    const { heading, details, mediaIcon, note } = describeRelease(release);

    const button = document.createElement("button");
    button.type = "button";
    button.className = `w-full text-left px-4 py-3 rounded-lg border
                        flex items-center gap-3
                        border-slate-200 dark:border-slate-700
                        hover:bg-slate-100 dark:hover:bg-slate-700
                        focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500
                        transition-colors`;

    // Number on the left, then the text, and the media icon (if any) on the right spanning all lines
    const numberElement = document.createElement("span");
    numberElement.className = "shrink-0 min-w-6 text-right font-medium tabular-nums text-slate-400 dark:text-slate-500";
    numberElement.textContent = `${number}.`;
    button.append(numberElement);

    const text = document.createElement("div");
    text.className = "min-w-0 flex-1";
    button.append(text);
    if (mediaIcon)
        button.append(createMediaIconElement(release.mediaType, mediaIcon));

    const headingElement = document.createElement("div");
    headingElement.className = "font-medium";
    headingElement.textContent = heading;
    text.append(headingElement);

    const detailsElement = document.createElement("div");
    detailsElement.className = "text-sm text-slate-500 dark:text-slate-400";
    const flagElement = createFlagElement(release.country);
    if (flagElement)
        detailsElement.append(flagElement);
    detailsElement.append(details);
    text.append(detailsElement);

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

    button.addEventListener("click", () => onChosen(release));
    return button;
}

export function choose(releases) {
    return new Promise(resolve => {
        let resolved = false;
        function resolveOnce(value) {
            if (resolved)
                return;
            resolved = true;
            resolve(value);
        }

        const first = releases[0];
        const root = document.createElement("div");
        root.className = "flex flex-col gap-4";

        const intro = document.createElement("p");
        intro.className = "text-slate-600 dark:text-slate-300";
        intro.textContent = `MusicBrainz has ${releases.length} releases of "${first.title}"`
            + (first.artistCombined ? ` by ${first.artistCombined}` : "")
            + ". Which one do you have?";
        root.append(intro);

        const list = document.createElement("div");
        list.className = "flex flex-col gap-2 max-h-[50vh] overflow-y-auto";
        root.append(list);

        let modal = null;
        for (const [index, release] of releases.entries()) {
            list.append(createReleaseButton(release, index + 1, chosen => {
                resolveOnce(chosen);
                modal.close();
            }));
        }

        modal = Modal.show({
            title: "Choose release",
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
