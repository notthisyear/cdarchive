import * as Modal from "./modal.js";
import * as styles from "../styles.js";
import * as util from "../util.js";

export function show(title, discNumber, trackNumber, durationSeconds, saveAction, deleteAction) {
    const form = document.createElement("form");
    form.innerHTML = `
                <form id="trackEditorForm"
                      class="space-y-5">
                    <div>
                        <label class="${styles.editorInputBoxLabel}">
                            Disc number
                        </label>

                        <input id="discNumber"
                               type="number"
                               placeholder="Disc number"
                               min="1"
                               value="${discNumber}"
                               required
                               class="${styles.editorInputBox}">
                    </div>

                    <div>
                        <label class="${styles.editorInputBoxLabel}">
                            Track number
                        </label>

                        <input id="trackNumber"
                               type="number"
                               placeholder="Track number"
                               min="1"
                               value="${trackNumber}"
                               required
                               class="${styles.editorInputBox}">
                    </div>

                    <div>
                        <label class="${styles.editorInputBoxLabel}">
                            Title
                        </label>

                        <input id="trackTitle"
                               type="text"
                               placeholder="Track title"
                               value="${title}"
                               required
                               minlength="1"
                               maxlength="200"
                               class="${styles.editorInputBox}">
                    </div>

                    <div>
                        <label class="${styles.editorInputBoxLabel}">
                            Duration
                        </label>

                        <input id="trackDuration"
                               type="text"
                               placeholder="Track duration ([hh:]mm:ss)"
                               required
                               value="${util.getAsColonSeparatedTimeString(durationSeconds)}"
                               class="${styles.editorInputBox}">
                    </div>
                </form>
            </div>
        </div>
    `;

    const discNumberElement = form.querySelector("#discNumber");
    const trackNumberElement = form.querySelector("#trackNumber");
    const trackTitleElement = form.querySelector("#trackTitle");
    const trackDurationElement = form.querySelector("#trackDuration");
    trackDurationElement.addEventListener("input", validateDuration);

    Modal.show({
        title: "Edit Track",
        content: form,
        saveOnEnter: true,
        buttons: [
            {
                "type": "delete",
                "action": () => {
                    if (deleteAction)
                        deleteAction();
                    return true;
                }
            },
            {
                "type": "close",
                "action": async () => {
                    const hasUnsavedChanged =
                        (discNumber !== Number(discNumberElement.value)) ||
                        (trackNumber !== Number(trackNumberElement.value)) ||
                        (title !== trackTitleElement.value) ||
                        (util.getAsColonSeparatedTimeString(durationSeconds) !== trackDurationElement.value);

                    if (hasUnsavedChanged)
                        return await Modal.confirm("Unsaved changes", "Do you want to discard unsaved changes?");

                    return true;
                }
            },
            {
                "type": "save",
                "action": () => {
                    if (!form.reportValidity())
                        return false;

                    if (saveAction)
                        saveAction(trackTitleElement.value, Number(discNumberElement.value), Number(trackNumberElement.value), util.parseDurationToSeconds(trackDurationElement.value));

                    return true;
                }
            }
        ]
    });
}

function validateDuration(input) {
    if (!isValidDuration(input.srcElement.value)) {
        input.srcElement.setCustomValidity("Duration must be in [hh:]mm:ss format.");
    } else {
        input.srcElement.setCustomValidity("");
    }
}

function isValidDuration(duration) {
    return /^([0-9]{1,2}:|)([0-5]|)[0-9]:[0-5][0-9]$/.test(duration);
}