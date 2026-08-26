import * as util from "./util.js";

const SPOTIFY_CLIENT_ID = "5440e7fafc7c4bde90a87b8feb32d3bb";
const SPOTIFY_REDIRECT_URI = "https://127.0.0.1";
const SPOTIFY_TOKEN_ENDPOINT = "https://accounts.spotify.com/api/token";

let token = localStorage.getItem("token");
let username = localStorage.getItem("username")
let spotifyToken = localStorage.getItem("spotifyToken");
let spotifyRefreshToken = localStorage.getItem("spotifyRefreshToken");
let spotifyTokenExpiresAt = localStorage.getItem("spotifyTokenExpiresAt");

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

export function login(username, jwt) {
    token = jwt;
    username = username;
    localStorage.setItem("token", jwt);
    localStorage.setItem("username", username)
}

export function logout() {
    console.log("logging out");
    token = null;
    localStorage.removeItem("token");
}

export function getSpotifyToken() {
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

export async function acquireSpotifyTokens(code) {
    const codeVerifier = localStorage.getItem("codeVerifier");
    console.log(`codeVerifier: ${codeVerifier}`);
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

    const body = await fetch(SPOTIFY_TOKEN_ENDPOINT, payload);
    const response = await body.json();

    localStorage.removeItem("codeVerifier");

    storeTokens(response.access_token, response.refresh_token, response.expires_in);
}

export async function verifySpotifyToken() {

    if (spotifyToken && spotifyTokenExpiresAt && (parseInt(spotifyTokenExpiresAt) > Date.now()))
        return;

    if (spotifyRefreshToken != null) {
        console.log("refresh token: ", spotifyRefreshToken);
        if (await refreshSpotifyToken())
            return;
    }

    console.log(`fell to the bottom of verifySpotifyToken, spotifyRefreshToken: ${spotifyRefreshToken}`);
    clearSpotifyTokens();
}

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
    const response = await result.json();

    console.log(`refresh result: ${result}, refresh response: ${response}`);
    if (result.ok)
        storeTokens(response.access_token, response.refresh_token, response.expires_in);

    return result.ok;
}

function storeTokens(accessToken, refreshToken, expiresIn) {
    spotifyToken = accessToken;
    spotifyRefreshToken = refreshToken;
    spotifyTokenExpiresAt = Date.now() + (1000 * expiresIn);

    localStorage.setItem('spotifyToken', spotifyToken);
    localStorage.setItem('spotifyRefreshToken', spotifyRefreshToken);
    localStorage.setItem('spotifyTokenExpiresAt', spotifyTokenExpiresAt);
}