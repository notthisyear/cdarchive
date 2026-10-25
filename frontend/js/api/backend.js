import * as common from "./common.js"

export async function getTotalNumberOfReleases() {
    const url = new URL("/api/releases/total", window.location.origin);
    const response = await common.fetchFromUrl(url);
    return (await response.json()).total;
}

export async function getReleasesSummary(offset, limit) {
    const url = new URL("/api/releases", window.location.origin);
    url.searchParams.set("offset", offset);
    url.searchParams.set("limit", limit);
    const response = await common.fetchFromUrl(url);

    return []; // (await response.json()).releases;
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

export async function getRelease(releaseId) {
    const url = new URL(`/api/releases/${encodeURIComponent(releaseId)}`, window.location.origin);
    const response = await common.fetchFromUrl(url);
    return (await response.json()).release;
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

export async function addNewCreator(creator) {
    const url = new URL("/api/creator/add", window.location.origin);
    const response = await common.fetchFromUrl(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(creator)
    });

    const location = response.headers.get("Location");
    if (!location) {
        throw new Error("No Location header in response");
    }

    return new URL(location, window.location.origin).pathname.split("/").at(-1);
}

export async function addNewRelease(release) {
    const url = new URL("/api/releases/add", window.location.origin);
    await common.fetchFromUrl(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(release)
    });
}

// The backend gives every image as a ready-made URL: "/images/<file>" for shared images, or a
// signed "/api/uploads/<id>?..." link for the user's own uploads. Returns it for use as an
// <img src>, or null when there's no image. Only same-origin paths are accepted, so a bad value
// can't point an <img> at another site ("//host" and "/\host" both mean another host).
export function toImageSrc(imageUrl) {
    const url = imageUrl?.trim() ?? "";
    return url.startsWith("/") && !/^\/[\/\\]/.test(url) ? url : null;
}
