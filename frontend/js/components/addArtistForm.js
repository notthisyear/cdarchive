import * as styles from "../styles.js"
import * as Modal from "./modal.js";
import * as Toast from "./toast.js";
import * as imageUploader from "../components/imageUploader.js";
import * as auth from "../auth.js"
import * as backendApi from "../api/backend.js";
import * as spotifyApi from "../api/spotify.js";
import * as artwork from "../api/artwork.js";
import * as musicbrainzApi from "../api/musicbrainz.js";
import * as confirmArtist from "./confirmArtist.js";
import * as lookupStatusLine from "./common/lookupStatus.js";
import * as mediaIcons from "./common/mediaIcons.js";
import * as countries from "./common/countries.js";
import * as artworkImage from "./common/artworkImage.js";
import { createFlagElement } from "./common/countryFlag.js";

import { Autocomplete } from "./autocomplete.js";

function isAbort(e) {
    return e?.name === "AbortError";
}

const MUSICBRAINZ_ARTIST_URL = "https://musicbrainz.org/artist/";

// The artist's mbid in a MusicBrainz URL like "https://musicbrainz.org/artist/<mbid>", or null
function mbidFromMusicBrainzUrl(url) {
    return /musicbrainz\.org\/artist\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/i
        .exec(url ?? "")?.[1].toLowerCase() ?? null;
}

/**
 * Shows the "Add Artist" dialog. Resolves with { name, id, musicBrainzId } of the added artist,
 * or null if the user cancelled.
 *
 * With a musicBrainzId (e.g. from a MusicBrainz release's artist credit) the name is filled in
 * and the artist's image is fetched straight away. Without one, initialName is searched for on
 * Spotify, and the artist the user picks is then looked up on MusicBrainz for its type and image.
 * creatorType preselects the Type dropdown when it's one of its options.
 */
