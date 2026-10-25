// Frontend wrapper around our own /api/musicbrainz endpoints.
// The backend sets the User-Agent MusicBrainz requires, enforces the 1 request/second limit
// across all users and caches results, so there is nothing to throttle or cache here.
import * as common from "./common.js"

const MUSICBRAINZ_API_URL = "/api/musicbrainz";

// Words that mark a bracketed or dashed suffix as edition info rather than part of the title.
// "Version" is left out on purpose, since it's part of real titles like "Red (Taylor's Version)".
const EDITION_WORDS = String.raw`remaster(?:ed)?|reissue|re-issue|deluxe|expanded|edition|anniversary|bonus|legacy|mono|stereo`;
const BRACKETED_EDITION = new RegExp(String.raw`\s*[(\[][^)\]]*\b(?:${EDITION_WORDS})\b[^)\]]*[)\]]`, "gi");
const DASHED_EDITION = new RegExp(String.raw`\s+[-–—]\s+[^-–—]*\b(?:${EDITION_WORDS})\b[^-–—]*$`, "i");

/**
 * Strips the edition info Spotify adds to album titles, which MusicBrainz doesn't have:
 *   "Ziggy Stardust (2012 Remaster)"         -> "Ziggy Stardust"
 *   "Rumours [Super Deluxe Edition]"         -> "Rumours"
 *   "Abbey Road - Remastered 2009"           -> "Abbey Road"
 * Returns the trimmed original if stripping would leave nothing.
 */
export function cleanAlbumTitle(title) {
    const trimmed = title?.trim() ?? "";
    const cleaned = trimmed
        .replace(BRACKETED_EDITION, "")
        .replace(DASHED_EDITION, "")
        .trim();

    return cleaned !== "" ? cleaned : trimmed;
}

/**
 * The MusicBrainz releases matching this album, best first. There's often more than one, e.g.
 * the CD, LP and digital release of the same album:
 *   [{ id, releaseGroupId, title, artist, artists, artistIds, date, releaseType, mediaType, country, disambiguation, trackCount }]
 * artists and artistIds are parallel arrays with one name and MusicBrainz id per credited artist.
 * date, releaseType, mediaType, country and disambiguation may be null, and trackCount 0 if unknown.
 *
 * releaseDate ("YYYY-MM-DD") and totalTracks are optional hints that help the backend rank them.
 * Streamed as server-sent events, so pass onProgress to hear how it's going and signal to cancel.
 */
export async function findReleases({ title, artists, releaseDate, totalTracks, releaseType } = {}, { onProgress, signal } = {}) {
    if (!title?.trim())
        return [];

    const params = { title: cleanAlbumTitle(title) };
    const hints = { releaseDate, totalTracks, releaseType };
    for (const [key, value] of Object.entries(hints)) {
        if (value != null && String(value).trim() !== "")
            params[key] = String(value).trim();
    }

    // Repeated key (artists=A&artists=B) so ASP.NET binds it to string[].
    const query = new URLSearchParams(params);
    for (const artist of artists ?? []) {
        const trimmed = artist?.trim() ?? "";
        if (trimmed !== "")
            query.append("artists", trimmed);
    }

    const url = `${MUSICBRAINZ_API_URL}/records/stream?${query}`;
    return (await common.streamFromUrl(url, { onProgress, signal })) ?? [];
}

/**
 * The MusicBrainz artists matching this name, best first. There may be several, e.g. for "Prince":
 *   [{ id, name, country, type, disambiguation }]
 * type is one of Person, Group or Other
 * country, type and disambiguation may be null.
 * Streamed like findReleases().
 */
export async function findArtists(name, { onProgress, signal } = {}) {
    const trimmed = name?.trim() ?? "";
    if (trimmed === "")
        return [];

    const url = `${MUSICBRAINZ_API_URL}/artists/stream?${new URLSearchParams({ name: trimmed })}`;
    return (await common.streamFromUrl(url, { onProgress, signal })) ?? [];
}

/**
 * One MusicBrainz artist by its mbid, in the same shape as findArtists() gives:
 *   { id, name, country, type, disambiguation }
 * or null if MusicBrainz doesn't know it. Streamed like findReleases().
 */
export async function getArtist(mbid, { onProgress, signal } = {}) {
    const trimmed = mbid?.trim() ?? "";
    if (trimmed === "")
        return null;

    const url = `${MUSICBRAINZ_API_URL}/artist/stream?${new URLSearchParams({ mbid: trimmed })}`;
    return (await common.streamFromUrl(url, { onProgress, signal })) ?? null;
}

/**
 * The track list of one release (an mbid from findReleases()):
 *   [{ title, discNumber, trackNumber, durationSeconds }]
 * Streamed like findReleases().
 */
export async function getReleaseTracks(mbid, { onProgress, signal } = {}) {
    const url = `${MUSICBRAINZ_API_URL}/tracklist/stream?${new URLSearchParams({ mbid })}`;
    return await common.streamFromUrl(url, { onProgress, signal });
}

/**
 * Our ReleaseFormat name for a MusicBrainz media type: "Cd" for anything containing "CD"
 * ("CD-R", "Enhanced CD", "2×CD"...), "Vinyl", "Cassette" and "Digital" likewise, "Unknown" when it's
 * missing and "Other" for everything else. Combined formats ("CD + DVD") are "Other" too,
 * since no single format describes them.
 */
export function toReleaseFormat(mediaType) {
    const trimmed = mediaType?.trim() ?? "";
    if (trimmed === "")
        return "Unknown";
    if (trimmed.includes("+"))
        return "Other";
    if (/CD/i.test(trimmed))
        return "Cd";
    if (/Vinyl/i.test(trimmed))
        return "Vinyl";
    if (/Cassette/i.test(trimmed))
        return "Cassette";
    if (/Digital/i.test(trimmed))
        return "Digital";
    return "Other";
}

/**
 * A message for the user from a progress event sent while the backend waits for MusicBrainz:
 *   { stage: "queued" }                                  waiting for our turn under the rate limit
 *   { stage: "requesting", step }                        talking to MusicBrainz
 *   { stage: "retrying", attempt, of, delaySeconds }     MusicBrainz was busy, trying again soon
 */
export function describeProgress(progress) {
    switch (progress?.stage) {
        case "queued":
            return "Waiting for MusicBrainz...";
        case "requesting": {
            const stepText = progress.step ? `${progress.step}...` : "Asking MusicBrainz...";
            const attempt = (progress.attempt && progress.attempt > 0) ? ` (attempt ${progress.attempt} of ${progress.of})` : "";
            return `${stepText}${attempt}`

        }
        case "retrying": {
            const attempt = progress.attempt && progress.of ? ` (attempt ${progress.attempt} of ${progress.of})` : "";
            return `MusicBrainz is busy, trying again${attempt}...`;
        }
        default:
            return "Waiting for MusicBrainz...";
    }
}
