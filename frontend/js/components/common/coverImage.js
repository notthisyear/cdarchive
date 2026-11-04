// A release's cover from our own backend, or a box saying there's none in its place.
//
//   const cover = createCoverImage({
//       src: backendApi.toImageSrc(release.imageUrl),     // null when there's no cover
//       alt: release.title,
//       className: "h-64 w-full",                         // size and shape, for both
//       placeholderClassName: "bg-slate-700 text-slate-400"
//   });
//   parent.append(cover);
//
// A cover that fails to load (a deleted file, an expired upload link...) is replaced by the box too.
export function createCoverImage({ src, alt = "", className = "", placeholderClassName = "", placeholder = "No cover art" }) {
    if (!src)
        return createPlaceholder(className, placeholderClassName, placeholder);

    const img = document.createElement("img");
    img.className = `${className} object-cover`;
    img.src = src;
    img.alt = alt;
    img.addEventListener("error", () => {
        img.replaceWith(createPlaceholder(className, placeholderClassName, placeholder));
    }, { once: true });
    return img;
}

function createPlaceholder(className, placeholderClassName, text) {
    const box = document.createElement("div");
    box.className = `${className} ${placeholderClassName} flex items-center justify-center px-4 text-sm text-center select-none`;
    box.textContent = text;
    return box;
}
