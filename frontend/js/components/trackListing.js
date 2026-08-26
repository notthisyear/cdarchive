import * as trackRow from "./trackRow.js";
import * as trackEditor from "../components/trackEditor.js";

let nextTrackId = 0;
const tracksMap = new Map();
const NEW_TRACK_PLACEHOLDER_NAME = "&lt;New Track&gt;"

function noTracksContent() {
    const div = document.createElement("div");
    div.className = `rounded-lg
                    border border-slate-200
                    dark:border-slate-700
                    bg-slate-50
                    dark:bg-slate-900
                    h-full
                    text-sm
                    text-slate-400
                    flex
                    items-center
                    justify-center
                    px-4`;
    div.innerHTML = "No tracks added";
    div.id = "no_tracks"
    return div;
}

function discNumberContent(discNumber) {
    const div = document.createElement("div");
    div.className = `flex
                     items-center
                     gap-2
                     mt-4
                     mb-2
                     px-4
                     text-sm
                     font-medium
                     text-gray-400`;
    div.innerHTML = `<svg class="w-4 h-4"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="1.5"
                            aria-hidden="true">
                            <circle cx="12" cy="12" r="9" />
                            <circle cx="12" cy="12" r="2" />
                        </svg><span>Disc ${discNumber}</span>`;
    return div;
}

function getTrackCount() {
    return tracksMap.size;
}

function getDiscCount() {
    return new Set([...tracksMap.values()].map(x => x.discNumber)).size;
}

function hasMultipleDiscs() {
    return getDiscCount() > 1;
}

function recreateTrackListing(trackList) {
    if (tracksMap.size === 0) {
        return;
    }

    trackList.innerHTML = "";
    const sortedTracks = [...tracksMap.entries()].sort(([, a], [, b]) => {
        if (a.discNumber !== b.discNumber) {
            return a.discNumber - b.discNumber;
        }
        return a.trackNumber - b.trackNumber;
    });

    let currentDiscNumber = -1;
    const multipleDiscs = hasMultipleDiscs();
    for (const [id, track] of sortedTracks) {
        if (multipleDiscs && track.discNumber !== currentDiscNumber) {
            trackList.appendChild(discNumberContent(track.discNumber));
            currentDiscNumber = track.discNumber;
        }
        trackList.appendChild(createTrackRowWithId(trackList, id, track.title, track.discNumber, track.trackNumber, track.durationSeconds));
    }
}

function updateTrackRow(trackList, oldTrackId, oldRow, newRow, hadMultipleDiscsBefore) {
    tracksMap.delete(oldTrackId);

    // If the number of discs didn't change, we can just replace the row. Otherwise, recreate the full listing.
    if (hadMultipleDiscsBefore === hasMultipleDiscs()) {
        trackList.replaceChild(newRow, oldRow);
    }
    else {
        recreateTrackListing(trackList);
    }
}

function removeTrackRow(trackList, trackId, row) {
    const hadMultipleDiscsBefore = hasMultipleDiscs();
    row.remove();
    tracksMap.delete(trackId);

    // If the number of tracks is now zero, we can just delete everything.
    // If the number of discs changed, we need to recreate the listing and
    // otherwise, there's nothing more to do.
    if (getTrackCount() === 0) {
        trackList.innerHTML = "";
        trackList.append(noTracksContent());
    }
    else if (hadMultipleDiscsBefore !== hasMultipleDiscs()) {
        recreateTrackListing(trackList);
    }
}

function createTrackRow(trackList, title, discNumber, trackNumber, durationSeconds) {
    const trackId = nextTrackId++;
    tracksMap.set(trackId, { title: title, discNumber: discNumber, trackNumber: trackNumber, durationSeconds: durationSeconds });
    return createTrackRowWithId(trackList, trackId, title, discNumber, trackNumber, durationSeconds);
}

function createTrackRowWithId(trackList, trackId, title, discNumber, trackNumber, durationSeconds) {
    const row = trackRow.create(title, trackNumber, durationSeconds);
    row.addEventListener("click", () => {
        trackEditor.show(title === NEW_TRACK_PLACEHOLDER_NAME ? "" : title,
            discNumber,
            trackNumber,
            durationSeconds,
            (newTitle, newDiscNumber, newTrackNumber, newDuration) => {
                const hadMultipleDiscsBefore = hasMultipleDiscs();
                const newRow = createTrackRow(trackList, newTitle, newDiscNumber, newTrackNumber, newDuration);
                updateTrackRow(trackList, trackId, row, newRow, hadMultipleDiscsBefore);
            },
            () => { removeTrackRow(trackList, trackId, row); });
    });
    return row;
}

export function addTrackRow(trackList, title, discNumber, trackNumber, durationSeconds) {
    if (tracksMap.size === 0) {
        trackList.innerHTML = "";
    }

    const hadMultipleDiscsBefore = hasMultipleDiscs();
    const row = createTrackRow(trackList, title, discNumber, trackNumber, durationSeconds);

    // If the number of discs didn't change, we can just append the row. Otherwise, recreate the full listing.
    if (hadMultipleDiscsBefore === hasMultipleDiscs()) {
        trackList.appendChild(row);
    }
    else {
        recreateTrackListing(trackList);
    }
}

export function addEmptyTrackRow(trackList) {
    addTrackRow(trackList, NEW_TRACK_PLACEHOLDER_NAME, Math.max(1, getDiscCount()), getTrackCount() + 1, 0);
}

export function clearAllTracks(trackList) {
    tracksMap.clear();
    nextTrackId = 0;
    trackList.innerHTML = "";
    trackList.append(noTracksContent());
}

export function getAllTracks() {
    return [...tracksMap.values()].map(x => x);
}