// The native share sheet, so "Share with friends" actually reaches a friend - WhatsApp,
// Messages, wherever they are - rather than only posting into this app's own feed.
//
// Returns a word rather than throwing, because every outcome here is normal: the browser may
// have no share sheet, it may refuse files, and a player dismissing the sheet is not an error.
window.scoreMyLife = window.scoreMyLife || {};

window.scoreMyLife.share = async function (card, title, text, url) {
    // The card is the point of sharing an achievement - a badge reads at a glance in a chat
    // where a line of text scrolls past. Text sharing stays as the fallback, not the goal.
    let file = null;
    try {
        const blob = await window.scoreMyLife.renderCard(card);
        if (blob) {
            file = new File([blob], 'scoremylife.png', { type: 'image/png' });
        }
    } catch (error) {
        console.warn('Could not draw the share card; falling back to text.', error);
    }

    if (file && navigator.canShare && navigator.canShare({ files: [file] })) {
        try {
            await navigator.share({ files: [file], text: text });
            return 'shared';
        } catch (error) {
            if (error && error.name === 'AbortError') return 'cancelled';
            // Some browsers advertise file sharing and then refuse it. Keep going.
        }
    }

    if (navigator.share) {
        try {
            await navigator.share({ title: title, text: text, url: url });
            return file ? 'shared-text' : 'shared';
        } catch (error) {
            if (error && error.name === 'AbortError') return 'cancelled';
        }
    }

    // No share sheet at all - a desktop browser, almost always. Hand over the card as a
    // download and the words on the clipboard, so both halves can still be posted by hand.
    let saved = false;
    if (file) {
        try {
            const href = URL.createObjectURL(file);
            const link = document.createElement('a');
            link.href = href;
            link.download = file.name;
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(href), 10000);
            saved = true;
        } catch {
            saved = false;
        }
    }

    try {
        await navigator.clipboard.writeText(text + '\n' + url);
        return saved ? 'saved' : 'copied';
    } catch {
        return saved ? 'saved-only' : 'unavailable';
    }
};
