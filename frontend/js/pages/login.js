import * as api from "../api.js";
import * as auth from "../auth.js";
import * as router from "../router.js";

export async function render() {

    const root = document.createElement("div");
    root.className = "container d-flex justify-content-center align-items-center vh-100";
    root.innerHTML = `
         <div class="flex justify-center items-center min-h-[75vh]">

            <div class="w-full max-w-md bg-slate-800 rounded-2xl shadow-2xl p-8">

                <div class="text-center mb-8">

                    <div class="text-6xl mb-3">
                        🎵
                    </div>

                    <h1 class="text-3xl font-bold text-white">
                        Record Collection
                    </h1>

                    <p class="text-slate-400 mt-2">
                        Sign in to manage your collection
                    </p>

                </div>

                <form id="loginForm" class="space-y-6">

                    <div>

                        <label
                            for="username"
                            class="block text-sm font-medium text-slate-300 mb-2">

                            Username

                        </label>

                        <input
                            id="username"
                            name="username"
                            type="text"
                            autocomplete="username"
                            required

                            class="
                                w-full
                                rounded-lg
                                bg-slate-700
                                border
                                border-slate-600
                                px-4
                                py-3
                                text-white
                                placeholder-slate-500
                                focus:outline-none
                                focus:ring-2
                                focus:ring-blue-500
                                focus:border-blue-500
                            "

                            placeholder="Enter your username">

                    </div>

                    <div>

                        <div class="flex justify-between items-center mb-2">

                            <label
                                for="password"
                                class="text-sm font-medium text-slate-300">

                                Password

                            </label>

                            <button
                                id="forgotPasswordButton"
                                type="button"

                                class="
                                    text-sm
                                    text-blue-400
                                    hover:text-blue-300
                                ">

                                Forgot password?

                            </button>

                        </div>

                        <input
                            id="password"
                            name="password"
                            type="password"
                            autocomplete="current-password"
                            required

                            class="
                                w-full
                                rounded-lg
                                bg-slate-700
                                border
                                border-slate-600
                                px-4
                                py-3
                                text-white
                                placeholder-slate-500
                                focus:outline-none
                                focus:ring-2
                                focus:ring-blue-500
                                focus:border-blue-500
                            "

                            placeholder="Enter your password">

                    </div>

                    <div
                        id="loginError"
                        class="hidden rounded-lg bg-red-900/40 border border-red-700 px-4 py-3 text-red-300">
                    </div>

                    <button
                        id="loginButton"
                        type="submit"

                        class="
                            w-full
                            rounded-lg
                            bg-blue-600
                            hover:bg-blue-700
                            py-3
                            font-semibold
                            transition-colors
                        ">

                        Login

                    </button>

                </form>

                <div class="mt-8 pt-6 border-t border-slate-700 text-center">

                    <p class="text-slate-400 mb-3">

                        New to Record Collection?

                    </p>

                    <button
                        id="createUserButton"
                        type="button"

                        class="
                            text-blue-400
                            hover:text-blue-300
                            font-medium
                        ">
                        Create new user
                    </button>
                </div>
            </div>
        </div>
        `;

    root.querySelector("form[id = loginForm]").addEventListener("submit", async (event) => {
        event.preventDefault();
        const username = root.querySelector("input[id = username]").value;
        const password = root.querySelector("input[id = password]").value;

        try {
            const token = await api.login(username, password);
            auth.login(username, token);
            router.navigate("/");
        }
        catch (e) {
            console.log(e);
            showError("Invalid username or password.");
        }
    });

    root.querySelector("button[id = createUserButton]").addEventListener("click", () => {
        console.log("Navigate to create account");
        // router.navigate("/register");
    });

    root.querySelector("button[id = forgotPasswordButton]").addEventListener("click", () => {
        console.log("Forgot password");
        // router.navigate("/forgot-password");
    });

    root.querySelector("input[id = username]").focus();

    return root;
}

function showError(message) {
    const error = document.getElementById("loginError");
    error.textContent = message;
    error.classList.remove("hidden");
}
