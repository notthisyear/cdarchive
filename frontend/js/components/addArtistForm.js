import * as styles from "../styles.js"
import * as Modal from "./modal.js";
import * as imageUploader from "../components/imageUploader.js";
import * as auth from "../auth.js"
import * as api from "../api.js";

import { Autocomplete } from "./autocomplete.js";

export function show() {
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
            }
        });

        if (!auth.hasSpotifyToken()) {
            artistSearch.disabled = true;
            artistSearch.placeholder = "Log in to Spotify to search";
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
                        artistAutocomplete.dispose();

                        try {
                            const artist = {
                                name: artistName.value,
                                imageUrl: artistImageUploader.getImageUrl(),
                                spotifyUrl: artistSpotifyUrl.value,
                            };
                            await api.addNewArtist(artist);
                            resolveOnce(artist);
                        }
                        catch (e) {
                            console.error(`Failed to add artist - ${e}`);
                            resolveOnce(null);
                        }
                        finally {
                            return true;
                        }
                    }
                },
                {
                    "type": "cancel",
                    "action": () => {
                        artistAutocomplete.dispose();
                        resolveOnce(null);
                        return true;
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

    });
}