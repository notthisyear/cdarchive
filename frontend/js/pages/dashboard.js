import * as api from "../api.js";
import * as recordCard from "../components/recordCard.js"; 

export async function render() {

    const records = await api.getRecordsSummary(0, 8);
    const root = document.createElement("div");

    root.innerHTML = "";

    const heading = document.createElement("div");

    heading.className =
        "flex justify-between items-center mb-8";

    heading.innerHTML = `
        <div>
            <h2 class="text-3xl font-bold">
                Collection
            </h2>

            <p class="text-slate-400 mt-1">
                ${records.length} records
            </p>
        </div>
    `;

    root.appendChild(heading);

    const grid = document.createElement("div");
    grid.className = "grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-6";

    records.forEach(record => {
        grid.append(recordCard.create(record));
    });

    root.appendChild(grid);

    return root;
}