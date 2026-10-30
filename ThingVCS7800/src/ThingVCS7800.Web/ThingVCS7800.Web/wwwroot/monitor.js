export class RetroMonitor {
    constructor(element, dotnet) {
        this.element = element; this.dotnet = dotnet;
        this.key = e => {
            if (!["ArrowUp","ArrowDown","ArrowLeft","ArrowRight","ControlLeft","ControlRight","ShiftLeft","ShiftRight"].includes(e.code)) return;
            e.preventDefault(); dotnet.invokeMethodAsync("Key", e.code, e.type === "keydown").catch(() => {});
        };
        this.blur = () => dotnet.invokeMethodAsync("Release").catch(() => {});
        element.addEventListener("keydown", this.key); element.addEventListener("keyup", this.key); element.addEventListener("blur", this.blur);
    }
    draw(bytes, width, height) {
        this.element.width = width; this.element.height = height;
        this.element.getContext("2d").putImageData(new ImageData(new Uint8ClampedArray(bytes), width, height), 0, 0);
    }
    download(name, bytes, type) {
        const url = URL.createObjectURL(new Blob([bytes], {type}));
        const link = document.createElement("a"); link.href = url; link.download = name; link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
    dispose() {
        this.element.removeEventListener("keydown", this.key); this.element.removeEventListener("keyup", this.key); this.element.removeEventListener("blur", this.blur);
    }
}
export function create(element, dotnet) { return new RetroMonitor(element, dotnet); }
