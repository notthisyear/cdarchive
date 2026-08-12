import * as util from "../util.js";
import * as styles from "../styles.js";

export class Autocomplete {

    // These are private fields
    #options;
    #dropdown;
    #items = [];
    #searchId = 0;
    #selectedIndex = -1;
    #debouncedSearch;

    // Bound listener references, kept so we can remove them later in dispose()
    #boundInputHandler = () => {
        const query = this.#options.input.value.trim();
 
        if (query.length === 0) {
            this.hide();
        } else {
            // Show feedback right away, even before the debounce timer
            // fires
            this.#items = [];
            this.#selectedIndex = -1;
            this.#renderLoading();
        }
        this.#debouncedSearch();
    }
    #boundKeyDown = e => {
        this.#onKeyDown(e).catch(err => console.error("onKeyDown failed: ", err))
    };
    #boundDocumentClick = e => this.#onDocumentClick(e);

    constructor(options) {
        this.#options = options;
        this.#createDropdown();

        // Create the debounced function ONCE and reuse it on every keystroke,
        // so it can actually cancel/reset its own pending timer.
        this.#debouncedSearch = util.debounce(
            () => this.#search(),
            this.#options?.debounceTime ?? 200
        );

        options.input.setAttribute("autocomplete", "off");
        options.input.setAttribute("role", "combobox");
        options.input.setAttribute("aria-expanded", "false");
        options.input.setAttribute("aria-autocomplete", "list");

        options.input.addEventListener("input", this.#boundInputHandler);
        options.input.addEventListener("keydown", this.#boundKeyDown);
        document.addEventListener("click", this.#boundDocumentClick);
    }

    #createDropdown() {
        const parent = this.#options.input.parentElement;
        if (!parent) {
            throw new Error("No parent element for autocomplete dropdown");
        }

        if (!parent.classList.contains("relative")) {
            console.warn("No relative parent element for autocomplete dropdown");
        }

        this.#dropdown = document.createElement("div");
        this.#dropdown.className = styles.autocompleteDropdown;

        this.#dropdown.setAttribute("role", "listbox");
        parent.appendChild(this.#dropdown);
    }

    async #search() {
        const id = ++this.#searchId;
        const query = this.#options.input.value.trim();

        if (query.length === 0) {
            this.hide();
            return;
        }

        let results;
        try {
            results = await this.#options.search(query);
        } catch (err) {
            console.error("Autocomplete search failed -", err);
            if (id === this.#searchId) {
                this.#items = [];
                this.hide();
            }
            return;
        }

        if (id !== this.#searchId)
            return; // A newer search has already started.

        this.#selectedIndex = -1;
        this.#items = results;
        this.#render();
    }

    #renderLoading() {
        this.#dropdown.innerHTML = `
            <div class="flex items-center gap-3 px-4 py-3 text-slate-500 dark:text-slate-400">
                <svg class="animate-spin h-4 w-4 shrink-0" viewBox="0 0 24 24" fill="none">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                </svg>
                <span class="text-sm">Searching...</span>
            </div>
        `;
        this.#options.input.setAttribute("aria-busy", "true");
        this.show();
    }

    #render() {
        this.#options.input.setAttribute("aria-busy", "false");
        this.#dropdown.innerHTML = "";

        if (this.#items.length === 0) {
            this.hide();
            return;
        }

        this.show();

        this.#items.forEach((item, index) => {
            const row = document.createElement("button");
            row.type = "button";
            row.id = `autocomplete-option-${index}`;
            row.setAttribute("role", "option");
            row.className = styles.autocompleteDropdownRow;
            row.innerHTML = this.#options.renderItem(item);
            row.addEventListener("click", async () => await this.#select(index));
            this.#dropdown.appendChild(row);
        });
    }

    #selecting = false;
    async #select(index) {
        if (this.#selecting)
            return;
        this.#selecting = true;
        try {
            const item = this.#items[index];
            this.hide();
            await this.#options.onSelected(item);
        } finally {
            this.#selecting = false;
        }
    }

    async #onKeyDown(event) {
        switch (event.key) {
            case "ArrowDown":
                if (this.#items.length === 0) break;
                event.preventDefault();
                this.#selectedIndex = Math.min(this.#selectedIndex + 1, this.#items.length - 1);
                this.#highlight();
                break;

            case "ArrowUp":
                if (this.#items.length === 0) break;
                event.preventDefault();
                this.#selectedIndex = this.#selectedIndex <= 0 ? -1 : this.#selectedIndex - 1;
                this.#highlight();
                break;

            case "Enter":
                if (this.#selectedIndex >= 0) {
                    event.preventDefault();
                    await this.#select(this.#selectedIndex);
                }
                break;

            case "Escape":
                this.hide();
                break;
        }
    }

    #highlight() {
        const rows = this.#dropdown.children;
        for (let i = 0; i < rows.length; i++) {
            const active = i === this.#selectedIndex;
            rows[i].classList.toggle("bg-slate-100", active);
            rows[i].classList.toggle("dark:bg-slate-700", active);
            if (active) {
                rows[i].scrollIntoView({ block: "nearest" });
                this.#options.input.setAttribute("aria-activedescendant", rows[i].id);
            }
        }

        if (this.#selectedIndex === -1) {
            this.#options.input.removeAttribute("aria-activedescendant");
        }
    }

    #onDocumentClick(event) {
        if (!this.#dropdown.contains(event.target) && event.target !== this.#options.input) {
            this.hide();
        }
    }

    show() {
        this.#dropdown.classList.remove("hidden");
        this.#options.input.setAttribute("aria-expanded", "true");
    }

    hide() {
        this.#dropdown.classList.add("hidden");
        this.#options.input.setAttribute("aria-expanded", "false");
        this.#options.input.removeAttribute("aria-activedescendant");
    }

    dispose() {
        this.#options.input.removeEventListener("input", this.#boundInputHandler);
        this.#options.input.removeEventListener("keydown", this.#boundKeyDown);
        document.removeEventListener("click", this.#boundDocumentClick);

        this.#dropdown.remove();
    }
}