export function show(initialName = "", { musicBrainzId = null, creatorType = null } = {}) {
    return new Promise(resolve => {
        let resolved = false;
        function resolveOnce(value) {
            if (resolved)
                return;
            resolved = true;
            cancelLookups();
            countryAutocomplete.dispose();
            resolve(value);
        }

        const root = document.createElement("div");
        root.innerHTML = `
            <div id="newArtistDialog"
                class="flex flex-col max-h-[50vh] overflow-hidden space-y-6">
                    <!-- Search section -->
                    <section class="space-y-4 shrink-0">
                        <div class="relative">
                            <input id="artistSearch"
                                    class="${styles.editorInputBox} w-full"
                                    type="text"
                                    placeholder="Search Spotify..." />
                        </div>
                    </section>

                    <!-- Artist info-->
                    <section class="min-h-0 grid-cols-1 grid-rows-2 lg:grid-cols-2 lg:grid-rows-1 border-t border-slate-200 dark:border-slate-700 py-4">
                        <div class="flex flex-col min-h-0 overflow-y-auto gap-4">
                            <form id="artistForm"
                                    class="space-y-4 shrink-0"
                                    novalidate>

                                <!-- Fields on the left, image centered vertically beside them -->
                                <div class="flex items-center gap-4">
                                    <div class="flex-1 flex flex-col gap-4">
                                        <!-- Name and country side by side; wraps when the column is narrow -->
                                        <div class="flex flex-wrap gap-4">
                                            <div class="flex-1 min-w-40">
                                                <label for="artistName"
                                                       class="${styles.addRecordInputBoxLabel}">
                                                    Name
                                                </label>
                                                <input id="artistName"
                                                        name="name"
                                                        type="text"
                                                        required
                                                        autocomplete="off"
                                                        class="${styles.editorInputBox}"
                                                        placeholder="Artist name" />
                                                <!-- The artist on MusicBrainz, if known -->
                                                <input type="hidden" id="artistMbid">
                                            </div>

                                            <div class="w-56 max-w-full">
                                                <label for="artistCountry"
                                                       class="${styles.addRecordInputBoxLabel}">
                                                    Country
                                                </label>
                                                <div class="flex items-center gap-2">
                                                    <!-- Free text to filter the list, but a country must be picked from it.
                                                         The picked country's code goes in the hidden field. -->
                                                    <div class="relative flex-1 min-w-0">
                                                        <input id="artistCountry"
                                                                type="text"
                                                                required
                                                                autocomplete="off"
                                                                class="${styles.editorInputBox} w-full pr-8"
                                                                placeholder="Search..." />
                                                        <button type="button"
                                                                id="artistCountryToggle"
                                                                class="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 hover:text-slate-200"
                                                                tabindex="-1"
                                                                aria-label="Show countries">
                                                            <svg class="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                                                                <path fill-rule="evenodd"
                                                                      d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.24 4.25a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z"
                                                                      clip-rule="evenodd" />
                                                            </svg>
                                                        </button>
                                                    </div>
                                                    <input type="hidden" id="artistCountryCode">
                                                    <!-- Flag of the picked country; keeps its width while empty -->
                                                    <span id="artistCountryFlag"
                                                          class="shrink-0 w-8 flex items-center justify-center"
                                                          aria-hidden="true"></span>
                                                </div>
                                            </div>
                                        </div>

                                        <div>
                                            <label for="artistType"
                                                   class="${styles.addRecordInputBoxLabel}">
                                                Type
                                            </label>
                                            <!-- Values are the backend's CreatorType names. The empty, hidden
                                                 first option means "not chosen yet", which can't be saved. -->
                                            <div class="flex items-center gap-2">
                                                <select id="artistType"
                                                        name="type"
                                                        class="${styles.editorInputBox} flex-1 min-w-0">
                                                    <option value="" disabled hidden selected>Select...</option>
                                                    <option value="Person">Person</option>
                                                    <option value="Group">Group</option>
                                                    <option value="Other">Other</option>
                                                </select>
                                                <!-- Icon for the selected type; keeps its width while empty -->
                                                <span id="artistTypeIcon"
                                                      class="shrink-0 w-8 h-8 text-slate-400 dark:text-slate-500"
                                                      aria-hidden="true"></span>
                                            </div>
                                        </div>

                                        <div>
                                            <label for="artistMusicBrainzUrl"
                                                   class="${styles.addRecordInputBoxLabel}">
                                                MusicBrainz URL (optional)
                                            </label>
                                            <input id="artistMusicBrainzUrl"
                                                name="musicBrainzUrl"
                                                type="url"
                                                class="${styles.editorInputBox} w-full"
                                                placeholder="${MUSICBRAINZ_ARTIST_URL}...">
                                        </div>

                                        <div>
                                            <label for="artistSpotifyUrl"
                                                   class="${styles.addRecordInputBoxLabel}">
                                                Spotify URL (optional)
                                            </label>
                                            <input id="artistSpotifyUrl"
                                                name="artistUrl"
                                                type="url"
                                                class="${styles.editorInputBox} w-full"
                                                placeholder="https://open.spotify.com/artist/...">
                                        </div>
                                    </div>

                                    <!-- Artist image -->
                                    <div id="artistImageSlot" />
                                </div>
                            </form>
                        </div>
                    </section>
                </div>
            `;

        // True while the image shown is one we looked up rather than one the user uploaded
        let imageIsLookedUp = false;

        const artistImageUploader = imageUploader.create({
            placeholderText: "No image selected",
            alt: "Artist image",
            widthClass: "w-full",
            onFileSelected: () => {
                // The user's own image wins over one we're still looking for
                clearLookedUpImageCredit();
                cancelImageLookup();
            },
            onRemove: () => {
                // TODO: Hook some backend logic here to delete from image store
                clearLookedUpImageCredit();
                cancelImageLookup();
            }
        });

        // Who took the looked-up image and under which license, below the image
        const imageCredit = artworkImage.createCreditBox();

        const imageColumn = document.createElement("div");
        imageColumn.className = "shrink-0 w-40 lg:w-1/3 flex flex-col gap-2";
        imageColumn.append(artistImageUploader.element, imageCredit.element);
        root.querySelector("#artistImageSlot").replaceWith(imageColumn);

        // Shows an ArtworkResult from artwork.js together with its credit
        function setLookedUpImage(result) {
            artistImageUploader.setImage(result.url);
            imageCredit.set(result);
            imageIsLookedUp = true;
        }

        function clearLookedUpImageCredit() {
            imageCredit.clear();
            imageIsLookedUp = false;
        }

        // Status for lookups that wait on MusicBrainz, shown in the dialog's footer
        const status = lookupStatusLine.create();

        const artistSearch = root.querySelector("#artistSearch");
        const artistName = root.querySelector("#artistName");
        const artistMbid = root.querySelector("#artistMbid");
        const artistType = root.querySelector("#artistType");
        const artistTypeIcon = root.querySelector("#artistTypeIcon");
        const artistMusicBrainzUrl = root.querySelector("#artistMusicBrainzUrl");
        const artistSpotifyUrl = root.querySelector("#artistSpotifyUrl");

        // The hidden mbid and the MusicBrainz URL field always describe the same artist
        function setMusicBrainzId(mbid) {
            artistMbid.value = mbid ?? "";
            artistMusicBrainzUrl.value = mbid ? `${MUSICBRAINZ_ARTIST_URL}${mbid}` : "";
        }

        // Type. Anything without an option shows as not chosen.
        function updateTypeIcon() {
            const icon = mediaIcons.forCreatorType(artistType.value);
            artistTypeIcon.innerHTML = icon ? mediaIcons.svg(icon, "w-8 h-8") : "";
        }

        function setType(type) {
            const hasOption = Array.from(artistType.options).some(o => o.value === type && !o.disabled);
            artistType.value = hasOption ? type : "";
            updateTypeIcon();
            setConfirmButtonStatus();
        }

        artistType.addEventListener("change", () => {
            updateTypeIcon();
            setConfirmButtonStatus();
        });

        // Country. The text box only filters the list: the country counts once it's picked from
        // it (or its full name is typed), which puts its code in the hidden field.
        const artistCountry = root.querySelector("#artistCountry");
        const artistCountryCode = root.querySelector("#artistCountryCode");
        const artistCountryFlag = root.querySelector("#artistCountryFlag");
        const artistCountryToggle = root.querySelector("#artistCountryToggle");

        function updateCountryFlag() {
            const flag = artistCountryCode.value ? createFlagElement(artistCountryCode.value) : null;
            if (flag)
                flag.className = "h-5 w-auto rounded-sm";
            artistCountryFlag.replaceChildren(...(flag ? [flag] : []));
        }

        // An unknown or missing code shows as not chosen
        function setCountry(code) {
            const country = countries.byCode(code);
            artistCountry.value = country?.name ?? "";
            artistCountryCode.value = country?.code ?? "";
            updateCountryFlag();
            setConfirmButtonStatus();
        }

        function renderCountryOption(country) {
            const flag = createFlagElement(country.code);
            return `
                <div class="flex items-center gap-2">
                    ${flag ? flag.outerHTML : ""}
                    <span>${country.name}</span>
                </div>`;
        }

        const countryAutocomplete = new Autocomplete({
            input: artistCountry,
            debounceTime: 0,
            maxVisibleItems: 8,
            // With a country picked, the box holds its full name, so show them all to choose another
            search: async (q) => artistCountryCode.value ? countries.ALL : countries.startingWith(q),
            renderItem: renderCountryOption,
            onSelected(country) {
                const previous = artistCountryCode.value;
                setCountry(country.code);
                if (country.code !== previous)
                    setMusicBrainzId(null);
            }
        });

        artistCountryToggle.addEventListener("click", (e) => {
            e.stopPropagation();
            if (countryAutocomplete.isOpen)
                countryAutocomplete.hide();
            else
                countryAutocomplete.open();
        });

        // Typing means no country is picked until one is chosen from the filtered list
        artistCountry.addEventListener("input", () => {
            artistCountryCode.value = "";
            updateCountryFlag();
            setMusicBrainzId(null);
            setConfirmButtonStatus();
        });

        // Typing a country's full name counts as picking it
        artistCountry.addEventListener("blur", () => {
            if (artistCountryCode.value)
                return;
            const match = countries.byName(artistCountry.value);
            if (match)
                setCountry(match.code);
        });

        artistMusicBrainzUrl.addEventListener("input", () => {
            artistMbid.value = mbidFromMusicBrainzUrl(artistMusicBrainzUrl.value) ?? "";
        });

        // One lookup at a time: picking another artist cancels the one in flight. The image has
        // its own controller on top, since uploading or removing an image by hand should only
        // cancel that part.
        let artistLookup = null;
        let imageLookup = null;

        function cancelImageLookup() {
            if (imageLookup === null)
                return;
            imageLookup.abort();
            imageLookup = null;
            artistImageUploader.setLoading(false);
            status.hide();
        }

        function cancelLookups() {
            artistLookup?.abort();
            artistLookup = null;
            cancelImageLookup();
            status.hide();
        }

        // Runs work({ signal, isCurrent, onProgress }) as the artist lookup, cancelling the one in flight
        async function runLookup(work) {
            cancelLookups();
            const lookup = new AbortController();
            artistLookup = lookup;
            const isCurrent = () => artistLookup === lookup;
            const onProgress = progress => {
                if (isCurrent())
                    status.show(musicbrainzApi.describeProgress(progress));
            };

            try {
                await work({ signal: lookup.signal, isCurrent, onProgress });
            }
            finally {
                if (isCurrent())
                    artistLookup = null;
            }
        }

        // Finds the artist the user picked from the Spotify search on MusicBrainz, letting them
        // choose if there's more than one, then fills in its type and fetches its image. If
        // MusicBrainz gave us an error, the status line gets a Retry button. Never throws.
        async function findMusicBrainzArtist(name, lookup) {
            const { signal, isCurrent, onProgress } = lookup;

            status.show("Looking up the artist on MusicBrainz...");

            let artists;
            try {
                artists = await musicbrainzApi.findArtists(name, { signal, onProgress });
            }
            catch (e) {
                if (isAbort(e) || !isCurrent())
                    return;
                console.warn("MusicBrainz artist lookup failed:", e);
                status.show("Couldn't reach MusicBrainz. You can choose the type and upload an image yourself.", {
                    busy: false,
                    onRetry: () => runLookup(again => findMusicBrainzArtist(name, again))
                });
                return;
            }

            if (!isCurrent())
                return;

            if (artists.length === 0) {
                status.show("MusicBrainz doesn't know this artist. You can choose the type and upload an image yourself.", { busy: false });
                return;
            }

            let artist = artists[0];
            if (artists.length > 1) {
                status.hide();
                artist = await confirmArtist.choose(artists, name);

                if (!isCurrent())
                    return;

                if (artist === null) {
                    status.show("No artist chosen. You can choose the type and upload an image yourself.", { busy: false });
                    return;
                }
            }

            setMusicBrainzId(artist.id);
            setType(artist.type);
            setCountry(artist.country);

            if (artist.id)
                await loadArtistImage(artist.id, lookup);
            else
                status.hide();
        }

        // For an artist whose mbid we already know: fills in its type and country, then fetches its
        // image. Never throws.
        async function loadKnownArtist(mbid, lookup) {
            const { signal, isCurrent, onProgress } = lookup;

            status.show("Looking up the artist on MusicBrainz...");

            let artist;
            try {
                artist = await musicbrainzApi.getArtist(mbid, { signal, onProgress });
            }
            catch (e) {
                if (isAbort(e) || !isCurrent())
                    return;
                console.warn("MusicBrainz artist lookup failed:", e);
                status.show("Couldn't reach MusicBrainz. You can choose the type and country yourself.", {
                    busy: false,
                    onRetry: () => runLookup(again => loadKnownArtist(mbid, again))
                });
                return;
            }

            if (!isCurrent())
                return;

            // Don't overwrite what the user has chosen while we were waiting
            if (artist) {
                if (!artistType.value)
                    setType(artist.type);
                if (!artistCountryCode.value)
                    setCountry(artist.country);
            }

            await loadArtistImage(mbid, lookup);
        }

        // Fetches the image of a MusicBrainz artist, with a spinner over the image meanwhile.
        // Never throws.
        async function loadArtistImage(mbid, { signal, isCurrent, onProgress }) {
            // Don't replace an image the user uploaded themselves
            if (artistImageUploader.getImageUrl() && !imageIsLookedUp) {
                status.hide();
                return;
            }

            const image = new AbortController();
            imageLookup = image;
            artistImageUploader.setLoading(true);
            status.show("Getting the artist image...");

            try {
                const result = await artwork.getArtistImage(mbid, {
                    signal: AbortSignal.any([signal, image.signal]),
                    onProgress
                });

                if (!isCurrent() || imageLookup !== image)
                    return;

                if (result?.url) {
                    setLookedUpImage(result);
                    status.hide();
                }
                else {
                    status.show("No image found for this artist, you can upload one yourself.", { busy: false });
                }
            }
            catch (e) {
                if (isAbort(e) || !isCurrent() || imageLookup !== image)
                    return;
                console.warn("Could not load artist image:", e);
                status.show("Couldn't get the artist image, you can upload one yourself.", {
                    busy: false,
                    onRetry: () => runLookup(again => loadArtistImage(mbid, again))
                });
            }
            finally {
                if (imageLookup === image) {
                    imageLookup = null;
                    artistImageUploader.setLoading(false);
                }
            }
        }

        // Artist autocomplete
        const artistAutocomplete = new Autocomplete({
            input: artistSearch,
            search: async (q) => await spotifyApi.searchOnSpotify(q, "artist", 5, 0, (b) => {
                return b.artists.total > 0 ? b.artists.items : [];
            }),
            renderItem(artist) {
                return `
                    <div class="flex items-center gap-4">
                        <img src="${artist.images.length > 0 ? artist.images.at(-1).url : ""}"
                                class="w-12 h-12 rounded">
                        </img>
                        <div>
                            <div class="font-medium">
                                ${artist.name}
                            </div>
                        </div>
                    </div>
                `;
            },
            async onSelected(artist) {
                artistSearch.value = artist.name;
                artistName.value = artist.name;
                artistSpotifyUrl.value = artist.external_urls.spotify ?? "";

                // Whatever we found for the previous pick belongs to another artist
                setMusicBrainzId(null);
                setType("");
                setCountry(null);
                if (imageIsLookedUp) {
                    artistImageUploader.clear();
                    clearLookedUpImageCredit();
                }

                setConfirmButtonStatus();

                // The image and type come from MusicBrainz rather than Spotify
                await runLookup(lookup => findMusicBrainzArtist(artist.name, lookup));
            }
        });

        if (!auth.hasSpotifyToken()) {
            artistSearch.disabled = true;
            artistSearch.placeholder = "Log in to Spotify to search";
        }

        // Confirm button enable/disable
        function isComplete() {
            return artistName.value.length > 0 && artistType.value.length > 0 && artistCountryCode.value.length > 0;
        }

        var confirmButton = null;
        function setConfirmButtonStatus() {
            // Not there yet while the form is being set up
            if (confirmButton) {
                const isEnabled = isComplete();
                confirmButton.disabled = !isEnabled;
                confirmButton.classList.toggle("opacity-50", !isEnabled);
                confirmButton.classList.toggle("cursor-not-allowed", !isEnabled);
                confirmButton.title = isEnabled ? "Click to add record" : "A new artist must have a name, a type and a country picked from the list";
            }
        }

        // The name we fill in ourselves when we know the artist on MusicBrainz isn't an unsaved change
        const prefilledName = musicBrainzId ? initialName : "";

        // Actual form
        const modalHandle = Modal.show({
            title: "Add Artist",
            content: root,
            footerContent: status.element,
            maxWidthClass: "max-w-2/5",
            buttons: [
                {
                    "type": "save",
                    "action": async () => {
                        if (!isComplete()) {
                            return false;
                        }

                        artistAutocomplete.dispose();

                        // Create the artist
                        try {
                            const musicBrainzId = artistMbid.value || null;

                            // A looked-up photo is sent as the artist it was looked up for, and the
                            // backend gets it again. If the user has since unlinked the artist
                            // (e.g. by renaming them) the photo may be someone else's, so it's left out.
                            let image = null;
                            let imageFile = null;
                            if (imageIsLookedUp) {
                                if (musicBrainzId)
                                    image = { source: "Wikimedia", externalId: musicBrainzId };
                            }
                            else if (artistImageUploader.getFile()) {
                                imageFile = artistImageUploader.getFile();
                                image = { source: "Upload", fileIndex: 0 };
                            }

                            const artist = {
                                name: artistName.value.trim(),
                                sortName: null,     // The backend gets it from MusicBrainz
                                type: artistType.value,
                                country: artistCountryCode.value,
                                musicBrainzId,
                                spotifyUrl: artistSpotifyUrl.value || null,
                                image
                            };
                            const { id: artistId, alreadyInCollection } = await backendApi.addNewCreator(artist, imageFile);
                            if (alreadyInCollection)
                                Toast.info(`"${artist.name}" was already in your collection, so that's the one used`, "Artist already added");
                            else
                                Toast.success(`Artist "${artist.name}" added`, "New artist added");
                            resolveOnce({ "name": artist.name, "id": artistId, "musicBrainzId": artist.musicBrainzId });
                        }
                        catch (e) {
                            Toast.error(e, "Adding artist failed");
                            resolveOnce(null);
                        }
                        finally {
                            return true;
                        }
                    }
                },
                {
                    "type": "close",
                    "text": "cancel",
                    "action": async () => {
                        const hasUploadedImage = artistImageUploader.getImageUrl().length > 0 && !imageIsLookedUp;
                        const hasUnsavedChanges = artistName.value !== prefilledName || hasUploadedImage || artistSpotifyUrl.value.length > 0;
                        if (!hasUnsavedChanges) {
                            artistAutocomplete.dispose();
                            resolveOnce(null);
                            return true;
                        }

                        if (await Modal.confirm("Unsaved changes", "Do you want to discard unsaved changes?")) {
                            artistAutocomplete.dispose();
                            resolveOnce(null);
                            return true;
                        }

                        return false;
                    }
                }
            ]
        });

        // Escape, clicking outside the dialog, and the header's (x) button
        // all bypass the button actions above and call modal.close()
        // directly. Without patching the close function here, we would never
        // resolve the Promise and hence, never return if the user cancels
        // via any other method than pressing the "Cancel" button.
        const originalClose = modalHandle.close;
        modalHandle.close = () => {
            resolveOnce(null);
            originalClose();
        };
        // Direct children only: the status line in the footer has a Retry button of its own
        confirmButton = modalHandle.dialog.querySelectorAll("#modalFooter > button")[0];

        // The links stop matching once the user edits the artist by hand: another name is another
        // artist everywhere, and another type at least isn't the one MusicBrainz knows.
        artistName.addEventListener("input", () => {
            setMusicBrainzId(null);
            artistSpotifyUrl.value = "";
            setConfirmButtonStatus();
        });
        artistType.addEventListener("change", () => setMusicBrainzId(null));

        setType(creatorType);

        if (musicBrainzId) {
            // We already know who it is, so no need to search
            artistName.value = initialName;
            setMusicBrainzId(musicBrainzId);
            runLookup(lookup => loadKnownArtist(musicBrainzId, lookup));
        }
        else {
            artistSearch.value = initialName;
            if (initialName !== "") {
                artistSearch.dispatchEvent(new Event("input", { bubbles: true }));
            }
        }

        setConfirmButtonStatus();
    });
}
