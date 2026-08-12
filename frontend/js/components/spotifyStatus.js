import * as auth from "../auth.js";
import * as styles from "../styles.js";
import * as Modal from "./modal.js";

const SPOTIFY_GREEN = "#1ED760";

// Official Spotify glyph (circle + soundwave bars)
function spotifyIcon({ colorClass = "", style = "" } = {}) {
    return `
        <svg class="w-5 h-5 ${colorClass} transition-colors shrink-0"
             style="${style}"
             viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
            <path d="M12 0C5.4 0 0 5.4 0 12s5.4 12 12 12 12-5.4 12-12S18.6 0 12 0zm5.521 17.34a.749.749 0 01-1.021.24c-2.82-1.74-6.36-2.1-10.561-1.141a.751.751 0 01-.899-.539.75.75 0 01.54-.9c4.56-1.02 8.52-.6 11.64 1.32a.75.75 0 01.301 1.02zm1.44-3.3a.935.935 0 01-1.262.301c-3.239-1.98-8.159-2.58-11.939-1.38a.937.937 0 01-1.14-.6.936.936 0 01.6-1.14C9.6 9.9 15 10.56 18.72 12.84a.936.936 0 01.24 1.2h.001zm.12-3.36C15.24 8.4 8.82 8.16 5.16 9.3a1.124 1.124 0 01-1.38-.72 1.125 1.125 0 01.72-1.381c4.26-1.26 11.28-1.02 15.721 1.62a1.125 1.125 0 01-1.14 1.941l-.001-.06z"/>
        </svg>
    `;
}

function showSpotifyAuthModal() {
    const root = document.createElement("div");
    root.innerHTML = `
        <div class="space-y-4 text-sm text-slate-600 dark:text-slate-300">
            <p>Connect your Spotify account to search and import albums directly.</p>
            <button type="button"
                    id="spotifyAuthStartBtn"
                    class="${styles.buttonPrimary} w-full text-center font-medium">
                Continue to Spotify
            </button>
        </div>
    `;

    root.querySelector("#spotifyAuthStartBtn").addEventListener("click", async () => {
        await auth.redirectToSpotifyAuthorization();
    });

    Modal.show({
        title: "Connect to Spotify",
        content: root
    });
}

/**
 * Mounts the Spotify status widget into `container`.
 *
 * options:
 *   initialState: { connected: boolean, username?: string }
 *   onDisconnect(): called when a signed-in user clicks "Disconnect".
 *
 * Returns { setConnected(username), setDisconnected(), dispose() } so
 * the caller can update the widget once auth state actually changes,
 * without needing to remount it.
 */
export function mount(container, options = {}) {
    let state = options.initialState ?? { connected: false };
    let cleanupListener = null;

    function render() {
        if (cleanupListener) {
            cleanupListener();
            cleanupListener = null;
        }

        container.innerHTML = "";

        if (state.connected) {
           container.innerHTML = `
                <div class="group flex items-center gap-2">
                    ${spotifyIcon({ style: `color: ${SPOTIFY_GREEN};` })}
                    <span class="text-sm text-slate-600 dark:text-slate-300">
                        Connected to Spotify as
                        <span class="font-medium text-slate-900 dark:text-slate-100">${state.username}</span>
                    </span>
                    <button type="button"
                            id="spotifyDisconnectBtn"
                            class="self-start mt-1 inline-flex items-center text-xs px-2 py-1 rounded-md
                                    border border-slate-200 dark:border-slate-700
                                    text-slate-600 dark:text-slate-300
                                    hover:bg-slate-100 dark:hover:bg-slate-700
                                    opacity-0 group-hover:opacity-100 focus-visible:opacity-100
                                    transition-opacity duration-200">
                        Disconnect
                    </button>
                </div>
            `;

            const disconnectBtn = container.querySelector("#spotifyDisconnectBtn");
            const handleDisconnect = () => options.onDisconnect?.();
            disconnectBtn.addEventListener("click", handleDisconnect);
            cleanupListener = () => disconnectBtn.removeEventListener("click", handleDisconnect);

        } else {
            container.innerHTML = `
                <button type="button"
                        id="spotifyConnectBtn"
                        title="Click to connect to Spotify"
                        class="group flex items-center gap-2">
                    ${spotifyIcon({ colorClass: "text-slate-400 group-hover:text-slate-500 dark:group-hover:text-slate-300" })}
                    <span class="text-sm text-slate-600 dark:text-slate-300">
                        Not connected to Spotify
                    </span>
                </button>
            `;

            const connectBtn = container.querySelector("#spotifyConnectBtn");
            const handleConnect = () => showSpotifyAuthModal();
            connectBtn.addEventListener("click", handleConnect);
            cleanupListener = () => connectBtn.removeEventListener("click", handleConnect);
        }
    }

    render();

    return {
        setConnected(username) {
            state =
            {
                connected: true,
                username
            };
            render();
        },
        setDisconnected() {
            state =
            {
                connected: false
            };
            render();
        },
        dispose() {
            if (cleanupListener)
                cleanupListener();

            container.innerHTML = "";
        }
    };
}