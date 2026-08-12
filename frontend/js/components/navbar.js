import * as auth from "../auth.js";
import * as api from "../api.js";
import * as router from "../router.js";
import * as styles from "../styles.js";

import * as addRecordForm from "./addRecordForm.js";
import * as spotifyStatus from "./spotifyStatus.js";

export async function create() {
    const nav = document.createElement("nav");
    nav.className =
        "bg-slate-900 border-b border-slate-700";
    nav.innerHTML = `
        <div class="max-w-7xl mx-auto px-6 py-4 flex justify-between items-center">

            <div class="flex items-center gap-8">

                <a href="/" data-link class="text-2xl font-bold hover:text-blue-400">
                    🎵 CD Archive
                </a>

                <a href="/" data-link class="text-slate-300 hover:text-white">
                    Dashboard
                </a>

                <button id="addRecordButton"
                        class="${styles.buttonPrimary}">
                    Add Record...
                </button>

                <div id="spotifyStatusIndicator"></div>
            </div>

            <div class="flex items-center gap-4">
                <input id="searchBox" type="text" placeholder="Search..." class="rounded-lg bg-slate-800 px-3 py-2 w-72 outline-none">
                <button id="logoutButton"
                        class="${styles.buttonDanger}">
                    Logout
                </button>
            </div>
        </div>
    `;

    nav.querySelector("button[id = logoutButton]").addEventListener("click", async (event) => {
            event.preventDefault();
            auth.logout();
            router.navigate("/")
            // TODO: Some sort of "Are you sure" when editing/adding a record
        });

         nav.querySelector("button[id = addRecordButton]").addEventListener("click", async (event) => {
            addRecordForm.show();
        });

    const widget = spotifyStatus.mount(nav.querySelector("div[id = spotifyStatusIndicator]"),
    {
        initialState: { connected: auth.hasSpotifyToken() },
        onDisconnect: () => {
            auth.clearSpotifyTokens();
            widget.setDisconnected();
        }
    });

    if (auth.hasSpotifyToken()) {
        const name = await api.getSpotifyDisplayName();
        widget.setConnected(name);
    }
    return nav;
}