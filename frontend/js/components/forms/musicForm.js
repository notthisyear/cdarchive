import * as auth from "../../auth.js";
import * as backendApi from "../../api/backend.js";
import * as spotifyApi from "../../api/spotify.js";
import * as artwork from "../../api/artwork.js";
import * as musicbrainzApi from "../../api/musicbrainz.js";
import * as styles from "../../styles.js";
import * as util from "../../util.js";
import * as trackListing from "../trackListing.js";
import * as addArtistForm from "../addArtistForm.js"
import * as confirmRelease from "../confirmRelease.js";
import * as mediaIcons from "../common/mediaIcons.js";
import * as lookupStatusLine from "../common/lookupStatus.js";

import { Autocomplete } from "../autocomplete.js";
import { spinnerSvg } from "../common/spinner.js";

function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, c => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[c]);
}

// editorInputBox has w-full and px-3, which win over a plain w-*/px-* in the compiled CSS. Made
// important (the "!" suffix) so each box is just wide enough for two digits, instead of each one
// growing to an input's default width and pushing the row out of its column.
const DURATION_INPUT_CLASSES = `${styles.editorInputBox} w-10! px-1! text-center bg-transparent placeholder:text-slate-400`;

// Spotify gives "1981", "1981-12" or "1981-12-29" depending on the precision.
// Pad it to a full date, so the backend always gets "YYYY-MM-DD".
function toFullDate(releaseDate, precision) {
    if (!releaseDate)
        return null;

    switch (precision) {
        case "year":
            return `${releaseDate}-01-01`;
        case "month":
            return `${releaseDate}-01`;
        default:
            return releaseDate;
    }
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

/**
 * Builds the "Music" tab. Returns a form view:
 *   element      - the DOM to show in the tab
 *   noun         - used in toasts/tooltips ("record")
 *   getValidity  - { ok, reason } for enabling/disabling the dialog's Save button
 *   validate     - runs the browser's validation UI (reportValidity)
 *   isDirty      - true if the user changed anything since the form was created
 *   save         - sends the record to the API, returns its name (for the toast)
 *   dispose      - tears down autocompletes
 *   footerStatus - the MusicBrainz lookup status line, for the dialog to show in its footer
 *
 * `onChange` is called whenever something happens that may change getValidity().
 */
export function create({ onChange } = {}) {
    const root = document.createElement("div");
    root.innerHTML = `
        <div id="addRecordDialog"
             class="flex flex-col max-h-[calc(80vh-3.5rem)] overflow-hidden space-y-6">
            
             <!-- Search section -->
            <section class="space-y-4 shrink-0">
                <div class="relative">
                    <input id="albumSearch"
                           class="${styles.editorInputBox} w-full"
                           type="text"
                           placeholder="Search Spotify..." />
                </div>
            </section>
            

            <!-- Album info and track listing side by side in two equal columns on wide screens,
                 each scrolling on its own. Stacked when narrow, where the section scrolls as a whole. -->
            <section class="grid grid-cols-1 lg:grid-cols-2 lg:grid-rows-1 gap-8 flex-1 min-h-0
                            overflow-y-auto lg:overflow-hidden">

                <!-- Album info. The right padding keeps the cover off this column's scrollbar. -->
                <div class="flex flex-col gap-4 lg:min-h-0 lg:overflow-y-auto lg:pr-3">
                    <h3 class="font-semibold text-slate-900 dark:text-slate-100">
                        Album information
                    </h3>

                    <form id="albumForm"
                          class="space-y-4 shrink-0"
                          novalidate>

                        <!-- Two columns: the name across both, then artists and year/duration/format
                             on the left with the cover centered beside them on the right.
                             A single column (cover last) when narrow. -->
                        <div class="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-4">
                                <div class="sm:col-span-2">
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
                                    <!-- The chosen MusicBrainz release, if any -->
                                    <input type="hidden" id="releaseMbid">
                                    <input type="hidden" id="releaseGroupMbid">
                                </div>
                                <div class="min-w-0 sm:col-start-1 sm:row-start-2">
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

                                <!-- Year, duration and format on one line; wraps when the column is narrow -->
                                <div class="flex flex-wrap gap-4 min-w-0 sm:col-start-1 sm:row-start-3">
                                    <div class="w-24">
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

                                    <div class="shrink-0">
                                        <label class="${styles.addRecordInputBoxLabel}">
                                            Duration
                                        </label>
                                        <div class="flex items-center gap-1">
                                            <input id="durationHours"
                                                type="text"
                                                inputmode="numeric"
                                                maxlength="2"
                                                placeholder="hh"
                                                class="${DURATION_INPUT_CLASSES}">
                                            <span class="text-slate-400">:</span>
                                            <input id="durationMinutes"
                                                type="text"
                                                inputmode="numeric"
                                                maxlength="2"
                                                placeholder="mm"
                                                class="${DURATION_INPUT_CLASSES}">
                                            <span class="text-slate-400">:</span>
                                            <input id="durationSeconds"
                                                type="text"
                                                inputmode="numeric"
                                                maxlength="2"
                                                placeholder="ss"
                                                class="${DURATION_INPUT_CLASSES}">
                                        </div>
                                    </div>

                                    <div class="flex-1 min-w-36">
                                        <label for="recordFormat"
                                            class="${styles.addRecordInputBoxLabel}">
                                            Format
                                        </label>
                                        <div class="flex items-center gap-2">
                                            <!-- Values are the backend's ReleaseFormat names, skipping the "Unknown".
                                                 The empty, hidden first option means "not chosen yet" (sent as Unknown). -->
                                            <select id="recordFormat"
                                                name="format"
                                                class="${styles.editorInputBox} flex-1 min-w-0">
                                                <option value="" disabled hidden selected>Select...</option>
                                                <option value="Cd">CD</option>
                                                <option value="Vinyl">Vinyl</option>
                                                <option value="Cassette">Cassette</option>
                                                <option value="Digital">Digital</option>
                                                <option value="Other">Other</option>
                                            </select>
                                            <!-- Icon for the selected format; keeps its width while empty -->
                                            <span id="recordFormatIcon"
                                                  class="shrink-0 w-8 h-8 text-slate-400 dark:text-slate-500"
                                                  aria-hidden="true"></span>
                                        </div>
                                    </div>
                                </div>

                            <!-- Cover art, centered in the right column beside artists and year/duration/format -->
                            <div class="w-full max-w-64 justify-self-center self-center sm:col-start-2 sm:row-start-2 sm:row-span-2">
                                <div id="coverContainer"
                                    class="relative group w-full aspect-square rounded-lg border
                                            border-slate-200 dark:border-slate-700 overflow-hidden
                                            bg-slate-50 dark:bg-slate-900 flex items-center justify-center">
                                    <img id="coverImage"
                                        class="w-full h-full object-cover hidden"
                                        alt="Cover art">

                                    <div id="coverPlaceholder"
                                        class="text-sm text-slate-400 text-center px-4">
                                        No cover selected
                                    </div>

                                    <div id="coverSpinner"
                                         class="hidden absolute inset-0 flex items-center justify-center
                                                bg-slate-50/80 dark:bg-slate-900/80 text-slate-400 pointer-events-none">
                                        ${spinnerSvg("h-8 w-8")}
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
                                                <path d="M9.25 13.75v-8.614l-2.955 3.129a.75.75 180 01-1.09-1.03l4.25-4.5a.75.75 180 011.09 0l4.25 4.5a.75.75 180 11-1.09 1.03L10.75 5.136V13.75a.75.75 0 01-1.5 0Z" />
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
                    </form>
                </div>


                <!-- Track listing -->
                <div class="flex flex-col min-h-64 lg:min-h-0">
                    <h3 class="font-semibold text-slate-900 dark:text-slate-100 shrink-0">
                        Track listing
                    </h3>

                    <div class="relative flex-1 min-h-0 mt-3">
                        <!-- Absolutely positioned so it gets a definite height from its flex parent,
                             which lets the "No tracks added" box (h-full) fill it. -->
                        <div id="trackList" class="absolute inset-0 overflow-y-auto space-y-2">

                        </div>

                        <div id="trackListSpinner"
                             class="hidden absolute inset-0 rounded-lg flex items-center justify-center
                                    bg-slate-50/80 dark:bg-slate-900/80 text-slate-400">
                            ${spinnerSvg("h-8 w-8")}
                        </div>
                    </div>

                    <button type="button"
                            id="addTrackBtn"
                            class="${styles.buttonPrimary} text-sm shrink-0 mt-3">
                        Add track
                    </button>
                </div>
            </section>
        </div>
    `;

    // Elements
    const albumSearch = root.querySelector("#albumSearch");
    const albumForm = root.querySelector("#albumForm");
    const recordName = root.querySelector("#recordName");
    const releaseMbid = root.querySelector("#releaseMbid");
    const releaseGroupMbid = root.querySelector("#releaseGroupMbid");
    const artistRows = root.querySelector("#artistRows");
    const addArtistBtn = root.querySelector("#addArtistBtn");
    const recordYear = root.querySelector("#recordYear");
    const durationHours = root.querySelector("#durationHours");
    const durationMinutes = root.querySelector("#durationMinutes");
    const durationSeconds = root.querySelector("#durationSeconds");
    const recordFormat = root.querySelector("#recordFormat");
    const recordFormatIcon = root.querySelector("#recordFormatIcon");

    const coverImage = root.querySelector("#coverImage");
    const coverPlaceholder = root.querySelector("#coverPlaceholder");
    const removeCoverBtn = root.querySelector("#removeCoverBtn");
    const coverFileInput = root.querySelector("#coverFileInput");
    const uploadCoverOverlayBtn = root.querySelector("#uploadCoverOverlayBtn");
    const coverSpinner = root.querySelector("#coverSpinner");
    const trackListSpinner = root.querySelector("#trackListSpinner");

    // The dialog owns the Save button. We just tell it when validity may have changed.
    function notifyChanged() {
        onChange?.();
    }

    function getValidity() {
        const artists = getArtists(root);
        const anyUnresolvedArtist = (artists.length === 0) || (artists.map(x => x.id).some(x => x === null));

        if (!albumForm.checkValidity())
            return { ok: false, reason: "Required field not valid" };

        if (anyUnresolvedArtist)
            return { ok: false, reason: "At least one artist is unresolved" };

        return { ok: true, reason: "" };
    }

    // Any typing inside the form (name, year, artist boxes...) may change validity.
    albumForm.addEventListener("input", notifyChanged);

    // The ids of the chosen MusicBrainz release stop matching once the user edits the form by hand.
    // A different year, duration or format is still the same release group (another pressing of
    // the album), but a different title or artist isn't.
    function clearReleaseIds({ keepReleaseGroup = false } = {}) {
        releaseMbid.value = "";
        if (!keepReleaseGroup)
            releaseGroupMbid.value = "";
    }

    recordName.addEventListener("input", () => clearReleaseIds());
    for (const input of [recordYear, durationHours, durationMinutes, durationSeconds])
        input.addEventListener("input", () => clearReleaseIds({ keepReleaseGroup: true }));
    recordFormat.addEventListener("change", () => clearReleaseIds({ keepReleaseGroup: true }));

    const trackList = root.querySelector("#trackList");
    trackListing.clearAllTracks(trackList);
    // A different track list is another release of the same album
    trackList.addEventListener(trackListing.TRACKS_EDITED_EVENT, () => clearReleaseIds({ keepReleaseGroup: true }));

    const autocompletes = new Map();
    let nextArtistBoxId = 0;

    // Artists
    const ADD_NEW_SENTINEL = { __addNew: true };

    function removeArtistRow(boxId, row) {
        const container = row.parentElement;
        if (row.querySelector(".artist-name-input").value.trim() !== "")
            clearReleaseIds();
        row.remove();
        autocompletes.get(boxId)?.dispose?.();
        autocompletes.delete(boxId);
        updateRemoveButtonVisibility(container);
        notifyChanged();
    }

    function clearArtistRows() {
        for (const row of artistRows.querySelectorAll(".artist-row")) {
            const boxId = Number(row.dataset.boxId);
            autocompletes.get(boxId)?.dispose?.();
            autocompletes.delete(boxId);
        }
        artistRows.innerHTML = "";
    }

    function attachArtistAutocomplete(boxId, row) {
        const nameInput = row.querySelector(".artist-name-input");
        const idInput = row.querySelector(".artist-id-input");
        const toggleBtn = row.querySelector(".artist-dropdown-toggle");

        const autocomplete = new Autocomplete({
            input: nameInput,
            search: async (q) => {
                let results = [];
                const queryString = q.trim();
                if (queryString.length > 0) {
                    try {
                        const response = await backendApi.getCreators([{ name: queryString, id: null }]);
                        results = response[queryString];
                    }
                    catch (e) {
                        console.error(e);
                        results = [];
                    }

                    idInput.value = results.length === 1 ? results[0].id : "";
                    updateArtistStatus(row);
                }
                return [...results, ADD_NEW_SENTINEL];
            },
            renderItem(item) {
                if (item.__addNew) {
                    return `<div class="px-3 py-2 text-sm text-blue-400 hover:bg-slate-700 cursor-pointer border-slate-700">
                            Add new...
                        </div>`;
                }
                const imageSrc = backendApi.toImageSrc(item.imageUrl);
                const image = imageSrc
                    ? `<img src="${escapeHtml(imageSrc)}" class="w-12 h-12 rounded object-cover shrink-0" />`
                    : `<div class="w-12 h-12 rounded shrink-0 bg-slate-200 dark:bg-slate-700"></div>`;
                return `
                    <div class="flex items-center gap-4">
                        ${image}
                        <div class="px-3 py-2 text-sm text-slate-200 hover:bg-slate-700 cursor-pointer">${escapeHtml(item.name)}</div>
                    </div>`;
            },
            async onSelected(item) {
                const nameBefore = nameInput.value;
                if (item.__addNew) {
                    const musicBrainzIdInput = row.querySelector(".artist-mbid-input");
                    const result = await addArtistForm.show(nameInput.value, {
                        musicBrainzId: musicBrainzIdInput.value || null
                    });
                    if (result !== null) {
                        nameInput.value = result.name;
                        idInput.value = result.id;
                        musicBrainzIdInput.value = result.musicBrainzId ?? "";
                    }
                }
                else {
                    nameInput.value = item.name;
                    idInput.value = item.id;
                }

                if (nameInput.value !== nameBefore)
                    clearReleaseIds();
                updateArtistStatus(row);
                notifyChanged();
            }
        });

        autocompletes.set(boxId, autocomplete);

        toggleBtn.addEventListener("click", (e) => {
            e.stopPropagation();
            if (autocomplete.isOpen)
                autocomplete.hide();
            else
                autocomplete.open();
        });
    }

    function updateArtistStatus(row) {
        const idInput = row.querySelector(".artist-id-input");
        const resolved = idInput.value.trim().length > 0;

        row.querySelector(".status-resolved").classList.toggle("hidden", !resolved);
        row.querySelector(".status-unresolved").classList.toggle("hidden", resolved);
    }

    // Synchronous: builds an empty row. Use resolveArtist() afterwards to look up a prefilled name.
    function createArtistRow() {
        const row = document.createElement("div");
        row.className = "artist-row group relative flex items-center gap-2";
        row.innerHTML = `
            <div class="artist-status shrink-0 w-5 h-5 flex items-center justify-center" title="">
                
            <!-- Yellow exclamation (default / unresolved) -->
                <div title="Artist is unknown, please add it from the dropdown">
                    <svg class="status-unresolved w-5 h-5 text-yellow-400" viewBox="0 0 20 20" fill="currentColor" >
                        <path fill-rule="evenodd"
                            d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l6.518 11.59c.75 1.334-.213 2.987-1.743 2.987H3.482c-1.53 0-2.493-1.653-1.743-2.987l6.518-11.59zM10 13a1 1 0 100-2 1 1 0 000 2zm-.75-6.25a.75.75 0 011.5 0v3.5a.75.75 0 01-1.5 0v-3.5z"
                            clip-rule="evenodd" />
                    </svg>
                </div>
                <!-- Green checkmark (resolved) -->
                <div title="Artist known">
                    <svg class="status-resolved hidden w-5 h-5 text-green-500" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd"
                            d="M16.704 5.29a1 1 0 010 1.415l-7.004 7a1 1 0 01-1.414 0l-3.5-3.5a1 1 0 111.414-1.414l2.793 2.792 6.296-6.293a1 1 0 011.415 0z"
                            clip-rule="evenodd" />
                    </svg>
                </div>
            </div>
            <div class="relative flex-1">
                <input type="text"
                    class="artist-name-input ${styles.editorInputBox} w-full pr-8"
                    autocomplete="off"
                    required
                    placeholder="Search for an artist...">
               <button type="button"
                       class="artist-dropdown-toggle absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 hover:text-slate-200"
                       tabindex="-1"
                       aria-label="Show artist options">
                    <svg class="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd"
                              d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.24 4.25a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z"
                              clip-rule="evenodd" />
                    </svg>
                </button>
                <input type="hidden" class="artist-id-input">
                <input type="hidden" class="artist-mbid-input">

                 <ul class="artist-dropdown hidden absolute left-0 right-0 top-full mt-1 z-20
                       bg-slate-800 border border-slate-700 rounded-lg shadow-lg
                       max-h-56 overflow-y-auto">
                </ul>
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
        const musicBrainzIdInput = row.querySelector(".artist-mbid-input");

        // If the user writes in the name box, clear out the ID fields, as they no longer match
        nameInput.addEventListener("input", () => {
            idInput.value = "";
            musicBrainzIdInput.value = "";
            clearReleaseIds();
            updateArtistStatus(row);
            notifyChanged();
        });

        const boxId = nextArtistBoxId++;
        row.dataset.boxId = boxId;
        attachArtistAutocomplete(boxId, row);

        updateArtistStatus(row);

        row.querySelector(".remove-artist-btn").addEventListener("click", () => {
            removeArtistRow(boxId, row);
        });

        return row;
    }

    // Looks up the name currently in the row's box and fills in the ID if there's exactly one match
    async function resolveArtist(row) {
        const nameInput = row.querySelector(".artist-name-input");
        const idInput = row.querySelector(".artist-id-input");
        const name = nameInput.value;
        if (name === "")
            return;

        try {
            const response = await backendApi.getCreators([{ name, id: null }]);
            const results = response[name];

            // The user may have edited the box while we were waiting
            if (nameInput.value === name)
                idInput.value = results.length === 1 ? results[0].id : "";
        }
        catch (e) {
            console.error(e);
        }

        updateArtistStatus(row);
        notifyChanged();
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
            notifyChanged();
        });
    }

    function getArtists(root) {
        const rows = root.querySelectorAll(".artist-row");
        return Array.from(rows).map(row => ({
            name: row.querySelector(".artist-name-input").value.trim(),
            id: row.querySelector(".artist-id-input").value || null,
            musicBrainzId: row.querySelector(".artist-mbid-input").value || null,
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
            format: recordFormat.value,
            coverImage: coverImage.src,
            tracks: trackListing.getAllTracks()
        };
    }

    // Format. Anything without an option (e.g. "Unknown") shows as not chosen.
    function updateFormatIcon() {
        const icon = mediaIcons.forFormat(recordFormat.value);
        recordFormatIcon.innerHTML = icon ? mediaIcons.svg(icon, "w-8 h-8") : "";
    }

    function setFormat(format) {
        const hasOption = Array.from(recordFormat.options).some(o => o.value === format && !o.disabled);
        recordFormat.value = hasOption ? format : "";
        updateFormatIcon();
    }

    recordFormat.addEventListener("change", updateFormatIcon);

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

        // The user's own cover wins over one we're still looking for
        cancelCoverLookup();

        const reader = new FileReader();
        reader.onload = () => setCoverImage(reader.result);
        reader.readAsDataURL(file);
    });

    removeCoverBtn.addEventListener("click", () => {
        // TODO: Hook some backend logic here to delete from image store
        cancelCoverLookup();
        clearCoverImage();
    });

    // Status line for lookups that wait on MusicBrainz. The dialog shows it in its footer.
    const lookupStatus = lookupStatusLine.create();

    function showLookupStatus(text, { busy = true, onRetry = null } = {}) {
        lookupStatus.show(text, {
            busy,
            onRetry: onRetry && (async () => {
                await onRetry();
                notifyChanged();
            })
        });
    }

    function hideLookupStatus() {
        lookupStatus.hide();
    }

    function setCoverLoading(loading) {
        coverSpinner.classList.toggle("hidden", !loading);
    }

    function setTracksLoading(loading) {
        trackListSpinner.classList.toggle("hidden", !loading);
    }

    // One album lookup at a time: picking another album cancels the one in flight, so a slow
    // answer can't overwrite a newer choice. The cover has its own controller on top, since
    // uploading or removing a cover by hand should only cancel that part.
    let albumLookup = null;
    let coverLookup = null;

    function cancelCoverLookup() {
        coverLookup?.abort();
        coverLookup = null;
        setCoverLoading(false);
    }

    function cancelAlbumLookup() {
        albumLookup?.abort();
        albumLookup = null;
        cancelCoverLookup();
        setTracksLoading(false);
        hideLookupStatus();
    }

    initArtistRows();

    // Album search
    //
    // Spotify is much better at fuzzy search than MusicBrainz, so the user searches Spotify. The
    // album they pick is then looked up on MusicBrainz, which gives us the track list of the exact
    // release they own and its cover art (it's unclear whether we may store Spotify's images).
    // Without Spotify the user can still fill in the form by hand.
    if (!auth.hasSpotifyToken()) {
        albumSearch.disabled = true;
        albumSearch.placeholder = "Log in to Spotify to search";
    }

    function renderAlbumOption({ imageUrl, title, subtitle }) {
        const image = imageUrl
            ? `<img src="${escapeHtml(imageUrl)}" class="w-12 h-12 rounded object-cover shrink-0">`
            : `<div class="w-12 h-12 rounded shrink-0 bg-slate-200 dark:bg-slate-700"></div>`;

        return `
            <div class="flex items-center gap-4">
                ${image}
                <div>
                    <div class="font-medium">
                        ${escapeHtml(title)}
                    </div>
                    <div class="text-sm text-slate-500">
                        ${escapeHtml(subtitle)}
                    </div>
                </div>
            </div>
        `;
    }

    function setCover(cover) {
        if (cover?.url)
            setCoverImage(cover.url);
        else
            clearCoverImage();
    }

    // Creators are { id, name, musicBrainzId? }. Those without an id are looked up by name.
    async function setArtistRows(creators) {
        clearArtistRows();
        const unresolvedRows = [];
        for (const creator of creators) {
            const row = createArtistRow();
            row.querySelector(".artist-name-input").value = creator.name;
            row.querySelector(".artist-id-input").value = creator.id ?? "";
            row.querySelector(".artist-mbid-input").value = creator.musicBrainzId ?? "";
            updateArtistStatus(row);
            artistRows.append(row);
            if (creator.id == null)
                unresolvedRows.push(row);
        }
        if (creators.length === 0)
            artistRows.append(createArtistRow());
        updateRemoveButtonVisibility(artistRows);
        await Promise.all(unresolvedRows.map(resolveArtist));
    }

    function setDuration(totalSeconds) {
        durationHours.value = "";
        durationMinutes.value = "";
        durationSeconds.value = "";
        if (!(totalSeconds > 0))
            return;

        const parts = util.getPartsFromSeconds(totalSeconds);
        if (parts.hours > 0)
            durationHours.value = parts.hours;
        if (parts.minutes > 0)
            durationMinutes.value = parts.minutes;
        if (parts.seconds > 0)
            durationSeconds.value = parts.seconds;
    }

    function setTracks(tracks, durationSeconds) {
        trackListing.clearAllTracks(trackList);
        for (const track of tracks)
            trackListing.addTrackRow(trackList, track.title, track.discNumber, track.trackNumber, track.durationSeconds);

        setDuration(durationSeconds > 0
            ? durationSeconds
            : tracks.reduce((sum, track) => sum + (track.durationSeconds ?? 0), 0));
    }

    function isAbort(e) {
        return e?.name === "AbortError";
    }

    // Used when MusicBrainz can't give us a track list. Never throws.
    async function getSpotifyTracks(album) {
        try {
            const albumInfo = await spotifyApi.getSpotifyAlbum(album.id);
            return (albumInfo.tracks?.items ?? []).map(track => ({
                title: track.name,
                discNumber: track.disc_number,
                trackNumber: track.track_number,
                durationSeconds: Math.round(track.duration_ms / 1000)
            }));
        }
        catch (e) {
            console.warn("Could not get the track list from Spotify:", e);
            return [];
        }
    }

    // Runs work({ signal, isCurrent, onProgress }) as the album lookup, cancelling the one in flight.
    async function runAlbumLookup(work) {
        cancelAlbumLookup();
        const lookup = new AbortController();
        albumLookup = lookup;
        const isCurrent = () => albumLookup === lookup;
        const onProgress = progress => {
            if (isCurrent())
                showLookupStatus(musicbrainzApi.describeProgress(progress));
        };

        try {
            await work({ signal: lookup.signal, isCurrent, onProgress });
        }
        finally {
            if (isCurrent()) {
                albumLookup = null;
                setTracksLoading(false);
                if (coverLookup === null)
                    setCoverLoading(false);
            }
        }
    }

    async function fallBackToSpotify(album, { isCurrent }, message, onRetry = null) {
        setTracksLoading(true);
        const tracks = await getSpotifyTracks(album);
        if (!isCurrent())
            return;
        setTracksLoading(false);
        setTracks(tracks, 0);
        showLookupStatus(message, { busy: false, onRetry });
    }

    // Fills in the form for an album picked from the Spotify search:
    //  1. Name, artists and year straight from the search result
    //  2. Ask MusicBrainz which releases match (often the CD, LP and digital release)
    //  3. Let the user pick one if there's more than one
    //  4. Get that release's track list and cover, with spinners over both meanwhile
    // MusicBrainz is flaky, so if any of that fails we fall back to Spotify's track list and
    // tell the user in the status line. When MusicBrainz or the Cover Art Archive gave us an
    // error, the status line gets a Retry button that redoes just the step that failed.
    // Never throws.
    async function lookUpAlbum(album) {
        await runAlbumLookup(async lookup => {
            const title = album.name;
            const creators = (album.artists ?? []).map(a => ({ id: null, name: a.name }));

            recordName.value = title ?? "";
            releaseMbid.value = "";
            releaseGroupMbid.value = "";
            recordYear.value = album.release_date?.slice(0, 4) ?? "";
            setFormat("");      // Spotify doesn't know; set from the MusicBrainz release below
            albumSearch.value = [util.concatenateArtists(creators), title].filter(x => x).join(" - ");
            clearCoverImage();
            trackListing.clearAllTracks(trackList);
            setDuration(0);
            await setArtistRows(creators);
            notifyChanged();

            await findRelease(album, lookup);
        });
    }

    // Steps 2 and 3
    async function findRelease(album, lookup) {
        const { signal, isCurrent, onProgress } = lookup;

        setTracksLoading(true);
        setCoverLoading(true);
        showLookupStatus("Looking up the album on MusicBrainz...");

        let releases;
        try {
            releases = await musicbrainzApi.findReleases({
                title: album.name,
                artists: (album.artists ?? []).map(a => a.name),
                releaseDate: toFullDate(album.release_date, album.release_date_precision),
                totalTracks: album.total_tracks,
                releaseType: album.album_type
            }, { signal, onProgress });
        }
        catch (e) {
            if (isAbort(e) || !isCurrent())
                return;
            console.warn("MusicBrainz release lookup failed:", e);
            setCoverLoading(false);
            await fallBackToSpotify(album, lookup,
                "Couldn't reach MusicBrainz, so the track list is from Spotify. You can upload a cover yourself.",
                () => runAlbumLookup(retry => findRelease(album, retry)));
            return;
        }

        if (!isCurrent())
            return;

        if (releases.length === 0) {
            setCoverLoading(false);
            await fallBackToSpotify(album, lookup, "MusicBrainz doesn't know this album, so the track list is from Spotify. You can upload a cover yourself.");
            return;
        }

        // 3. Let the user pick a release
        let release = releases[0];
        if (releases.length > 1) {
            setTracksLoading(false);
            setCoverLoading(false);
            hideLookupStatus();

            release = await confirmRelease.choose(releases);

            if (!isCurrent())
                return;

            if (release === null) {
                await fallBackToSpotify(album, lookup, "No release chosen, so the track list is from Spotify. You can upload a cover yourself.");
                return;
            }
        }

        releaseMbid.value = release.id ?? "";
        releaseGroupMbid.value = release.releaseGroupId ?? "";
        setFormat(musicbrainzApi.toReleaseFormat(release.mediaType));

        // The release's own title and year win over Spotify's, which often has edition info in the
        // title and the year of the remaster. Keep Spotify's where MusicBrainz has none. The duration
        // follows from the release's track list in loadRelease().
        if (release.title?.trim())
            recordName.value = release.title.trim();
        const year = /^\d{4}/.exec(release.date ?? "")?.[0];
        if (year)
            recordYear.value = year;

        // MusicBrainz's artist credit is more reliable than Spotify's, and gives us their ids.
        // artists and artistIds are parallel arrays, one entry per artist.
        if (release.artists?.length > 0) {
            await setArtistRows(release.artists.map((name, i) => ({
                id: null,
                name,
                musicBrainzId: release.artistIds?.[i] ?? null
            })));
            if (!isCurrent())
                return;
        }
        notifyChanged();

        await loadRelease(album, release, lookup);
    }

    // Step 4: the track list and/or cover of the chosen release
    async function loadRelease(album, release, lookup, { tracks: wantTracks = true, cover: wantCover = true } = {}) {
        const { signal, isCurrent, onProgress } = lookup;

        setTracksLoading(wantTracks);
        setCoverLoading(wantCover);
        showLookupStatus(wantTracks && wantCover ? "Getting the track list and cover art..."
            : wantTracks ? "Getting the track list..."
                : "Getting the cover art...");

        const cover = wantCover ? new AbortController() : null;
        if (cover)
            coverLookup = cover;

        const [tracksResult, coverResult] = await Promise.allSettled([
            wantTracks ? musicbrainzApi.getReleaseTracks(release.id, { signal, onProgress }) : null,
            wantCover ? artwork.getReleaseCover(release.id, {
                signal: AbortSignal.any([signal, cover.signal]),
                onProgress
            }) : null
        ]);

        if (!isCurrent())
            return;

        const problems = [];
        let coverFailed = false;

        if (cover && coverLookup === cover) {
            coverLookup = null;
            setCoverLoading(false);

            if (coverResult.status === "fulfilled") {
                setCover(coverResult.value);
                if (!coverResult.value?.url)
                    problems.push("No cover art found for this release, you can upload one yourself.");
            }
            else if (!isAbort(coverResult.reason)) {
                console.warn("Could not load cover:", coverResult.reason);
                problems.push("Couldn't get the cover art, you can upload one yourself.");
                coverFailed = true;
            }
        }

        const tracksFailed = wantTracks && tracksResult.status === "rejected";
        if (tracksFailed)
            console.warn("Could not get the track list from MusicBrainz:", tracksResult.reason);

        // Don't fetch a cover again if the user has uploaded one in the meantime
        const retry = (tracksFailed || coverFailed)
            ? () => runAlbumLookup(again => loadRelease(album, release, again, {
                tracks: tracksFailed,
                cover: coverFailed && !coverImage.getAttribute("src")
            }))
            : null;

        if (wantTracks) {
            if (tracksResult.status === "fulfilled" && tracksResult.value?.length > 0) {
                setTracksLoading(false);
                setTracks(tracksResult.value);
            }
            else {
                await fallBackToSpotify(album, lookup,
                    ["Couldn't get the track list from MusicBrainz, so it's from Spotify.", ...problems].join(" "),
                    retry);
                return;
            }
        }

        if (problems.length > 0)
            showLookupStatus(problems.join(" "), { busy: false, onRetry: retry });
        else
            hideLookupStatus();
    }

    autocompletes.set(nextArtistBoxId++, new Autocomplete({
        input: albumSearch,
        maxVisibleItems: 5,
        async search(q) {
            return await spotifyApi.searchOnSpotify(q, "album", 10, 0, (b) => {
                return b.albums.total > 0 ? b.albums.items : [];
            });
        },
        renderItem(album) {
            return renderAlbumOption({
                imageUrl: album.images?.at(-1)?.url,
                title: album.name,
                subtitle: `${util.concatenateArtists(album.artists ?? [])} • ${album.release_date ?? ""}`
            });
        },
        async onSelected(album) {
            await lookUpAlbum(album);
            notifyChanged();
        }
    }));

    // Track listing
    root.querySelector("#addTrackBtn").addEventListener("click", () => {
        trackListing.addEmptyTrackRow(trackList);
    });

    function disposeAutocompletes() {
        for (const autocomplete of autocompletes.values())
            autocomplete.dispose?.();
        autocompletes.clear();
    }

    const baseline = JSON.stringify(getState());

    async function save() {
        const duration = [
            durationHours.value,
            durationMinutes.value,
            durationSeconds.value
        ].map(x => x.padStart(2, "0")).join(":");

        const newRecordRequest = {
            summary: {
                name: recordName.value,
                artists: getArtists(root),
                year: recordYear.value,
                imageUrl: coverImage.src,
                spotifyLink: "",    // Still expected by the backend
            },
            durationSeconds: util.parseDurationToSeconds(duration),
            format: recordFormat.value || "Unknown",
            tracks: trackListing.getAllTracks()
        };

        // TODO: Maybe it'd be nice to catch if we added something already had with another message
        await backendApi.addNewRelease(newRecordRequest);
        return recordName.value;
    }

    return {
        element: root,
        footerStatus: lookupStatus.element,
        noun: "record",
        getValidity,
        validate: () => albumForm.reportValidity(),
        isDirty: () => JSON.stringify(getState()) !== baseline,
        save,
        dispose() {
            cancelAlbumLookup();
            disposeAutocompletes();
        }
    };
}