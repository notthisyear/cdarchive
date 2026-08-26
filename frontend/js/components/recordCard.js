import * as router from "../router.js";
import * as util from "../util.js";
import * as styles from "../styles.js"

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

    card.querySelector(".cover").src = `/images/${record.summary.imageUrl}`;
    card.querySelector(".cover").alt = record.summary.name;

    card.querySelector(".artist").textContent = util.concatenateArtists(record.summary.artists);

    card.querySelector(".album").textContent = record.summary.name;
    card.querySelector(".year").textContent = record.summary.year;

    card.firstElementChild.addEventListener("click", () => {
        router.navigate("/records/" + record.id)
    });
    return card;
}