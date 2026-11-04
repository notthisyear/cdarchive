// A rating of five stars in half steps, for a score of 1-10 (each point half a star). While the
// pointer is over the stars, they fill up to it in a lighter colour, to show what a click would
// give. Clicking the rating it already has clears it. Works as a slider from the keyboard too:
// arrows step by half a star, Delete (or Backspace) clears it.
//
//   const rating = createStarRating({ value: 7, label: "Your rating", onChange: score => save(score) });
//   parent.append(rating.element);
//   rating.setValue(8)     // shows it without calling onChange, e.g. to undo a failed save
//
// onChange gets the new score (1-10), or null when cleared.

const STAR_PATH = "M10.788 3.21c.448-1.077 1.976-1.077 2.424 0l2.082 5.006 5.404.434c1.164.093 1.636 1.545.749 2.305l-4.117 3.527 1.257 5.273c.271 1.136-.964 2.033-1.96 1.425L12 18.354 7.373 21.18c-.996.608-2.231-.29-1.96-1.425l1.257-5.273-4.117-3.527c-.887-.76-.415-2.212.749-2.305l5.404-.434 2.082-5.005Z";

const MAX_SCORE = 10;

// The saved rating, and the one the pointer is showing
const SAVED_COLOR = "text-yellow-400";
const HOVER_COLOR = "text-yellow-400/50";

function starSvg(classes) {
    return `<svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" class="${classes}">
                <path fill-rule="evenodd" clip-rule="evenodd" d="${STAR_PATH}"/>
            </svg>`;
}

// "3.5 of 5 stars" for 7
function describe(score) {
    return score ? `${score / 2} of 5 stars` : "Not rated";
}

export function createStarRating({ value = null, label = "Rating", onChange } = {}) {
    let score = value;

    const element = document.createElement("div");
    element.className = "inline-flex items-center gap-3";

    const stars = document.createElement("div");
    stars.className = "inline-flex items-center gap-0.5 cursor-pointer select-none rounded focus:outline-none focus-visible:ring-2 focus-visible:ring-yellow-400";
    stars.tabIndex = 0;
    stars.setAttribute("role", "slider");
    stars.setAttribute("aria-label", label);
    stars.setAttribute("aria-valuemin", "0");
    stars.setAttribute("aria-valuemax", String(MAX_SCORE));

    // A fixed width, room for the widest text ("4.5 of 5 stars"), so the stars beside it never move
    // when it changes while hovering. Right-aligned, so it lines up with the right edge of the box.
    const text = document.createElement("span");
    text.className = "inline-block w-28 shrink-0 whitespace-nowrap tabular-nums text-right text-sm text-slate-500 dark:text-slate-400";

    // Each star: an empty one, with a filled one on top cut to how much of it is filled
    const fills = [];
    for (let i = 0; i < 5; i++) {
        const star = document.createElement("span");
        star.className = "relative w-6 h-6";
        star.innerHTML = starSvg("w-6 h-6 text-slate-300 dark:text-slate-600");

        const fill = document.createElement("span");
        fill.className = "absolute inset-y-0 left-0 overflow-hidden";
        fill.innerHTML = starSvg("w-6 h-6 max-w-none");
        star.append(fill);

        // The left half of a star is half a star
        star.addEventListener("mousemove", e => {
            const rect = star.getBoundingClientRect();
            show(i * 2 + (e.clientX - rect.left < rect.width / 2 ? 1 : 2), { hovering: true });
        });

        stars.append(star);
        fills.push(fill);
    }

    function show(shown, { hovering = false } = {}) {
        fills.forEach((fill, i) => {
            const halves = Math.max(0, Math.min(2, (shown ?? 0) - i * 2));
            fill.style.width = `${halves * 50}%`;
            fill.classList.toggle(SAVED_COLOR, !hovering);
            fill.classList.toggle(HOVER_COLOR, hovering);
        });
        text.textContent = describe(shown);
    }

    function setValue(newScore) {
        score = newScore ?? null;
        stars.setAttribute("aria-valuenow", String(score ?? 0));
        stars.setAttribute("aria-valuetext", describe(score));
        show(score);
    }

    function change(newScore) {
        const next = newScore >= 1 ? Math.min(MAX_SCORE, newScore) : null;
        if (next === score)
            return;
        setValue(next);
        onChange?.(next);
    }

    stars.addEventListener("mouseleave", () => show(score));

    // Clicking where it already is clears it
    stars.addEventListener("click", e => {
        const star = e.target.closest?.("span.relative");
        const i = [...stars.children].indexOf(star);
        if (i < 0)
            return;
        const rect = star.getBoundingClientRect();
        const clicked = i * 2 + (e.clientX - rect.left < rect.width / 2 ? 1 : 2);
        change(clicked === score ? null : clicked);
    });

    stars.addEventListener("keydown", e => {
        if (e.key === "ArrowRight" || e.key === "ArrowUp")
            change((score ?? 0) + 1);
        else if (e.key === "ArrowLeft" || e.key === "ArrowDown")
            change((score ?? 0) - 1);
        else if (e.key === "Home")
            change(1);
        else if (e.key === "End")
            change(MAX_SCORE);
        else if (e.key === "Delete" || e.key === "Backspace")
            change(null);
        else
            return;
        e.preventDefault();
    });

    element.append(stars, text);
    setValue(score);

    return { element, setValue, getValue: () => score };
}
