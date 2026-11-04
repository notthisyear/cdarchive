import * as router from "../router.js";
import * as styles from "../styles.js"
import * as backendApi from "../api/backend.js"
import { createCoverImage } from "./common/coverImage.js";

// record is a ReleaseSummary (see backendApi.getReleasesSummary)
export function create(record) {
    const card = document.createElement("div");
    card.className = styles.recordCard;
    card.innerHTML = `
        <div class="cover"></div>
        <div class="p-4">
            <h2 class="artist text-lg font-semibold"></h2>
            <p class="album text-slate-400"></p>
            <p class="year text-sm text-slate-500 mt-2"></p>
        </div>
    `;

    // The card is dark in both themes, so the box saying there's no cover is too
    card.querySelector(".cover").replaceWith(createCoverImage({
        src: backendApi.toImageSrc(record.imageUrl),
        alt: record.title,
        className: "h-64 w-full",
        placeholderClassName: "bg-slate-700 text-slate-400"
    }));

    card.querySelector(".artist").textContent = record.artistName;

    card.querySelector(".album").textContent = record.title;
    card.querySelector(".year").textContent = record.year ?? "";

    // The whole card, as the cover may be swapped for the "no cover" box if it fails to load
    card.addEventListener("click", () => {
        router.navigate("/releases/" + record.id)
    });
    return card;
}