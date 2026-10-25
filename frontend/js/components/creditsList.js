import * as styles from "../styles.js";
import { createCreatorPicker, createRemoveButton } from "./creatorPicker.js";

// An editable list of credits: who was involved, in which role, in what order.
// Generic over roles, so the film form can use Director/Writer/Actor/... and a book form could
// reuse it with Author/Illustrator/Editor.
//
//   const credits = createCreditsList({
//       roles: [
//           { value: "Director", label: "Director" },
//           { value: "Actor", label: "Actor", hasCharacter: true },   // shows a "Character" box
//       ],
//       creatorType: "Person",
//       lookup: name => ...,       // optional, see createCreatorPicker
//       onChange: () => { }
//   });
//
//   credits.element         DOM to place in a form
//   credits.add({ name, creatorId, role, characterName })
//   credits.clear()
//   credits.getItems()      [{ name, creatorId, role, characterName, sortOrder }]  (empty rows skipped;
//                           sortOrder is the row's position, i.e. billing order)
//   credits.hasUnresolved() true if any named credit isn't matched to a catalog creator yet
//   credits.resolveAll()    async, looks up every row's name (use after importing credits)
//   credits.dispose()
//
// `role` values should match the backend's CreditRole enum names.

export function createCreditsList({ roles, creatorType = "Person", lookup, onChange } = {}) {
    const roleByValue = new Map(roles.map(r => [r.value, r]));
    const entries = [];

    const element = document.createElement("div");

    const rowsHost = document.createElement("div");
    rowsHost.className = "space-y-2";

    const addButton = document.createElement("button");
    addButton.type = "button";
    addButton.className = "mt-2 flex items-center gap-1 text-sm text-slate-400 hover:text-slate-200 transition";
    addButton.innerHTML = `
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
        </svg>
        Add credit
    `;
    // New rows start with the same role as the previous one: handy when adding a whole cast
    addButton.addEventListener("click", () => {
        const entry = add({ role: entries.at(-1)?.roleSelect.value });
        entry.picker.focus();
    });

    element.append(rowsHost, addButton);

    function add({ name = "", creatorId = null, role, characterName = "" } = {}) {
        const row = document.createElement("div");
        row.className = "credit-row group flex items-center gap-2";

        const picker = createCreatorPicker({
            placeholder: "Search for a person...",
            creatorType,
            lookup,
            onChange
        });
        picker.element.classList.add("flex-1", "min-w-0");
        if (name !== "")
            picker.setValue({ name, id: creatorId });

        const roleSelect = document.createElement("select");
        roleSelect.className = `${styles.editorInputBox} w-32 shrink-0`;
        roleSelect.setAttribute("aria-label", "Role");
        for (const r of roles) {
            const option = document.createElement("option");
            option.value = r.value;
            option.textContent = r.label;
            roleSelect.append(option);
        }
        roleSelect.value = roleByValue.has(role) ? role : roles[0].value;

        const characterInput = document.createElement("input");
        characterInput.type = "text";
        characterInput.autocomplete = "off";
        characterInput.placeholder = "Character";
        characterInput.setAttribute("aria-label", "Character name");
        characterInput.className = `${styles.editorInputBox} w-36 shrink-0`;
        characterInput.value = characterName;

        const removeButton = createRemoveButton("Remove credit");

        const entry = { row, picker, roleSelect, characterInput };

        function syncCharacterVisibility() {
            const hasCharacter = roleByValue.get(roleSelect.value)?.hasCharacter ?? false;
            characterInput.classList.toggle("hidden", !hasCharacter);
        }

        roleSelect.addEventListener("change", () => {
            syncCharacterVisibility();
            onChange?.();
        });
        characterInput.addEventListener("input", () => onChange?.());
        removeButton.addEventListener("click", () => remove(entry));

        syncCharacterVisibility();

        row.append(picker.element, roleSelect, characterInput, removeButton);
        rowsHost.append(row);
        entries.push(entry);
        onChange?.();

        return entry;
    }

    function remove(entry) {
        entry.picker.dispose();
        entry.row.remove();
        entries.splice(entries.indexOf(entry), 1);
        onChange?.();
    }

    function clear() {
        for (const entry of entries)
            entry.picker.dispose();
        entries.length = 0;
        rowsHost.innerHTML = "";
        onChange?.();
    }

    function getItems() {
        return entries
            .map(e => {
                const { name, id } = e.picker.getValue();
                const hasCharacter = roleByValue.get(e.roleSelect.value)?.hasCharacter ?? false;
                return {
                    name,
                    creatorId: id,
                    role: e.roleSelect.value,
                    characterName: hasCharacter ? (e.characterInput.value.trim() || null) : null
                };
            })
            .filter(item => item.name.length > 0)
            .map((item, index) => ({ ...item, sortOrder: index }));
    }

    return {
        element,
        add,
        clear,
        getItems,
        hasUnresolved: () => getItems().some(item => item.creatorId === null),
        resolveAll: () => Promise.all(entries.map(e => e.picker.resolve())),
        dispose() {
            for (const entry of entries)
                entry.picker.dispose();
            entries.length = 0;
        }
    };
}