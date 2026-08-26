import * as Modal from "./modal.js";
import * as styles from "../styles.js";

/**
 * Shows a dialog for resolving one or more artist names against possible
 * existing-artist matches from the backend. Stacks on top of whatever
 * modal is currently open (e.g. the Add Record form), rather than
 * replacing it. Cancel simply closes the dialog
 *
 * @param {Object<string, Array<{id: string|number, name: string, thumbnail: string}>>} matches
 *   Keyed by the artist name as entered by the user. Each value is the
 *   backend's list of candidate matches for that name. An empty array
 *   means that there was no match.
 *
 * @returns {Promise<null | Object<string, {type: "existing", id, name} | {type: "new", name}>>}
 *   Resolves to `null` if the user cancels. Otherwise, it resolves to an
 *   object keyed by the same artist names, where each value records what
 *   the user chose for it.
 */
export function confirm(matches) {
    return new Promise(resolve => {
        let resolved = false;
        function resolveOnce(value) {
            if (resolved)
                return;
            resolved = true;
            resolve(value);
        }

        const artistNames = Object.keys(matches);
        const selections = new Map(); // index -> { type: "existing", id, name } | { type: "new", name }

        const root = document.createElement("div");
        root.innerHTML = `
        <p class="text-sm
                  text-slate-500
                  dark:text-slate-400
                  mb-4">
            We found some possible existing matches. Pick the right artist
            for each name below, or add it as new.
        </p>
        <div id="artistRows"
             class="max-h-72
                    overflow-y-auto
                    space-y-3
                    pr-1"></div>
        `;

        const rowsContainer = root.querySelector("#artistRows");

        artistNames.forEach((name, index) => {
            const options = matches[name] ?? [];
            const groupName = `artist-row-${index}`;

            const row = document.createElement("div");
            row.className = "border border-slate-200 dark:border-slate-700 rounded-lg p-3 space-y-2";
            row.innerHTML = `
                <div class="font-medium
                            text-sm
                            text-slate-900
                            dark:text-slate-100">"${name}"</div>
                <div class="space-y-1"></div>
            `;

            const optionsContainer = row.querySelector(".space-y-1");

            function addOption({ value, thumbnail, label, isNew = false }) {
                const optionLabel = document.createElement("label");
                optionLabel.className = `
                    flex
                    items-center
                    gap-3
                    p-2
                    rounded-md
                    cursor-pointer
                    border
                    border-transparent
                    hover:bg-slate-50
                    dark:hover:bg-slate-700/50
                    transition-colors
                `;

                optionLabel.innerHTML = `
                    <input type="radio"
                           name="${groupName}"
                           value="${value}"class="sr-only">
                    ${isNew
                        ? `<div class="w-8
                                       h-8
                                       rounded-full
                                       shrink-0
                                       bg-slate-100
                                       dark:bg-slate-800
                                       flex
                                       items-center
                                       justify-center
                                       text-slate-400
                                       text-lg">+</div>`
                        : `<img src="${thumbnail}" 
                                class="w-8
                                       h-8
                                       rounded-full
                                       object-cover
                                       shrink-0"
                                       alt="">`
                    }
                    <span class="text-sm">${label}</span>
                `;

                optionLabel.querySelector("input").addEventListener("change", () => {
                    selections.set(index, isNew
                        ? { type: "new", name }
                        : { type: "existing", id: value, name: label });
                    updateConfirmButtonState();
                    optionsContainer.querySelectorAll("label").forEach(x => {
                        const isSelected = x.querySelector("input").checked;
                        x.classList.toggle("bg-indigo-50", isSelected);
                        x.classList.toggle("dark:bg-indigo-900/30", isSelected);
                        x.classList.toggle("border-indigo-400", isSelected);
                        x.classList.toggle("dark:border-indigo-400", isSelected);
                    });

                });

                optionsContainer.appendChild(optionLabel);
            }

            for (const match of options) {
                addOption({ value: match.id, thumbnail: match.thumbnail, label: match.name });
            }

            // We can always add the artist as a new artist
            addOption({ value: "new", label: `Add "${name}" as a new artist`, isNew: true });

            rowsContainer.appendChild(row);
        });

        function updateConfirmButtonState() {
            const allResolved = selections.size === artistNames.length;
            confirmButton.disabled = !allResolved;
            confirmButton.classList.toggle("opacity-50", !allResolved);
            confirmButton.classList.toggle("cursor-not-allowed", !allResolved);
        }

        function buildResolvedMap() {
            const result = {};
            artistNames.forEach((name, index) => {
                result[name] = selections.get(index);
            });
            return result;
        }

        const modalHandle = Modal.show({
            title: "Confirm artists",
            content: root,
            maxWidthClass: "max-w-lg",
            buttons: [
                {
                    type: "cancel",
                    action: () => {
                        resolveOnce(null);
                        return true;
                    }
                },
                {
                    text: "Confirm",
                    className: styles.buttonPrimary,
                    action: () => {
                        resolveOnce(buildResolvedMap());
                        return true;
                    }
                }
            ]
        });

        // Escape, clicking outside the dialog, and the header's (x) button
        // all bypass the button actions above and call modal.close()
        // directly. Without patching the close function here, we would never
        // resolve the Promise and hence, bnever return if the user cancels
        // via any other method than pressing the "Cancel" button.
        const originalClose = modalHandle.close;
        modalHandle.close = () => {
            resolveOnce(null);
            originalClose();
        };

        const confirmButton = modalHandle.dialog.querySelectorAll("#modalFooter button")[1];
        updateConfirmButtonState();
    });
}