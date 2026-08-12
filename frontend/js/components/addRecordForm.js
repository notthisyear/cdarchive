import * as Modal from "./modal.js";
import * as Toast from "./toast.js";

import * as api from "../api.js";
import * as styles from "../styles.js";
import * as util from "../util.js";
import * as trackRow from "./trackRow.js";
import * as trackEditor from "../components/trackEditor.js";

import { Autocomplete } from "./autocomplete.js";

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

function wireDurationSegment(input, { next, prev } = {}) {
    input.addEventListener("input", () => {
        input.value = input.value.replace(/\D/g, "").slice(0, 2);
        if (input.value.length === 2 && next) {
            next.focus();
            next.select();
        }
    });

    input.addEventListener("keydown", e => {
        if (e.key === "Backspace" && input.value === "" && prev) {
            prev.focus();
            prev.select();
        }
    });
}

export function show() {
    const root = document.createElement("div");
    root.innerHTML = `
        <div id="addRecordDialog"
            class="flex flex-col max-h-[80vh] overflow-hidden space-y-6">

            <!-- Search section -->
            <section class="space-y-4 shrink-0">
                <div class="relative">
                    <input id="albumSearch"
                           class="${styles.editorInputBox} w-full"
                           type="text"
                           placeholder="Search Spotify...">
                    </div>
            </section>

            <!-- Album info and track listing-->
            <section class="grid grid-cols-1 grid-rows-2 lg:grid-cols-2 lg:grid-rows-1 gap-8 flex-1 min-h-0">

                <!-- Album info -->
                <div class="flex flex-col min-h-0 overflow-y-auto gap-4">
                    <h3 class="font-semibold text-slate-900 dark:text-slate-100">
                        Album information
                    </h3>

                    <form id="albumForm"
                          class="space-y-4 shrink-0"
                          novalidate>

                        <div class="flex gap-4">
                            <div class="flex-1 space-y-4">
                                <div>
                                    <label for="recordName"
                                        class="${styles.addRecordInputBoxLabel}">
                                        Name
                                    </label>
                                    <input id="recordName"
                                        name="name"
                                        type="text"
                                        required
                                        autocomplete="off"
                                        class="${styles.editorInputBox}"
                                        placeholder="Album name">
                                </div>
                                <div>
                                    <label for="recordArtist"
                                        class="${styles.addRecordInputBoxLabel}">
                                        Artist
                                    </label>
                                    <div class="relative">
                                        <input id="recordArtist"
                                            name="artist"
                                            type="text"
                                            required
                                            autocomplete="off"
                                            class="${styles.editorInputBox} pr-8"
                                            placeholder="Search for an artist...">
                                        <svg class="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2
                                                    h-4 w-4 text-slate-400"
                                            viewBox="0 0 20 20" fill="currentColor">
                                            <path fill-rule="evenodd"
                                                d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.24 4.25a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z"
                                                clip-rule="evenodd" />
                                        </svg>
                                        <input type="hidden" id="recordArtistId" name="artistId">
                                    </div>
                                </div>
                            </div>

                            <!-- Cover art -->
                            <div class="shrink-0">
                                <div id="coverContainer"
                                    class="relative group w-40 aspect-square rounded-lg border
                                            border-slate-200 dark:border-slate-700 overflow-hidden
                                            bg-slate-50 dark:bg-slate-900 flex items-center justify-center">
                                    <img id="coverImage"
                                        class="w-full h-full object-cover hidden"
                                        alt="Cover art">
    
                                    <div id="coverPlaceholder"
                                        class="text-sm text-slate-400 text-center px-4">
                                        No cover selected
                                    </div>

                                    <div class="absolute inset-0 flex items-center justify-center
                                                bg-black/0 group-hover:bg-black/30
                                                transition-colors pointer-events-none">
                                        <button type="button"
                                                id="uploadCoverOverlayBtn"
                                                aria-label="Upload cover photo"
                                                title="Upload cover photo..."
                                                class="pointer-events-auto w-9 h-9 rounded-full
                                                    bg-white/90 dark:bg-slate-800/90 shadow
                                                    flex items-center justify-center
                                                    opacity-0 group-hover:opacity-100
                                                    hover:bg-white dark:hover:bg-slate-700
                                                    transition">
                                            <svg class="w-4 h-4 text-slate-600 dark:text-slate-300"
                                                viewBox="0 0 20 20" fill="currentColor">
                                                <path d="M9.25 13.75v-8.614l-2.955 3.129a.75.75 180 01-1.09-1.03l4.25-4.5a.75.75 180 011.09 0l4.25 4.5a.75.75 180 11-1.09 1.03L10.75 5.136V13.75a.75.75 180 01-1.5 0Z" />
                                                <path d="M3.5 12.75a.75.75 0 00-1.5 0v2.5A2.75 2.75 0 004.75 18h10.5A2.75 2.75 0 0018 15.25v-2.5a.75.75 0 00-1.5 0v2.5c0 .69-.56 1.25-1.25 1.25H4.75c-.69 0-1.25-.56-1.25-1.25v-2.5z" />
                                            </svg>
                                        </button>
                                    </div>

                                    <button type="button"
                                            id="removeCoverBtn"
                                            aria-label="Remove cover photo"
                                            title="Remove cover photo"
                                            class="hidden absolute top-2 right-2 w-6 h-6 rounded-full group/remove
                                                bg-white dark:bg-slate-800 hover:bg-red-500 dark:hover:bg-red-500
                                                shadow items-center justify-center
                                                opacity-0 group-hover:opacity-100
                                                transition">
                                        <svg class="w-3.5 h-3.5 text-slate-600 dark:text-slate-300
                                                    group-hover/remove:text-white transition-colors"
                                            viewBox="0 0 20 20" fill="currentColor">
                                            <path d="M6.28 5.22a.75.75 0 00-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 101.06 1.06L10 11.06l3.72 3.72a.75.75 0 101.06-1.06L11.06 10l3.72-3.72a.75.75 0 00-1.06-1.06L10 8.94 6.28 5.22z" />
                                        </svg>
                                    </button>
                                </div>
    
                                <input type="file"
                                    id="coverFileInput"
                                    accept="image/*"
                                    class="hidden">
                            </div>
                        </div>
                        
                        <div class="grid grid-cols-2 gap-4">
                            <div>
                                <label for="recordYear"
                                    class="${styles.addRecordInputBoxLabel}">
                                    Year
                                </label>
                                <input id="recordYear"
                                    name="year"
                                    type="text"
                                    inputmode="numeric"
                                    required
                                    pattern="[0-9]{4}"
                                    maxlength="4"
                                    title="Enter a 4-digit year"
                                    class="${styles.editorInputBox}"
                                    placeholder="YYYY">
                            </div>
    
                            <div>
                                <label class="${styles.addRecordInputBoxLabel}">
                                    Duration
                                </label>
                                <div class="flex items-center gap-1">
                                    <input id="durationHours"
                                        type="text"
                                        inputmode="numeric"
                                        maxlength="2"
                                        placeholder="hh"
                                        class="${styles.editorInputBox} w-8 bg-transparent placeholder:text-slate-400">
                                    <span class="text-slate-400">:</span>
                                    <input id="durationMinutes"
                                        type="text"
                                        inputmode="numeric"
                                        maxlength="2"
                                        placeholder="mm"
                                        class="${styles.editorInputBox} w-8 bg-transparent placeholder:text-slate-400">
                                    <span class="text-slate-400">:</span>
                                    <input id="durationSeconds"
                                        type="text"
                                        inputmode="numeric"
                                        maxlength="2"
                                        placeholder="ss"
                                        class="${styles.editorInputBox} w-8 bg-transparent placeholder:text-slate-400">
                                </div>
                            </div>
                        </div>

                        <div>
                            <label for="recordSpotifyUrl"
                                class="${styles.addRecordInputBoxLabel}">
                                Spotify URL (optional)
                            </label>
                            <input id="recordSpotifyUrl"
                                name="spotifyUrl"
                                type="url"
                                class="${styles.editorInputBox} w-full"
                                placeholder="https://open.spotify.com/album/...">
                        </div>
                    </form>
                </div>
            

                <!-- Track listing -->
                <div class="flex flex-col min-h-0">
                    <h3 class="font-semibold text-slate-900 dark:text-slate-100 pt-3 shrink-0">
                        Track listing
                    </h3>

                    <div id="trackList" class="flex-1 min-h-0 overflow-y-auto space-y-2 mt-3">
                        
                    </div>

                    <button type="button"
                            id="addTrackBtn"
                            class="${styles.buttonPrimary} text-sm shrink-0 mt-3">
                        Add track
                    </button>
                </div>
            </section>

            <div id="formLoadingOverlay"
                 class="absolute inset-0 z-10
                        bg-black/50
                        flex items-center justify-center
                        opacity-0 pointer-events-none
                        transition-opacity duration-200">
                <svg class="animate-spin h-30 w-30 text-white" viewBox="0 0 24 24" fill="none">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                </svg>
            </div>
        </div>
    `;

    // Elements
    const albumSearch = root.querySelector("#albumSearch");
    const albumForm = root.querySelector("#albumForm");
    const recordName = root.querySelector("#recordName");
    const recordArtist = root.querySelector("#recordArtist");
    const recordArtistId = root.querySelector("#recordArtistId");
    const recordYear = root.querySelector("#recordYear");
    const recordSpotifyUrl = root.querySelector("#recordSpotifyUrl");
    const durationHours = root.querySelector("#durationHours");
    const durationMinutes = root.querySelector("#durationMinutes");
    const durationSeconds = root.querySelector("#durationSeconds");

    const coverImage = root.querySelector("#coverImage");
    const coverPlaceholder = root.querySelector("#coverPlaceholder");
    const removeCoverBtn = root.querySelector("#removeCoverBtn");
    const coverFileInput = root.querySelector("#coverFileInput");
    const uploadCoverOverlayBtn = root.querySelector("#uploadCoverOverlayBtn");
    const formLoadingOverlay = root.querySelector("#formLoadingOverlay");

    const trackList = root.querySelector("#trackList");
    trackList.append(noTracksContent());

    // Track management
    let nextTrackId = 0;
    const tracksMap = new Map();

    function createTrackRow(title, trackNumber, durationSeconds) {
        const row = trackRow.create(title, trackNumber, durationSeconds);
        const trackId = nextTrackId++;
        tracksMap.set(trackId, { title: title, trackNumber: trackNumber, durationSeconds: durationSeconds });
        row.addEventListener("click", () => {
            trackEditor.show(title === NEW_TRACK_PLACEHOLDER_NAME ? "" : title,
                trackNumber,
                durationSeconds,
                (newTitle, newTrackNumber, newDuration) => { updateTrackRow(trackId, row, createTrackRow(newTitle, newTrackNumber, newDuration)); },
                () => { removeTrackRow(trackId, row); });
        });
        return row;
    }

    function addTrackRow(title, trackNumber, durationSeconds) {
        const row = createTrackRow(title, trackNumber, durationSeconds);
        trackList.appendChild(row);
    }

    function getTrackCount() {
        return trackList.querySelectorAll(".track-row").length;
    }

    function removeTrackRow(trackId, row) {
        row.remove();
        tracksMap.delete(trackId);
        if (getTrackCount() === 0) {
            trackList.innerHTML = "";
            trackList.append(noTracksContent());
        }
    }

    function updateTrackRow(oldTrackId, oldRow, newRow) {
        trackList.replaceChild(newRow, oldRow);
        tracksMap.delete(oldTrackId);
    }

    // Function to get current form state
    function getState() {
        return {
            name: recordName.value,
            artistName: recordArtist.value,
            year: recordYear.value,
            durationHours: durationHours.value,
            durationMinutes: durationMinutes.value,
            durationSeconds: durationSeconds.value,
            spotifyUrl: recordSpotifyUrl.value,
            coverImage: coverImage.src,
            tracks: [...tracksMap.values()].map(x => x)
        };
    }

    // Wire up the duration segments
    wireDurationSegment(durationHours, { next: durationMinutes });
    wireDurationSegment(durationMinutes, { next: durationSeconds, prev: durationHours });
    wireDurationSegment(durationSeconds, { prev: durationMinutes });

    // Cover photo
    function setCoverImage(url) {
        coverImage.src = url;
        coverImage.classList.remove("hidden");
        coverPlaceholder.classList.add("hidden");
        removeCoverBtn.classList.remove("hidden");
        removeCoverBtn.classList.add("flex");
    }

    function clearCoverImage() {
        coverImage.removeAttribute("src");
        coverImage.classList.add("hidden");
        coverPlaceholder.classList.remove("hidden");
        removeCoverBtn.classList.add("hidden");
        removeCoverBtn.classList.remove("flex");
        coverFileInput.value = "";
    }

    uploadCoverOverlayBtn.addEventListener("click", () => coverFileInput.click());
    coverFileInput.addEventListener("change", () => {
        const file = coverFileInput.files?.[0];
        if (!file)
            return;

        const reader = new FileReader();
        reader.onload = () => setCoverImage(reader.result);
        reader.readAsDataURL(file);
    });

    removeCoverBtn.addEventListener("click", () => {
        // Hook your own delete logic here too (e.g. telling the backend
        // to drop a previously-uploaded cover) if the image didn't just
        // come from the file picker above.
        clearCoverImage();
    });

    // Artist autocomplete
    const artistSearchAutocomplete = new Autocomplete({
        input: recordArtist,
        search: query => {
            return [
                {
                    "name": "some artist",
                    "imageUrl": "some url"
                },
                {
                    "name": "Kiss",
                    "imageUrl": "some url"
                },
                {
                    "name": "Eagles",
                    "imageUrl": "some url"
                }];
        },
        renderItem(artist) {
            return `
                <img src="${artist.imageUrl}"
                     class="w-8 h-8 rounded-full object-cover">
                <div class="font-medium">${artist.name}</div>
            `;
        },
        onSelected(artist) {
            recordArtist.value = artist.name;
            recordArtistId.value = artist.id;
        }
    });

    // Album autocomplete
    const albumSearchAutocomplete = new Autocomplete({
        input: albumSearch,
        search: async (q) => await api.searchOnSpotify(q, "album", 5, 0, (b) => {
            return b.albums.total > 0 ? b.albums.items : [];
        }),
        renderItem(album) {
            return `
                <div class="flex items-center gap-4">
                    <img src="${album.images.at(-1).url}"
                         class="w-12 h-12 roun
                         ded">
                    </img>
                    <div>
                        <div class="font-medium">
                            ${album.name}
                        </div>
                        <div class="text-sm text-slate-500">
                            ${util.concatenateArtists(album.artists)} • ${album.release_date}
                        </div>
                    </div>
                </div>
            `;
        },
        async onSelected(album) {
            const albumInfo = await api.getSpotifyAlbum(album.id);
            trackList.innerHTML = "";

            recordName.value = albumInfo.name ?? "";
            recordArtist.value = util.concatenateArtists(albumInfo.artists ?? "name");
            recordYear.value = albumInfo.release_date?.slice(0, 4) ?? "";
            recordSpotifyUrl.value = albumInfo.external_urls?.spotify ?? "";

            if (albumInfo.images?.at(0).url ?? false) {
                setCoverImage(album.images.at(0).url);
            }

            if (albumInfo.tracks) {
                let totalDurationSeconds = 0;
                for (const track of albumInfo.tracks.items) {
                    const durationSeconds = Math.round(track.duration_ms / 1000);
                    totalDurationSeconds += durationSeconds;
                    addTrackRow(track.name, track.track_number, durationSeconds);
                }
                const parts = util.getPartsFromSeconds(totalDurationSeconds);

                if (parts.hours > 0)
                    durationHours.value = parts.hours;
                if (parts.minutes > 0)
                    durationMinutes.value = parts.minutes;
                if (parts.seconds > 0)
                    durationSeconds.value = parts.seconds;
            }

            albumSearch.value = `${recordArtist.value} - ${recordName.value}`;
        }
    });

    // Track listing
    root.querySelector("#addTrackBtn").addEventListener("click", () => {
        const trackCount = getTrackCount();
        if (trackCount === 0) {
            trackList.innerHTML = "";
            addTrackRow(NEW_TRACK_PLACEHOLDER_NAME, 1, 0);
        }
        else {
            addTrackRow(NEW_TRACK_PLACEHOLDER_NAME, trackCount + 1, 0);
        }
    });

    // Loading overlays
    function showLoadingOverlay() {
        formLoadingOverlay.classList.remove("opacity-0", "pointer-events-none");
        formLoadingOverlay.classList.add("opacity-100", "pointer-events-auto");
    }

    function hideLoadingOverlay() {
        formLoadingOverlay.classList.add("opacity-0", "pointer-events-none");
        formLoadingOverlay.classList.remove("opacity-100", "pointer-events-auto");
    }

    // Actual form
    function disposeAutocompletes() {
        albumSearchAutocomplete.dispose();
        artistSearchAutocomplete.dispose();
    }

    let previousState = JSON.stringify(getState());
    let blockFormClose = false;

    Modal.show({
        title: "Add Record",
        content: root,
        maxWidthClass: "max-w-4xl",
        buttons: [
            {
                "type": "save",
                "action": () => {
                    if (!albumForm.reportValidity()) {
                        return false;
                    }

                    previousState = JSON.stringify(getState());
                    const duration = [
                        durationHours.value,
                        durationMinutes.value,
                        durationSeconds.value
                    ].map(x => x.padStart(2, "0")).join(":");

                    const newRecordRequest = {
                        summary: {
                            name: recordName.value,
                            artist: {
                                name: recordArtist.value
                            },
                            year: recordYear.value,
                            imageUrl: coverImage.src,
                            spotifyLink: recordSpotifyUrl.value,
                        },
                        lengthSeconds: util.parseDurationToSeconds(duration),
                        tracks: [...tracksMap.values()].map(x => x)
                    };

                    blockFormClose = true;
                    showLoadingOverlay();
                    try {
                        api.addNewRecord(newRecordRequest);
                        Toast.success(`Record '${recordName.value}' added successful`, "New record added");
                    }
                    catch (e) {
                        Toast.error(`Could not add record - ${e}`, "Adding record failed");
                    }
                    finally {
                        hideLoadingOverlay();
                        blockFormClose = false;
                    }
                    return false;
                }
            },
            {
                "type": "close",
                "action": async () => {
                    if (blockFormClose)
                        return false;

                    let shouldClose = false;
                    const currentState = JSON.stringify(getState());
                    if (previousState !== currentState) {
                        shouldClose = await Modal.confirm("Unsaved changes", "Do you want to discard unsaved changes?");
                    }
                    else {
                        shouldClose = true;
                    }

                    if (shouldClose)
                        disposeAutocompletes();
                    return shouldClose;
                }
            }
        ]
    });
}
