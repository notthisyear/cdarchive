import * as backendApi from "../api/backend.js";
import * as util from "../util.js"
import * as styles from "../styles.js";
import * as trackListing from "../components/trackListing.js"
import { createCoverImage } from "../components/common/coverImage.js";
import * as Modal from "../components/modal.js";
import * as Toast from "../components/toast.js";
import * as router from "../router.js";
import { createFlagElement } from "../components/common/countryFlag.js";
import { spotifyIcon } from "../components/spotifyStatus.js";
import * as mediaIcons from "../components/common/mediaIcons.js";
import * as editReleaseDialog from "../components/editReleaseDialog.js";
import { createStarRating } from "../components/common/starRating.js";

export async function render(params) {
    // ReleaseDetails (see backendApi.getRelease)
    const record = await backendApi.getRelease(params.id);

    const root = document.createElement("div");

    root.className = "space-y-8";

    root.innerHTML = `
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
            
            <!-- Left side -->
            <div class="space-y-6">
                <!-- The pen shows on hover, as on the box below -->
                <div class="relative group">
                    <div id="recordCover"></div>

                    <button class="${styles.recordSummaryEditButton}"
                            id="editRecordCover"
                            type="button"
                            title="Upload your own image"
                            aria-label="Upload your own image">
                        <svg xmlns="http://www.w3.org/2000/svg"
                             fill="none"
                             viewBox="0 0 24 24"
                             stroke-width="2"
                             stroke="currentColor"
                             class="w-5 h-5">
                            <path stroke-linecap="round"
                                  stroke-linejoin="round"
                                  d="M16.862 3.487a2.25 2.25 0 113.182 3.182L7.5 19.213l-4.5 1.125L4.125 15.84 16.862 3.487z"/>
                        </svg>
                    </button>
                    <input id="recordCoverFile" type="file" accept="image/jpeg,image/png,image/webp" class="hidden">
                </div>

                <div class="${styles.recordSummary}">

                <button class="${styles.recordSummaryEditButton}"
                        id=editRecordSummary
                        aria-label="Edit album">
                    <svg xmlns="http://www.w3.org/2000/svg"
                         fill="none"
                         viewBox="0 0 24 24"
                         stroke-width="2"
                         stroke="currentColor"
                         class="w-5 h-5">
                        <path stroke-linecap="round"
                              stroke-linejoin="round"
                              d="M16.862 3.487a2.25 2.25 0 113.182 3.182L7.5 19.213l-4.5 1.125L4.125 15.84 16.862 3.487z"/>
                    </svg>
                </button>
            
                    <!-- Title and artist are set as text below, as they're whatever the user typed -->
                    <h1 id="recordTitle" class="text-3xl font-bold text-slate-900 dark:text-white"></h1>

                    <h2 id="recordArtist" class=" text-xl text-slate-600 dark:text-slate-300 mt-1"></h2>
                    <p id="recordCreditedAs" class="hidden mt-1 text-sm italic text-slate-500 dark:text-slate-400"></p>
                    <!-- Shown (as flex) only for a format we have an icon for -->
                    <p id="recordFormat" class="hidden mt-2 items-center gap-2 text-slate-500 dark:text-slate-400"></p>

                    <dl class="mt-6 space-y-4">
                        <div class="flex justify-between gap-4">
                            <dt class="text-slate-500 dark:text-slate-400">
                                Year
                            </dt>
                            <dd id="recordYear" class="text-right"></dd>
                        </div>

                        <div class="flex justify-between">
                            <dt class="text-slate-500 dark:text-slate-400">
                                Tracks
                            </dt>
                            <dd>
                                ${record.tracks.length}
                            </dd>
                        </div>

                        <div class="flex justify-between">
                            <dt class="text-slate-500 dark:text-slate-400">
                                Length
                            </dt>
                            <dd>
                                ${util.getAsTimeStringWithSuffix(record.durationSeconds)}
                            </dd>
                        </div>
                    </dl>

                    <!-- Links to the release elsewhere, greyed out where we don't know it there -->
                    <div id="recordLinks"
                         class="mt-6 pt-4 border-t border-slate-200 dark:border-slate-700 flex items-center gap-3"></div>
                </div>

                <!-- The user's own data about the release. Each change is saved straight away, but the
                     notes have a Save button of their own. -->
                <div class="rounded-xl p-6 bg-white dark:bg-slate-800 border-slate-200 dark:border-slate-700 shadow-lg">
                    <h2 class="text-lg font-semibold text-slate-900 dark:text-white">Personal notes</h2>

                    <div class="mt-4 space-y-4">
                        <div class="flex justify-between items-center gap-4">
                            <label for="personalAcquired" class="text-slate-500 dark:text-slate-400">First acquired</label>
                            <div class="w-44">
                                <input id="personalAcquired" type="date" class="${styles.editorInputBox} dark:[color-scheme:dark]">
                            </div>
                        </div>

                        <div class="flex justify-between items-center gap-4">
                            <span class="text-slate-500 dark:text-slate-400">Rating</span>
                            <div id="personalRating"></div>
                        </div>

                        <div class="flex justify-between items-center gap-4">
                            <label for="personalConsumed" id="personalConsumedLabel" class="text-slate-500 dark:text-slate-400"></label>
                            <input id="personalConsumed" type="checkbox" class="w-5 h-5 accent-blue-600 cursor-pointer">
                        </div>

                        <div>
                            <label for="personalNotes" class="block mb-2 text-slate-500 dark:text-slate-400">Notes</label>
                            <textarea id="personalNotes" rows="4" maxlength="4000"
                                      class="${styles.editorInputBox} resize-y"
                                      placeholder="Where you got it, what you think of it..."></textarea>
                            <div class="mt-2 flex justify-end">
                                <button id="personalNotesSave" type="button" class="${styles.buttonPrimary} disabled:opacity-50 disabled:cursor-not-allowed" disabled>
                                    Save notes
                                </button>
                            </div>
                        </div>
                    </div>
                </div>

                <button id="removeReleaseButton"
                        type="button"
                        class="${styles.buttonDanger} w-full">
                    Remove from collection
                </button>
            </div>

            <!-- Right side -->
            <div class="lg:col-span-2">
                <div class="
                    rounded-xl
                    bg-white
                    dark:bg-slate-800
                    shadow-lg
                    overflow-hidden">
                    <div class="px-6 py-4 border-b border-slate-200 dark:border-slate-700">
                        <h2 class="text-xl font-semibold">
                            Track Listing
                        </h2>
                    </div>

                    <div id="trackList"> </div>
                </div>

                <div class="mt-4">
                    <button id="addTrackButton"
                            class="${styles.buttonPrimary}">
                        Add Track...
                    </button>
                </div>
            </div>
        </div>
    `;

    root.querySelector("#recordTitle").textContent = record.summary.title;
    renderArtists(root.querySelector("#recordArtist"), root.querySelector("#recordCreditedAs"), record);
    renderFormat(root.querySelector("#recordFormat"), record.format);
    renderYear(root.querySelector("#recordYear"), record);
    renderLinks(root.querySelector("#recordLinks"), record.externalIds ?? {});

    // Without a cover, a square box saying so, as wide as a cover would be
    root.querySelector("#recordCover").replaceWith(createCoverImage({
        src: backendApi.toImageSrc(record.summary.imageUrl),
        alt: record.summary.title,
        className: "w-full rounded-xl shadow-2xl",
        placeholderClassName: "aspect-square text-lg bg-slate-200 text-slate-500 dark:bg-slate-700 dark:text-slate-400"
    }));

    setUpCoverUpload(root, record);
    setUpPersonalData(root, record);

    // The page shows the release as saved, so it's drawn again after any change
    const editRecordSummary = root.querySelector("#editRecordSummary");
    editRecordSummary.addEventListener("click", async () => {
        if (await editReleaseDialog.show(record))
            await router.refresh();
    });

    const trackListElement = root.querySelector("#trackList");
    trackListing.clearAllTracks(trackListElement);
    for (const track of record.tracks) {
        trackListing.addTrackRow(trackListElement, track.title, track.discNumber, track.trackNumber, track.durationSeconds);
    }

    // Other tracks make it another release than the one on MusicBrainz, so a linked release asks first
    trackListing.setEditGuard(trackListElement, async () => {
        if (!record.externalIds?.MusicBrainz)
            return true;
        return await Modal.confirm("Unlink from MusicBrainz?",
            "Changing the tracks means this is no longer the MusicBrainz release it's linked to, so the link will be removed.",
            { yesText: "Change and unlink", noText: "Cancel" });
    });

    // Each change is saved straight away. Drawn again either way, so the page never shows tracks
    // that weren't saved, and the length, track count and links match.
    trackListElement.addEventListener(trackListing.TRACKS_EDITED_EVENT, async () => {
        const tracks = trackListing.getAllTracks().map(x => ({
            discNumber: x.discNumber,
            trackNumber: x.trackNumber,
            title: x.title,
            durationSeconds: x.durationSeconds ?? null
        }));

        try {
            await backendApi.replaceTracks(record.summary.id, tracks);
        }
        catch (e) {
            Toast.error(e.message, "Saving the track list failed");
        }
        await router.refresh();
    });

    // The track is only added once it's been edited and saved
    const addTrackButton = root.querySelector("#addTrackButton");
    addTrackButton.addEventListener("click", () => trackListing.editNewTrack(trackListElement));

    const removeReleaseButton = root.querySelector("#removeReleaseButton");
    removeReleaseButton.addEventListener("click", async () => {
        removeReleaseButton.disabled = true;
        try {
            await removeRelease(record);
        }
        finally {
            removeReleaseButton.disabled = false;
        }
    });

    return root;
}

