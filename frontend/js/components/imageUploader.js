/**
 * Creates a self-contained image upload widget.
 *
 * @param {Object} [options]
 * @param {string} [options.placeholderText="No image selected"]
 * @param {string} [options.alt="Image"]
 * @param {(file: File) => void} [options.onFileSelected] - called when a new file is chosen
 * @param {() => void} [options.onRemove] - called when the remove button is pressed
 * @returns {{
 *   element: HTMLElement,
 *   setImage: (url: string) => void,
 *   clear: () => void,
 *   getImageUrl: () => string | null
 *   getFile: () => File | null
 * }}
 */
export function create(options = {}) {
    const {
        placeholderText = "No image selected",
        alt = "Image",
        onFileSelected,
        onRemove,
    } = options;

    const root = document.createElement("div");
    root.className = "shrink-0";
    root.innerHTML = `
        <div class="image-uploader-container relative group w-40 aspect-square rounded-lg border
                    border-slate-200 dark:border-slate-700 overflow-hidden
                    bg-slate-50 dark:bg-slate-900 flex items-center justify-center">
            <img class="image-uploader-preview w-full h-full object-cover hidden" alt="${alt}">

            <div class="image-uploader-placeholder text-sm text-slate-400 text-center px-4">
                ${placeholderText}
            </div>

            <div class="absolute inset-0 flex items-center justify-center
                        bg-black/0 group-hover:bg-black/30
                        transition-colors pointer-events-none">
                <button type="button"
                        class="image-uploader-upload-btn pointer-events-auto w-9 h-9 rounded-full
                            bg-white/90 dark:bg-slate-800/90 shadow
                            flex items-center justify-center
                            opacity-0 group-hover:opacity-100
                            hover:bg-white dark:hover:bg-slate-700
                            transition"
                        aria-label="Upload image"
                        title="Upload image...">
                    <svg class="w-4 h-4 text-slate-600 dark:text-slate-300"
                        viewBox="0 0 20 20" fill="currentColor">
                        <path d="M9.25 13.75v-8.614l-2.955 3.129a.75.75 180 01-1.09-1.03l4.25-4.5a.75.75 180 011.09 0l4.25 4.5a.75.75 180 11-1.09 1.03L10.75 5.136V13.75a.75.75 180 01-1.5 0Z" />
                        <path d="M3.5 12.75a.75.75 0 00-1.5 0v2.5A2.75 2.75 0 004.75 18h10.5A2.75 2.75 0 0018 15.25v-2.5a.75.75 0 00-1.5 0v2.5c0 .69-.56 1.25-1.25 1.25H4.75c-.69 0-1.25-.56-1.25-1.25v-2.5z" />
                    </svg>
                </button>
            </div>

            <button type="button"
                    class="image-uploader-remove-btn hidden absolute top-2 right-2 w-6 h-6 rounded-full group/remove
                        bg-white dark:bg-slate-800 hover:bg-red-500 dark:hover:bg-red-500
                        shadow items-center justify-center
                        opacity-0 group-hover:opacity-100
                        transition"
                    aria-label="Remove image"
                    title="Remove image">
                <svg class="w-3.5 h-3.5 text-slate-600 dark:text-slate-300
                            group-hover/remove:text-white transition-colors"
                    viewBox="0 0 20 20" fill="currentColor">
                    <path d="M6.28 5.22a.75.75 0 00-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 101.06 1.06L10 11.06l3.72 3.72a.75.75 0 101.06-1.06L11.06 10l3.72-3.72a.75.75 0 00-1.06-1.06L10 8.94 6.28 5.22z" />
                </svg>
            </button>
        </div>

        <input type="file" class="image-uploader-file-input hidden" accept="image/*">
    `;

    const previewImg = root.querySelector(".image-uploader-preview");
    const placeholder = root.querySelector(".image-uploader-placeholder");
    const removeBtn = root.querySelector(".image-uploader-remove-btn");
    const uploadOverlayBtn = root.querySelector(".image-uploader-upload-btn");
    const fileInput = root.querySelector(".image-uploader-file-input");

    let currentFile = null;

    function setImage(url) {
        previewImg.src = url;
        previewImg.classList.remove("hidden");
        placeholder.classList.add("hidden");
        removeBtn.classList.remove("hidden");
        removeBtn.classList.add("flex");
    }

    function clear() {
        previewImg.removeAttribute("src");
        previewImg.classList.add("hidden");
        placeholder.classList.remove("hidden");
        removeBtn.classList.add("hidden");
        removeBtn.classList.remove("flex");
        fileInput.value = "";
        currentFile = null;
    }

    uploadOverlayBtn.addEventListener("click", () => fileInput.click());

    fileInput.addEventListener("change", () => {
        const file = fileInput.files?.[0];
        if (!file)
            return;

        currentFile = file;

        const reader = new FileReader();
        reader.onload = () => setImage(reader.result);
        reader.readAsDataURL(file);

        onFileSelected?.(file);
    });

    removeBtn.addEventListener("click", () => {
        clear();
        onRemove?.();
    });

    return {
        element: root,
        setImage,
        clear,
        getImageUrl: () => previewImg.src,
        getFile: () => currentFile,
    };
}