import * as styles from "../styles.js"
import * as Modal from "./modal.js";
import * as api from "../api.js"

export function check(artist) {
    return new Promise(resolve => {
        let resolved = false;
        function resolveOnce(value) {
            if (resolved)
                return;
            resolved = true;
            resolve(value);
        }

        const root = document.createElement("div");
        root.innerHTML = `
          <div class="flex flex-col md:flex-row items-center gap-8 p-6 max-w-4xl mx-auto">
            <div class="flex-1">
                    <h2 class="text-2xl text-slate-300 mb-4">
                        Found exact match for <span class="text-white font-bold">${artist.name}</span>
                    </h2>
                    <p class="text-slate-300 leading-relaxed">
                        Would you like to use the existing artist or create a new artist with the same name?
                    </p>
                </div>

                <div class="flex-1 w-full">
                    <img src="${api.getImageSrcUrl(artist.imageUrl)}"
                         alt="Cover image"
                         class="rounded-2xl w-full h-auto object-cover shadow-lg">
                </div>
            </div>
            `;

        // Actual form
        const modalHandle = Modal.show({
            title: "Create duplicate?",
            content: root,
            maxWidthClass: "max-w-2/5",
            buttons: [
                {
                    "text": "Use existing",
                    "className": styles.buttonPrimary,
                    "action": () => {
                        resolveOnce(false);
                        return true;
                    }
                },
                {
                    "text": "Create new",
                    "className": styles.buttonSecondary,
                    "action": () => {
                        resolveOnce(true);
                        return true;
                    }
                }
            ],
            showCloseIcon: false,
            closeOnClickOutside: false,
            closeOnEscape: false
        });
    });
}