// As the backend allows (MediaFileService.MaxImageSizeBytes)
const MAX_IMAGE_BYTES = 8 * 1024 * 1024;

// The pen on the cover uploads an image of the user's own, which becomes the one shown. It's added
// to the release's images rather than replacing them, as other users' copies may share those.
function setUpCoverUpload(root, record) {
    const button = root.querySelector("#editRecordCover");
    const fileInput = root.querySelector("#recordCoverFile");

    button.addEventListener("click", () => fileInput.click());

    fileInput.addEventListener("change", async () => {
        const file = fileInput.files?.[0];
        fileInput.value = "";       // so picking the same file again is a change too
        if (!file)
            return;

        if (file.size > MAX_IMAGE_BYTES) {
            Toast.error("The image must be a JPEG, PNG or WebP file of at most 8 MiB", "Image too large");
            return;
        }

        button.disabled = true;
        try {
            await backendApi.uploadReleaseImage(record.summary.id, file);
        }
        catch (e) {
            Toast.error(e.message, "Uploading the image failed");
            button.disabled = false;
            return;
        }

        Toast.success("It's now the image shown for this release", "Image uploaded");
        await router.refresh();
    });
}

// "Listened to" for a record, and the like for a book or a film
const CONSUMED_LABELS = {
    Record: "Listened to",
    Book: "Read",
    Film: "Watched"
};

