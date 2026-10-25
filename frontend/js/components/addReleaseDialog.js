import * as Modal from "./modal.js";
import * as Toast from "./toast.js";
import { createTabs } from "./common/tabs.js";

import * as musicForm from "./forms/musicForm.js"
// import * as filmForm from "./filmForm.js";
// import * as bookForm from "./bookForm.js";

// Every tab's create() returns a "form view" with this shape (see musicForm.js):
//
//   element      DOM to show in the tab
//   noun         "record" / "film" / "book", used in toasts and tooltips
//   getValidity  () => ({ ok, reason })   drives the Save button
//   validate     () => boolean            browser validation UI, called when Save is clicked
//   isDirty      () => boolean            any unsaved changes?
//   save         async () => string       sends it to the API, returns the name for the toast
//   dispose      () => void               cleanup (autocompletes etc.)
//   footerStatus HTMLElement (optional)   status line shown in the dialog's footer while the tab is active

export function show() {
    let tabs = null;
    let saveButton = null;
    let blockClose = false;

    const overlay = createLoadingOverlay();

    function refreshSaveButton() {
        const view = tabs?.active;
        if (!saveButton || !view)
            return;

        const { ok, reason } = view.getValidity();
        saveButton.disabled = !ok;
        saveButton.classList.toggle("opacity-50", !ok);
        saveButton.classList.toggle("cursor-not-allowed", !ok);
        saveButton.title = ok ? `Click to add ${view.noun}` : reason;
    }

    const defs = [
        { id: "music", label: "Music", create: () => musicForm.create({ onChange: refreshSaveButton }) },
        { id: "film", label: "Film", create: notImplemented("Film") },
        { id: "book", label: "Book", create: notImplemented("Book") },
    ];

    // The footer shows the active tab's status line, if it has one
    const footerStatus = document.createElement("div");
    function refreshFooterStatus() {
        const status = tabs?.active?.footerStatus;
        footerStatus.replaceChildren(...(status ? [status] : []));
    }

    tabs = createTabs({
        tabs: defs,
        selected: "music",
        // Switching tabs changes which form the Save button and the status line belong to
        onChange: () => {
            refreshSaveButton();
            refreshFooterStatus();
        }
    });

    const content = document.createElement("div");
    content.append(tabs.element, overlay.element);

    const modalHandle = Modal.show({
        title: "Add to Collection",
        content,
        footerContent: footerStatus,
        maxWidthClass: "max-w-7/10",
        buttons: [
            {
                "type": "save",
                "action": async () => {
                    const view = tabs.active;
                    if (!view.validate())
                        return false;

                    blockClose = true;
                    overlay.show();

                    const noun = view.noun;
                    const Noun = noun[0].toUpperCase() + noun.slice(1);

                    try {
                        const name = await view.save();

                        // Success: queue the toast and reload so the page shows the new item.
                        // The overlay stays up until the reload, so there's no flash of the form.
                        Toast.showAfterReload({ type: "success", title: `New ${noun} saved`, message: `${Noun} '${name}' added successfully` });
                        location.reload();
                        return true;
                    }
                    catch (e) {
                        // Failure: stay in the dialog with everything the user typed intact.
                        console.error(e);
                        overlay.hide();
                        blockClose = false;

                        Toast.show({
                            type: "error",
                            title: `Adding ${noun} failed`,
                            message: `Could not add ${noun} - ${e?.message ?? e}`,
                            duration: 8000
                        });
                        return false;     // keeps the modal open
                    }
                }
            },
            {
                "type": "close",
                "action": async () => {
                    if (blockClose)
                        return false;

                    // Only tabs that were actually opened have a view
                    const views = defs.map(d => tabs.getView(d.id)).filter(Boolean);

                    let shouldClose = true;
                    if (views.some(v => v.isDirty()))
                        shouldClose = await Modal.confirm("Unsaved changes", "Do you want to discard unsaved changes?");

                    if (shouldClose)
                        views.forEach(v => v.dispose());
                    return shouldClose;
                }
            }
        ]
    });

    // Direct children only: the status line in the footer has a Retry button of its own
    saveButton = modalHandle.dialog.querySelectorAll("#modalFooter > button")[0];
    refreshSaveButton();
    refreshFooterStatus();

    return modalHandle;
}

function createLoadingOverlay() {
    const element = document.createElement("div");
    element.className = `
        absolute inset-0 z-10
        bg-black/50
        flex items-center justify-center
        opacity-0 pointer-events-none
        transition-opacity duration-200
    `;
    element.innerHTML = `
        <svg class="animate-spin h-30 w-30 text-white" viewBox="0 0 24 24" fill="none">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
        </svg>
    `;

    return {
        element,
        show() {
            element.classList.remove("opacity-0", "pointer-events-none");
            element.classList.add("opacity-100", "pointer-events-auto");
        },
        hide() {
            element.classList.add("opacity-0", "pointer-events-none");
            element.classList.remove("opacity-100", "pointer-events-auto");
        }
    };
}

// Placeholder tab until the real form exists
function notImplemented(label) {
    return () => {
        const element = document.createElement("p");
        element.className = "py-8 text-center text-slate-500 dark:text-slate-400";
        element.textContent = `The ${label.toLowerCase()} form isn't built yet.`;

        return {
            element,
            noun: label.toLowerCase(),
            getValidity: () => ({ ok: false, reason: `Adding ${label.toLowerCase()}s isn't supported yet` }),
            validate: () => false,
            isDirty: () => false,
            save: async () => { throw new Error("Not implemented"); },
            dispose() { }
        };
    };
}