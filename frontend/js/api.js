import * as auth from "./auth.js";

const SPOTIFY_API_URL = "https://api.spotify.com/v1";

async function fetchFromUrl(url, options = {}) {
    const token = auth.getToken();
    const headers = {
        ...options.headers,
        ...(token
            ? { Authorization: `Bearer ${token}` }
            : {})
    };

    const response = await fetch(url, {
        ...options,
        headers
    });

    // If we fail specifically with NotAuthorized while we had a token,
    // let's throw it away (it's probably not valid anymore)
    if (!response.ok) {
        if (token && response.status == 401) {
            auth.logout()
        }
        throw new Error(`HTTP ${response.status}`);
    }

    return response;
}

export async function getTotalNumberOfRecords() {
    const url = new URL("/api/records/total", window.location.origin);
    const response = await fetchFromUrl(url);
    return (await response.json()).total;
}

export async function getRecordsSummary(offset, limit) {
    const url = new URL("/api/records", window.location.origin);
    url.searchParams.set("offset", offset);
    url.searchParams.set("limit", limit);
    const response = await fetchFromUrl(url);

    return (await response.json()).records;
}

export async function login(username, password) {
    const url = new URL("/api/auth/login", window.location.origin);
    const response = await fetchFromUrl(url, {
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

export async function getRecord(recordId) {
    const url = new URL(`/api/records/${encodeURIComponent(recordId)}`, window.location.origin);
    const response = await fetchFromUrl(url);
    return (await response.json()).record;
}

export async function getArtists(artists) {
    const url = new URL("/api/artists", window.location.origin);
    const response = await fetchFromUrl(url, {
        method: "QUERY",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(artists)
    });
    return await response.json();
}

export async function tryAddNewRecord(record) {
    const url = new URL("/api/records/add", window.location.origin);
    await fetchFromUrl(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(record)
    });
}

export async function getSpotifyDisplayName() {
    const response = await fetch(`${SPOTIFY_API_URL}/me`, {
        method: "GET",
        headers: { Authorization: `Bearer ${auth.getSpotifyToken()}` },
    });
    return (await response.json()).display_name;
}

export async function searchOnSpotify(q, type, limit, offset, reponseExtractor) {
    const params = new URLSearchParams({
        q: q,
        type: type,
        limit: limit,
        offset: offset
    });
    const response = await fetch(`${SPOTIFY_API_URL}/search?${params}`, {
        method: "GET",
        headers: { Authorization: `Bearer ${auth.getSpotifyToken()}` }
    });

    const r = reponseExtractor(await response.json());
    return r;
}

export async function getSpotifyAlbum(spotifyAlbumId) {
    const url = new URL(`${SPOTIFY_API_URL}/albums/${encodeURIComponent(spotifyAlbumId)}`, window.location.origin);
    const response = await fetch(url, {
        method: "GET",
        headers: { Authorization: `Bearer ${auth.getSpotifyToken()}` }
    });

    return await response.json();
}