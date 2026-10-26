import * as common from "./common.js"

export async function getTotalNumberOfReleases() {
    const url = new URL("/api/releases/total", window.location.origin);
    const response = await common.fetchFromUrl(url);
    return (await response.json()).total;
}

/**
 * One page of the releases the user owns, sorted by artist, year and title. limit is at most 100.
 * Each is a ReleaseSummary: { id, title, artistName, imageUrl, year, type, status }
 */
export async function getReleasesSummary(offset, limit) {
    const url = new URL("/api/releases", window.location.origin);
    url.searchParams.set("offset", offset);
    url.searchParams.set("limit", limit);
    const response = await common.fetchFromUrl(url);

    return (await response.json()).releases;
}

export async function login(username, password) {
    const url = new URL("/api/auth/login", window.location.origin);
    const response = await common.fetchFromUrl(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({
            username,
            password
        })
    });

    return (await response.json()).token;
}

/**
 * One of the user's releases, as ReleaseDetails:
 *   { summary: ReleaseSummary, format, releaseYear, firstReleaseYear, durationSeconds, barcode,
 *     notes, score, acquiredOn, createdAt,
 *     credits: [{ creatorId, name, role, joinPhrase, characterName }],
 *     tracks: [{ discNumber, trackNumber, title, durationSeconds }],
 *     images: [{ url, author, license, licenseUrl, sourcePageUrl, attributionRequired }],
 *     externalIds: { MusicBrainz: "...", MusicBrainzReleaseGroup: "...", Spotify: "..." } }
 * Throws if it isn't the user's (404).
 */
export async function getRelease(releaseId) {
    const url = new URL(`/api/releases/${encodeURIComponent(releaseId)}`, window.location.origin);
    const response = await common.fetchFromUrl(url);
    return await response.json();
}

export async function getCreators(creators) {
    const url = new URL("/api/creators", window.location.origin);
    const response = await common.fetchFromUrl(url, {
        method: "QUERY",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(creators)
    });
    return (await response.json()).result;
}

/**
 * Adds a creator to the user's collection. creator is a NewCreatorRequest:
 *   { name, sortName, type, country, musicBrainzId, spotifyUrl, image }
 * where image is null, { source: "Wikimedia", externalId: <artist mbid> } for a looked-up photo,
 * or { source: "Upload", fileIndex: 0 } for imageFile.
 *
 * Returns { id, alreadyInCollection }. If the user already has the creator (the same MusicBrainz
 * or Spotify artist), nothing is added and id is the one they have.
 */
export async function addNewCreator(creator, imageFile = null) {
    const url = new URL("/api/creators", window.location.origin);

    // Multipart, so an uploaded image goes as a file rather than inside the JSON. No
    // Content-Type header: the browser sets it, with the boundary between the parts.
    const form = new FormData();
    form.append("request", JSON.stringify(creator));
    if (imageFile)
        form.append("files", imageFile);

    const response = await common.fetchFromUrl(url, {
        method: "POST",
        body: form,
        allowStatuses: [409]
    });

    // A conflict with a creatorId is "you already have them". Without one, it's that the same
    // creator was added elsewhere at the same moment, which is worth an error.
    if (response.status === 409) {
        const body = await response.json().catch(() => null);
        if (body?.creatorId == null)
            throw new Error(body?.message ?? "HTTP 409");

        return { id: String(body.creatorId), alreadyInCollection: true };
    }

    const location = response.headers.get("Location");
    if (!location) {
        throw new Error("No Location header in response");
    }

    return { id: new URL(location, window.location.origin).pathname.split("/").at(-1), alreadyInCollection: false };
}

/**
 * Adds a release to the user's collection and returns its id. release is a NewReleaseRequest:
 *   { title, type, status, format, releaseYear, firstReleaseYear, durationSeconds, barcode,
 *     musicBrainzReleaseId, musicBrainzReleaseGroupId, spotifyAlbumId, notes, score, acquiredOn,
 *     credits: [{ creatorId, role, joinPhrase, characterName }],
 *     tracks: [{ discNumber, trackNumber, title, durationSeconds }],
 *     images: [...] }
 * Each image is { source: "CoverArtArchive", externalId: <release mbid> } for a looked-up cover,
 * or { source: "Upload", fileIndex: i } for imageFiles[i]. The first image is the cover.
 */
export async function addNewRelease(release, imageFiles = []) {
    const url = new URL("/api/releases", window.location.origin);

    // Multipart, so uploaded images go as files rather than inside the JSON. No Content-Type
    // header: the browser sets it, with the boundary between the parts.
    const form = new FormData();
    form.append("request", JSON.stringify(release));
    for (const file of imageFiles)
        form.append("files", file);

    const response = await common.fetchFromUrl(url, {
        method: "POST",
        body: form
    });

    return (await response.json()).id;
}

// The backend gives every image as a ready-made URL: "/images/<file>" for shared images, or a
// signed "/api/uploads/<id>?..." link for the user's own uploads. Returns it for use as an
// <img src>, or null when there's no image. Only same-origin paths are accepted, so a bad value
// can't point an <img> at another site ("//host" and "/\host" both mean another host).
export function toImageSrc(imageUrl) {
    const url = imageUrl?.trim() ?? "";
    return url.startsWith("/") && !/^\/[\/\\]/.test(url) ? url : null;
}
