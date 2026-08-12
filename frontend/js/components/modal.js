import * as styles from "../styles.js";

const modalStack = []

const variants = {
    save: {
        className: styles.buttonPrimary,
        text: "Save"
    },
    delete: {
        className: styles.buttonDanger,
        text: "Delete"
    },
    close: {
        className: styles.buttonSecondary,
        text: "Close"
    },
    cancel: {
        className: styles.buttonSecondary,
        text: "Cancel"
    },
};

export function show(options) {
    const overlay = document.createElement("div");

    overlay.className = `
        fixed inset-0
        flex items-center justify-center
        bg-black/50
        opacity-0
        transition-opacity
        duration-200
    `;
    const zIndex = 1000 + modalStack.length * 10;
    overlay.style.zIndex = zIndex;

    const maxWidthClass = options.maxWidthClass ?? "max-w-lg";

    overlay.innerHTML = `
        <div class="bg-white
                    dark:bg-slate-800

                    text-slate-900
                    dark:text-white

                    rounded-xl
                    shadow-2xl
                    
                    w-full
                    ${maxWidthClass}

                    mx-4

                    overflow-hidden

                    opacity-0
                    scale-95

                    transition-all
                    duration-200"

                    role="dialog"

                    aria-modal="true"

                    tabindex="-1">
            <div class="
                    px-6
                    py-4

                    border-b

                    border-slate-200
                    dark:border-slate-700

                    flex
                    justify-between
                    items-center">

                <h2 class="text-xl font-semibold">
                    ${options.title}
                </h2>

                <button data-modal-close
                        class="text-slate-500 hover:text-red-500 text-2xl">
                        ×
                </button>
            </div>

            <div class="pb-6 pr-6 pl-6 pt-4"
                id="modalContent">
            </div>

            <div class="px-6
                        py-4
                        border-t
                        border-slate-200
                        dark:border-slate-700
                        flex
                        justify-end
                        gap-3"
                id="modalFooter">
            </div>
        </div>
    `;

    document.body.appendChild(overlay);

    const dialog = overlay.firstElementChild;
    const content = dialog.querySelector("#modalContent");

    if (typeof options.content === "string") {
        content.innerHTML = options.content;
    } else {
        content.appendChild(options.content);
    }

    const modal = {
        overlay,
        dialog,
        previouslyFocused: document.activeElement,
        keyDownHandler: null,
        close() { close(modal) }
    };

    const footer = dialog.querySelector("#modalFooter");
    for (const button of options.buttons ?? []) {
        addButton(modal, button, footer);
    }

    modalStack.push(modal);

    hookEvents(modal, options);

    requestAnimationFrame(() => {
        overlay.classList.remove("opacity-0");
        dialog.classList.remove("opacity-0", "scale-95");
    });

    dialog.focus();
}

export async function confirm(title, message) {
    return new Promise(resolve => {
        const body = document.createElement("p");
        body.textContent = message;
        show({
            title,
            content: body,
            buttons: [
                {
                    "text": "No",
                    "className": styles.buttonSecondary,
                    "action": () => resolve(false)
                },
                {
                    "text": "Yes",
                    "className": styles.buttonPrimary,
                    "action": () => resolve(true)
                }
            ]
        });
    });
}

export function alert(title, message) {
    const body = document.createElement("p");
    body.textContent = message;
    show({
        title,
        content: body,
        buttons: [{ "text": "Ok", "className": styles.buttonPrimary, "action": () => { } }]
    });
}

function addButton(modal, button, footer) {
    const element = document.createElement("button");
    element.type = "button";
    if (button.type) {
        element.textContent = variants[button.type].text;
        element.className = variants[button.type].className;
    }
    else {
        element.textContent = button.text;
        element.className = button.className;
    }

    element.addEventListener("click", async () => {
        const shouldClose = await button.action();
        if (shouldClose !== false) {
            modal.close();
        }
    });
    footer.appendChild(element);
}

function hookEvents(modal, options) {
    async function closeModalAndInvokeCloseActionIfAny() {
        if (options.buttons && options.buttons.some(x => x.type === "close")) {
            const shouldClose = await options.buttons.find(x => x.type === "close").action();
            if (shouldClose)
                modal.close();
        }
        else {
            modal.close();
        }
    }

    async function closeModalAndInvokeSaveActionIfAny() {
        if (options.buttons && options.buttons.some(x => x.type === "save")) {
            const shouldClose = await options.buttons.find(x => x.type === "save").action();
            if (shouldClose)
                modal.close();
        }
        else {
            modal.close();
        }
    }

    modal.overlay.addEventListener("click", e => {
        if (e.target !== modal.overlay)
            return;

        if (modalStack.at(-1) !== modal)
            return;

        closeModalAndInvokeCloseActionIfAny();
    });

    modal.dialog.querySelector("[data-modal-close]").onclick = closeModalAndInvokeCloseActionIfAny;

    const keyDownHandler = e => {
        if (modalStack.at(-1) !== modal)
            return;

        if (e.key === "Escape")
            closeModalAndInvokeCloseActionIfAny();
        else if (e.key === "Enter" && options.saveOnEnter && options.saveOnEnter === true)
            closeModalAndInvokeSaveActionIfAny();
    };

    document.addEventListener("keydown", keyDownHandler);
    modal.keyDownHandler = keyDownHandler;
}

function close(modal) {
    modal.overlay.classList.add("opacity-0");
    modal.overlay.firstElementChild.classList.add("opacity-0", "scale-95");
    document.removeEventListener("keydown", modal.keyDownHandler);

    setTimeout(() => {
        modal.overlay.remove();
        modal.previouslyFocused?.focus();
    }, 200);

    modalStack.pop();
}

