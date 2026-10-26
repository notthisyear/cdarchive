import * as auth from "../auth.js";

// Throws on any response that isn't ok. Pass { allowNotFound: true } to get 404 responses back
// instead, for lookups where "not found" is a normal answer, or the statuses to get back in
// { allowStatuses: [...] } for other answers the caller handles itself.
export async function fetchFromUrl(url, { allowNotFound = false, allowStatuses = [], ...options } = {}) {
    const token = auth.getToken();
    const headers = {
        ...options.headers,
        ...(token
            ? { Authorization: `Bearer ${token}` }
            : {})
    };

    const response = await fetch(url, {
        ...options,
        headers
    });

    if ((allowNotFound && response.status === 404) || allowStatuses.includes(response.status))
        return response;

    // If we fail specifically with NotAuthorized while we had a token,
    // let's throw it away (it's probably not valid anymore)
    if (!response.ok) {
        if (token && response.status == 401) {
            auth.logout()
        }
        throw new Error(await getErrorMessage(response));
    }

    return response;
}

// The backend's own explanation ({ message: "..." }) where it gives one, else the status
async function getErrorMessage(response) {
    try {
        const body = await response.json();
        if (typeof body?.message === "string" && body.message.length > 0)
            return body.message;
    }
    catch {
        // Not JSON
    }
    return `HTTP ${response.status}`;
}

/**
 * Calls an endpoint that answers with server-sent events, for slow lookups where the backend
 * reports how it's getting on. The stream is a number of "progress" events followed by
 * one "result" or "error" event:
 *
 *   event: progress
 *   data: { "stage": "retrying", "attempt": 2, "of": 4, "delaySeconds": 3 }
 *
 *   event: result
 *   data: { ... }
 *
 * Calls onProgress with each progress event's data and resolves with the result's data.
 * Throws if the stream reports an error or ends without a result. Pass { signal } to cancel.
 */
export async function streamFromUrl(url, { onProgress, signal } = {}) {
    const response = await fetchFromUrl(url, {
        signal,
        headers: { Accept: "text/event-stream" }
    });

    for await (const { event, data } of readServerSentEvents(response)) {
        if (event === "progress")
            onProgress?.(data);
        else if (event === "result")
            return data;
        else if (event === "error")
            throw new Error(data?.message ?? "The lookup failed");
    }

    throw new Error("The lookup ended without a result");
}

// Yields { event, data } for each event in the response, with data parsed as JSON
async function* readServerSentEvents(response) {
    const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
    let buffer = "";

    try {
        while (true) {
            const { value, done } = await reader.read();
            if (done)
                return;
            buffer += value.replace(/\r\n?/g, "\n");

            // Events are separated by a blank line
            let end;
            while ((end = buffer.indexOf("\n\n")) >= 0) {
                const block = buffer.slice(0, end);
                buffer = buffer.slice(end + 2);

                let event = "message";
                const data = [];
                for (const line of block.split("\n")) {
                    // Lines starting with ":" are comments, used as keep-alives
                    if (line.startsWith("event:"))
                        event = line.slice(6).trim();
                    else if (line.startsWith("data:"))
                        data.push(line.slice(5).trimStart());
                }

                if (data.length > 0)
                    yield { event, data: JSON.parse(data.join("\n")) };
            }
        }
    }
    finally {
        reader.releaseLock();
    }
}