// The user's own data: when they first got it, their rating, whether they've listened to it and
// their notes. The date, rating and checkbox are saved as soon as they change; the notes when their
// Save button is clicked. A failed save puts the field back as it was saved. record is a ReleaseDetails.
function setUpPersonalData(root, record) {
    const acquiredInput = root.querySelector("#personalAcquired");
    const consumedInput = root.querySelector("#personalConsumed");
    const notesInput = root.querySelector("#personalNotes");
    const notesSaveButton = root.querySelector("#personalNotesSave");
    root.querySelector("#personalConsumedLabel").textContent = CONSUMED_LABELS[record.summary.type] ?? "Consumed";

    // As saved. All of it is sent with every save, as the backend takes it all at once.
    let saved = {
        acquiredOn: record.acquiredOn ?? null,
        score: record.score ?? null,
        consumed: !!record.consumed,
        notes: record.notes ?? null
    };

    const rating = createStarRating({
        value: saved.score,
        label: "Your rating",
        onChange: score => save({ score }, () => rating.setValue(saved.score))
    });
    root.querySelector("#personalRating").replaceWith(rating.element);

    acquiredInput.value = saved.acquiredOn ?? "";
    consumedInput.checked = saved.consumed;
    notesInput.value = saved.notes ?? "";

    // One save at a time, in the order they were made, each on top of the last one saved
    let queue = Promise.resolve(true);
    function save(changes, undo) {
        queue = queue.then(async () => {
            try {
                const updated = await backendApi.setPersonalReleaseData(record.summary.id, { ...saved, ...changes });
                saved = {
                    acquiredOn: updated.acquiredOn ?? null,
                    score: updated.score ?? null,
                    consumed: !!updated.consumed,
                    notes: updated.notes ?? null
                };
                return true;
            }
            catch (e) {
                Toast.error(e.message, "Saving failed");
                undo?.();
                return false;
            }
        });
        return queue;
    }

    acquiredInput.addEventListener("change", () =>
        save({ acquiredOn: acquiredInput.value || null }, () => acquiredInput.value = saved.acquiredOn ?? ""));

    consumedInput.addEventListener("change", () =>
        save({ consumed: consumedInput.checked }, () => consumedInput.checked = saved.consumed));

    // Notes: Save only does something when they differ from what's saved
    function updateNotesSaveButton() {
        notesSaveButton.disabled = notesInput.value.trim() === (saved.notes ?? "");
    }
    notesInput.addEventListener("input", updateNotesSaveButton);

    notesSaveButton.addEventListener("click", async () => {
        notesSaveButton.disabled = true;
        if (await save({ notes: notesInput.value.trim() || null }))
            Toast.success("Your notes were saved", "Notes saved");
        updateNotesSaveButton();
    });
}

