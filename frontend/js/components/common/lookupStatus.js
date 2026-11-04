import { spinnerSvg } from "./spinner.js";

// A status line for lookups that wait on MusicBrainz, with a spinner while busy and a Retry
// button when the lookup failed and can be tried again. Keeps its height while empty, so the
// layout doesn't jump when it appears. Meant for a dialog's footer (Modal's footerContent).
//
//   const status = lookupStatus.create();
//   Modal.show({ ..., footerContent: status.element });
//   status.show("Looking up the album on MusicBrainz...");
//   status.show("Couldn't reach MusicBrainz.", { busy: false, onRetry: () => lookUpAgain() });
//   status.hide();
export function create() {
    const element = document.createElement("p");
    element.setAttribute("role", "status");
    element.setAttribute("aria-live", "polite");
    element.className = `invisible shrink-0 min-h-5 flex items-center justify-center gap-2 text-center
                         text-sm text-slate-500 dark:text-slate-400`;
    element.innerHTML = `
        ${spinnerSvg("lookup-spinner h-4 w-4")}
        <span class="lookup-text"></span>
        <button type="button"
                class="lookup-retry hidden shrink-0 font-medium text-blue-500 dark:text-blue-400
                       hover:text-blue-400 dark:hover:text-blue-300 transition">
            Retry
        </button>
    `;

    const text = element.querySelector(".lookup-text");
    const spinner = element.querySelector(".lookup-spinner");
    const retryBtn = element.querySelector(".lookup-retry");

    // Set by show() when the step that failed can be tried again
    let retry = null;

    function show(message, { busy = true, onRetry = null } = {}) {
        text.textContent = message;
        spinner.classList.toggle("hidden", !busy);
        retry = onRetry;
        retryBtn.classList.toggle("hidden", !onRetry);
        element.classList.remove("invisible");
    }

    function hide() {
        element.classList.add("invisible");
        text.textContent = "";
        retry = null;
        retryBtn.classList.add("hidden");
    }

    retryBtn.addEventListener("click", () => {
        const onRetry = retry;
        if (!onRetry)
            return;

        hide();
        onRetry();
    });

    return { element, show, hide };
}
