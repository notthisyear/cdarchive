import * as backendApi from "../api/backend.js";
import * as styles from "../styles.js";
import * as addArtistForm from "../components/addArtistForm.js";

import { Autocomplete } from "./autocomplete.js";

// A single "find or add a creator" input: a text box with autocomplete, a dropdown toggle and a
// status icon (yellow = not matched to a catalog entry yet, green = matched).
//
// Used for artist rows in the music form and for cast/crew rows in the film form. It deliberately
// has no remove button: the row that contains it owns that (see createRemoveButton below).
//
//   const picker = createCreatorPicker({ placeholder: "Search for an artist...", creatorType: "Artist", onChange });
//   picker.element              DOM to place in a row
//   picker.getValue()           { name, id }   (id is null until matched)
//   picker.setValue({ name, id })
//   picker.resolve()            async, looks up the current name and fills in the id if exactly one match
//   picker.isResolved(), picker.focus(), picker.dispose()
//
// Options:
//   lookup(name) => Promise<[{ id, name, imageUrl }]>   defaults to api.getArtists
//   creatorType                                          passed on to the "Add new..." form
//   required                                             browser validation on the text box
//   onChange                                             called when validity may have changed

const ADD_NEW_SENTINEL = { __addNew: true };

function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, c => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[c]);
}

async function defaultLookup(name) {
    const response = await backendApi.getCreators([{ name, id: null }]);
    return response[name] ?? [];
}

