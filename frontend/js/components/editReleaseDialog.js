import * as styles from "../styles.js";
import * as Modal from "./modal.js";
import * as Toast from "./toast.js";
import * as backendApi from "../api/backend.js";
import * as musicbrainzApi from "../api/musicbrainz.js";
import * as spotifyApi from "../api/spotify.js";
import * as artwork from "../api/artwork.js";
import * as confirmRelease from "./confirmRelease.js";
import * as lookupStatusLine from "./common/lookupStatus.js";
import * as mediaIcons from "./common/mediaIcons.js";
import { createCountryPicker } from "./common/countryPicker.js";
import { createCreatorPicker, createRemoveButton } from "./creatorPicker.js";
import { namesMatch } from "../util.js";

const MUSICBRAINZ_RELEASE_URL = "https://musicbrainz.org/release/";

function isAbort(e) {
    return e?.name === "AbortError";
}

// "2009" from "2009-08-24", or "" for none
function yearOf(date) {
    return /^\d{4}/.exec(date ?? "")?.[0] ?? "";
}

// A year typed into a box: null for none, NaN for something that isn't a year
function parseYear(text) {
    const trimmed = text.trim();
    if (trimmed === "")
        return null;
    return /^\d{1,4}$/.test(trimmed) && Number(trimmed) >= 1 ? Number(trimmed) : NaN;
}

/**
 * Shows the "Edit record details" dialog for a release (a ReleaseDetails). Resolves with its
 * ReleaseDetails as saved, or null if the user cancelled.
 *
 * A release linked to MusicBrainz only lets its Spotify link change: everything else is
 * MusicBrainz's. One that isn't can be changed freely, or filled in from a MusicBrainz release (or
 * one of a release group's), which then links it. "Unlink" makes a linked release editable again.
 * Filling in replaces the track list and cover as well when saved.
 */
