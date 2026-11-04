
import * as auth from "../auth.js";

const SPOTIFY_API_URL = "https://api.spotify.com/v1";

// Refreshes the access token first if it has expired
async function getFromSpotify(url) {
    const token = await auth.getValidSpotifyToken();
    if (!token)
        throw new Error("Not connected to Spotify");

    const response = await fetch(url, {
        method: "GET",
        headers: { Authorization: `Bearer ${token}` }
    });

    if (!response.ok)
        throw new Error(`Spotify request failed (${response.status})`);

    return await response.json();
}

export async function getSpotifyDisplayName() {
    return (await getFromSpotify(`${SPOTIFY_API_URL}/me`)).display_name;
}

export async function searchOnSpotify(q, type, limit, offset, reponseExtractor) {
    const params = new URLSearchParams({
        q: q,
        type: type,
        limit: limit,
        offset: offset
    });

    return reponseExtractor(await getFromSpotify(`${SPOTIFY_API_URL}/search?${params}`));
}

const SPOTIFY_ID = String.raw`[A-Za-z0-9]{22}`;
const ALBUM_URL = new RegExp(String.raw`^(?:https?://)?open\.spotify\.com/(?:intl-[a-z]{2}(?:-[a-z]{2})?/)?album/(${SPOTIFY_ID})(?:[/?#].*)?$`, "i");
const BARE_ID = new RegExp(`^${SPOTIFY_ID}$`);

/**
 * The album id in what the user pasted: a Spotify album URL ("https://open.spotify.com/album/<id>",
 * also with a locale like "/intl-de/" or a "?si=..." as share links have), or a bare id. Null for
 * anything else. Ids are case-sensitive, so it's returned as given.
 */
export function parseSpotifyAlbumId(text) {
    const trimmed = text?.trim() ?? "";
    if (BARE_ID.test(trimmed))
        return trimmed;
    return ALBUM_URL.exec(trimmed)?.[1] ?? null;
}

export function spotifyAlbumUrl(spotifyAlbumId) {
    return `https://open.spotify.com/album/${encodeURIComponent(spotifyAlbumId)}`;
}

export async function getSpotifyAlbum(spotifyAlbumId) {
    return await getFromSpotify(`${SPOTIFY_API_URL}/albums/${encodeURIComponent(spotifyAlbumId)}`);
}
