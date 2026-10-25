import * as router from "../router.js";
import * as util from "../util.js";
import * as styles from "../styles.js"
import * as backendApi from "../api/backend.js"

export function create(record) {
    const card = document.createElement("div");
    card.className = styles.recordCard;
    card.innerHTML = `
        <img class="cover h-64 w-full object-cover">
            <div class="p-4">
                <h2 class="artist text-lg font-semibold"></h2>
                <p class="album text-slate-400"></p>
                <p class="year text-sm text-slate-500 mt-2"></p>
            </div>
        </img>
    `;

    // Without a cover, an empty box of the same size instead of a broken image
    const cover = card.querySelector(".cover");
    const coverSrc = backendApi.toImageSrc(record.summary.imageUrl);
    if (coverSrc) {
        cover.src = coverSrc;
        cover.alt = record.summary.name;
    }
    else {
        cover.alt = "";
        cover.classList.add("bg-slate-200", "dark:bg-slate-700");
    }

    card.querySelector(".artist").textContent = util.concatenateArtists(record.summary.artists);

    card.querySelector(".album").textContent = record.summary.name;
    card.querySelector(".year").textContent = record.summary.year;

    card.firstElementChild.addEventListener("click", () => {
        router.navigate("/records/" + record.id)
    });
    return card;
}