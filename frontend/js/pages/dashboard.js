import * as addReleaseDialog from "../components/addReleaseDialog.js";
import * as backendApi from "../api/backend.js";
import * as auth from "../auth.js";
import * as recordCard from "../components/recordCard.js";
import * as styles from "../styles.js";

const PAGE_SIZE = 8;
export async function render() {

    const releases = await backendApi.getReleasesSummary(0, PAGE_SIZE);
    const totalNumberOfReleases = await backendApi.getTotalNumberOfReleases();

    const root = document.createElement("div");

    root.innerHTML = "";

    const heading = document.createElement("div");

    heading.className =
        "flex justify-between items-center mb-8";

    heading.innerHTML = `
        <div>
            <h2 class="text-3xl font-bold">
            ${auth.getUsername()}'s Collection
            </h2>

            <p class="text-slate-400 mt-1">
                ${totalNumberOfReleases} records
            </p>
        </div>
    `;

    root.appendChild(heading);

    const grid = document.createElement("div");
    grid.className = "grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-6";

    releases.forEach(r => {
        grid.append(recordCard.create(r));
    });

    const addNewRecordCard = document.createElement("div");
    addNewRecordCard.className = styles.addRecordCard;
    addNewRecordCard.innerHTML = `
        <div class="h-64 w-full flex items-center justify-center">
            <svg class="w-12 h-12 text-slate-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
            </svg>
        </div>
        <div class="p-4 text-center">
            <h2 class="text-lg font-semibold text-slate-400">Add new..</h2>
            <p class="text-sm text-slate-500 mt-1">Click to add new record</p>
        </div>
    `;

    grid.append(addNewRecordCard);

    addNewRecordCard.addEventListener("click", async () => {
        await addReleaseDialog.show();
    });
    root.appendChild(grid);


    let offset = releases.length;
    let loading = false;
    let hasMoreReleases = releases.length === PAGE_SIZE;

    const sentinel = document.createElement("div");
    sentinel.className = "col-span-full flex justify-center py-8 min-h-[4rem]";
    sentinel.innerHTML = `
        <svg class="animate-spin h-6 w-6 text-slate-500"
             viewBox="0 0 24 24"
             fill="none">
            <circle class="opacity-25" cx="12" cy="12" r="10"
                    stroke="currentColor"
                    stroke-width="4"></circle>
            <path class="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"></path>
        </svg>
    `;
    sentinel.style.visibility = "hidden";

    grid.append(sentinel);

    const observer = new IntersectionObserver(async (entries) => {
        const entry = entries[0];

        console.log("before if check")
        if (!entry.isIntersecting || loading || !hasMoreReleases)
            return;
        console.log("after if check")

        loading = true;
        sentinel.style.visibility = "visible";

        try {
            const nextReleases = await backendApi.getReleasesSummary(offset, PAGE_SIZE);

            nextReleases.forEach(r => {
                grid.insertBefore(recordCard.create(r), addNewRecordCard);
            });

            offset += nextReleases.length;

            if (nextReleases.length < PAGE_SIZE) {
                hasMoreReleases = false;
                observer.disconnect();
            }
        } catch (err) {
            console.error("Failed to load more releases", err);
            hasMoreReleases = false;
            observer.disconnect();
        } finally {
            loading = false;
            sentinel.style.visibility = "hidden";
        }
    },
        {
            root: null,          // viewport
            rootMargin: "400px", // start loading before the sentinel is actually visible
            threshold: 0
        });

    if (hasMoreReleases) {
        observer.observe(sentinel);
    }

    return root;
}