// Flags are images rather than emoji, since Windows has no flag emoji and shows "SE" instead of 🇸🇪.
const FLAG_URL = "https://flagcdn.com";

// MusicBrainz uses a few codes that aren't ISO 3166-1 alpha-2 countries.
const WORLDWIDE = "XW";
const SPECIAL_REGIONS = {
    XE: "eu"    // Europe
};

// An element showing the country's flag, or null if the code isn't one we can show.
export function createFlagElement(code) {
    const upper = code?.trim().toUpperCase() ?? "";

    if (upper === WORLDWIDE) {
        const globe = document.createElement("span");
        globe.className = "mr-1.5";
        globe.textContent = "🌍";
        globe.title = "Worldwide";
        globe.setAttribute("aria-label", "Worldwide");
        return globe;
    }

    const flagCode = SPECIAL_REGIONS[upper] ?? (/^[A-Z]{2}$/.test(upper) ? upper.toLowerCase() : null);
    if (!flagCode)
        return null;

    const image = document.createElement("img");
    image.className = "inline-block h-3 w-auto mr-1.5 align-baseline rounded-sm";
    image.src = `${FLAG_URL}/h20/${flagCode}.png`;
    image.srcset = `${FLAG_URL}/h40/${flagCode}.png 2x`;
    image.alt = upper;
    image.title = upper;
    image.loading = "lazy";
    // Unknown code or flagcdn unreachable: show the code as text instead of a broken image
    image.addEventListener("error", () => image.replaceWith(`${upper} · `), { once: true });
    return image;
}
