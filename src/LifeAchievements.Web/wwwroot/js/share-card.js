// Draws the unlock as a shareable image - the trophy screen, not a sentence about it.
//
// 1080x1350 (4:5), which is what WhatsApp, Instagram and Messages all show without cropping.
// Colours and type are the app's own, so a shared card looks like it came from the app rather
// than from a generic card generator.
window.scoreMyLife = window.scoreMyLife || {};

(function () {
    const W = 1080, H = 1350;

    const C = {
        bg: '#0E1020',
        surface: '#181B30',
        surface2: '#22264A',
        border: '#2C3160',
        text: '#F2F3FA',
        muted: '#A3A8C8',
        gold: '#F5B83D',
        teal: '#4FD1C5'
    };

    // The card is drawn once, at the moment of sharing, so a font still swapping in would be
    // baked into the image permanently. Wait for the faces the card actually uses.
    async function waitForFonts() {
        if (!document.fonts) return;
        try {
            await Promise.all([
                document.fonts.load('700 76px "Space Grotesk"'),
                document.fonts.load('400 34px Inter'),
                document.fonts.load('600 34px Inter'),
                document.fonts.load('italic 400 32px Inter')
            ]);
            await document.fonts.ready;
        } catch {
            // Draw with whatever is available - a card in a fallback face beats no card.
        }
    }

    // The badge icon is lifted out of the live DOM rather than restated here, so the card can
    // never drift from the icon set the app actually renders.
    async function iconImage(selector, colour) {
        const source = document.querySelector(selector);
        if (!source) return null;

        const svg = source.cloneNode(true);
        svg.setAttribute('width', '160');
        svg.setAttribute('height', '160');
        svg.setAttribute('stroke', colour);
        svg.setAttribute('stroke-width', '1.6');
        svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');

        const markup = new XMLSerializer().serializeToString(svg);
        // A data: URL keeps the canvas untainted, so toBlob still works.
        const url = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(markup);

        return await new Promise(resolve => {
            const img = new Image();
            img.onload = () => resolve(img);
            img.onerror = () => resolve(null);
            img.src = url;
        });
    }

    function wrap(ctx, text, maxWidth, maxLines) {
        const words = String(text || '').split(/\s+/).filter(Boolean);
        const lines = [];
        let line = '';
        let overflowed = false;

        for (const word of words) {
            const next = line ? line + ' ' + word : word;
            if (ctx.measureText(next).width <= maxWidth || !line) {
                line = next;
            } else {
                lines.push(line);
                line = word;
                if (lines.length === maxLines) { overflowed = true; break; }
            }
        }
        if (!overflowed && lines.length < maxLines && line) lines.push(line);

        // Anything that did not fit is cut with an ellipsis rather than silently dropped.
        if (overflowed) {
            let last = lines[maxLines - 1];
            while (last.length > 1 && ctx.measureText(last + '...').width > maxWidth) {
                last = last.slice(0, -1);
            }
            lines[maxLines - 1] = last.replace(/[\s.,;:]+$/, '') + '...';
        }
        return lines;
    }

    // Shrinks the heading until it fits, the way the app's own layout does - a long
    // achievement name should not spill off the card.
    function fitLines(ctx, text, maxWidth, maxLines, start, min) {
        let size = start;
        for (;;) {
            ctx.font = '700 ' + size + 'px "Space Grotesk", sans-serif';
            const lines = wrap(ctx, text, maxWidth, maxLines + 1);
            if (lines.length <= maxLines || size <= min) {
                return { size: size, lines: lines.slice(0, maxLines) };
            }
            size -= 4;
        }
    }

    function centred(ctx, lines, y, lineHeight) {
        for (const line of lines) {
            ctx.fillText(line, W / 2, y);
            y += lineHeight;
        }
        return y;
    }

    function pillWidth(ctx, text) {
        ctx.font = '600 32px Inter, sans-serif';
        return ctx.measureText(text).width + 60;
    }

    function pill(ctx, text, x, y, opts) {
        const width = pillWidth(ctx, text);
        const height = 64;

        ctx.beginPath();
        ctx.roundRect(x, y, width, height, height / 2);
        if (opts.fill) {
            ctx.fillStyle = opts.fill;
            ctx.fill();
        } else {
            ctx.strokeStyle = opts.stroke;
            ctx.lineWidth = 2.5;
            ctx.stroke();
        }

        ctx.fillStyle = opts.colour;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(text, x + width / 2, y + height / 2 + 1);
        ctx.textBaseline = 'alphabetic';
        return width;
    }

    window.scoreMyLife.renderCard = async function (data) {
        await waitForFonts();

        const canvas = document.createElement('canvas');
        canvas.width = W;
        canvas.height = H;
        const ctx = canvas.getContext('2d');

        ctx.fillStyle = C.bg;
        ctx.fillRect(0, 0, W, H);

        ctx.beginPath();
        ctx.roundRect(44, 44, W - 88, H - 88, 56);
        ctx.fillStyle = C.surface;
        ctx.fill();
        ctx.strokeStyle = C.border;
        ctx.lineWidth = 3;
        ctx.stroke();

        ctx.textAlign = 'center';

        ctx.fillStyle = C.gold;
        ctx.font = '700 32px "Space Grotesk", sans-serif';
        if ('letterSpacing' in ctx) ctx.letterSpacing = '9px';
        ctx.fillText((data.kicker || 'Achievement unlocked').toUpperCase(), W / 2, 164);
        if ('letterSpacing' in ctx) ctx.letterSpacing = '0px';

        const cx = W / 2, cy = 392, ring = 178;
        ctx.beginPath();
        ctx.setLineDash([16, 16]);
        ctx.strokeStyle = C.border;
        ctx.lineWidth = 4;
        ctx.arc(cx, cy, ring, 0, Math.PI * 2);
        ctx.stroke();
        ctx.setLineDash([]);

        // The same six confetti dots the unlock screen animates, held still.
        const dots = [[0.0, C.gold], [0.19, C.teal], [0.36, C.gold], [0.54, C.teal], [0.72, C.gold], [0.88, C.teal]];
        for (const dot of dots) {
            const a = dot[0] * Math.PI * 2 - Math.PI / 2;
            ctx.beginPath();
            ctx.fillStyle = dot[1];
            ctx.arc(cx + Math.cos(a) * (ring - 34), cy + Math.sin(a) * (ring - 34), 9, 0, Math.PI * 2);
            ctx.fill();
        }

        const size = 228;
        ctx.save();
        ctx.translate(cx, cy);
        ctx.rotate(-8 * Math.PI / 180);
        const grad = ctx.createLinearGradient(-size / 2, -size / 2, size / 2, size / 2);
        grad.addColorStop(0, '#FFD167');
        grad.addColorStop(1, C.gold);
        ctx.beginPath();
        ctx.roundRect(-size / 2, -size / 2, size, size, 58);
        ctx.fillStyle = grad;
        ctx.fill();

        const icon = await iconImage(data.iconSelector || '.badge svg', C.bg);
        if (icon) ctx.drawImage(icon, -80, -80, 160, 160);
        ctx.restore();

        let y = 672;

        const name = fitLines(ctx, data.name, W - 230, 2, 78, 46);
        ctx.fillStyle = C.text;
        ctx.font = '700 ' + name.size + 'px "Space Grotesk", sans-serif';
        y = centred(ctx, name.lines, y, name.size + 14);

        y += 10;
        ctx.fillStyle = C.muted;
        ctx.font = '400 33px Inter, sans-serif';
        y = centred(ctx, wrap(ctx, data.description, W - 230, 2), y, 44);

        y += 24;
        const gap = 18;
        const total = pillWidth(ctx, data.xpText) + gap + pillWidth(ctx, data.tier);
        let px = (W - total) / 2;
        px += pill(ctx, data.xpText, px, y, { fill: C.gold, colour: C.bg }) + gap;
        pill(ctx, data.tier, px, y, { stroke: C.teal, colour: C.teal });
        y += 64;

        // The progress block is pinned rather than flowed, so every card has the same footing
        // whatever the name, description and note did above it. Without this the bar and the
        // footer end up crushed together on a busy card and adrift on a sparse one.
        const progressY = 1150;
        const barX = 150, barW = W - 300;

        // The note, when there is one - the part worth reading in a year. It takes whatever
        // room is left between the pills and the pinned block, so a long name simply means a
        // shorter note rather than a collision.
        if (data.note) {
            const top = y + 38;
            const room = (progressY - 56) - top;
            const maxLines = Math.min(2, Math.floor(room / 44));
            if (maxLines >= 1) {
                ctx.fillStyle = C.text;
                ctx.font = 'italic 400 32px Inter, sans-serif';
                centred(ctx, wrap(ctx, '"' + data.note + '"', W - 260, maxLines), top, 44);
            }
        }

        // A hairline to separate the achievement from the running score.
        ctx.beginPath();
        ctx.moveTo(barX, progressY - 56);
        ctx.lineTo(barX + barW, progressY - 56);
        ctx.strokeStyle = C.border;
        ctx.lineWidth = 2;
        ctx.stroke();

        y = progressY;
        ctx.fillStyle = C.muted;
        ctx.font = '500 29px Inter, sans-serif';
        ctx.textAlign = 'left';
        ctx.fillText(data.levelLabel || '', barX, y);
        ctx.textAlign = 'right';
        ctx.fillText(data.levelRight || '', barX + barW, y);
        ctx.textAlign = 'center';

        y += 26;
        ctx.beginPath();
        ctx.roundRect(barX, y, barW, 16, 8);
        ctx.fillStyle = C.surface2;
        ctx.fill();

        const pct = Math.max(0, Math.min(100, Number(data.progressPercent) || 0));
        if (pct > 0) {
            ctx.beginPath();
            ctx.roundRect(barX, y, Math.max(16, barW * pct / 100), 16, 8);
            ctx.fillStyle = C.gold;
            ctx.fill();
        }

        ctx.fillStyle = C.muted;
        ctx.font = '500 30px Inter, sans-serif';
        ctx.fillText(data.footer || 'scoremylife.pages.dev', W / 2, H - 92);

        return await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    };
})();
