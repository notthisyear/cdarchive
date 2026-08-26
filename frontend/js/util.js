export function getPartsFromSeconds(s) {
    if (s < 0) {
        return null;
    }

    const secPerMin = 60;
    const minPerHour = 60;
    const hourPerDay = 24;

    const secsPerHour = secPerMin * minPerHour;
    const secsPerDay = secPerMin * minPerHour * hourPerDay;

    let days = 0;
    let hours = 0;
    let minutes = 0;

    if (s >= secsPerDay) {
        days = Math.floor(s / secsPerDay);
        s = s - (days * secsPerDay);
    }

    if (s >= secsPerHour) {
        hours = Math.floor(s / secsPerHour);
        s = s - (hours * secsPerHour);
    }

    if (s >= secPerMin) {
        minutes = Math.floor(s / secPerMin);
        s = s - (minutes * secPerMin);
    }

    return { days: days, hours: hours, minutes: minutes, seconds: s };
}

export function getAsTimeStringWithSuffix(s) {
    const parts = getPartsFromSeconds(s);
    if (parts === null) {
        return "N/a";
    }

    const result = [];
    addToTimeStringWithSuffixIfNonZero(result, parts.days, "d");
    addToTimeStringWithSuffixIfNonZero(result, parts.hours, "h");
    addToTimeStringWithSuffixIfNonZero(result, parts.minutes, "min");
    addToTimeStringWithSuffixIfNonZero(result, parts.seconds, "sec");

    return result.join('');
}

export function getAsColonSeparatedTimeString(s, forceHours = false, forceMinutes = false) {
    const parts = getPartsFromSeconds(s);
    if (parts === null) {
        return "N/a";
    }

    if (parts.days > 0) {
        return "N/a"
    }

    const result = [];
    addToTimeStringWithColonIfNonZero(result, parts.hours, forceHours);
    addToTimeStringWithColonIfNonZero(result, parts.minutes, forceMinutes);
    addToTimeStringWithColonIfNonZero(result, parts.seconds);

    return result.join('');
}

function addToTimeStringWithSuffixIfNonZero(result, v, suffix) {
    if (v > 0) {
        if (result.length > 0) {
            result.push(", ");
        }
        result.push(v, " ", suffix);
    }
}

function addToTimeStringWithColonIfNonZero(result, v, force = false) {
    if (result.length > 0) {
        result.push(":");
        result.push(String(v).padStart(2, '0'));
    }
    else if (force || v > 0) {
        result.push(v);
    }
}

export function debounce(callback, wait) {
    let timeoutId = null;
    return (...args) => {
        window.clearTimeout(timeoutId);
        timeoutId = window.setTimeout(() => {
            callback.apply(null, args);
        }, wait);
    };
}

export function generateRandomString(length) {
    const possible = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
    const values = crypto.getRandomValues(new Uint8Array(length));
    return values.reduce((acc, x) => acc + possible[x % possible.length], "");
}

export async function sha256(s) {
    const data = (new TextEncoder()).encode(s);
    return await crypto.subtle.digest('SHA-256', data);
}

export function base64encode(s) {
    return btoa(String.fromCharCode(...new Uint8Array(s)))
        .replace(/=/g, '')
        .replace(/\+/g, '-')
        .replace(/\//g, '_');
}

export function concatenateArtists(artists) {
    let i = 0;
    const result = [];
    artists.forEach(e => {
        result.push((i === (artists.length - 1)) ? e.name : `${e.name}, `);
        i++;
    });
    return result.join('');
}

export function splitArtists(artists) {
    return artists.split(",").map(x => x.trim());
}

export function parseDurationToSeconds(text) {
    const parts = text.split(":").map(p => p.trim());

    if (parts.length < 2 || parts.length > 3)
        throw new Error(`Invalid duration format: "${text}"`);

    if (parts.some(p => !/^\d{1,2}$/.test(p)))
        throw new Error(`Invalid duration format: "${text}"`);

    return parts.map(Number).reduce((total, value) => total * 60 + value, 0);
}