// The artists by their own names, each followed by their flag, joined as the release joins them:
// "John Mellencamp 🇺🇸". When the release credits them otherwise, that goes below: "Credited as
// John Cougar". record is a ReleaseDetails.
function renderArtists(artistElement, creditedAsElement, record) {
    const credits = (record.credits ?? []).filter(x => x.role === "Primary");
    if (credits.length === 0) {
        artistElement.textContent = record.summary.artistName;
        return;
    }

    for (const credit of credits) {
        artistElement.append(credit.name);
        const flag = createFlagElement(credit.country, { trailing: true });
        if (flag)
            artistElement.append(flag);
        artistElement.append(credit.joinPhrase ?? "");
    }

    // The summary's artist line is the credited names, joined the same way
    const ownNames = credits.map(x => x.name + (x.joinPhrase ?? "")).join("").trim();
    if (record.summary.artistName && record.summary.artistName !== ownNames) {
        creditedAsElement.textContent = `Credited as ${record.summary.artistName}`;
        creditedAsElement.classList.remove("hidden");
    }
}

// As the music form's format dropdown names them
const FORMAT_NAMES = {
    Cd: "CD",
    Vinyl: "Vinyl",
    Cassette: "Cassette",
    Digital: "Digital",
    Other: "Other format"
};

// The release's format as an icon and its name: CD, vinyl... Left out when it's unknown.
// format is a ReleaseFormat name.
function renderFormat(element, format) {
    const icon = mediaIcons.forFormat(format);
    if (!icon)
        return;

    element.innerHTML = mediaIcons.svg(icon, "w-6 h-6 shrink-0");
    const name = document.createElement("span");
    name.textContent = FORMAT_NAMES[format] ?? format;
    element.append(name);
    element.classList.replace("hidden", "flex");
}

// This release's year with its country's flag, and when the album first came out if that was
// another year: "2009 🇬🇧 (first released in 1969)". record is a ReleaseDetails.
function renderYear(element, record) {
    const { releaseYear, firstReleaseYear } = record;
    element.append(String(releaseYear ?? firstReleaseYear ?? "N/a"));

    const flag = createFlagElement(record.country, { trailing: true });
    if (flag)
        element.append(flag);

    if (releaseYear && firstReleaseYear && releaseYear !== firstReleaseYear) {
        const firstReleased = document.createElement("span");
        firstReleased.className = "ml-1.5 text-sm text-slate-500 dark:text-slate-400";
        firstReleased.textContent = `(first released in ${firstReleaseYear})`;
        element.append(firstReleased);
    }
}

const MUSICBRAINZ_ICON_URL = "https://musicbrainz.org/static/images/favicons/favicon-32x32.png";

