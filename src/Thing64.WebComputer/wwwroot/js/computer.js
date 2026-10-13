// The browser side of the C64: the animation loop that drives the board, the canvas that shows its frames,
// keyboard, gamepad, sound, drag and drop, and the ROMs kept in localStorage.
// Talks to WebComputer.cs (JSExport/JSImport)

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
    canvas.addEventListener('pointerdown', () => startAudio());
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

    const sound = document.getElementById('sound');
    if (sound) {
        showSound();
        sound.addEventListener('click', toggleSound);
    }

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

// Sound: started by a user gesture on the screen, muted and unmuted with the Sound button
async function startAudio() {
    if (audio || !audioWanted) return;
    audio = new AudioContext();
    await audio.audioWorklet.addModule(new URL('audio-worklet.js', import.meta.url));
    speaker = new AudioWorkletNode(audio, 'thing64-speaker', { outputChannelCount: [2] });
    speaker.connect(audio.destination);
    computer.EnableAudio(audio.sampleRate);
    showSound();
}

async function toggleSound() {
    audioWanted = !audioWanted;
    localStorage.setItem('thing64.sound', audioWanted ? 'on' : 'off');
    if (audio) {
        if (audioWanted) await audio.resume(); else await audio.suspend();
    } else if (audioWanted) {
        await startAudio();
    }
    showSound();
    canvas.focus();
}

function showSound() {
    const sound = document.getElementById('sound');
    if (sound) sound.textContent = audioWanted ? 'Sound: on' : 'Sound: off';
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

export function storeRom(name, content) {
    let text = '';
    for (const byte of content) text += String.fromCharCode(byte);
    localStorage.setItem('thing64.rom.' + name, btoa(text));
}
