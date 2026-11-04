import * as backendApi from "../api/backend.js";
import * as util from "../util.js"
import * as styles from "../styles.js";
import * as trackListing from "../components/trackListing.js"
import { createCoverImage } from "../components/common/coverImage.js";
import * as Modal from "../components/modal.js";
import * as Toast from "../components/toast.js";
import * as router from "../router.js";

export async function render(params) {
    // ReleaseDetails (see backendApi.getRelease)
    const record = await backendApi.getRelease(params.id);

    const root = document.createElement("div");

    root.className = "space-y-8";

    root.innerHTML = `
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
            
            <!-- Left side -->
            <div class="space-y-6">
                <div id="recordCover"></div>

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

                    <dl class="mt-6 space-y-4">
                        <div class="flex justify-between">
                            <dt class="text-slate-500 dark:text-slate-400">
                                Year
                            </dt>
                            <dd>
                                ${record.summary.year ?? "N/a"}
                            </dd>
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
    root.querySelector("#recordArtist").textContent = record.summary.artistName;

    // Without a cover, a square box saying so, as wide as a cover would be
    root.querySelector("#recordCover").replaceWith(createCoverImage({
        src: backendApi.toImageSrc(record.summary.imageUrl),
        alt: record.summary.title,
        className: "w-full rounded-xl shadow-2xl",
        placeholderClassName: "aspect-square text-lg bg-slate-200 text-slate-500 dark:bg-slate-700 dark:text-slate-400"
    }));

    const editRecordSummary = root.querySelector("#editRecordSummary");
    editRecordSummary.addEventListener("click", () => {
        console.log("edit summary");
    });

    const trackListElement = root.querySelector("#trackList");
    trackListing.clearAllTracks(trackListElement);
    for (const track of record.tracks) {
        trackListing.addTrackRow(trackListElement, track.title, track.discNumber, track.trackNumber, track.durationSeconds);
    }

    const addTrackButton = root.querySelector("#addTrackButton");
    addTrackButton.addEventListener("click", () => trackListing.addEmptyTrackRow(trackListElement));

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