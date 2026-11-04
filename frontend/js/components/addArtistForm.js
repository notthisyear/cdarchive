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
import { createCountryPicker } from "./common/countryPicker.js";

import { Autocomplete } from "./autocomplete.js";
import { escapeHtml } from "../util.js";

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
            countryPicker.dispose();
            artistAutocomplete.dispose();
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
                                                <!-- What the release credits them as, when the name above is MusicBrainz's instead -->
                                                <p id="artistCreditedName"
                                                   class="hidden mt-1 text-xs italic text-slate-500 dark:text-slate-400"></p>
                                            </div>

                                            <div class="w-56 max-w-full">
                                                <label for="artistCountry"
                                                       class="${styles.addRecordInputBoxLabel}">
                                                    Country
                                                </label>
                                                <!-- A countryPicker: a country must be picked from its list -->
                                                <div id="artistCountrySlot"></div>
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

        // Removes a looked-up image (and stops looking for one), but not one the user uploaded
        function clearLookedUpImage() {
            cancelImageLookup();
            if (imageIsLookedUp) {
                artistImageUploader.clear();
                clearLookedUpImageCredit();
            }
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
        const artistCreditedName = root.querySelector("#artistCreditedName");

        // Says what the release credits the artist as, when the name field has their own name instead.
        // Null hides it.
        function setCreditedNameCaption(creditedName) {
            artistCreditedName.textContent = creditedName ? `credited as "${creditedName}" on this release` : "";
            artistCreditedName.classList.toggle("hidden", !creditedName);
        }

        // What MusicBrainz says about the linked artist, as { type, country } with null for what it
        // doesn't say. Null while no artist is linked, or when we haven't looked it up (a pasted URL).
        let musicBrainzValues = null;

        // The hidden mbid and the MusicBrainz URL field always describe the same artist
        function setMusicBrainzId(mbid, values = null) {
            artistMbid.value = mbid ?? "";
            artistMusicBrainzUrl.value = mbid ? `${MUSICBRAINZ_ARTIST_URL}${mbid}` : "";
            musicBrainzValues = mbid ? values : null;
        }

        // A looked-up image is of the artist we no longer say this is, and wouldn't be saved anyway
        function unlinkMusicBrainzArtist() {
            setMusicBrainzId(null);
            clearLookedUpImage();
        }

        // Filling in what MusicBrainz doesn't say is fine, but another type or country than the one it
        // does say means this isn't quite the artist MusicBrainz knows. Only for values the user has
        // settled on: a country being typed has no code yet.
        function unlinkIfContradictingMusicBrainz() {
            if (musicBrainzValues === null)
                return;

            const { type, country } = musicBrainzValues;
            if ((type && artistType.value !== type) || (country && countryPicker.getCode() !== country))
                unlinkMusicBrainzArtist();
        }

        // In the form's terms: a country it has no option for counts as not said, like a missing one
        function toMusicBrainzValues(artist) {
            return {
                type: artist.type ?? null,
                country: countries.byCode(artist.country)?.code ?? null
            };
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

        // Country. The text box only filters the list: the country counts once it's picked from it
        // (or its full name is typed). A country the user picks may contradict MusicBrainz.
        const countryPicker = createCountryPicker({
            id: "artistCountry",
            required: true,
            onChange: () => setConfirmButtonStatus(),
            onPicked: () => unlinkIfContradictingMusicBrainz()
        });
        root.querySelector("#artistCountrySlot").replaceWith(countryPicker.element);

        // An unknown or missing code shows as not chosen
        function setCountry(code) {
            countryPicker.setCode(code);
        }

        // A pasted URL says which artist this is, so it's looked up and fills in the form like picking
        // that artist would. Nothing is linked until MusicBrainz has confirmed it knows them.
        artistMusicBrainzUrl.addEventListener("input", () => {
            const mbid = mbidFromMusicBrainzUrl(artistMusicBrainzUrl.value) ?? "";
            if (mbid === artistMbid.value)
                return;

            cancelLookups();
            artistMbid.value = "";
            musicBrainzValues = null;
            clearLookedUpImage();
            if (mbid)
                runLookup(lookup => loadPastedArtist(mbid, lookup));
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

        // Fills in the form from a MusicBrainz artist the user picked or pasted the URL of, then fetches
        // its image. The name is as MusicBrainz spells it ("Motörhead" rather than Spotify's "Motorhead"),
        // and the type "Other" when MusicBrainz doesn't say: the artist has to be something.
        // Never throws.
        async function useMusicBrainzArtist(artist, lookup) {
            if (artist.name)
                artistName.value = artist.name;
            setCreditedNameCaption(null);
            setMusicBrainzId(artist.id, toMusicBrainzValues(artist));
            setType(artist.type ?? "Other");
            setCountry(artist.country);

            await loadArtistImage(artist.id, lookup);
        }

        // Finds the artist the user picked from the Spotify search on MusicBrainz. The one linking to
        // their Spotify page is them for certain. Without one, it's the artists with their name, and
        // the user has to confirm even a single one, as many artists share a name. If MusicBrainz
        // gave us an error, the status line gets a Retry button. Never throws.
        async function findMusicBrainzArtist(name, spotifyId, lookup) {
            const { signal, isCurrent, onProgress } = lookup;

            status.show("Looking up the artist on MusicBrainz...");

            let artists = [];
            let linkedToSpotify = false;
            try {
                if (spotifyId) {
                    artists = await musicbrainzApi.findArtistsBySpotifyId(spotifyId, { signal, onProgress });
                    linkedToSpotify = artists.length > 0;
                }
                if (!linkedToSpotify)
                    artists = await musicbrainzApi.findArtists(name, { signal, onProgress });
            }
            catch (e) {
                if (isAbort(e) || !isCurrent())
                    return;
                console.warn("MusicBrainz artist lookup failed:", e);
                status.show("Couldn't reach MusicBrainz. You can choose the type and upload an image yourself.", {
                    busy: false,
                    onRetry: () => runLookup(again => findMusicBrainzArtist(name, spotifyId, again))
                });
                return;
            }

            if (!isCurrent())
                return;

            if (artists.length === 0) {
                status.show("MusicBrainz doesn't know this artist. You can choose the type and upload an image yourself.", { busy: false });
                return;
            }

            let artist = linkedToSpotify && artists.length === 1 ? artists[0] : null;
            if (artist === null) {
                status.hide();
                artist = await confirmArtist.choose(artists, name);

                if (!isCurrent())
                    return;

                if (artist === null) {
                    status.show("No artist chosen. You can choose the type and upload an image yourself.", { busy: false });
                    return;
                }
            }

            await useMusicBrainzArtist(artist, lookup);
        }

        // For a MusicBrainz URL the user pasted. Never throws.
        async function loadPastedArtist(mbid, lookup) {
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
                status.show("Couldn't reach MusicBrainz to check the URL, so the artist isn't linked.", {
                    busy: false,
                    onRetry: () => runLookup(again => loadPastedArtist(mbid, again))
                });
                return;
            }

            if (!isCurrent())
                return;

            // The URL stays, for the user to correct
            if (artist === null) {
                status.show("MusicBrainz doesn't know the artist in that URL, so nothing is linked.", { busy: false });
                return;
            }

            await useMusicBrainzArtist(artist, lookup);
        }

        // For an artist whose mbid we already know (from a release's credit): names them as MusicBrainz
        // does, fills in their type and country, then fetches their image. The release may credit them
        // by another name ("John Cougar" for John Mellencamp), which the caption keeps showing. The
        // release keeps that name; the artist is saved under their own. Never throws.
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

            // Don't overwrite what the user has chosen while we were waiting. If it says otherwise than
            // MusicBrainz, it's no longer the artist MusicBrainz knows, and their image isn't wanted.
            // An artist merged into another on MusicBrainz comes back as that one, with its mbid.
            const linkedMbid = artist?.id ?? mbid;
            if (artist) {
                // Not an unsaved change: it's what we'd have filled in had we known it to begin with.
                // Still the same artist, so it stays linked (setting the value fires no input event).
                if (artist.name && artist.name !== artistName.value) {
                    setCreditedNameCaption(artistName.value.trim() || null);
                    artistName.value = artist.name;
                    prefilledName = artist.name;
                }

                setMusicBrainzId(linkedMbid, toMusicBrainzValues(artist));
                if (!artistType.value)
                    setType(artist.type ?? "Other");
                if (!countryPicker.getCode())
                    setCountry(artist.country);
                unlinkIfContradictingMusicBrainz();
            }

            if (artistMbid.value !== linkedMbid) {
                status.hide();
                return;
            }

            await loadArtistImage(linkedMbid, lookup);
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
                        <img src="${escapeHtml(artist.images.length > 0 ? artist.images.at(-1).url : "")}"
                                class="w-12 h-12 rounded">
                        </img>
                        <div>
                            <div class="font-medium">
                                ${escapeHtml(artist.name)}
                            </div>
                        </div>
                    </div>
                `;
            },
            async onSelected(artist) {
                artistSearch.value = artist.name;
                artistName.value = artist.name;
                setCreditedNameCaption(null);
                artistSpotifyUrl.value = artist.external_urls.spotify ?? "";

                // Whatever we found for the previous pick belongs to another artist
                unlinkMusicBrainzArtist();
                setType("");
                setCountry(null);

                setConfirmButtonStatus();

                // The image and type come from MusicBrainz rather than Spotify
                await runLookup(lookup => findMusicBrainzArtist(artist.name, artist.id, lookup));
            }
        });

        if (!auth.hasSpotifyToken()) {
            artistSearch.disabled = true;
            artistSearch.placeholder = "Log in to Spotify to search";
        }

        // Confirm button enable/disable. A name of only spaces is no name.
        function isComplete() {
            return artistName.value.trim().length > 0 && artistType.value.length > 0 && countryPicker.getCode().length > 0;
        }

        // True while the artist is being saved, so that a second click can't add them twice
        let saving = false;

        var confirmButton = null;
        function setConfirmButtonStatus() {
            // Not there yet while the form is being set up
            if (confirmButton) {
                const isEnabled = isComplete() && !saving;
                confirmButton.disabled = !isEnabled;
                confirmButton.classList.toggle("opacity-50", !isEnabled);
                confirmButton.classList.toggle("cursor-not-allowed", !isEnabled);
                confirmButton.title = saving ? "Adding the artist..."
                    : isEnabled ? "Click to add the artist"
                    : "A new artist must have a name, a type and a country picked from the list";
            }
        }

        // The backend only knows an artist is already in the collection by their MusicBrainz or Spotify
        // id, so one added without them (by hand, or while MusicBrainz was down) would be added again.
        // The user's creator with exactly this name, unless linked to this very MusicBrainz artist (the
        // backend finds those itself), or null. Also null if we can't tell, as it's only a precaution.
        async function findSameNamedCreator(name, musicBrainzId) {
            try {
                const { creators, matchId } = await backendApi.findCreators(name, musicBrainzId);
                const existing = creators.find(c => String(c.id) === String(matchId));
                if (!existing || (musicBrainzId && existing.musicBrainzId === musicBrainzId))
                    return null;
                return existing;
            }
            catch (e) {
                console.warn("Could not check the collection for the artist:", e);
                return null;
            }
        }

        // Adds the artist (or uses the one the user already has). True when done, false when it failed,
        // in which case the form stays open to try again or fix what was wrong.
        async function save() {
            const existing = await findSameNamedCreator(artistName.value.trim(), artistMbid.value || null);
            if (existing && await Modal.confirm("Artist already in your collection?",
                `You already have an artist called "${existing.name}". Use them rather than adding a new one?`)) {
                resolveOnce({ "name": existing.name, "id": String(existing.id), "musicBrainzId": existing.musicBrainzId ?? null });
                return true;
            }

            const musicBrainzId = artistMbid.value || null;

            // A looked-up photo is sent as the artist it was looked up for, and the
            // backend gets it again. Unlinking the artist removes the photo, so it's
            // only left out here should the two ever get out of step.
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
                country: countryPicker.getCode(),
                musicBrainzId,
                spotifyUrl: artistSpotifyUrl.value || null,
                image
            };

            let added;
            try {
                added = await backendApi.addNewCreator(artist, imageFile);
            }
            catch (e) {
                Toast.error(e, "Adding artist failed");
                return false;
            }

            // An artist already in the collection is as the user has them, which may
            // differ from what we sent
            if (added.alreadyInCollection) {
                Toast.info(`"${added.name}" was already in your collection, so that's the one used`, "Artist already added");
                resolveOnce({ "name": added.name, "id": added.id, "musicBrainzId": added.musicBrainzId });
            }
            else {
                Toast.success(`Artist "${artist.name}" added`, "New artist added");
                resolveOnce({ "name": artist.name, "id": added.id, "musicBrainzId": artist.musicBrainzId });
            }
            return true;
        }

        // The name we fill in ourselves when we know the artist on MusicBrainz isn't an unsaved change:
        // the credited name at first, then MusicBrainz's once loadKnownArtist has it
        let prefilledName = musicBrainzId ? initialName : "";

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
                        if (!isComplete() || saving)
                            return false;

                        saving = true;
                        setConfirmButtonStatus();
                        try {
                            return await save();
                        }
                        finally {
                            saving = false;
                            setConfirmButtonStatus();
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
                            resolveOnce(null);
                            return true;
                        }

                        if (await Modal.confirm("Unsaved changes", "Do you want to discard unsaved changes?")) {
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

        // Another name is another artist everywhere, so the links stop matching, and a lookup still
        // under way is for the old name. Another type only matters if MusicBrainz says which it is.
        artistName.addEventListener("input", () => {
            cancelLookups();
            unlinkMusicBrainzArtist();
            setCreditedNameCaption(null);
            artistSpotifyUrl.value = "";
            setConfirmButtonStatus();
        });
        artistType.addEventListener("change", unlinkIfContradictingMusicBrainz);

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
