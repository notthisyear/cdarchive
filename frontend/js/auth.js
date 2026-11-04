import * as util from "./util.js";

const SPOTIFY_CLIENT_ID = "5440e7fafc7c4bde90a87b8feb32d3bb";
const SPOTIFY_REDIRECT_URI = "https://172.18.41.100";
const SPOTIFY_TOKEN_ENDPOINT = "https://accounts.spotify.com/api/token";

// Treat the Spotify token as expired a bit early, so it doesn't run out while a request is in flight
const SPOTIFY_EXPIRY_MARGIN_MS = 60 * 1000;

// Earlier versions could store the strings "undefined"/"null", so treat those as missing
function readStored(key) {
    const value = localStorage.getItem(key);
    return (value === "undefined" || value === "null") ? null : value;
}

let token = readStored("token");
let username = readStored("username");
let spotifyToken = readStored("spotifyToken");
let spotifyRefreshToken = readStored("spotifyRefreshToken");
let spotifyTokenExpiresAt = Number(readStored("spotifyTokenExpiresAt")) || null;

// The refresh currently in flight, shared by everyone who needs a fresh token. Spotify may
// rotate the refresh token, so two parallel refreshes would make the second one fail.
let spotifyRefreshInFlight = null;

export function isLoggedIn() {
    return token !== null;
}

export function hasSpotifyToken() {
    return spotifyToken !== null;
}

export function getToken() {
    return token;
}

export function getUsername() {
    return username;
}

export function login(name, jwt) {
    token = jwt;
    username = name;
    localStorage.setItem("token", jwt);
    localStorage.setItem("username", name);
}

export function logout() {
    console.log("logging out");
    token = null;
    username = null;
    localStorage.removeItem("token");
    localStorage.removeItem("username");
}

export function getSpotifyToken() {
    return spotifyToken;
}

/**
 * The Spotify access token, refreshed first if it has expired. Null if we're not
 * connected to Spotify (or the refresh token was rejected).
 */
export async function getValidSpotifyToken() {
    await verifySpotifyToken();
    return spotifyToken;
}

export function clearSpotifyTokens() {
    console.log("spotify tokens cleared");

    spotifyToken = null;
    spotifyRefreshToken = null;
    spotifyTokenExpiresAt = null;

    localStorage.removeItem("spotifyToken");
    localStorage.removeItem("spotifyRefreshToken");
    localStorage.removeItem("spotifyTokenExpiresAt");
}

export async function redirectToSpotifyAuthorization() {
    const authorizationEndpoint = "https://accounts.spotify.com/authorize";
    const scopes = "user-read-private user-read-email";

    const codeVerifier = util.generateRandomString(64);
    const hashed = await util.sha256(codeVerifier)
    const codeChallenge = util.base64encode(hashed);

    window.localStorage.setItem("codeVerifier", codeVerifier);

    const authUrl = new URL(authorizationEndpoint)
    const params = {
        response_type: "code",
        client_id: SPOTIFY_CLIENT_ID,
        scope: scopes,
        code_challenge_method: "S256",
        code_challenge: codeChallenge,
        redirect_uri: SPOTIFY_REDIRECT_URI,
    };

    authUrl.search = new URLSearchParams(params).toString();
    window.location.href = authUrl.toString();
}

/**
 * Exchanges the code from Spotify's redirect for tokens. Returns true on success.
 * Throws on network errors.
 */
export async function acquireSpotifyTokens(code) {
    const codeVerifier = localStorage.getItem("codeVerifier");
    localStorage.removeItem("codeVerifier");

    if (!codeVerifier) {
        console.warn("Got a Spotify code but no code verifier, ignoring it");
        return false;
    }

    const payload = {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded",
        },
        body: new URLSearchParams({
            client_id: SPOTIFY_CLIENT_ID,
            grant_type: "authorization_code",
            code: code,
            redirect_uri: SPOTIFY_REDIRECT_URI,
            code_verifier: codeVerifier,
        })
    };

    const result = await fetch(SPOTIFY_TOKEN_ENDPOINT, payload);
    if (!result.ok) {
        console.warn(`Spotify token exchange failed (${result.status})`);
        return false;
    }

    const response = await result.json();
    storeTokens(response.access_token, response.refresh_token, response.expires_in);
    return true;
}

function isSpotifyTokenFresh() {
    return spotifyToken !== null
        && spotifyTokenExpiresAt !== null
        && (spotifyTokenExpiresAt - SPOTIFY_EXPIRY_MARGIN_MS) > Date.now();
}

/**
 * Makes sure the Spotify access token is valid, refreshing it if needed. Never throws.
 * Clears the tokens if Spotify rejects the refresh token, but keeps them on network or
 * server errors so a temporary problem doesn't disconnect the user.
 */
export async function verifySpotifyToken() {
    if (isSpotifyTokenFresh())
        return;

    if (spotifyRefreshToken === null) {
        clearSpotifyTokens();
        return;
    }

    spotifyRefreshInFlight ??= refreshSpotifyToken().finally(() => spotifyRefreshInFlight = null);

    try {
        if (await spotifyRefreshInFlight)
            return;
    }
    catch (e) {
        console.warn("Could not refresh the Spotify token, will try again later:", e);
        return;
    }

    clearSpotifyTokens();
}

/**
 * Returns true if the token was refreshed, false if Spotify rejected the refresh token.
 * Throws on network and server errors.
 */
async function refreshSpotifyToken() {
    const payload = {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded"
        },
        body: new URLSearchParams({
            grant_type: "refresh_token",
            refresh_token: spotifyRefreshToken,
            client_id: SPOTIFY_CLIENT_ID
        }),
    }

    const result = await fetch(SPOTIFY_TOKEN_ENDPOINT, payload);
    if (result.status >= 500)
        throw new Error(`Spotify token refresh failed (${result.status})`);
    if (!result.ok) {
        console.warn(`Spotify rejected the refresh token (${result.status})`);
        return false;
    }

    const response = await result.json();
    storeTokens(response.access_token, response.refresh_token, response.expires_in);
    return true;
}

function storeTokens(accessToken, refreshToken, expiresIn) {
    spotifyToken = accessToken;
    // Spotify doesn't always send a new refresh token, in which case the old one is still valid
    spotifyRefreshToken = refreshToken ?? spotifyRefreshToken;
    spotifyTokenExpiresAt = Date.now() + (1000 * expiresIn);

    localStorage.setItem("spotifyToken", spotifyToken);
    localStorage.setItem("spotifyRefreshToken", spotifyRefreshToken);
    localStorage.setItem("spotifyTokenExpiresAt", spotifyTokenExpiresAt);
}
