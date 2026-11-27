// The native share sheet, so "Share with friends" actually reaches a friend - WhatsApp,
// Messages, wherever they are - rather than only posting into this app's own feed.
//
// Returns a word rather than throwing, because every outcome here is normal: the browser
// may have no share sheet, and a player dismissing the sheet is not an error.
window.scoreMyLife = window.scoreMyLife || {};

window.scoreMyLife.share = async function (title, text, url) {
    if (navigator.share) {
        try {
            await navigator.share({ title, text, url });
            return 'shared';
        } catch (error) {
            // Dismissing the sheet raises AbortError. Say nothing and leave the screen alone.
            if (error && error.name === 'AbortError') {
                return 'cancelled';
            }
            // Anything else (no handler installed, permission refused) falls through to the
            // clipboard, which is still a usable way to send this to someone.
        }
    }

    try {
        await navigator.clipboard.writeText(`${text}\n${url}`);
        return 'copied';
    } catch {
        return 'unavailable';
    }
};
