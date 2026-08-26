import * as Modal from "./modal.js";
import * as Toast from "./toast.js";

import * as api from "../api.js";
import * as styles from "../styles.js";
import * as util from "../util.js";
import * as trackListing from "../components/trackListing.js";
import * as confirmArtist from "../components/confirmArtist.js"

import { Autocomplete } from "./autocomplete.js";

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
                                    <label class="${styles.addRecordInputBoxLabel}">
                                        Artist
                                    </label>

                                    <div id="artistRows" class="space-y-2"></div>

                                    <button type="button"
                                            id="addArtistBtn"
                                            class="mt-2 flex items-center gap-1 text-sm text-slate-400
                                                hover:text-slate-200 transition">
                                        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24"
                                            stroke="currentColor" stroke-width="2">
                                            <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                                        </svg>
                                        Add artist
                                    </button>
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
    const artistRows = root.querySelector("#artistRows");
    const addArtistBtn = root.querySelector("#addArtistBtn");
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
    trackListing.clearAllTracks(trackList);

    const autocompletes = new Map();
    let nextArtistBoxId = 0;

    // Artist rows
    function removeArtistRow(boxId, row) {
        const container = row.parentElement;
        row.remove();
        autocompletes.delete(boxId);
        updateRemoveButtonVisibility(container);
    }

    function createArtistRow(value = "") {
        const row = document.createElement("div");
        row.className = "artist-row group relative flex items-center gap-2";
        row.innerHTML = `
            <div class="relative flex-1">
                <input type="text"
                    class="artist-name-input ${styles.editorInputBox} w-full pr-8"
                    autocomplete="off"
                    required
                    placeholder="Search for an artist...">
                <svg class="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2
                            h-4 w-4 text-slate-400"
                    viewBox="0 0 20 20" fill="currentColor">
                    <path fill-rule="evenodd"
                        d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.24 4.25a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z"
                        clip-rule="evenodd" />
                </svg>
                <input type="hidden" class="artist-id-input">
            </div>

            <button type="button"
                    class="remove-artist-btn shrink-0 w-6 h-6 rounded-full
                        flex items-center justify-center
                        text-slate-500 hover:text-white hover:bg-red-500
                        opacity-0 group-hover:opacity-100 focus:opacity-100
                        transition"
                    aria-label="Remove artist"
                    title="Remove artist">
                <svg class="w-3.5 h-3.5" viewBox="0 0 20 20" fill="currentColor">
                    <path d="M6.28 5.22a.75.75 0 00-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 101.06 1.06L10 11.06l3.72 3.72a.75.75 0 101.06-1.06L11.06 10l3.72-3.72a.75.75 0 00-1.06-1.06L10 8.94 6.28 5.22z" />
                </svg>
            </button>
        `;

        const nameInput = row.querySelector(".artist-name-input");
        const idInput = row.querySelector(".artist-id-input");

        nameInput.addEventListener("input", () => {
            idInput.value = "";
        });

        const boxId = nextArtistBoxId++;
        autocompletes.set(boxId, new Autocomplete({
            input: nameInput,
            search: query => {
                return [
                    { "name": "some artist" },
                    { "name": "Kiss" },
                    { "name": "Eagles" }
                ];
            },
            renderItem(artist) {
                return `<div class="font-medium">${artist.name}</div>`;
            },
            onSelected(artist) {
                nameInput.value = artist.name;
                idInput.value = artist.id;
            }
        }));

        row.querySelector(".remove-artist-btn").addEventListener("click", () => {
            removeArtistRow(boxId, row);
        });

        if (value !== "") {
            nameInput.value = value;
        }
        return row;
    }

    function updateRemoveButtonVisibility(container) {
        const rows = container.querySelectorAll(".artist-row");
        // Don't allow removing the last remaining artist row.
        rows.forEach(row => {
            const btn = row.querySelector(".remove-artist-btn");
            btn.classList.toggle("hidden", rows.length === 1);
        });
    }

    function initArtistRows() {
        // Start with a single artist row.
        artistRows.append(createArtistRow());
        updateRemoveButtonVisibility(artistRows);

        addArtistBtn.addEventListener("click", () => {
            artistRows.append(createArtistRow());
            updateRemoveButtonVisibility(artistRows);
        });
    }

    function getArtists(root) {
        const rows = root.querySelectorAll(".artist-row");
        return Array.from(rows).map(row => ({
            name: row.querySelector(".artist-name-input").value.trim(),
            id: row.querySelector(".artist-id-input").value || null,
        })).filter(a => a.name.length > 0);
    }

    function getState() {
        return {
            name: recordName.value,
            artistName: getArtists(root),
            year: recordYear.value,
            durationHours: durationHours.value,
            durationMinutes: durationMinutes.value,
            durationSeconds: durationSeconds.value,
            spotifyUrl: recordSpotifyUrl.value,
            coverImage: coverImage.src,
            tracks: trackListing.getAllTracks()
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
        // TODO: Hook some backend logic here to delete from image store
        clearCoverImage();
    });

    initArtistRows();

    // Album autocomplete
    autocompletes.set(nextArtistBoxId++, new Autocomplete({
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

            recordName.value = albumInfo.name ?? "";
            recordYear.value = albumInfo.release_date?.slice(0, 4) ?? "";
            recordSpotifyUrl.value = albumInfo.external_urls?.spotify ?? "";

            if (albumInfo.images?.at(0).url ?? false) {
                setCoverImage(album.images.at(0).url);
            }

            artistRows.innerHTML = "";
            if (albumInfo.artists) {
                for (const artist of albumInfo.artists) {
                    artistRows.append(createArtistRow(artist.name));
                }
            }

            trackListing.clearAllTracks(trackList);
            if (albumInfo.tracks) {
                let totalDurationSeconds = 0;
                for (const track of albumInfo.tracks.items) {
                    const durationSeconds = Math.round(track.duration_ms / 1000);
                    totalDurationSeconds += durationSeconds;
                    trackListing.addTrackRow(trackList, track.name, track.disc_number, track.track_number, durationSeconds);
                }
                const parts = util.getPartsFromSeconds(totalDurationSeconds);

                if (parts.hours > 0)
                    durationHours.value = parts.hours;
                if (parts.minutes > 0)
                    durationMinutes.value = parts.minutes;
                if (parts.seconds > 0)
                    durationSeconds.value = parts.seconds;
            }

            albumSearch.value = `${util.concatenateArtists(albumInfo.artists ?? "name")} - ${recordName.value}`;
        }
    }));

    // Track listing
    root.querySelector("#addTrackBtn").addEventListener("click", () => {
        trackListing.addEmptyTrackRow(trackList);
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

    function disposeAutocompletes() {
        for (const autocomplete in autocompletes.values())
            autocomplete.dispose();
        autocompletes.clear();
    }

    let previousState = JSON.stringify(getState());
    let blockFormClose = false;

    // Actual form
    Modal.show({
        title: "Add Record",
        content: root,
        maxWidthClass: "max-w-4xl",
        buttons: [
            {
                "type": "save",
                "action": async () => {
                    if (!albumForm.reportValidity()) {
                        return false;
                    }

                    previousState = JSON.stringify(getState());
                    blockFormClose = true;
                    showLoadingOverlay();

                    // First, we fetch artist information
                    let artistData;
                    try {
                        artistData = (await api.getArtists(getArtists(root))).result;
                        console.log(artistData);
                    }
                    catch (e) {
                        Toast.error(`Could not add record - ${e}`, "Adding record failed");
                        hideLoadingOverlay();
                        blockFormClose = false;
                        return false;
                    }

                    const resolution = await confirmArtist.confirm(artistData);

                    if (resolution === null) {
                        hideLoadingOverlay();
                        blockFormClose = false;
                        return false;
                    }

                    const resolvedArtists = artistNames.map(name => resolution[name]);
                    console.log(resolvedArtists);

                    const duration = [
                        durationHours.value,
                        durationMinutes.value,
                        durationSeconds.value
                    ].map(x => x.padStart(2, "0")).join(":");

                    const newRecordRequest = {
                        summary: {
                            name: recordName.value,
                            artists: resolvedArtists.map(x =>
                            ({
                                id: x.type === "new" ? null : x.id,
                                name: x.name
                            })),
                            year: recordYear.value,
                            imageUrl: coverImage.src,
                            spotifyLink: recordSpotifyUrl.value,
                        },
                        durationSeconds: util.parseDurationToSeconds(duration),
                        tracks: trackListing.getAllTracks()
                    };

                    try {
                        await api.tryAddNewRecord(newRecordRequest)
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
