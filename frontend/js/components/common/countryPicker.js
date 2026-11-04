import * as styles from "../../styles.js";
import * as countries from "./countries.js";
import { createFlagElement } from "./countryFlag.js";
import { Autocomplete } from "../autocomplete.js";

// A country box: free text to filter the list, but a country only counts once it's picked from it
// (or its full name is typed), with its flag beside the box. MusicBrainz's XE (Europe) and XW
// (Worldwide) are among the countries.
//
//   const country = createCountryPicker({ id: "artistCountry", onChange, onPicked });
//   parent.append(country.element);      // put a <label for="artistCountry"> before it
//   country.getCode()                    // "SE", or "" while none is picked
//   country.setCode("se")                // shows Sweden; an unknown or missing code shows none
//   country.setDisabled(true), country.dispose()
//
// Options:
//   id         the text box's id, for its label
//   required   browser validation on the text box
//   onChange   called whenever the picked country (or whether there is one) may have changed
//   onPicked   called when the user picked a country (from the list or by typing its full name),
//              but not for setCode()
export function createCountryPicker({ id, required = false, onChange, onPicked } = {}) {
    const element = document.createElement("div");
    element.className = "flex items-center gap-2";
    element.innerHTML = `
        <div class="relative flex-1 min-w-0">
            <input type="text"
                    ${required ? "required" : ""}
                    autocomplete="off"
                    class="${styles.editorInputBox} w-full pr-8"
                    placeholder="Search..." />
            <button type="button"
                    class="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 hover:text-slate-200"
                    tabindex="-1"
                    aria-label="Show countries">
                <svg class="w-4 h-4" viewBox="0 0 20 20" fill="currentColor">
                    <path fill-rule="evenodd"
                          d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.24 4.25a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z"
                          clip-rule="evenodd" />
                </svg>
            </button>
        </div>
        <!-- Flag of the picked country; keeps its width while empty -->
        <span class="shrink-0 w-8 flex items-center justify-center" aria-hidden="true"></span>
    `;

    const input = element.querySelector("input");
    const toggle = element.querySelector("button");
    const flagSlot = element.querySelector("span");
    if (id)
        input.id = id;

    // The picked country's code, or "" while none is
    let code = "";

    function updateFlag() {
        const flag = code ? createFlagElement(code) : null;
        if (flag)
            flag.className = "h-5 w-auto rounded-sm";
        flagSlot.replaceChildren(...(flag ? [flag] : []));
    }

    function setCode(newCode) {
        const country = countries.byCode(newCode);
        input.value = country?.name ?? "";
        code = country?.code ?? "";
        updateFlag();
        onChange?.();
    }

    function renderOption(country) {
        const flag = createFlagElement(country.code);
        return `
            <div class="flex items-center gap-2">
                ${flag ? flag.outerHTML : ""}
                <span>${country.name}</span>
            </div>`;
    }

    const autocomplete = new Autocomplete({
        input,
        debounceTime: 0,
        maxVisibleItems: 8,
        // With a country picked, the box holds its full name, so show them all to choose another
        search: async (q) => code ? countries.ALL : countries.startingWith(q),
        renderItem: renderOption,
        onSelected(country) {
            setCode(country.code);
            onPicked?.();
        }
    });

    toggle.addEventListener("click", (e) => {
        e.stopPropagation();
        if (autocomplete.isOpen)
            autocomplete.hide();
        else
            autocomplete.open();
    });

    // Typing means no country is picked until one is chosen from the filtered list
    input.addEventListener("input", () => {
        code = "";
        updateFlag();
        onChange?.();
    });

    // Typing a country's full name counts as picking it
    input.addEventListener("blur", () => {
        if (code)
            return;
        const match = countries.byName(input.value);
        if (match) {
            setCode(match.code);
            onPicked?.();
        }
    });

    return {
        element,
        getCode: () => code,
        setCode,
        // The shared input style greys the box out while disabled; the flag is greyed out to match
        setDisabled(disabled) {
            input.disabled = disabled;
            flagSlot.classList.toggle("opacity-50", disabled);
            toggle.disabled = disabled;
            toggle.classList.toggle("hidden", disabled);
            if (disabled)
                autocomplete.hide();
        },
        dispose: () => autocomplete.dispose()
    };
}