export function show(record) {
    return new Promise(resolve => {
        let resolved = false;
        function resolveOnce(value) {
            if (resolved)
                return;
            resolved = true;
            cancelLookup();
            countryPicker.dispose();
            for (const row of artistRows)
                row.picker.dispose();
            resolve(value);
        }

        const root = document.createElement("div");
        root.innerHTML = `
            <form class="space-y-4" novalidate>
                <div>
                    <label for="editReleaseTitle" class="${styles.addRecordInputBoxLabel}">Name</label>
                    <input id="editReleaseTitle" type="text" required maxlength="500" autocomplete="off"
                           class="${styles.editorInputBox} w-full">
                </div>

                <div>
                    <div class="flex gap-2">
                        <span class="flex-1 ${styles.addRecordInputBoxLabel}">Artist(s)</span>
                        <span class="w-48 ${styles.addRecordInputBoxLabel}">Credited as</span>
                        <!-- Room for the rows' remove buttons -->
                        <span class="w-6 shrink-0"></span>
                    </div>
                    <div id="editReleaseArtists" class="space-y-2"></div>
                    <button type="button" id="editReleaseAddArtist"
                            class="mt-2 text-sm text-blue-500 hover:text-blue-400">
                        + Add artist
                    </button>
                </div>

                <div class="flex flex-wrap gap-4">
                    <div class="flex-1 min-w-36">
                        <label for="editReleaseFormat" class="${styles.addRecordInputBoxLabel}">Type</label>
                        <div class="flex items-center gap-2">
                            <!-- Values are the backend's ReleaseFormat names. The empty, hidden first option
                                 means "not known" (sent as Unknown). -->
                            <select id="editReleaseFormat" class="${styles.editorInputBox} flex-1 min-w-0">
                                <option value="" disabled hidden>Select...</option>
                                <option value="Cd">CD</option>
                                <option value="Vinyl">Vinyl</option>
                                <option value="Cassette">Cassette</option>
                                <option value="Digital">Digital</option>
                                <option value="Other">Other</option>
                            </select>
                            <span id="editReleaseFormatIcon" class="shrink-0 w-8 h-8 text-slate-400 dark:text-slate-500"
                                  aria-hidden="true"></span>
                        </div>
                    </div>

                    <div class="w-24">
                        <label for="editReleaseYear" class="${styles.addRecordInputBoxLabel}">Year</label>
                        <input id="editReleaseYear" type="text" inputmode="numeric" maxlength="4" placeholder="YYYY"
                               title="The year this release came out, e.g. of the remaster"
                               class="${styles.editorInputBox}">
                    </div>

                    <div class="w-28">
                        <label for="editReleaseFirstYear" class="${styles.addRecordInputBoxLabel}">First released</label>
                        <input id="editReleaseFirstYear" type="text" inputmode="numeric" maxlength="4" placeholder="YYYY"
                               title="The year the album first came out"
                               class="${styles.editorInputBox}">
                    </div>

                    <div class="w-56 max-w-full">
                        <label for="editReleaseCountry" class="${styles.addRecordInputBoxLabel}">Country</label>
                        <div id="editReleaseCountrySlot"></div>
                    </div>
                </div>

                <div>
                    <div class="flex items-baseline justify-between gap-2">
                        <label for="editReleaseMusicBrainz" class="${styles.addRecordInputBoxLabel}">MusicBrainz link</label>
                        <button type="button" id="editReleaseFill"
                                class="hidden text-sm text-blue-500 hover:text-blue-400">
                            Fill from MusicBrainz
                        </button>
                        <button type="button" id="editReleaseUnlink"
                                class="hidden text-sm text-red-500 hover:text-red-400">
                            Unlink
                        </button>
                    </div>
                    <input id="editReleaseMusicBrainz" type="url" autocomplete="off"
                           placeholder="${MUSICBRAINZ_RELEASE_URL}... or a release group's link"
                           class="${styles.editorInputBox} w-full">
                </div>

                <div>
                    <label for="editReleaseSpotify" class="${styles.addRecordInputBoxLabel}">Spotify link</label>
                    <input id="editReleaseSpotify" type="url" autocomplete="off"
                           placeholder="https://open.spotify.com/album/..."
                           class="${styles.editorInputBox} w-full">
                </div>

                <p id="editReleaseLockedNote" class="hidden text-center text-sm italic text-slate-500 dark:text-slate-400">
                    Release info cannot be changed, as release is linked to a MusicBrainz release.
                </p>
            </form>
        `;

        const titleInput = root.querySelector("#editReleaseTitle");
        const artistsElement = root.querySelector("#editReleaseArtists");
        const addArtistButton = root.querySelector("#editReleaseAddArtist");
        const formatSelect = root.querySelector("#editReleaseFormat");
        const formatIcon = root.querySelector("#editReleaseFormatIcon");
        const yearInput = root.querySelector("#editReleaseYear");
        const firstYearInput = root.querySelector("#editReleaseFirstYear");
        const musicBrainzInput = root.querySelector("#editReleaseMusicBrainz");
        const fillButton = root.querySelector("#editReleaseFill");
        const unlinkButton = root.querySelector("#editReleaseUnlink");
        const spotifyInput = root.querySelector("#editReleaseSpotify");
        const lockedNote = root.querySelector("#editReleaseLockedNote");

        const countryPicker = createCountryPicker({ id: "editReleaseCountry", onChange: () => updateSaveButton() });
        root.querySelector("#editReleaseCountrySlot").replaceWith(countryPicker.element);

        // Status for MusicBrainz lookups, shown in the dialog's footer
        const status = lookupStatusLine.create();

        // --- What isn't in a field ---

        // The MusicBrainz release this is linked to, or null. Linked means locked.
        let linkedMbid = record.externalIds?.MusicBrainz ?? null;
        let releaseGroupId = record.externalIds?.MusicBrainzReleaseGroup ?? null;
        let barcode = record.barcode ?? null;
        let durationSeconds = record.durationSeconds ?? null;

        // From filling in from MusicBrainz, for Save: null keeps what the release has
        let filledTracks = null;
        let filledCover = null;

        // Credits other than the primary ones (none for records yet) are kept as they are
        const otherCredits = (record.credits ?? [])
            .filter(x => x.role !== "Primary")
            .map(x => ({
                creatorId: x.creatorId,
                role: x.role,
                joinPhrase: x.joinPhrase ?? "",
                characterName: x.characterName ?? null,
                creditedName: x.creditedName ?? null
            }));

        // --- Artist rows ---

        // { element, picker, creditedInput, removeButton, joinPhrase }
        const artistRows = [];

        function addArtistRow({ name = "", id = null, musicBrainzId = null, creditedName = "", joinPhrase = null } = {}) {
            const element = document.createElement("div");
            element.className = "group flex items-center gap-2";

            const picker = createCreatorPicker({
                placeholder: "Search for an artist...",
                onChange: () => {
                    updateLockedState();
                    updateSaveButton();
                }
            });
            picker.element.classList.add("flex-1", "min-w-0");
            picker.setValue({ name, id, musicBrainzId });

            const creditedInput = document.createElement("input");
            creditedInput.type = "text";
            creditedInput.maxLength = 300;
            creditedInput.autocomplete = "off";
            creditedInput.placeholder = "Same as the name";
            creditedInput.title = "The name this release credits the artist as, if it isn't their own";
            creditedInput.className = styles.editorInputBox;
            creditedInput.value = creditedName ?? "";

            // The input box style is full width, so the column's width is the wrapper's
            const creditedColumn = document.createElement("div");
            creditedColumn.className = "w-48 shrink-0";
            creditedColumn.append(creditedInput);

            const removeButton = createRemoveButton("Remove artist");
            const row = { element, picker, creditedInput, removeButton, joinPhrase };
            removeButton.addEventListener("click", () => {
                picker.dispose();
                element.remove();
                artistRows.splice(artistRows.indexOf(row), 1);
                updateLockedState();
                updateSaveButton();
            });

            element.append(picker.element, creditedColumn, removeButton);
            artistsElement.append(element);
            artistRows.push(row);
            return row;
        }

        function clearArtistRows() {
            for (const row of artistRows)
                row.picker.dispose();
            artistRows.length = 0;
            artistsElement.replaceChildren();
        }

        addArtistButton.addEventListener("click", () => {
            addArtistRow().picker.focus();
            updateLockedState();
            updateSaveButton();
        });

        // The artists as the dialog has them now: { name, creditedName, musicBrainzId }
        function getCurrentArtists() {
            return artistRows
                .map(row => ({ ...row.picker.getValue(), creditedName: row.creditedInput.value.trim() }))
                .filter(x => x.name !== "");
        }

        // --- Format ---

        function updateFormatIcon() {
            const icon = mediaIcons.forFormat(formatSelect.value);
            formatIcon.innerHTML = icon ? mediaIcons.svg(icon, "w-8 h-8") : "";
        }

        // An unknown format (or one without an option) shows as not chosen
        function setFormat(format) {
            const hasOption = Array.from(formatSelect.options).some(o => o.value === format && !o.disabled);
            formatSelect.value = hasOption ? format : "";
            updateFormatIcon();
        }

        formatSelect.addEventListener("change", updateFormatIcon);

        // --- Locking ---

        // The shared input style greys it out while disabled
        function setDisabled(input, disabled) {
            input.disabled = disabled;
        }

        // Linked to MusicBrainz: only the Spotify link can change. An artist the release credits who
        // isn't in the collection yet can still be picked (or added), or it couldn't be saved.
        function updateLockedState() {
            const locked = linkedMbid !== null;

            for (const input of [titleInput, formatSelect, yearInput, firstYearInput])
                setDisabled(input, locked);
            formatIcon.classList.toggle("opacity-50", locked);
            countryPicker.setDisabled(locked);

            // Read-only rather than disabled, so the link can still be selected and copied. Greyed out
            // here, as the shared style only does that for disabled inputs.
            musicBrainzInput.readOnly = locked;
            musicBrainzInput.classList.toggle("opacity-50", locked);
            unlinkButton.classList.toggle("hidden", !locked);
            fillButton.classList.toggle("hidden", locked || !musicbrainzApi.parseReleaseMbid(musicBrainzInput.value));

            for (const row of artistRows) {
                row.picker.setDisabled(locked && row.picker.isResolved());
                setDisabled(row.creditedInput, locked);
                // Invisible rather than hidden, so it keeps its room and the columns stay under their labels
                row.removeButton.classList.toggle("invisible", locked || artistRows.length === 1);
            }
            addArtistButton.classList.toggle("hidden", locked);

            lockedNote.classList.toggle("hidden", !locked);
        }

        // --- Filling in from MusicBrainz ---

        // One lookup at a time: starting another (or closing) cancels the one in flight
        let lookup = null;

        function cancelLookup() {
            lookup?.abort();
            lookup = null;
            status.hide();
        }

        async function runLookup(work) {
            cancelLookup();
            const controller = new AbortController();
            lookup = controller;
            const isCurrent = () => lookup === controller;
            const onProgress = progress => {
                if (isCurrent())
                    status.show(musicbrainzApi.describeProgress(progress));
            };

            try {
                await work({ signal: controller.signal, isCurrent, onProgress });
            }
            finally {
                if (isCurrent())
                    lookup = null;
            }
        }

        // Whether the release seems to be by (one of) the artists the dialog has now: by their
        // MusicBrainz id where both are known, else by name, the artist's own or the credited one
        function isBySameArtist(release, artists) {
            return (release.artists ?? []).some((name, i) => {
                const artistMbid = release.artistIds?.[i];
                return artists.some(artist =>
                    (artistMbid && artist.musicBrainzId === artistMbid) ||
                    namesMatch(name, artist.name) ||
                    namesMatch(name, artist.creditedName));
            });
        }

        // Fills in the dialog from the release an mbid stands for, or one of a release group's that
        // the user picks, and links it. Nothing changes if it seems to be by another artist, or its
        // track list can't be had. Never throws.
        async function fillFromMusicBrainz(mbid, { signal, isCurrent, onProgress }) {
            status.show("Looking up the release on MusicBrainz...");

            let releases;
            try {
                releases = await musicbrainzApi.lookUpReleases(mbid, { signal, onProgress });
            }
            catch (e) {
                if (isAbort(e) || !isCurrent())
                    return;
                console.warn("MusicBrainz release lookup failed:", e);
                status.show("Couldn't reach MusicBrainz.", {
                    busy: false,
                    onRetry: () => runLookup(again => fillFromMusicBrainz(mbid, again))
                });
                return;
            }

            if (!isCurrent())
                return;

            if (releases.length === 0) {
                status.show("MusicBrainz doesn't know a release or release group with that link.", { busy: false });
                return;
            }

            let release = releases[0];
            if (releases.length > 1) {
                status.hide();
                release = await confirmRelease.choose(releases);
                if (!isCurrent())
                    return;
                if (release === null) {
                    status.show("No release chosen.", { busy: false });
                    return;
                }
            }

            const currentArtists = getCurrentArtists();
            if (currentArtists.length > 0 && !isBySameArtist(release, currentArtists)) {
                const artistLine = currentArtists.map(x => x.name).join(" & ");
                status.show(`Release "${release.title}" does not seem to be by ${artistLine}.`, { busy: false });
                return;
            }

            status.show("Getting the track list and cover art...");
            const [tracksResult, coverResult] = await Promise.allSettled([
                musicbrainzApi.getReleaseTracks(release.id, { signal, onProgress }),
                artwork.getReleaseCover(release.id, { signal, onProgress })
            ]);

            if (!isCurrent())
                return;

            // Linking it without its tracks would keep the old ones under MusicBrainz's name
            if (tracksResult.status === "rejected" || !(tracksResult.value?.length > 0)) {
                if (tracksResult.status === "rejected" && isAbort(tracksResult.reason))
                    return;
                console.warn("Could not get the track list from MusicBrainz:", tracksResult.reason);
                status.show("Couldn't get the release's track list from MusicBrainz, so nothing was filled in.", {
                    busy: false,
                    onRetry: () => runLookup(again => fillFromMusicBrainz(release.id, again))
                });
                return;
            }

            // The artists as the release credits them, each the user's creator if they have them:
            // the one already in the dialog for the same MusicBrainz artist, or else looked up
            const before = artistRows.map(row => row.picker.getValue());
            clearArtistRows();
            const resolving = (release.artists ?? []).map((name, i) => {
                const artistMbid = release.artistIds?.[i] || null;
                const known = artistMbid ? before.find(x => x.id && x.musicBrainzId === artistMbid) : null;
                const row = addArtistRow({
                    name: known?.name ?? name,
                    id: known?.id ?? null,
                    musicBrainzId: known?.musicBrainzId ?? null,
                    creditedName: name,
                    joinPhrase: release.joinPhrases?.[i] ?? ""
                });
                return known ? null : row.picker.resolve({ musicBrainzId: artistMbid });
            });

            titleInput.value = release.title?.trim() || titleInput.value;
            setFormat(musicbrainzApi.toReleaseFormat(release.mediaType));
            yearInput.value = yearOf(release.date) || yearInput.value;
            firstYearInput.value = yearOf(release.firstReleaseDate);
            countryPicker.setCode(release.country);
            barcode = release.barcode ?? null;
            releaseGroupId = release.releaseGroupId ?? null;

            filledTracks = tracksResult.value.map(x => ({
                discNumber: x.discNumber,
                trackNumber: x.trackNumber,
                title: x.title,
                durationSeconds: x.durationSeconds ?? null
            }));
            durationSeconds = filledTracks.some(x => x.durationSeconds != null)
                ? filledTracks.reduce((sum, x) => sum + (x.durationSeconds ?? 0), 0)
                : null;

            // No cover isn't a reason to stop: the release keeps the images it has
            const hasCover = coverResult.status === "fulfilled" && !!coverResult.value?.url;
            filledCover = hasCover ? { source: "CoverArtArchive", externalId: release.id } : null;

            linkedMbid = release.id;
            musicBrainzInput.value = `${MUSICBRAINZ_RELEASE_URL}${release.id}`;

            await Promise.all(resolving);
            if (!isCurrent())
                return;

            updateLockedState();
            updateSaveButton();

            const unresolved = artistRows.some(row => !row.picker.isResolved());
            const notes = [
                hasCover ? null : "No cover art was found, so the current cover stays.",
                unresolved ? "Add the artists who aren't in your collection yet before saving." : null
            ].filter(x => x);
            if (notes.length > 0)
                status.show(notes.join(" "), { busy: false });
            else
                status.hide();
        }

        fillButton.addEventListener("click", () => {
            const mbid = musicbrainzApi.parseReleaseMbid(musicBrainzInput.value);
            if (mbid)
                runLookup(work => fillFromMusicBrainz(mbid, work));
        });

        musicBrainzInput.addEventListener("input", () => {
            updateLockedState();
            updateSaveButton();
        });

        unlinkButton.addEventListener("click", async () => {
            if (!await Modal.confirm("Unlink from MusicBrainz?",
                "The release will no longer be linked to MusicBrainz, and you can change its details yourself. Nothing changes until you save.",
                { yesText: "Unlink", noText: "Cancel" })) {
                return;
            }

            cancelLookup();
            linkedMbid = null;
            releaseGroupId = null;
            musicBrainzInput.value = "";
            updateLockedState();
            updateSaveButton();
        });

        // --- Validity and saving ---

        // Why it can't be saved yet, or null if it can
        function getProblem() {
            if (titleInput.value.trim() === "")
                return "The release needs a name";
            if (artistRows.length === 0 || artistRows.some(row => row.picker.getValue().name === ""))
                return "The release needs at least one artist, and every artist row a name";
            if (artistRows.some(row => !row.picker.isResolved()))
                return "Every artist must be one from your collection: pick them from the list, or add them";

            const year = parseYear(yearInput.value);
            const firstYear = parseYear(firstYearInput.value);
            if (Number.isNaN(year) || Number.isNaN(firstYear))
                return "Years are written like 1977";
            if (year !== null && firstYear !== null && firstYear > year)
                return "It can't have been first released after this release came out";

            if (linkedMbid === null && musicBrainzInput.value.trim() !== "")
                return "Fill in from the MusicBrainz link, or clear it";
            if (spotifyInput.value.trim() !== "" && !spotifyApi.parseSpotifyAlbumId(spotifyInput.value))
                return "The Spotify link must be an album's link, like https://open.spotify.com/album/...";
            return null;
        }

        let saving = false;
        let saveButton = null;
        function updateSaveButton() {
            // Not there yet while the dialog is being set up
            if (!saveButton)
                return;
            const problem = getProblem();
            const enabled = problem === null && !saving;
            saveButton.disabled = !enabled;
            saveButton.classList.toggle("opacity-50", !enabled);
            saveButton.classList.toggle("cursor-not-allowed", !enabled);
            saveButton.title = saving ? "Saving..." : problem ?? "Save the changes";
        }

        for (const input of [titleInput, yearInput, firstYearInput, spotifyInput])
            input.addEventListener("input", updateSaveButton);
        root.addEventListener("input", e => {
            if (e.target.closest?.("#editReleaseArtists"))
                updateSaveButton();
        });

        function buildRequest() {
            const primary = artistRows.map((row, i) => ({
                creatorId: Number(row.picker.getValue().id),
                role: "Primary",
                // Nothing follows the last name, and an artist added here is joined with " & "
                joinPhrase: i === artistRows.length - 1 ? "" : (row.joinPhrase || " & "),
                characterName: null,
                // The backend leaves it out when it's just the artist's own name
                creditedName: row.creditedInput.value.trim() || null
            }));

            return {
                title: titleInput.value.trim(),
                format: formatSelect.value || "Unknown",
                releaseYear: parseYear(yearInput.value),
                firstReleaseYear: parseYear(firstYearInput.value),
                durationSeconds,
                barcode,
                country: countryPicker.getCode() || null,
                musicBrainzReleaseId: linkedMbid,
                musicBrainzReleaseGroupId: releaseGroupId,
                spotifyAlbumId: spotifyApi.parseSpotifyAlbumId(spotifyInput.value),
                credits: [...primary, ...otherCredits],
                tracks: filledTracks,
                cover: filledCover
            };
        }

        // True when saved, false when it failed, in which case the dialog stays open
        async function save() {
            let updated;
            try {
                updated = await backendApi.updateRelease(record.summary.id, buildRequest());
            }
            catch (e) {
                Toast.error(e.message, "Saving the release failed");
                return false;
            }

            Toast.success(`"${updated.summary.title}" was saved`, "Release saved");
            resolveOnce(updated);
            return true;
        }

        // For telling whether anything changed when cancelling
        function getState() {
            return JSON.stringify({
                title: titleInput.value,
                artists: artistRows.map(row => [row.picker.getValue().id, row.picker.getValue().name, row.creditedInput.value]),
                format: formatSelect.value,
                year: yearInput.value,
                firstYear: firstYearInput.value,
                country: countryPicker.getCode(),
                musicBrainz: musicBrainzInput.value,
                spotify: spotifyInput.value,
                linkedMbid,
                filled: filledTracks !== null
            });
        }

        // --- Starting state ---

        titleInput.value = record.summary.title ?? "";
        for (const credit of (record.credits ?? []).filter(x => x.role === "Primary")) {
            addArtistRow({
                name: credit.name,
                id: credit.creatorId,
                musicBrainzId: credit.musicBrainzId ?? null,
                creditedName: credit.creditedName ?? "",
                joinPhrase: credit.joinPhrase ?? ""
            });
        }
        if (artistRows.length === 0)
            addArtistRow();
        setFormat(record.format);
        yearInput.value = record.releaseYear ?? "";
        firstYearInput.value = record.firstReleaseYear ?? "";
        countryPicker.setCode(record.country);
        musicBrainzInput.value = linkedMbid ? `${MUSICBRAINZ_RELEASE_URL}${linkedMbid}` : "";
        spotifyInput.value = record.externalIds?.Spotify ? spotifyApi.spotifyAlbumUrl(record.externalIds.Spotify) : "";
        updateLockedState();

        const baseline = getState();

        const modalHandle = Modal.show({
            title: "Edit record details",
            content: root,
            footerContent: status.element,
            maxWidthClass: "max-w-2xl",
            buttons: [
                {
                    "type": "save",
                    "action": async () => {
                        if (getProblem() !== null || saving)
                            return false;

                        saving = true;
                        updateSaveButton();
                        try {
                            return await save();
                        }
                        finally {
                            saving = false;
                            updateSaveButton();
                        }
                    }
                },
                {
                    "type": "close",
                    "text": "cancel",
                    "action": async () => {
                        if (getState() === baseline || await Modal.confirm("Unsaved changes", "Do you want to discard unsaved changes?")) {
                            resolveOnce(null);
                            return true;
                        }
                        return false;
                    }
                }
            ]
        });

        // Escape, clicking outside and the header's (x) close the modal without the buttons' actions,
        // which would otherwise leave the promise unresolved
        const originalClose = modalHandle.close;
        modalHandle.close = () => {
            resolveOnce(null);
            originalClose();
        };

        // Direct children only: the status line in the footer has a Retry button of its own
        saveButton = modalHandle.dialog.querySelectorAll("#modalFooter > button")[0];
        updateSaveButton();
    });
}
