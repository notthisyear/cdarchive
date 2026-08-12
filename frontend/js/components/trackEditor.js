import * as Modal from "./modal.js";
import * as styles from "../styles.js";
import * as util from "../util.js";

export function show(title, number, durationSeconds, saveAction, deleteAction) {
    const form = document.createElement("form");
    form.innerHTML = `
                <form id="trackEditorForm"
                      class="space-y-5">
                    <div>
                        <label class="${styles.editorInputBoxLabel}">
                            Track number
                        </label>

                        <input id="trackNumber"
                               type="number"
                               placeholder="Track number"
                               min="1"
                               value="${number}"
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

    const trackNumber = form.querySelector("#trackNumber");
    const trackTitle = form.querySelector("#trackTitle");
    const trackDuration = form.querySelector("#trackDuration");
    trackDuration.addEventListener("input", validateDuration);

    Modal.show({
        title: "Edit Track",
        content: form,
        saveOnEnter: true,
        buttons: [
            {
                "type": "delete",
                "action": () => {
                    if (deleteAction)
                        deleteAction(title, number, durationSeconds);
                    return true;
                }
            },
            {
                "type": "close",
                "action": async () => {
                    const hasUnsavedChanged = (number !== Number(trackNumber.value)) ||
                        (title !== trackTitle.value) ||
                        (util.getAsColonSeparatedTimeString(durationSeconds) !== trackDuration.value);
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
                        saveAction(trackTitle.value, Number(trackNumber.value), util.parseDurationToSeconds(trackDuration.value));

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