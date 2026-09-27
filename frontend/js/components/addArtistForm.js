import * as styles from "../styles.js"
import * as Modal from "./modal.js";
import * as Toast from "./toast.js";
import * as imageUploader from "../components/imageUploader.js";
import * as auth from "../auth.js"
import * as api from "../api.js";
import * as confirmDuplicateArtist from "./confirmDuplicateArtist.js"

import { Autocomplete } from "./autocomplete.js";

export function show(initialValue = "") {
    return new Promise(resolve => {
        let resolved = false;
        function resolveOnce(value) {
            if (resolved)
                return;
            resolved = true;
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

                                <div class="flex gap-4">
                                    <div class="flex-1 flex flex-col gap-4">
                                        <div>
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

        const artistImageUploader = imageUploader.create({
            placeholderText: "No image selected",
            alt: "Artist image",
            onRemove: () => {
                // TODO: Hook some backend logic here to delete from image store
            }
        });
        root.querySelector("#artistImageSlot").replaceWith(artistImageUploader.element);

        // Artist autocomplete
        const artistSearch = root.querySelector("#artistSearch");
        const artistName = root.querySelector("#artistName");
        const artistSpotifyUrl = root.querySelector("#artistSpotifyUrl");
        const artistAutocomplete = new Autocomplete({
            input: artistSearch,
            search: async (q) => await api.searchOnSpotify(q, "artist", 5, 0, (b) => {
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
                if (artist.images?.at(0).url ?? false) {
                    artistImageUploader.setImage(artist.images.at(0).url);
                }

                artistSearch.value = artist.name;
                artistName.value = artist.name;
                artistSpotifyUrl.value = artist.external_urls.spotify ?? "";

                setConfirmButtonStatus();
            }
        });

        if (!auth.hasSpotifyToken()) {
            artistSearch.disabled = true;
            artistSearch.placeholder = "Log in to Spotify to search";
        }

        // Confirm button enable/disable
        var confirmButton = null;
        function setConfirmButtonStatus() {
            if (confirmButton !== null) {
                const isEnabled = artistName.value.length > 0;
                confirmButton.disabled = !isEnabled;
                confirmButton.classList.toggle("opacity-50", !isEnabled);
                confirmButton.classList.toggle("cursor-not-allowed", !isEnabled);
                confirmButton.title = isEnabled ? "Click to add record" : "A new artist must at least have a name";
            }
        }

        // Actual form
        const modalHandle = Modal.show({
            title: "Add Artist",
            content: root,
            maxWidthClass: "max-w-2/5",
            buttons: [
                {
                    "type": "save",
                    "action": async () => {
                        if (artistName.value.length === 0) {
                            return false;
                        }

                        artistAutocomplete.dispose();

                        // Check if this artist already exists
                        const response = await api.getArtists([{ name: artistName.value, id: null }]);
                        const results = response[artistName.value];
                        if (results.length === 1) {
                            const existingArtist = results[0];
                            if (await confirmDuplicateArtist.check(existingArtist) === false) {
                                resolveOnce(existingArtist);
                                return true;
                            }
                        }

                        // Create the artist
                        try {
                            const artist = {
                                name: artistName.value,
                                imageUrl: artistImageUploader.getImageUrl(),
                                spotifyUrl: artistSpotifyUrl.value,
                            };
                            const artistId = await api.addNewArtist(artist);
                            Toast.success(`Artist "${artist.name}" added`, "New artist added");
                            resolveOnce({ "name": artist.name, "id": artistId });
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
                        const hasUnsavedChanges = artistName.value.length > 0 || artistImageUploader.getImageUrl().length > 0 || artistSpotifyUrl.value.length > 0;
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
        confirmButton = modalHandle.dialog.querySelectorAll("#modalFooter button")[0];
        setConfirmButtonStatus()

        artistName.addEventListener("input", () => { setConfirmButtonStatus() });

        artistSearch.value = initialValue;
        if (initialValue !== "") {
            artistSearch.dispatchEvent(new Event("input", { bubbles: true }));
        }
    });
}