// Icons linking to the release on MusicBrainz and its album on Spotify. Both are always there, so
// it's clear where the release could be linked: one we don't know the id for is greyed out and
// does nothing. externalIds is ReleaseDetails.externalIds, keyed by ExternalSource name.
function renderLinks(element, externalIds) {
    const musicBrainzId = externalIds.MusicBrainz;
    const musicBrainzIcon = document.createElement("img");
    musicBrainzIcon.src = MUSICBRAINZ_ICON_URL;
    musicBrainzIcon.alt = "";
    musicBrainzIcon.className = "w-6 h-6";
    musicBrainzIcon.referrerPolicy = "no-referrer";
    // MusicBrainz unreachable: its name instead of a broken image
    musicBrainzIcon.addEventListener("error", () => musicBrainzIcon.replaceWith("MusicBrainz"), { once: true });

    const spotifyId = externalIds.Spotify;
    const spotifyLogo = document.createElement("span");
    spotifyLogo.innerHTML = spotifyIcon({ style: "color: #1ED760" });

    element.replaceChildren(
        musicBrainzId ?
            createLink(`https://musicbrainz.org/release/${encodeURIComponent(musicBrainzId)}`, "Open on MusicBrainz", musicBrainzIcon) :
            createDisabledLink("Not linked to a MusicBrainz release", musicBrainzIcon),
        spotifyId ?
            createLink(`https://open.spotify.com/album/${encodeURIComponent(spotifyId)}`, "Open on Spotify", spotifyLogo) :
            createDisabledLink("Not linked to a Spotify album", spotifyLogo));
}

const LINK_CLASS = "inline-flex items-center text-sm text-slate-500 dark:text-slate-400";

function createLink(href, label, icon) {
    const link = document.createElement("a");
    link.href = href;
    link.target = "_blank";
    link.rel = "noopener noreferrer";
    link.title = label;
    link.setAttribute("aria-label", label);
    link.className = `${LINK_CLASS} opacity-80 hover:opacity-100 transition-opacity`;
    link.append(icon);
    return link;
}

// The same icon, but grey and not a link: there's nowhere to go
function createDisabledLink(label, icon) {
    const disabled = document.createElement("span");
    disabled.title = label;
    disabled.setAttribute("role", "link");
    disabled.setAttribute("aria-disabled", "true");
    disabled.setAttribute("aria-label", label);
    disabled.className = `${LINK_CLASS} grayscale opacity-40 cursor-not-allowed`;
    disabled.append(icon);
    return disabled;
}

// The artist's image beside the question, so it's clear whom it's about. creator is a CreatorSummary.
function createArtistQuestion(creator, question) {
    const element = document.createElement("div");
    element.className = "flex items-center gap-4";

    const text = document.createElement("p");
    text.className = "flex-1 min-w-0";
    text.textContent = question;

    element.append(
        createCoverImage({
            src: backendApi.toImageSrc(creator.imageUrl),
            alt: creator.name,
            className: "shrink-0 w-24 h-24 rounded-lg",
            placeholderClassName: "text-xs bg-slate-200 text-slate-500 dark:bg-slate-700 dark:text-slate-400",
            placeholder: "No image"
        }),
        text);
    return element;
}

// Asks first, then removes the release and goes back to the collection. An artist who's on none of
// the user's releases anymore is offered for removal too, one at a time. Never throws.
async function removeRelease(record) {
    const { id, title, artistName } = record.summary;
    const description = artistName ? `"${title}" by ${artistName}` : `"${title}"`;
    if (!await Modal.confirm("Remove release?", `Remove ${description} from your collection? This can't be undone.`,
        { yesText: "Remove", noText: "Cancel", danger: true })) {
        return;
    }

    let removed;
    try {
        removed = await backendApi.removeRelease(id);
    }
    catch (e) {
        Toast.error(e.message, "Removing the release failed");
        return;
    }

    Toast.success(`${description} was removed from your collection`, "Release removed");

    // The release is gone either way, so its page is too
    await router.navigate("/");

    for (const creator of removed.creatorsWithoutReleases ?? []) {
        if (!await Modal.confirm("Remove artist too?",
            createArtistQuestion(creator, `"${creator.name}" isn't on any other release in your collection. Remove them as well?`),
            { yesText: "Remove artist", noText: "Keep", danger: true })) {
            continue;
        }

        try {
            await backendApi.removeCreator(creator.id);
            Toast.success(`"${creator.name}" was removed from your collection`, "Artist removed");
        }
        catch (e) {
            Toast.error(e.message, `Removing "${creator.name}" failed`);
        }
    }
}