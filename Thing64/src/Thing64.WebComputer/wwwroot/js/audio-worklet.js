// The PC speakers of the browser C64: an AudioWorklet that plays the SID samples sent by computer.js.
// The emulation runs on the page's clock, the sound card on its own: the queue starts playing when it holds
// about 50 ms, and drops the oldest audio when it grows past 150 ms, so the latency stays low
class Speaker extends AudioWorkletProcessor {
    constructor() {
        super();
        this.queue = [];
        this.offset = 0;       // samples already played from queue[0]
        this.queued = 0;       // samples waiting
        this.playing = false;
        this.port.onmessage = event => {
            this.queue.push(event.data);
            this.queued += event.data.length;
            while (this.queued > sampleRate * 0.15 && this.queue.length > 1) {
                this.queued -= this.queue.shift().length - this.offset;
                this.offset = 0;
            }
        };
    }

    process(inputs, outputs) {
        const channels = outputs[0];
        const out = channels[0];
        if (!this.playing && this.queued >= sampleRate * 0.05) this.playing = true;
        for (let i = 0; i < out.length; i++) {
            if (!this.playing || this.queue.length === 0) {
                out[i] = 0;
                continue;
            }
            const block = this.queue[0];
            out[i] = block[this.offset++];
            this.queued--;
            if (this.offset >= block.length) {
                this.queue.shift();
                this.offset = 0;
            }
        }
        if (this.queue.length === 0) this.playing = false;   // ran dry: fill up again before playing
        for (let c = 1; c < channels.length; c++) channels[c].set(out);
        return true;
    }
}

registerProcessor('thing64-speaker', Speaker);
