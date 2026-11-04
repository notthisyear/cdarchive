// Frontend wrapper around our own /api/artwork endpoints (see Services/Artwork.cs).
//
//   import * as artwork from "./artwork.js";
//
//   const photo = await artwork.getArtistImage(mbid);                 // the artist's MusicBrainz ID
//   const cover0 = await artwork.getReleaseCover({ mbid });            // a specific MusicBrainz release
//   const cover = await artwork.getReleaseCover({ barcode });          // most precise without an mbid
//   const cover2 = await artwork.getReleaseCover({ title, artist });   // any pressing of the album
//
// Both resolve to null when there is no image, or an object:
//   { url, originalUrl, source: "wikimedia" | "coverartarchive",
//     author, license, licenseUrl, sourcePageUrl, attributionRequired }
// and throw on network/server errors. Pass { signal } (an AbortSignal) to cancel.
//
// Credit the image wherever you show it: see artworkImage.js, which does that for you.

import * as common from "./common.js"

const BASE_URL = "/api/artwork";

export async function getArtistImage(mbid = {}, { signal, onProgress } = {}) {
    if (!mbid)
        return null;

    return await common.streamFromUrl(`${BASE_URL}/artist/stream?${new URLSearchParams({ mbid })}`, { onProgress, signal });
}

/**
 * Pass onProgress to get MusicBrainz progress while the backend works (see describeProgress
 * in musicbrainz.js). The result is then streamed from /album/stream as server-sent events.
 */
export async function getReleaseCover(mbid = {}, { signal, onProgress } = {}) {
    if (!mbid)
        return null;

    return await common.streamFromUrl(`${BASE_URL}/album/stream?${new URLSearchParams({ mbid })}`, { onProgress, signal });
}