// The viewers of the memory of the C64 (Memory tab of the studio): the bytes, a charset, sprites, a screen, a bitmap.
// Everything is drawn here, in the browser, from the snapshot the computer posts (studio.js hands it over): moving through the memory,
// the wheel, the keys, the hover and the clicks never go back to the server. Blazor only holds the settings of each viewer and calls
// update() when one changes.
//
// The snapshot is the layout of ThingCommodore.Graphics.MemoryCapture: RAM (64 KB), Color RAM (1024), VIC-II registers (64), character ROM
// (4 KB), VIC-II bank, CPU port, BASIC ROM (8 KB), KERNAL ROM (8 KB)
window.thing64Mem = (() => {
    const RAM = 65536, COLOR = 1024, REGS = 64, CHARS = 4096, ROM = 8192;

    // Pepto palette, as the pixels of an ImageData want it (0xAABBGGRR)
    const PALETTE = [0x000000, 0xFFFFFF, 0x68372B, 0x70A4B2, 0x6F3D86, 0x588D43, 0x352879, 0xB8C76F,
        0x6F4F25, 0x433900, 0x9A6759, 0x444444, 0x6C6C6C, 0x9AD284, 0x6C5EB5, 0x959595]
        .map(rgb => (0xFF000000 | (rgb & 0xFF) << 16 | (rgb >> 8 & 0xFF) << 8 | rgb >> 16) >>> 0);

    let snap = null;
    const views = new Set();

    // ---------------------------------------------------------------------------------------------------------- the snapshot

    function setSnapshot(bytes) {
        const data = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
        const expected = RAM + COLOR + REGS + CHARS + 2 + 2 * ROM;
        if (data.length !== expected) { snap = null; return false; }
        let at = 0;
        const take = size => { const part = data.subarray(at, at + size); at += size; return part; };
        snap = { ram: take(RAM), color: take(COLOR), regs: take(REGS), chars: take(CHARS), bank: data[at++] & 3, port: data[at++],
            basic: take(ROM), kernal: take(ROM) };
        // Every source is a power of two long: a view wraps its addresses with a mask
        snap.sources = { ram: snap.ram, rom: snap.chars, color: snap.color, cpu: decode(snap), basic: snap.basic, kernal: snap.kernal };
        for (const view of views) view.snapshotChanged();
        return true;
    }

    // The memory as the CPU sees it: -1 where a byte is not in the snapshot (the SID, the CIAs, the I/O expansion)
    function decode(s) {
        const out = new Int16Array(RAM);
        const loram = (s.port & 1) !== 0, hiram = (s.port & 2) !== 0, charen = (s.port & 4) !== 0;
        for (let address = 0; address < RAM; address++) {
            let value = s.ram[address];
            if (address >= 0xA000 && address < 0xC000 && loram && hiram) value = s.basic[address - 0xA000];
            else if (address >= 0xE000 && hiram) value = s.kernal[address - 0xE000];
            else if (address >= 0xD000 && address < 0xE000 && (loram || hiram)) {
                if (!charen) value = s.chars[address - 0xD000];
                else if (address < 0xD400) value = s.regs[address & 0x3F];
                else if (address >= 0xD800 && address < 0xDC00) value = s.color[address - 0xD800] & 15;
                else value = -1;
            }
            out[address] = value;
        }
        return out;
    }

    // What the VIC-II is set to show now (the same questions MemoryCapture answers)
    function info() {
        if (!snap) return null;
        const r = i => snap.regs[i];
        const charset = snap.bank * 0x4000 + (r(0x18) >> 1 & 7) * 0x800;
        return {
            bank: snap.bank,
            screen: snap.bank * 0x4000 + (r(0x18) >> 4) * 0x400,
            charset,
            charsetIsRom: (snap.bank & 1) === 0 && (charset & 0x3FFF) >= 0x1000 && (charset & 0x3FFF) < 0x2000,
            bitmap: snap.bank * 0x4000 + (r(0x18) & 8) * 0x400,
            bitmapMode: (r(0x11) & 0x20) !== 0,
            multicolor: (r(0x16) & 0x10) !== 0,
            background: r(0x21) & 15, multicolor1: r(0x22) & 15, multicolor2: r(0x23) & 15,
            spriteMulticolor1: r(0x25) & 15, spriteMulticolor2: r(0x26) & 15,
            sprites: Array.from({ length: 8 }, (_, i) => ({
                enabled: (r(0x15) >> i & 1) !== 0,
                color: r(0x27 + i) & 15,
                multicolor: (r(0x1C) >> i & 1) !== 0,
                address: snap.bank * 0x4000 + snap.ram[(snap.bank * 0x4000 + (r(0x18) >> 4) * 0x400 + 0x3F8 + i) & 0xFFFF] * 64,
            })),
        };
    }

    // ------------------------------------------------------------------------------------------------------------- helpers

    const hex = (value, digits) => value.toString(16).toUpperCase().padStart(digits, '0');
    const escapeHtml = text => text.replace(/&/g, '&amp;').replace(/</g, '&lt;');

    function button(text, title, onclick) {
        const b = document.createElement('button');
        b.className = 'tool border border-night-700';
        b.textContent = text;
        b.title = title;
        b.addEventListener('click', onclick);
        return b;
    }

    function ascii(value) { return value >= 0x20 && value < 0x7F ? String.fromCharCode(value) : '·'; }

    function screenCode(code) {
        if (code === 0) return '@';
        if (code >= 1 && code <= 26) return String.fromCharCode(64 + code);
        if (code === 27) return '['; if (code === 28) return '£'; if (code === 29) return ']'; if (code === 30) return '↑'; if (code === 31) return '←';
        if (code >= 0x20 && code <= 0x3F) return String.fromCharCode(code);
        return '▒';
    }

    function petscii(value) {
        if (value >= 0x20 && value <= 0x5A) return String.fromCharCode(value);
        if (value === 0x5B) return '['; if (value === 0x5C) return '£'; if (value === 0x5D) return ']'; if (value === 0x5E) return '↑'; if (value === 0x5F) return '←';
        if (value >= 0xC1 && value <= 0xDA) return String.fromCharCode(value - 0x80);
        if ((value >= 0x60 && value <= 0x7F) || (value >= 0xA0 && value <= 0xBF)) return '▒';
        return '·';
    }

    // Where an address of the CPU's memory goes, for the dump of the decoded view
    function region(address) {
        const port = snap.port, loram = (port & 1) !== 0, hiram = (port & 2) !== 0, charen = (port & 4) !== 0;
        if (address >= 0xA000 && address < 0xC000 && loram && hiram) return 'BASIC ';
        if (address >= 0xE000 && hiram) return 'KERNAL';
        if (address >= 0xD000 && address < 0xE000 && (loram || hiram)) {
            if (!charen) return 'CHARS ';
            return address < 0xD400 ? 'VIC-II' : address < 0xD800 ? 'SID   ' : address < 0xDC00 ? 'COLOR ' : address < 0xDE00 ? 'CIA   ' : 'I/O   ';
        }
        return 'RAM   ';
    }

    // 8 pixels of a row of a tile: hires (a bit is the background or the ink) or multicolor (pairs of bits choose among four colors, two pixels wide)
    function paintRow(pixels, width, x, y, bits, multicolor, pairs, ink) {
        const at = y * width + x;
        if (multicolor) for (let px = 0; px < 8; px++) pixels[at + px] = pairs[bits >> (6 - (px >> 1) * 2) & 3];
        else for (let px = 0; px < 8; px++) pixels[at + px] = (bits >> (7 - px) & 1) !== 0 ? ink : pairs[0];
    }

    // -------------------------------------------------------------------------------------------------------------- the view

    // The measures of each kind of view, in bytes of memory: what a row, an item and a page move
    const kinds = {
        dump: { item: 'byte', row: o => o.bytesPerRow, step: () => 1, page: (o, v) => o.bytesPerRow * v.rows() },
        charset: { item: 'character', row: o => o.perRow * 8, step: () => 8, page: o => o.count * 8, cell: () => [8, 8] },
        sprites: { item: 'sprite', row: o => o.perRow * o.stride, step: o => o.stride, page: o => o.count * o.stride, cell: () => [24, 21] },
        screen: { item: 'cell', row: o => o.columns, step: () => 1, page: o => o.columns * o.screenRows, cell: () => [8, 8] },
        bitmap: { item: 'cell', row: () => 320, step: () => 8, page: () => 8000, cell: () => [8, 8] },
    };

    const defaults = {
        source: 'ram', bytesPerRow: 16, characters: 'petscii',
        zoom: 3, grid: true, columns: 40, screenRows: 25, screenCharset: 'vic', screenColors: 'colorram', cellMode: 'bycolor',
        charsetAddress: 0x2800, bitmapScreen: 0x0400, count: 256, perRow: 16, stride: 64, multicolor: false,
        background: 6, multicolor1: 14, multicolor2: 1, ink: 7,
    };

    class View {
        constructor(host, kind, options) {
            this.host = host;
            this.kind = kind;
            this.spec = kinds[kind];
            this.opts = { ...defaults, ...options };
            this.offset = options?.offset ?? 0;
            this.glyphs = new Map();          // the characters of a screen, drawn once for each code, color and mode
            this.pending = 0;
            this.build();
            views.add(this);
            this.redraw();
        }

        // ---- the pieces: a bar to move, the picture (or the dump) and a line of status
        build() {
            const host = this.host;
            host.innerHTML = '';
            host.className = (host.className + ' flex flex-col min-w-0 min-h-0').trim();

            this.bar = document.createElement('div');
            this.bar.className = 'flex shrink-0 flex-wrap items-center gap-1 border-b border-night-800 px-3 py-1.5';
            this.label = document.createElement('span');
            this.label.className = 'mr-1 font-mono text-chip-300';
            this.bar.append(this.label);
            const step = (name, title, amount) => this.bar.append(button(name, title, () => this.move(amount())));
            step('⏮', 'A page back', () => -this.page());
            step('▲ row', 'A row back', () => -this.row());
            if (this.spec.item !== 'byte') step('◀ ' + this.spec.item, 'One ' + this.spec.item + ' back', () => -this.step());
            step('-1', 'One byte back', () => -1);
            step('+1', 'One byte on', () => 1);
            if (this.spec.item !== 'byte') step(this.spec.item + ' ▶', 'One ' + this.spec.item + ' on', () => this.step());
            step('▼ row', 'A row on', () => this.row());
            step('⏭', 'A page on', () => this.page());
            this.slider = document.createElement('input');
            this.slider.type = 'range';
            this.slider.min = 0;
            this.slider.step = 1;
            this.slider.className = 'ml-2 min-w-40 flex-1';
            this.slider.title = 'Anywhere in the memory';
            this.slider.addEventListener('input', () => { this.offset = +this.slider.value; this.redraw(); });
            this.bar.append(this.slider);
            const form = document.createElement('form');
            form.className = 'flex items-center gap-1';
            this.address = document.createElement('input');
            this.address.className = 'field w-24 py-0.5 font-mono';
            this.address.placeholder = '$2000';
            this.address.title = 'An address: $hex, a number, or +n / -n from here';
            const go = document.createElement('button');
            go.className = 'rounded border border-night-600 px-2 py-0.5';
            go.textContent = 'Go';
            form.append(this.address, go);
            form.addEventListener('submit', e => { e.preventDefault(); this.goTo(this.address.value); });
            this.bar.append(form);

            this.body = document.createElement('div');
            this.body.className = 'min-h-0 flex-1 overflow-auto p-3 outline-none';
            this.body.tabIndex = -1;
            this.body.addEventListener('wheel', e => { e.preventDefault(); this.move(e.deltaY > 0 ? this.row() : -this.row()); }, { passive: false });
            this.body.addEventListener('keydown', e => this.key(e));
            this.body.addEventListener('click', () => this.body.focus());

            this.status = document.createElement('div');
            this.status.className = 'shrink-0 border-t border-night-800 px-3 py-1 text-night-300';
            this.status.textContent = ' ';

            if (this.kind === 'dump') {
                this.text = document.createElement('div');
                this.text.className = 'font-mono text-[13px] leading-6 whitespace-pre';
                this.text.addEventListener('click', e => {
                    const row = e.target.closest('[data-address]');
                    if (row) { this.offset = +row.dataset.address; this.redraw(); }
                });
                this.body.append(this.text);
            } else {
                this.stage = document.createElement('div');
                this.stage.className = 'relative inline-block select-none';
                this.stage.style.cursor = 'crosshair';
                this.canvas = document.createElement('canvas');
                this.canvas.className = 'pixelated block';
                this.gridLayer = document.createElement('div');
                this.gridLayer.className = 'pointer-events-none absolute inset-0';
                this.stage.append(this.canvas, this.gridLayer);
                this.stage.addEventListener('mousemove', e => this.hover(e));
                this.stage.addEventListener('mouseleave', () => { this.status.textContent = ' '; });
                this.stage.addEventListener('click', e => this.pick(e));
                this.body.append(this.stage);
            }
            host.append(this.bar, this.body, this.status);
            // A viewer in a tab that was hidden gets its size when it is shown (and the window can change): the rows in view follow it
            this.resizer = new ResizeObserver(() => this.redraw());
            this.resizer.observe(this.body);
        }

        dispose() { views.delete(this); this.resizer?.disconnect(); cancelAnimationFrame(this.pending); this.host.innerHTML = ''; }

        // ---- settings and snapshot
        update(options) {
            Object.assign(this.opts, options);
            if (options && 'offset' in options) this.offset = options.offset;
            this.glyphs.clear();
            this.redraw();
        }

        snapshotChanged() { this.glyphs.clear(); this.redraw(); }

        get source() { return snap?.sources[this.opts.source] ?? snap?.sources.ram; }
        get size() { return this.source?.length ?? RAM; }
        rows() { return Math.max(4, Math.floor(this.body.clientHeight / 24)); }
        row() { return this.spec.row(this.opts, this); }
        step() { return this.spec.step(this.opts, this); }
        page() { return this.spec.page(this.opts, this); }

        // ---- moving
        move(delta) { this.offset = (this.offset + delta) & (this.size - 1); this.redraw(); }

        goTo(text) {
            text = text.trim().replace(/_/g, '');
            let value;
            if (/^[+-]\d+$/.test(text)) value = this.offset + parseInt(text, 10);
            else if (text.startsWith('$')) value = parseInt(text.slice(1), 16);
            else if (/^0x/i.test(text)) value = parseInt(text.slice(2), 16);
            else value = parseInt(text, 10);
            if (Number.isNaN(value) || value < 0 || value >= this.size) { this.say(`${text} is not an address from 0 to $${hex(this.size - 1, 4)}`); return; }
            this.offset = value;
            this.redraw();
        }

        key(e) {
            const moves = { ArrowUp: () => this.move(-this.row()), ArrowDown: () => this.move(this.row()),
                ArrowLeft: () => this.move(e.shiftKey ? -this.step() : -1), ArrowRight: () => this.move(e.shiftKey ? this.step() : 1),
                PageUp: () => this.move(-this.page()), PageDown: () => this.move(this.page()),
                Home: () => { this.offset = 0; this.redraw(); }, End: () => { this.offset = this.size - 1; this.redraw(); } };
            if (moves[e.key]) { e.preventDefault(); moves[e.key](); }
        }

        say(text) { this.status.textContent = text || ' '; }

        // ---- drawing, at most once for each frame of the browser
        redraw() {
            if (this.pending) return;
            this.pending = requestAnimationFrame(() => {
                this.pending = 0;
                try { this.draw(); } catch (e) { this.say('Cannot draw: ' + e.message); console.error(e); }
            });
        }

        draw() {
            this.offset &= this.size - 1;
            this.label.textContent = '$' + hex(this.offset, 4);
            this.slider.max = this.size - 1;
            this.slider.value = this.offset;
            if (!snap) return;
            if (this.kind === 'dump') this.drawDump();
            else this.drawPicture();
        }

        // ---- the dump: only the rows in view are made
        drawDump() {
            const o = this.opts, source = this.source, mask = source.length - 1, cpu = o.source === 'cpu';
            const html = [];
            for (let r = 0, rows = this.rows(); r < rows; r++) {
                const address = (this.offset + r * o.bytesPerRow) & mask;
                let bytes = '', chars = '', reverse = false;
                for (let i = 0; i < o.bytesPerRow; i++) {
                    const value = source[(address + i) & mask];
                    if (i > 0) bytes += i % 8 === 0 ? '  ' : ' ';
                    bytes += value < 0 ? '··' : hex(value, 2);
                    const known = value >= 0;
                    const inverse = o.characters === 'screen' && known && value >= 0x80;
                    if (inverse !== reverse) { chars += inverse ? '<span class="bg-night-200 text-night-900">' : '</span>'; reverse = inverse; }
                    const glyph = !known ? '·' : o.characters === 'ascii' ? ascii(value) : o.characters === 'screen' ? screenCode(value & 0x7F) : petscii(value);
                    chars += escapeHtml(glyph);
                }
                if (reverse) chars += '</span>';
                const tag = cpu ? `<span class="w-16 inline-block text-night-500">${region(address)}</span>` : '';
                html.push(`<div class="cursor-pointer rounded-sm hover:bg-night-800" data-address="${address}"><span class="inline-block w-14 text-night-400">$${hex(address, 4)}</span>${tag}<span>${bytes}</span><span class="ml-3 border-l border-night-800 pl-3">${chars}</span></div>`);
            }
            this.text.innerHTML = html.join('');
        }

        copyBytes() {
            const o = this.opts, source = this.source, mask = source.length - 1;
            const lines = [];
            for (let r = 0, rows = this.rows(); r < rows; r++) {
                const address = (this.offset + r * o.bytesPerRow) & mask;
                const values = Array.from({ length: o.bytesPerRow }, (_, i) => '$' + hex(Math.max(0, source[(address + i) & mask]), 2));
                lines.push(`            .byte ${values.join(',')} ; $${hex(address, 4)}`);
            }
            return { text: lines.join('\n') + '\n', message: `${this.rows() * o.bytesPerRow} bytes copied` };
        }

        copyCharset() {
            const o = this.opts, source = this.source, mask = source.length - 1;
            const lines = [';@ charset first=$00' + (o.multicolor ? ' multicolor=1' : ''), 'charset__characters:'];
            for (let k = 0; k < o.count; k++) {
                const values = Array.from({ length: 8 }, (_, i) => '$' + hex(source[(this.offset + k * 8 + i) & mask], 2));
                lines.push(`            .byte ${values.join(',')} ; $${hex(k & 0xFF, 2)} (from $${hex((this.offset + k * 8) & mask, 4)})`);
            }
            lines.push('charset__charactersEnd:', ';@ end');
            return { text: lines.join('\n') + '\n', message: `${o.count} characters copied` };
        }

        // ---- the pictures
        colors() {
            const o = this.opts;
            return { bg: PALETTE[o.background], mc1: PALETTE[o.multicolor1], mc2: PALETTE[o.multicolor2], ink: PALETTE[o.ink], inkIndex: o.ink };
        }

        drawPicture() {
            const o = this.opts;
            let image;
            try {
                image = { screen: () => this.screenImage(), charset: () => this.charsetImage(), sprites: () => this.spritesImage(), bitmap: () => this.bitmapImage() }[this.kind]();
            } catch (e) { this.say(e.message); return; }
            const { width, height, pixels } = image;
            if (this.canvas.width !== width || this.canvas.height !== height) { this.canvas.width = width; this.canvas.height = height; }
            this.canvas.style.width = width * o.zoom + 'px';
            this.canvas.style.height = height * o.zoom + 'px';
            this.canvas.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(pixels.buffer), width, height), 0, 0);
            const [cw, ch] = this.spec.cell(o);
            this.gridLayer.style.display = o.grid ? 'block' : 'none';
            this.gridLayer.style.backgroundImage = 'linear-gradient(to right, rgba(124,138,247,.3) 1px, transparent 1px), linear-gradient(to bottom, rgba(124,138,247,.3) 1px, transparent 1px)';
            this.gridLayer.style.backgroundSize = `${cw * o.zoom}px ${ch * o.zoom}px`;
            this.imageWidth = width;
            this.imageHeight = height;
        }

        charsetImage() {
            const o = this.opts, c = this.colors(), source = this.source, mask = source.length - 1;
            const rows = Math.max(1, Math.ceil(o.count / o.perRow)), width = o.perRow * 8, height = rows * 8;
            const pixels = new Uint32Array(width * height);
            const pairs = [c.bg, c.mc1, c.mc2, PALETTE[o.ink & 7]];
            for (let k = 0; k < o.perRow * rows; k++)
                for (let row = 0; row < 8; row++)
                    paintRow(pixels, width, k % o.perRow * 8, Math.floor(k / o.perRow) * 8 + row, k < o.count ? source[(this.offset + k * 8 + row) & mask] : 0, o.multicolor, pairs, c.ink);
            return { width, height, pixels };
        }

        // A multicolor sprite's bit pairs are 00 transparent (the background), 01 multicolor 1, 10 its own color, 11 multicolor 2
        spritesImage() {
            const o = this.opts, c = this.colors(), source = this.source, mask = source.length - 1;
            const rows = Math.max(1, Math.ceil(o.count / o.perRow)), width = o.perRow * 24, height = rows * 21;
            const pixels = new Uint32Array(width * height);
            const pairs = [c.bg, c.mc1, c.ink, c.mc2];
            for (let k = 0; k < o.perRow * rows; k++) {
                const x0 = k % o.perRow * 24, y0 = Math.floor(k / o.perRow) * 21;
                const glyph = this.glyph(`s${k < o.count ? this.spriteKey(k) : ''}|${o.multicolor}`, () => {
                    const g = new Uint32Array(24 * 21);
                    for (let y = 0; y < 21; y++)
                        for (let b = 0; b < 3; b++)
                            paintRow(g, 24, b * 8, y, k < o.count ? source[(this.offset + k * o.stride + y * 3 + b) & mask] : 0, o.multicolor, pairs, c.ink);
                    return g;
                });
                for (let y = 0; y < 21; y++) pixels.set(glyph.subarray(y * 24, y * 24 + 24), (y0 + y) * width + x0);
            }
            return { width, height, pixels };
        }

        // The 63 bytes of a sprite as a string: the key of its picture in the cache (a sprite that is the same as another is drawn once)
        spriteKey(k) {
            const source = this.source, mask = source.length - 1, o = this.opts;
            let key = '';
            for (let i = 0; i < 63; i++) key += String.fromCharCode(source[(this.offset + k * o.stride + i) & mask]);
            return key;
        }

        // A screen of characters: the cell of a code in a color is drawn once (a screen is mostly the same few cells)
        screenImage() {
            const o = this.opts, c = this.colors(), source = this.source, mask = source.length - 1, color = snap.color;
            const width = o.columns * 8, height = o.screenRows * 8;
            const pixels = new Uint32Array(width * height);
            const charRow = this.charRow();
            for (let cell = 0; cell < o.columns * o.screenRows; cell++) {
                const code = source[(this.offset + cell) & mask];
                const nibble = o.screenColors === 'fixed' ? o.ink : color[cell & (COLOR - 1)] & 15;
                const multicolor = o.cellMode === 'multicolor' || (o.cellMode === 'bycolor' && (nibble & 8) !== 0);
                const ink = o.cellMode === 'hires' ? nibble : nibble & 7;
                const glyph = this.glyph(`c${code}|${nibble}|${multicolor}|${ink}`, () => {
                    const g = new Uint32Array(64);
                    const pairs = [c.bg, c.mc1, c.mc2, PALETTE[ink]];
                    for (let row = 0; row < 8; row++) paintRow(g, 8, 0, row, charRow(code, row), multicolor, pairs, PALETTE[ink]);
                    return g;
                });
                const x0 = cell % o.columns * 8, y0 = Math.floor(cell / o.columns) * 8;
                for (let y = 0; y < 8; y++) pixels.set(glyph.subarray(y * 8, y * 8 + 8), (y0 + y) * width + x0);
            }
            return { width, height, pixels };
        }

        // How a screen finds the bytes of a character: as the VIC-II does, from the character ROM, or from RAM
        charRow() {
            const o = this.opts, rom = snap.chars, ram = snap.ram, bank = snap.bank;
            if (o.screenCharset === 'upper') return (code, row) => rom[(code * 8 + row) & 0x0FFF];
            if (o.screenCharset === 'lower') return (code, row) => rom[(0x0800 + code * 8 + row) & 0x0FFF];
            if (o.screenCharset === 'ram') return (code, row) => ram[(o.charsetAddress + code * 8 + row) & 0xFFFF];
            const address = this.vicCharset();
            const inRom = (bank & 1) === 0 && (address & 0x3FFF) >= 0x1000 && (address & 0x3FFF) < 0x2000;
            return (code, row) => inRom ? rom[(address + code * 8 + row) & 0x0FFF] : ram[(address + code * 8 + row) & 0xFFFF];
        }

        vicCharset() { return snap.bank * 0x4000 + (snap.regs[0x18] >> 1 & 7) * 0x800; }

        // A bitmap screen, 320x200: the colors of a cell come from the screen RAM (hires: high nibble ink, low nibble background; multicolor: 01 the
        // high nibble, 10 the low one, 11 the Color RAM) and 00 is the background
        bitmapImage() {
            const o = this.opts, source = this.source, mask = source.length - 1, ram = snap.ram, color = snap.color;
            const pixels = new Uint32Array(320 * 200);
            for (let cell = 0; cell < 1000; cell++) {
                const colors = ram[(o.bitmapScreen + cell) & 0xFFFF], high = colors >> 4, low = colors & 15, fromColor = color[cell] & 15;
                const pairs = o.multicolor ? [PALETTE[o.background], PALETTE[high], PALETTE[low], PALETTE[fromColor]] : [PALETTE[low], 0, 0, 0];
                for (let row = 0; row < 8; row++)
                    paintRow(pixels, 320, cell % 40 * 8, Math.floor(cell / 40) * 8 + row, source[(this.offset + cell * 8 + row) & mask], o.multicolor, pairs, PALETTE[high]);
            }
            return { width: 320, height: 200, pixels };
        }

        glyph(key, make) {
            let glyph = this.glyphs.get(key);
            if (!glyph) {
                if (this.glyphs.size > 4096) this.glyphs.clear();
                glyph = make();
                this.glyphs.set(key, glyph);
            }
            return glyph;
        }

        // ---- what is under the mouse, as an address and an index
        at(e) {
            const o = this.opts, rect = this.canvas.getBoundingClientRect();
            const x = Math.floor((e.clientX - rect.left) / o.zoom), y = Math.floor((e.clientY - rect.top) / o.zoom), mask = this.size - 1;
            if (x < 0 || y < 0 || x >= this.imageWidth || y >= this.imageHeight) return null;
            switch (this.kind) {
                case 'screen': {
                    const cell = Math.floor(y / 8) * o.columns + Math.floor(x / 8), address = (this.offset + cell) & mask;
                    return { address, text: `cell ${Math.floor(x / 8)},${Math.floor(y / 8)} at $${hex(address, 4)}: screen code $${hex(this.source[address], 2)}` };
                }
                case 'charset': {
                    const k = Math.floor(y / 8) * o.perRow + Math.floor(x / 8), address = (this.offset + k * 8) & mask;
                    return { address, text: `character #${k} ($${hex(k & 0xFF, 2)}) at $${hex(address, 4)}` };
                }
                case 'sprites': {
                    const k = Math.floor(y / 21) * o.perRow + Math.floor(x / 24), address = (this.offset + k * o.stride) & mask;
                    return { address, text: `sprite #${k} at $${hex(address, 4)} (block ${hex(address >> 6, 2)}, $${hex(address >> 6, 2)} * 64)` };
                }
                default: {
                    const cell = Math.floor(y / 8) * 40 + Math.floor(x / 8), address = (this.offset + cell * 8) & mask;
                    return { address, text: `cell ${Math.floor(x / 8)},${Math.floor(y / 8)}: bitmap byte at $${hex(address, 4)}` };
                }
            }
        }

        hover(e) { this.say(this.at(e)?.text); }

        pick(e) {
            const at = this.at(e);
            if (at) { this.offset = at.address; this.redraw(); }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ API

    const mounted = new WeakMap();

    return {
        setSnapshot,
        info,
        hasSnapshot: () => snap !== null,
        mount(host, kind, options) {
            mounted.get(host)?.dispose();
            const view = new View(host, kind, options);
            mounted.set(host, view);
        },
        update(host, options) { mounted.get(host)?.update(options); },
        dispose(host) { mounted.get(host)?.dispose(); mounted.delete(host); },
        // Copies the bytes in view as .byte lines, or the characters as a charset region, and tells what was copied
        copy(host, what) {
            const view = mounted.get(host);
            if (!view) return '';
            const { text, message } = what === 'charset' ? view.copyCharset() : view.copyBytes();
            window.thing64Studio?.copyText(text);
            return message;
        },
        offset: host => mounted.get(host)?.offset ?? 0,
    };
})();
