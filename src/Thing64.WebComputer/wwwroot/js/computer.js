// The browser side of the C64: the animation loop that drives the board, the canvas that shows its frames,
// the keyboard, drag and drop, and the ROMs kept in localStorage. Talks to WebComputer.cs (JSExport/JSImport)

let computer = null;      // the [JSExport] methods of WebComputer
let canvas = null;
let context = null;
let image = null;
let running = false;
let last = 0;
let speedSamples = [];

async function exportsOf() {
    const runtime = await globalThis.getDotnetRuntime(0);
    const exports = await runtime.getAssemblyExports('Thing64.WebComputer.dll');
    return exports.Thing64.WebComputer.WebComputer;
}

export async function start(canvasId) {
    computer = await exportsOf();
    canvas = document.getElementById(canvasId);
    context = canvas.getContext('2d');
    image = context.createImageData(canvas.width, canvas.height);

    canvas.addEventListener('keydown', e => { if (computer.KeyDown(e.code)) e.preventDefault(); });
    canvas.addEventListener('keyup', e => { if (computer.KeyUp(e.code)) e.preventDefault(); });
    canvas.addEventListener('blur', () => computer.ReleaseKeys());
    canvas.addEventListener('dragover', e => e.preventDefault());
    canvas.addEventListener('drop', async e => {
        e.preventDefault();
        const file = e.dataTransfer.files[0];
        if (!file) return;
        const status = computer.Load(file.name, new Uint8Array(await file.arrayBuffer()));
        document.querySelector('.status').textContent = status;
        canvas.focus();
    });

    running = true;
    last = performance.now();
    requestAnimationFrame(tick);
    canvas.focus();
}

export function stop() {
    running = false;
}

export function focus(elementId) {
    document.getElementById(elementId)?.focus();
}

// One animation frame: the board runs the cycles of the elapsed time
function tick(now) {
    if (!running) return;
    const speed = computer.Frame(now - last);
    last = now;
    if (speed > 0) showSpeed(speed);
    requestAnimationFrame(tick);
}

// How many times faster than a real C64 the emulation could run, averaged over about a second
let speedSince = 0;
function showSpeed(speed) {
    speedSamples.push(speed);
    const now = performance.now();
    if (now - speedSince < 1000) return;
    speedSince = now;
    const average = speedSamples.reduce((a, b) => a + b, 0) / speedSamples.length;
    speedSamples = [];
    const element = document.getElementById('speed');
    if (element) element.textContent = `emulation capacity ${average.toFixed(2)}x real time`;
    console.log(`thing64 speed ${average.toFixed(2)}`);
}

// A VIC-II frame (32-bit BGRA, from the WebAssembly memory) to the canvas (RGBA)
export function present(view) {
    const data = image.data;
    view.copyTo(new Uint8Array(data.buffer));   // copyTo wants a Uint8Array: a view on the image's bytes
    for (let i = 0; i < data.length; i += 4) {
        const blue = data[i];
        data[i] = data[i + 2];
        data[i + 2] = blue;
    }
    context.putImageData(image, 0, 0);
}

export function storedRom(name) {
    const text = localStorage.getItem('thing64.rom.' + name);
    if (!text) return null;
    return Uint8Array.from(atob(text), c => c.charCodeAt(0));
}

export function storeRom(name, content) {
    let text = '';
    for (const byte of content) text += String.fromCharCode(byte);
    localStorage.setItem('thing64.rom.' + name, btoa(text));
}
