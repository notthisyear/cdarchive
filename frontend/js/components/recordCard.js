import * as router from "../router.js";

export function create(record) {
    const card = document.createElement("div");
    card.className = `
        bg-slate-800
        rounded-xl
        shadow-lg
        overflow-hidden
        
        transition-all
        duration-200
        
        hover:-translate-y-1
        hover:shadow-x1
        
        cursor-pointer`;
    
    card.innerHTML = `
        <div class="record-card ">
            <img class="cover h-64 w-full object-cover">
                <div class="p-4">
                    <h2 class="artist text-lg font-semibold"></h2>
                    <p class="album text-slate-400"></p>
                    <p class="year text-sm text-slate-500 mt-2"></p>
                </div>
            </img>
        </div>
    `;
    
    card.querySelector(".cover").src =
        record.coverUrl;

    card.querySelector(".cover").alt =
        record.album;

    card.querySelector(".artist").textContent =
        record.artist.name;

    card.querySelector(".album").textContent =
        record.album;

    card.querySelector(".year").textContent =
        record.year;

    card.firstElementChild.addEventListener("click", () => {
        router.navigate("/records/" + record.id)
    });
    return card;
}