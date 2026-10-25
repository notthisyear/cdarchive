// Reusable tab strip with a sliding underline and swappable content.
//
//   const tabs = createTabs({
//       tabs: [
//           { id: "music", label: "Music", create: createMusicForm },
//           { id: "film",  label: "Film",  create: createFilmForm },
//       ],
//       selected: "music",
//       onChange: id => { }
//   });
//   parent.appendChild(tabs.element);
//   tabs.active.getData();   // the view of the currently selected tab
//
// `create` is the equivalent of a WPF DataTemplate: a factory that builds the view for one tab.
// It may return a DOM element, or an object `{ element, ...anything }` (e.g. getData, validate).
// Views are created lazily the first time a tab is selected, then kept alive (just hidden), so
// whatever the user typed is still there when they switch back.

const ACTIVE = ["font-bold", "text-indigo-600", "dark:text-indigo-400"];                  // <- accent color
const INACTIVE = ["text-slate-500", "dark:text-slate-400", "hover:text-slate-900", "dark:hover:text-white"];
const INDICATOR = "bg-indigo-600 dark:bg-indigo-400";                                      // <- accent color

let nextId = 0;

export function createTabs({ tabs, selected = tabs[0].id, onChange }) {
    const uid = nextId++;
    let current = null;

    const element = document.createElement("div");

    const bar = document.createElement("div");
    bar.className = "relative flex gap-6 border-b border-slate-200 dark:border-slate-700 mb-4";
    bar.setAttribute("role", "tablist");

    const indicator = document.createElement("span");
    indicator.className = `absolute -bottom-px left-0 h-0.5 ${INDICATOR} transition-[transform,width] duration-300 ease-in-out`;
    indicator.setAttribute("aria-hidden", "true");
    bar.appendChild(indicator);

    const panels = document.createElement("div");
    element.append(bar, panels);

    // id -> { def, button, panel, view }
    const entries = new Map();

    for (const def of tabs) {
        const button = document.createElement("button");
        button.type = "button";
        button.id = `tab-${uid}-${def.id}`;
        button.className = "grid px-1 pb-2 transition-colors";
        button.setAttribute("role", "tab");
        button.setAttribute("aria-controls", `panel-${uid}-${def.id}`);

        // Two stacked labels in the same grid cell: the visible one, and an invisible bold copy.
        // The button is always as wide as the bold text, so switching to bold never shifts the layout.
        const visible = document.createElement("span");
        visible.className = "col-start-1 row-start-1 justify-self-center";
        visible.textContent = def.label;
        const sizer = document.createElement("span");
        sizer.className = "col-start-1 row-start-1 justify-self-center font-bold invisible";
        sizer.setAttribute("aria-hidden", "true");
        sizer.textContent = def.label;
        button.append(visible, sizer);

        const panel = document.createElement("div");
        panel.id = `panel-${uid}-${def.id}`;
        panel.setAttribute("role", "tabpanel");
        panel.setAttribute("aria-labelledby", button.id);
        panel.hidden = true;

        button.addEventListener("click", () => select(def.id));

        bar.appendChild(button);
        panels.appendChild(panel);
        entries.set(def.id, { def, button, panel, view: null });
    }

    function moveIndicator(animate) {
        const entry = entries.get(current);
        // offsetLeft/offsetWidth ignore CSS transforms, so this is correct even while the
        // modal is still doing its scale-95 -> scale-100 entrance animation.
        const { offsetLeft, offsetWidth } = entry.button;
        if (offsetWidth === 0)
            return; // not attached/laid out yet; the ResizeObserver below will call us again

        if (!animate)
            indicator.style.transition = "none";

        indicator.style.width = `${offsetWidth}px`;
        indicator.style.transform = `translateX(${offsetLeft}px)`;

        if (!animate) {
            indicator.getBoundingClientRect(); // force reflow so the jump isn't animated
            indicator.style.transition = "";
        }
    }

    function select(id, { animate = true } = {}) {
        const entry = entries.get(id);
        if (!entry)
            return;

        const changed = id !== current;
        current = id;

        for (const e of entries.values()) {
            const isActive = e === entry;
            e.button.setAttribute("aria-selected", String(isActive));
            e.button.tabIndex = isActive ? 0 : -1;
            e.button.classList.remove(...(isActive ? INACTIVE : ACTIVE));
            e.button.classList.add(...(isActive ? ACTIVE : INACTIVE));
            e.panel.hidden = !isActive;
        }

        if (!entry.view) {
            const result = entry.def.create();
            entry.view = result instanceof Node ? { element: result } : result;
            entry.panel.appendChild(entry.view.element);
        }

        moveIndicator(animate);

        if (changed)
            onChange?.(id);
    }

    // The tab strip is usually built before it's attached to the DOM, so it can't be measured yet.
    // ResizeObserver fires as soon as the buttons get a real size (and again if fonts load, etc.).
    const observer = new ResizeObserver(() => moveIndicator(false));
    for (const e of entries.values())
        observer.observe(e.button);

    // Arrow keys / Home / End, as per the WAI-ARIA tabs pattern
    bar.addEventListener("keydown", e => {
        const ids = [...entries.keys()];
        let i = ids.indexOf(current);

        if (e.key === "ArrowRight") i = (i + 1) % ids.length;
        else if (e.key === "ArrowLeft") i = (i - 1 + ids.length) % ids.length;
        else if (e.key === "Home") i = 0;
        else if (e.key === "End") i = ids.length - 1;
        else return;

        e.preventDefault();
        select(ids[i]);
        entries.get(ids[i]).button.focus();
    });

    select(selected, { animate: false });

    return {
        element,
        select,
        get activeId() { return current; },
        get active() { return entries.get(current).view; },
        getView(id) { return entries.get(id)?.view ?? null; }
    };
}