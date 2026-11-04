
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

export async function getSpotifyAlbum(spotifyAlbumId) {
    return await getFromSpotify(`${SPOTIFY_API_URL}/albums/${encodeURIComponent(spotifyAlbumId)}`);
}
