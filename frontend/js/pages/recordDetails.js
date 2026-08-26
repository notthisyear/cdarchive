import * as api from "../api.js";
import * as util from "../util.js"
import * as styles from "../styles.js";
import * as trackListing from "../components/trackListing.js"

export async function render(params) {
    const record = await api.getRecord(params.id);

    const root = document.createElement("div");

    root.className = "space-y-8";

    root.innerHTML = `
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
            
            <!-- Left side -->
            <div class="space-y-6">
                <img src="/images/${record.summary.imageUrl}"
                     alt="${record.summary.name}"
                     class="
                        w-full
                        rounded-xl
                        shadow-2xl
                        object-cover
                    ">

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
            
                    <h1 class="text-3xl font-bold text-slate-900 dark:text-white">
                        ${record.summary.name}
                    </h1>

                    <h2 class=" text-xl text-slate-600 dark:text-slate-300 mt-1">
                        ${util.concatenateArtists(record.summary.artists)}
                    </h2>

                    <dl class="mt-6 space-y-4">
                        <div class="flex justify-between">
                            <dt class="text-slate-500 dark:text-slate-400">
                                Year
                            </dt>
                            <dd>
                                ${record.summary.year}
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

    return root;
}