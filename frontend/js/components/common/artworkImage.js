// An image box that shows an ArtworkResult from artwork.js together with the credit line that the
// licenses (and good manners) require: author, linked license, and where the image came from.
//
//   const photo = createArtworkImage({ widthClass: "w-40", placeholder: "No photo" });
//   parent.appendChild(photo.element);
//
//   // Fetch and show. Safe to call repeatedly: a newer call cancels the older one, so a slow
//   // response can never overwrite a newer one.
//   await photo.load(signal => artwork.getArtistImage(mbid, { signal }));
//
//   photo.set(result);       // show a result you already have (e.g. loaded from your own database)
//   photo.clear();
//   photo.getResult();       // the ArtworkResult currently shown, or null (to save its credit data)
//   photo.dispose();
//
// For an image shown some other way, createCreditBox() gives just the credit (see below).
//
// Options:
//   widthClass    Tailwind width for the box and its caption (default "w-40")
//   aspectClass   default "aspect-square"
//   roundedClass  default "rounded-lg"
//   placeholder   text shown when there is no image
//   alt           alt text for the image

// Only ever put https URLs from the server into src/href
function safeHttps(url) {
    try {
        const u = new URL(url);
        return u.protocol === "https:" ? u.href : null;
    }
    catch {
        return null;
    }
}

function createLink(text, href) {
    const safe = safeHttps(href);
    if (!safe) {
        const span = document.createElement("span");
        span.textContent = text;
        return span;
    }

    const a = document.createElement("a");
    a.href = safe;
    a.target = "_blank";
    a.rel = "noopener noreferrer";
    a.className = "underline hover:text-slate-700 dark:hover:text-slate-200";
    a.textContent = text;
    return a;
}

const SOURCE_NAMES = {
    wikimedia: "Wikimedia Commons",
    coverartarchive: "Cover Art Archive"
};

/**
 * A small box crediting an ArtworkResult, to put below an image shown some other way (e.g. in an
 * imageUploader). One line each, leaving out what the result doesn't have:
 *   <title, linked to the source page>
 *   By <author>
 *   License: <license, linked to the license>
 *
 *   const credit = createCreditBox();
 *   parent.append(credit.element);
 *   credit.set(result);      // hidden again for null, or with nothing to credit
 *   credit.clear();
 */
export function createCreditBox() {
    const element = document.createElement("div");
    element.className = `hidden rounded-lg border border-slate-200 dark:border-slate-700
                         bg-slate-50 dark:bg-slate-900 px-3 py-2
                         text-xs text-slate-500 dark:text-slate-400 break-words space-y-0.5`;

    function clear() {
        element.replaceChildren();
        element.classList.add("hidden");
    }

    function set(result) {
        const lines = [];

        const title = result?.title?.trim() || SOURCE_NAMES[result?.source];
        if (title) {
            const line = document.createElement("div");
            line.className = "font-medium";
            line.append(createLink(title, result.sourcePageUrl));
            lines.push(line);
        }

        if (result?.author?.trim()) {
            const line = document.createElement("div");
            line.textContent = `By ${result.author.trim()}`;
            lines.push(line);
        }

        if (result?.license?.trim()) {
            const line = document.createElement("div");
            line.append("License: ", createLink(result.license.trim(), result.licenseUrl));
            lines.push(line);
        }

        element.replaceChildren(...lines);
        element.classList.toggle("hidden", lines.length === 0);
    }

    return { element, set, clear };
}

export function createArtworkImage({
    widthClass = "w-40",
    aspectClass = "aspect-square",
    roundedClass = "rounded-lg",
    placeholder = "No image",
    alt = ""
} = {}) {
    const element = document.createElement("figure");
    element.className = widthClass;

    const frame = document.createElement("div");
    frame.className = `${aspectClass} w-full ${roundedClass} border border-slate-200 dark:border-slate-700
                       overflow-hidden bg-slate-50 dark:bg-slate-900 flex items-center justify-center`;

    const img = document.createElement("img");
    img.className = "w-full h-full object-cover hidden";
    img.alt = alt;
    img.loading = "lazy";
    img.decoding = "async";
    img.referrerPolicy = "no-referrer";       // don't tell the image host which page the user is on

    const placeholderElement = document.createElement("div");
    placeholderElement.className = "text-sm text-slate-400 text-center px-4";
    placeholderElement.textContent = placeholder;

    frame.append(img, placeholderElement);

    const caption = document.createElement("figcaption");
    caption.className = "mt-1 text-xs text-slate-500 dark:text-slate-400 break-words hidden";

    element.append(frame, caption);

    let current = null;
    let controller = null;      // for the load currently in flight

    function renderCredit(result) {
        const parts = [];

        if (result.source === "wikimedia") {
            if (result.author)
                parts.push(document.createTextNode(result.author));
            if (result.license)
                parts.push(createLink(result.license, result.licenseUrl));
            parts.push(createLink("Wikimedia Commons", result.sourcePageUrl));
        }
        else if (result.source === "coverartarchive") {
            parts.push(createLink("Cover Art Archive", result.sourcePageUrl));
        }

        caption.replaceChildren();
        parts.forEach((part, i) => {
            if (i > 0)
                caption.append(" · ");
            caption.append(part);
        });
        caption.classList.toggle("hidden", parts.length === 0);
    }

    function clear() {
        current = null;
        img.removeAttribute("src");
        img.classList.add("hidden");
        placeholderElement.classList.remove("hidden");
        caption.replaceChildren();
        caption.classList.add("hidden");
    }

    function set(result) {
        const url = result ? safeHttps(result.url) : null;
        if (!url) {
            clear();
            return;
        }

        current = result;
        img.src = url;
        img.classList.remove("hidden");
        placeholderElement.classList.add("hidden");
        renderCredit(result);
    }

    // A broken image (deleted file, blocked host...) falls back to the placeholder
    img.addEventListener("error", () => {
        if (img.getAttribute("src"))
            clear();
    });

    async function load(fetcher) {
        controller?.abort();
        const mine = new AbortController();
        controller = mine;

        frame.classList.add("animate-pulse");
        try {
            const result = await fetcher(mine.signal);
            if (controller !== mine)
                return;                  // superseded by a newer load
            set(result);
        }
        catch (e) {
            if (e?.name === "AbortError" || controller !== mine)
                return;
            console.warn("Could not load artwork:", e);
            clear();
        }
        finally {
            if (controller === mine) {
                frame.classList.remove("animate-pulse");
                controller = null;
            }
        }
    }

    return {
        element,
        set,
        clear,
        load,
        getResult: () => current,
        dispose() {
            controller?.abort();
            controller = null;
        }
    };
}