import * as styles from "../styles.js";

const CONTAINER_ID = "toastContainer";
const DEFAULT_DURATION = 4000;

const variants = {
    success: {
        borderClass: "border-l-4 border-green-500",
        iconWrapperClass: "bg-green-100 text-green-600 dark:bg-green-900/40 dark:text-green-400",
        icon: `
            <svg class="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd"
                      d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75 0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 011.05-.143z"
                      clip-rule="evenodd" />
            </svg>
        `
    },
    error: {
        borderClass: "border-l-4 border-red-500",
        iconWrapperClass: "bg-red-100 text-red-600 dark:bg-red-900/40 dark:text-red-400",
        icon: `
            <svg class="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd"
                      d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.28 7.22a.75.75 0 00-1.06 1.06L8.94 10l-1.72 1.72a.75.75 0 101.06 1.06L10 11.06l1.72 1.72a.75.75 0 101.06-1.06L11.06 10l1.72-1.72a.75.75 0 00-1.06-1.06L10 8.94 8.28 7.22z"
                      clip-rule="evenodd" />
            </svg>
        `
    },
    info: {
        borderClass: "border-l-4 border-slate-400",
        iconWrapperClass: "bg-slate-100 text-slate-500 dark:bg-slate-700 dark:text-slate-300",
        icon: `
            <svg class="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd"
                      d="M18 10A8 8 0 11 2 10a8 8 0 0116 0zM9 9a1 1 0 012 0v4a1 1 0 11-2 0V9zm1-4a1 1 0 100 2 1 1 0 000-2z"
                      clip-rule="evenodd" />
            </svg>
        `
    }
};

function getContainer() {
    let container = document.getElementById(CONTAINER_ID);
    if (!container) {
        container = document.createElement("div");
        container.id = CONTAINER_ID;
        container.className = styles.toastNotificationContainer;
        document.body.appendChild(container);
    }
    return container;
}

export function show({ type = "info", title, message, duration = DEFAULT_DURATION } = {}) {
    const container = getContainer();
    const variant = variants[type] ?? variants.info;

    const toast = document.createElement("div");
    toast.setAttribute("role", type === "error" ? "alert" : "status");
    toast.setAttribute("aria-live", type === "error" ? "assertive" : "polite");
    toast.className = `
        ${variant.borderClass}
        ${styles.toastNotification}
    `;

    toast.innerHTML = `
        <div class="w-7 h-7 rounded-full flex items-center justify-center shrink-0 ${variant.iconWrapperClass}">
            ${variant.icon}
        </div>
        <div class="flex-1 min-w-0">
            ${title ? `<div class="font-medium text-sm">${title}</div>` : ""}
            ${message ? `<div class="text-sm text-slate-500 dark:text-slate-400">${message}</div>` : ""}
        </div>
        <button type="button"
                data-toast-close
                aria-label="Dismiss notification"
                class="shrink-0 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
            <svg class="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                <path d="M6.28 5.22a.75.75 0 00-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 101.06 1.06L10 11.06l3.72 3.72a.75.75 0 101.06-1.06L11.06 10l3.72-3.72a.75.75 0 00-1.06-1.06L10 8.94 6.28 5.22z" />
            </svg>
        </button>
    `;

    // New toasts enter at the bottom and push older ones up -- container
    // uses flex-col-reverse so appending here visually stacks upward.
    container.appendChild(toast);

    requestAnimationFrame(() => {
        toast.classList.remove("opacity-0", "translate-x-4");
    });

    let timeoutId = null;

    function dismiss() {
        clearTimeout(timeoutId);
        toast.classList.add("opacity-0", "translate-x-4");
        toast.addEventListener("transitionend", () => toast.remove(), { once: true });
    }

    function startTimer() {
        if (duration > 0) {
            timeoutId = setTimeout(dismiss, duration);
        }
    }

    // Pause auto-dismiss while the user's mouse is over it, so a toast
    // doesn't disappear while they're in the middle of reading it.
    toast.addEventListener("mouseenter", () => clearTimeout(timeoutId));
    toast.addEventListener("mouseleave", startTimer);
    toast.querySelector("[data-toast-close]").addEventListener("click", dismiss);

    startTimer();

    return { dismiss };
}

export function success(message, title = "Success") {
    return show({ type: "success", title, message });
}

export function error(message, title = "Something went wrong") {
    return show({ type: "error", title, message });
}

export function info(message, title) {
    return show({ type: "info", title, message });
}