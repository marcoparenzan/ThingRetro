// The browser side of the C64: the animation loop that drives the board, the canvas that shows its frames,
// keyboard, gamepad, sound, drag and drop, and the ROMs kept in localStorage.
// Talks to WebComputer.cs (JSExport/JSImport).
// Opened by a page of the same site (the web studio: in an iframe, or in a window it opened), it runs the programs
// that page sends:
//   host -> computer: { type: 'load', name, bytes }, { type: 'focus' }, { type: 'capture' }, { type: 'memory' }
//   computer -> host: { type: 'ready' }, { type: 'status', text }, { type: 'capture', bytes } (the video memory),
//                     { type: 'memory', bytes } (the whole memory)

let computer = null;      // the [JSExport] methods of WebComputer
let canvas = null;
let context = null;
let image = null;
let running = false;
let last = 0;
let speedSamples = [];
let speedSince = 0;
let gamepadBits = 0;
let audio = null;         // AudioContext, created at the first user gesture (browsers require one)
let speaker = null;       // AudioWorkletNode (audio-worklet.js)
let audioWanted = localStorage.getItem('thing64.sound') !== 'off';
let pendingLoad = null;   // a program sent before the C64 was running (ROMs still to choose)

const host = window.parent !== window ? window.parent : window.opener;   // the studio, if any

window.addEventListener('message', e => {
    if (e.origin !== location.origin) return;
    // The studio sends these right after a user gesture (F5, a click): with allow="autoplay" on the iframe the
    // sound can start then, without a click on the screen
    if (e.data?.type === 'load') {
        if (running) load(e.data.name, e.data.bytes); else pendingLoad = e.data;
        startAudio();
    } else if (e.data?.type === 'focus') {
        canvas?.focus();
        startAudio();
    } else if (e.data?.type === 'capture') {
        e.source.postMessage({ type: 'capture', bytes: running ? computer.CaptureVideo() : new Uint8Array(0) }, location.origin);
    } else if (e.data?.type === 'memory') {
        e.source.postMessage({ type: 'memory', bytes: running ? computer.CaptureMemory() : new Uint8Array(0) }, location.origin);
    }
});
host?.postMessage({ type: 'ready' }, location.origin);

function load(name, bytes) {
    const status = computer.Load(name, bytes);
    const element = document.querySelector('.status');
    if (element) element.textContent = status;
    host?.postMessage({ type: 'status', text: status }, location.origin);
    canvas.focus();
}

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

    canvas.addEventListener('keydown', e => { startAudio(); if (computer.KeyDown(e.code)) e.preventDefault(); });
    canvas.addEventListener('keyup', e => { if (computer.KeyUp(e.code)) e.preventDefault(); });
    document.addEventListener('pointerdown', () => startAudio());
    canvas.addEventListener('blur', () => computer.ReleaseKeys());
    canvas.addEventListener('dragover', e => e.preventDefault());
    canvas.addEventListener('drop', async e => {
        e.preventDefault();
        const file = e.dataTransfer.files[0];
        if (!file) return;
        load(file.name, new Uint8Array(await file.arrayBuffer()));
    });

    const sound = document.getElementById('sound');
    if (sound) {
        showSound();
        sound.addEventListener('click', toggleSound);
    }

    running = true;
    last = performance.now();
    requestAnimationFrame(tick);
    if (window.parent === window) canvas.focus();    // embedded, the page around keeps the keyboard until a click
    if (pendingLoad) {
        load(pendingLoad.name, pendingLoad.bytes);
        pendingLoad = null;
    }
}

export function stop() {
    running = false;
}

export function focus(elementId) {
    document.getElementById(elementId)?.focus();
}

// One animation frame: gamepad, then the board runs the cycles of the elapsed time
function tick(now) {
    if (!running) return;
    readGamepads();
    const speed = computer.Frame(now - last);
    last = now;
    if (speed > 0) showSpeed(speed);
    requestAnimationFrame(tick);
}

// Any gamepad moves the joystick: left stick or directional pad, buttons A/B/X/Y as fire
function readGamepads() {
    let bits = 0;
    for (const pad of navigator.getGamepads?.() ?? []) {
        if (!pad) continue;
        const x = pad.axes[0] ?? 0, y = pad.axes[1] ?? 0;
        const pressed = i => pad.buttons[i]?.pressed;
        if (y < -0.35 || pressed(12)) bits |= 0x01;
        if (y > 0.35 || pressed(13)) bits |= 0x02;
        if (x < -0.35 || pressed(14)) bits |= 0x04;
        if (x > 0.35 || pressed(15)) bits |= 0x08;
        if (pressed(0) || pressed(1) || pressed(2) || pressed(3)) bits |= 0x10;
    }
    if (bits !== gamepadBits) {
        gamepadBits = bits;
        computer.Gamepad(bits);
    }
}

// How many times faster than a real C64 the emulation could run, averaged over about a second
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

// Sound: browsers let an AudioContext play only after a user gesture, and one created before (or at a key that
// does not count as a gesture, like Esc) stays suspended. So every gesture tries again: the context is created once,
// then resumed until it runs. Muted and unmuted with the Sound button
let audioStarting = null;

async function startAudio() {
    if (!audioWanted || !computer) return;
    if (!audio) {
        audioStarting ??= (async () => {
            const context = new AudioContext();
            context.addEventListener('statechange', showSound);
            await context.audioWorklet.addModule(new URL('audio-worklet.js', import.meta.url));
            speaker = new AudioWorkletNode(context, 'thing64-speaker', { outputChannelCount: [2] });
            speaker.connect(context.destination);
            computer.EnableAudio(context.sampleRate);
            audio = context;
        })();
        await audioStarting;
    }
    // Not awaited: without a gesture the promise waits until there is one; 'statechange' updates the button
    if (audio.state === 'suspended') audio.resume().catch(() => { });
    showSound();
}

async function toggleSound() {
    audioWanted = !audioWanted;
    localStorage.setItem('thing64.sound', audioWanted ? 'on' : 'off');
    if (audioWanted) await startAudio();
    else await audio?.suspend();
    showSound();
    canvas.focus();
}

function showSound() {
    const sound = document.getElementById('sound');
    // "on" only when it really plays: until the browser allows it, the button says what to do
    if (sound) sound.textContent = !audioWanted ? 'Sound: off' : audio?.state === 'running' ? 'Sound: on' : 'Sound: click to start';
}

// 10 ms of 16-bit samples from the SID to the AudioWorklet, as floats
export function playAudio(view) {
    if (!speaker || audio.state !== 'running') return;
    const bytes = view.slice();
    const samples = new Int16Array(bytes.buffer, bytes.byteOffset, bytes.byteLength / 2);
    const floats = new Float32Array(samples.length);
    for (let i = 0; i < samples.length; i++) floats[i] = samples[i] / 32768;
    speaker.port.postMessage(floats, [floats.buffer]);
}

export function storedRom(name) {
    const text = localStorage.getItem('thing64.rom.' + name);
    if (!text) return null;
    return Uint8Array.from(atob(text), c => c.charCodeAt(0));
}

export function setting(name) {
    return localStorage.getItem('thing64.' + name);
}

export function setSetting(name, value) {
    localStorage.setItem('thing64.' + name, value);
}

export function download(name, content) {
    const link = document.createElement('a');
    link.href = URL.createObjectURL(new Blob([content], { type: 'application/octet-stream' }));
    link.download = name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), 1000);
}

export function storeRom(name, content) {
    let text = '';
    for (const byte of content) text += String.fromCharCode(byte);
    localStorage.setItem('thing64.rom.' + name, btoa(text));
}
