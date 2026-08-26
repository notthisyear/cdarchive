import * as addRecordForm from "../components/addRecordForm.js";
import * as api from "../api.js";
import * as auth from "../auth.js";
import * as recordCard from "../components/recordCard.js";
import * as styles from "../styles.js";

const PAGE_SIZE = 8;
export async function render() {

    const records = await api.getRecordsSummary(0, PAGE_SIZE);
    const totalNumberOfRecords = await api.getTotalNumberOfRecords();

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
                ${totalNumberOfRecords} records
            </p>
        </div>
    `;

    root.appendChild(heading);

    const grid = document.createElement("div");
    grid.className = "grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-6";

    records.forEach(record => {
        grid.append(recordCard.create(record));
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

    addNewRecordCard.addEventListener("click", () => {
        addRecordForm.show();
    });
    root.appendChild(grid);


    let offset = records.length;
    let loading = false;
    let hasMoreRecords = records.length === PAGE_SIZE;

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
        if (!entry.isIntersecting || loading || !hasMoreRecords)
            return;
        console.log("after if check")

        loading = true;
        sentinel.style.visibility = "visible";

        try {
            const nextRecords = await api.getRecordsSummary(offset, PAGE_SIZE);

            nextRecords.forEach(record => {
                grid.insertBefore(recordCard.create(record), addNewRecordCard);
            });

            offset += nextRecords.length;

            if (nextRecords.length < PAGE_SIZE) {
                hasMoreRecords = false;
                observer.disconnect();
            }
        } catch (err) {
            console.error("Failed to load more records", err);
            hasMoreRecords = false;
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

    if (hasMoreRecords) {
        observer.observe(sentinel);
    }

    return root;
}