export function createCreatorPicker({
    placeholder = "Search...",
    creatorType = null,
    lookup = defaultLookup,
    required = true,
    onChange
} = {}) {
    const element = document.createElement("div");
    element.className = "flex items-center gap-2";
    element.innerHTML = `
        <div class="shrink-0 w-5 h-5 flex items-center justify-center">
            <!-- Yellow exclamation (default / unresolved) -->
            <div title="Not in the catalog yet, please add it from the dropdown">
                <svg class="status-unresolved w-5 h-5 text-yellow-400" viewBox="0 0 20 20" fill="currentColor">
                    <path fill-rule="evenodd"
                        d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l6.518 11.59c.75 1.334-.213 2.987-1.743 2.987H3.482c-1.53 0-2.493-1.653-1.743-2.987l6.518-11.59zM10 13a1 1 0 100-2 1 1 0 000 2zm-.75-6.25a.75.75 0 011.5 0v3.5a.75.75 0 01-1.5 0v-3.5z"
                        clip-rule="evenodd" />
                </svg>
            </div>
            <!-- Green checkmark (resolved) -->
            <div title="Found in the catalog">
                <svg class="status-resolved hidden w-5 h-5 text-green-500" viewBox="0 0 20 20" fill="currentColor">
                    <path fill-rule="evenodd"
                        d="M16.704 5.29a1 1 0 010 1.415l-7.004 7a1 1 0 01-1.414 0l-3.5-3.5a1 1 0 111.414-1.414l2.793 2.792 6.296-6.293a1 1 0 011.415 0z"
                        clip-rule="evenodd" />
                </svg>
            </div>
        </div>
        <div class="relative flex-1 min-w-0">
            <input type="text"
                data-picker-name
                class="${styles.editorInputBox} w-full pr-8"
                autocomplete="off"
                ${required ? "required" : ""}
                placeholder="${escapeHtml(placeholder)}">
            <button type="button"
                    data-picker-toggle
                    class="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 hover:text-slate-200"
                    tabindex="-1"
                    aria-label="Show options">
                <svg class="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                    <path fill-rule="evenodd"
                          d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.24 4.25a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z"
                          clip-rule="evenodd" />
                </svg>
            </button>
            <input type="hidden" data-picker-id>

            <!-- Class name kept from the original artist rows in case Autocomplete looks for it -->
            <ul class="artist-dropdown hidden absolute left-0 right-0 top-full mt-1 z-20
                       bg-slate-800 border border-slate-700 rounded-lg shadow-lg
                       max-h-56 overflow-y-auto">
            </ul>
        </div>
    `;

    const nameInput = element.querySelector("[data-picker-name]");
    const idInput = element.querySelector("[data-picker-id]");
    const toggleBtn = element.querySelector("[data-picker-toggle]");

    function updateStatus() {
        const resolved = idInput.value.trim().length > 0;
        element.querySelector(".status-resolved").classList.toggle("hidden", !resolved);
        element.querySelector(".status-unresolved").classList.toggle("hidden", resolved);
    }

    // If the user types in the name box, whatever was matched before no longer applies
    nameInput.addEventListener("input", () => {
        idInput.value = "";
        updateStatus();
        onChange?.();
    });

    const autocomplete = new Autocomplete({
        input: nameInput,
        search: async (q) => {
            let results = [];
            const queryString = q.trim();
            if (queryString.length > 0) {
                try {
                    results = await lookup(queryString);
                }
                catch (e) {
                    console.error(e);
                    results = [];
                }

                // Exactly one hit counts as a match
                idInput.value = results.length === 1 ? results[0].id : "";
                updateStatus();
                onChange?.();
            }
            return [...results, ADD_NEW_SENTINEL];
        },
        renderItem(item) {
            if (item.__addNew) {
                return `<div class="px-3 py-2 text-sm text-blue-400 hover:bg-slate-700 cursor-pointer border-slate-700">
                        Add new...
                    </div>`;
            }
            const imageSrc = backendApi.toImageSrc(item.imageUrl);
            const image = imageSrc
                ? `<img src="${escapeHtml(imageSrc)}" class="w-12 h-12 rounded object-cover shrink-0" />`
                : `<div class="w-12 h-12 rounded shrink-0 bg-slate-200 dark:bg-slate-700"></div>`;
            return `
                <div class="flex items-center gap-4">
                    ${image}
                    <div class="px-3 py-2 text-sm text-slate-200 hover:bg-slate-700 cursor-pointer">${escapeHtml(item.name)}</div>
                </div>`;
        },
        async onSelected(item) {
            if (item.__addNew) {
                const result = await addArtistForm.show(nameInput.value, { creatorType });
                if (result !== null) {
                    nameInput.value = result.name;
                    idInput.value = result.id;
                }
            }
            else {
                nameInput.value = item.name;
                idInput.value = item.id;
            }

            updateStatus();
            onChange?.();
        }
    });

    toggleBtn.addEventListener("click", (e) => {
        e.stopPropagation();
        if (autocomplete.isOpen)
            autocomplete.hide();
        else
            autocomplete.open();
    });

    updateStatus();

    return {
        element,

        getValue: () => ({ name: nameInput.value.trim(), id: idInput.value || null }),

        setValue({ name = "", id = null } = {}) {
            nameInput.value = name;
            idInput.value = id ?? "";
            updateStatus();
        },

        isResolved: () => idInput.value !== "",

        focus: () => nameInput.focus(),

        async resolve() {
            const name = nameInput.value;
            if (name.trim() === "")
                return;

            try {
                const results = await lookup(name);

                // The user may have edited the box while we were waiting
                if (nameInput.value === name)
                    idInput.value = results.length === 1 ? results[0].id : "";
            }
            catch (e) {
                console.error(e);
            }

            updateStatus();
            onChange?.();
        },

        dispose() {
            autocomplete.dispose?.();
        }
    };
}

// The small "x" that appears when hovering a row. The row must have the `group` class.
export function createRemoveButton(label = "Remove") {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `
        remove-btn shrink-0 w-6 h-6 rounded-full
        flex items-center justify-center
        text-slate-500 hover:text-white hover:bg-red-500
        opacity-0 group-hover:opacity-100 focus:opacity-100
        transition
    `;
    button.setAttribute("aria-label", label);
    button.title = label;
    button.innerHTML = `
        <svg class="w-3.5 h-3.5" viewBox="0 0 20 20" fill="currentColor">
            <path d="M6.28 5.22a.75.75 0 00-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 101.06 1.06L10 11.06l3.72 3.72a.75.75 0 101.06-1.06L11.06 10l3.72-3.72a.75.75 0 00-1.06-1.06L10 8.94 6.28 5.22z" />
        </svg>
    `;
    return button;
}