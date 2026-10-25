import * as login from "./pages/login.js";
import * as dashboard from "./pages/dashboard.js";
import * as recordDetails from "./pages/recordDetails.js";

import * as navbar from "./components/navbar.js";

import * as backendApi from "./api/backend.js";
import * as auth from "./auth.js";

const routes = [
    {
        path: "/",
        page: dashboard,
        requiresAuth: true
    },
    {
        path: "/login",
        page: login,
        requiresAuth: false
    },
    {
        path: "/releases/:id",
        page: recordDetails,
        requiresAuth: true
    }
];

export async function initialize() {
    await checkAuthenticationStatus();

    window.addEventListener("popstate", renderCurrentRoute);
    installLinkHandler();
    renderCurrentRoute();
}

function installLinkHandler() {
    document.body.addEventListener("click", async event => {
        const link = event.target.closest("a[data-link]");
        if (!link)
            return;

        // Let Ctrl+Click, middle click, etc. work normally.
        if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) {
            return;
        }

        event.preventDefault();
        console.log("calling navigate from link handler with pathname: ", link.pathname);
        await navigate(link.pathname);
    });
}

async function checkAuthenticationStatus() {
    if (auth.isLoggedIn()) {
        try {
            const _ = await backendApi.getTotalNumberOfReleases();
        }
        catch (e) {
            // fetchFromUrl already logs out on 401. Anything else (backend down,
            // network error...) shouldn't throw away a token that may be fine.
            console.log(e);
        }
    }

    // If the URL has a "code" or "error" search parameter, we got redirected from Spotify's
    // authorization flow, so let's go fetch a token
    const args = new URLSearchParams(window.location.search);
    const code = args.get("code");
    const error = args.get("error");
    if (code || error) {
        if (error) {
            console.warn(`Spotify authorization failed: ${error}`);
        }
        else {
            console.log("acquiring tokens");
            try {
                await auth.acquireSpotifyTokens(code);
            }
            catch (e) {
                console.warn("Could not get Spotify tokens:", e);
            }
        }

        // Now, we can reload the page without the search parameters Spotify added
        const url = new URL(window.location.href);
        url.searchParams.delete("code");
        url.searchParams.delete("error");
        url.searchParams.delete("state");
        window.history.replaceState({}, document.title, url.href);
    }
    else if (auth.hasSpotifyToken()) {
        // Wait for this, or the first page render would use an expired token
        await auth.verifySpotifyToken();
    }
}

export async function navigate(url) {
    history.pushState({}, "", url);
    await renderCurrentRoute();
}

async function renderCurrentRoute() {
    const route = getCurrentRoute();
    console.log(route)

    if (!route) {
        render404();
        return;
    }

    const forceLoginScreen = route.route.requiresAuth && !auth.isLoggedIn();
    const app = document.getElementById("app");
    app.replaceChildren();

    const content = document.createElement("main");
    content.id = "content";
    content.className = "max-w-7xl mx-auto p-6";

    if (forceLoginScreen) {
        content.append(await login.render());
        app.append(content);
    }
    else {
        content.append(await route.page.render(route.params));
        if (route.route.requiresAuth)
            app.append(await navbar.create(), content);
        else
            app.append(content);
    }
}


function getCurrentPath() {

    let path = window.location.pathname;

    // Running directly as index.html?
    if (path === "/index.html")
        path = "/";

    // Remove trailing slash except for "/"
    if (path.length > 1 && path.endsWith("/"))
        path = path.slice(0, -1);

    return path;
}

function getCurrentRoute() {

    const currentPath = getCurrentPath();
    for (const route of routes) {

        const params = matchPath(route.path, currentPath);

        if (params === null)
            continue;

        return {
            route: route,
            page: route.page,
            params
        };
    }

    return null;
}

function matchPath(routePath, currentPath) {

    const routeParts = routePath.split("/");
    const pathParts = currentPath.split("/");

    if (routeParts.length !== pathParts.length)
        return null;

    const params = {};

    for (let i = 0; i < routeParts.length; i++) {

        const route = routeParts[i];
        const path = pathParts[i];

        if (route.startsWith(":")) {

            params[route.substring(1)] = decodeURIComponent(path);
            // TODO: Add query parameters?
            continue;
        }

        if (route !== path)
            return null;
    }

    return params;
}

function render404() {
    const app = document.getElementById("app");
    const content = document.createElement("main");
    content.id = "content";
    content.className = "max-w-7xl mx-auto p-6";
    app.replaceChildren(content);

    content.innerHTML = `
        <div class="text-center mt-16">
            <h1 class="text-4xl font-bold mb-4">
                404
            </h1>

            <p class="text-slate-400">
                Page not found.
            </p>
        </div>
    `;
}

