import * as styles from "../styles.js";
import * as util from "../util.js";

export function create(trackTitle, trackNumber, trackDurationSeconds) {
    const row = document.createElement("button");
    row.className = styles.trackRow;
    row.innerHTML = `
        <div class="flex items-center gap-4">
            <span class="w-3 text-slate-500">
                ${trackNumber}.
            </span>
            <span>
                ${trackTitle}
            </span>
        </div>

        <div class="flex items-center gap-4">
            <span class="text-sm text-slate-500">
                ${util.getAsColonSeparatedTimeString(trackDurationSeconds, false, true)}
            </span>

            <span class="opacity-0
                            translate-x-1
                            group-hover:opacity-100
                            group-hover:translate-x-0
                            transition-all
                            text-slate-500
                            hover:text-blue-500">
                <svg xmlns="http://www.w3.org/2000/svg"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke-width="2"
                        stroke="currentColor"
                        class="w-5 h-5">
                <path stroke-linecap="round"
                        stroke-linejoin="round"
                        d="M16.862 3.487a2.25 2.25 0 113.182 3.182L7.5 19.213l-4.5 1.125L4.125 15.84 16.862 3.487z"/>
                </svg>
            </span>
        </div>
    `;
    return row;
}
