// The browser side of a project page (ProjectPage.razor): Monaco with one model per open file, the keys of the
// studio, and the C64: in the iframe of the C64 tab, or in a window of its own.
//   server -> page: init, open, show, close, text, reload, saved, markers, clearMarkers, reveal, run, focusComputer,
//                   openComputerWindow, closeComputerWindow, chatInput, scrollToEnd, dispose
//   page -> server: Command(save | build | run), Changed(path, dirty), ComputerStatus(text), ComputerWindowClosed()
'use strict';

window.thing64Studio = (() => {
    const MONACO = 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/';
    let editor = null;
    let page = null;                    // DotNetObjectReference of the page
    const models = new Map();           // path -> { model, saved (version id), dirty }
    const ready = new Set();            // the C64s (windows) that posted 'ready': they can receive programs
    let pendingRun = null;              // a program for a C64 that is not ready yet
    let captureTimer = 0;               // waiting for the C64 to answer a capture
    let memoryTimer = 0;                // ... or a request for its memory
    let computerWindow = null;          // the C64 in a window of its own
    let windowWatch = 0;

    function loadMonaco() {
        if (window.monaco) return Promise.resolve();
        // Monaco's web workers come from another origin: they are started through a data: URL that imports them
        window.MonacoEnvironment = {
            getWorkerUrl: () => 'data:text/javascript;charset=utf-8,' + encodeURIComponent(
                `self.MonacoEnvironment = { baseUrl: '${MONACO}' }; importScripts('${MONACO}vs/base/worker/workerMain.js');`)
        };
        require.config({ paths: { vs: MONACO + 'vs' } });
        return new Promise((resolve, reject) => require(['vs/editor/editor.main'], resolve, reject));
    }

    // F5, F7 and Ctrl+S anywhere in the page (not in the C64, which has its own document): captured before Monaco
    function keys(e) {
        const command = e.key === 'F5' ? 'run' : e.key === 'F7' ? 'build' : (e.ctrlKey || e.metaKey) && e.key === 's' ? 'save' : null;
        if (!command || !page) return;
        e.preventDefault();
        e.stopPropagation();
        page.invokeMethodAsync('Command', command);
    }

    function messages(e) {
        if (e.origin !== location.origin || !page) return;
        if (e.data?.type === 'ready') {
            ready.add(e.source);
            if (pendingRun && (pendingRun.target === e.source || pendingRun.target === null)) {
                e.source.postMessage(pendingRun.message, location.origin);
                pendingRun = null;
            }
        } else if (e.data?.type === 'status') {
            page.invokeMethodAsync('ComputerStatus', e.data.text);
        } else if (e.data?.type === 'capture') {
            clearTimeout(captureTimer);
            page.invokeMethodAsync('ComputerCaptured', e.data.bytes);
        } else if (e.data?.type === 'memory') {
            clearTimeout(memoryTimer);
            // The memory stays here, in the browser: the viewers draw from it, and the server only learns that there is a new snapshot
            const bytes = e.data.bytes;
            page.invokeMethodAsync('ComputerMemory', bytes?.length ? (window.thing64Mem.setSnapshot(bytes) ? bytes.length : -1) : 0);
        }
    }

    // Where programs go: the window of the C64 while it is open, otherwise the iframe of the tab
    function computerTarget() {
        if (computerWindow && !computerWindow.closed) return computerWindow;
        return document.getElementById('computer')?.contentWindow ?? null;
    }

    // An image of any format the browser can show becomes a PNG here (the converter reads PNG only), no larger than
    // 1280 pixels: plenty for a C64 screen, and small enough to travel to the server
    async function toPng(file) {
        const bitmap = await createImageBitmap(file);
        const scale = Math.min(1, 1280 / Math.max(bitmap.width, bitmap.height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(bitmap.width * scale);
        canvas.height = Math.round(bitmap.height * scale);
        canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        const png = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
        return new Uint8Array(await png.arrayBuffer());
    }

    async function importImage(e) {
        const file = e.target.files[0];
        e.target.value = '';
        if (!file || !page) return;
        await page.invokeMethodAsync('ImageImported', file.name, await toPng(file));
    }

    // Above each ";@ editor" line that opens a region (not its continuation lines, not ";@ end"), a link that
    // opens the region's visual editor
    // A file shown while the editor was hidden (another tab was active) gets no lenses: showing a file asks again
    let lensesRegistered = false;
    let lensesChanged = null;
    function registerRegionLenses() {
        if (lensesRegistered) return;
        lensesRegistered = true;
        lensesChanged = new monaco.Emitter();
        monaco.editor.registerCommand('thing64.openRegion', (_, path, line) => page?.invokeMethodAsync('OpenRegion', path, line));
        monaco.languages.registerCodeLensProvider('asm6502', {
            onDidChange: lensesChanged.event,
            provideCodeLenses(model) {
                const path = [...models].find(([, entry]) => entry.model === model)?.[0];
                const lenses = [];
                if (path) {
                    model.getLinesContent().forEach((text, i) => {
                        const open = /^\s*;@\s*(\w+)/.exec(text);
                        if (!open || open[1] === 'end' || (i > 0 && /^\s*;@/.test(model.getLineContent(i)))) return;
                        lenses.push({
                            range: new monaco.Range(i + 1, 1, i + 1, 1),
                            command: { id: 'thing64.openRegion', title: `✎ Edit ${open[1]}`, arguments: [path, i + 1] },
                        });
                    });
                }
                return { lenses, dispose() { } };
            },
        });
    }

    // Pictures to and from the clipboard, for the sprite editor: Ctrl+V anywhere in the page goes to the editor
    // that is visible (its .NET object registers itself), unless the clipboard has no picture
    const pasteTargets = new Map();
    document.addEventListener('paste', async e => {
        const target = [...pasteTargets.values()][0];
        const file = [...(e.clipboardData?.items ?? [])].find(item => item.type.startsWith('image/'))?.getAsFile();
        if (!target || !file) return;
        e.preventDefault();
        await target.invokeMethodAsync('PasteImage', await toPng(file));
    });

    // A picture as PNG bytes, no larger than maxSide pixels when given
    async function toPng(blob, maxSide) {
        const bitmap = await createImageBitmap(blob);
        const scale = maxSide ? Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height)) : 1;
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(bitmap.width * scale);
        canvas.height = Math.round(bitmap.height * scale);
        canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        const png = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
        return new Uint8Array(await png.arrayBuffer());
    }

    // Animation previews: an <img> showing its frames in turn
    const animations = new Map();

    function languageOf(path) {
        const extension = path.slice(path.lastIndexOf('.')).toLowerCase();
        return ['.asm', '.a', '.s', '.inc'].includes(extension) ? 'asm6502'
            : extension === '.json' ? 'json'
            : extension === '.md' ? 'markdown'
            : 'plaintext';
    }

    return {
        async init(elementId, dotnet, language) {
            page = dotnet;
            window.addEventListener('keydown', keys, true);
            window.addEventListener('message', messages);
            document.getElementById('import-image')?.addEventListener('change', importImage);
            await loadMonaco();
            registerAsm6502(language.mnemonics, language.directives, language.descriptions);
            registerRegionLenses();
            editor = monaco.editor.create(document.getElementById(elementId), {
                model: null,
                theme: 'thing64',
                fontFamily: 'Cascadia Mono, Consolas, monospace',
                fontSize: 14,
                tabSize: 4,
                insertSpaces: true,
                automaticLayout: true,
                minimap: { enabled: true },
                renderWhitespace: 'selection',
                mouseWheelZoom: true,
                scrollbar: { verticalScrollbarSize: 10, horizontalScrollbarSize: 10, useShadows: false },
            });
        },

        open(path, text) {
            if (models.has(path)) return;
            const model = monaco.editor.createModel(text, languageOf(path), monaco.Uri.parse('file:///' + encodeURI(path)));
            const entry = { model, saved: model.getAlternativeVersionId(), dirty: false, reloading: false };
            model.onDidChangeContent(() => {
                if (entry.reloading) return;
                const dirty = model.getAlternativeVersionId() !== entry.saved;
                if (dirty !== entry.dirty) {
                    entry.dirty = dirty;
                    page.invokeMethodAsync('Changed', path, dirty);
                }
            });
            models.set(path, entry);
        },

        show(path) {
            const entry = models.get(path);
            if (!entry) return;
            editor.setModel(entry.model);
            editor.focus();
            setTimeout(() => { editor?.layout(); lensesChanged?.fire(); }, 150);
        },

        close(path) {
            const entry = models.get(path);
            if (!entry) return;
            if (editor.getModel() === entry.model) editor.setModel(null);
            entry.model.dispose();
            models.delete(path);
        },

        text: path => models.get(path)?.model.getValue() ?? '',

        // The file changed on disk (RalfAI wrote it): the new text, as one edit that can be undone
        reload(path, text) {
            const entry = models.get(path);
            if (!entry || entry.model.getValue() === text) return;
            entry.reloading = true;
            entry.model.pushEditOperations([], [{ range: entry.model.getFullModelRange(), text }], () => null);
            entry.reloading = false;
            entry.saved = entry.model.getAlternativeVersionId();
            entry.dirty = false;
        },

        pasteTarget(id, dotnet) {
            if (dotnet) pasteTargets.set(id, dotnet); else pasteTargets.delete(id);
        },

        async pasteFromClipboard(dotnet) {
            try {
                for (const item of await navigator.clipboard.read()) {
                    const type = item.types.find(t => t.startsWith('image/'));
                    if (type) {
                        await dotnet.invokeMethodAsync('PasteImage', await toPng(await item.getType(type)));
                        return true;
                    }
                }
            } catch { }
            return false;
        },

        // Asks the C64 for the video memory; the answer comes as a 'capture' message. False if there is no C64 to ask
        captureComputer() {
            const target = computerTarget();
            if (!target || !ready.has(target)) return false;
            target.postMessage({ type: 'capture' }, location.origin);
            // A C64 published before this existed never answers
            clearTimeout(captureTimer);
            captureTimer = setTimeout(() => page?.invokeMethodAsync('ComputerStatus',
                'The C64 does not answer the capture: it was published before captures existed. Publish it again: dotnet publish src/Thing64.WebComputer -c Release -o artifacts/web'), 5000);
            return true;
        },

        // One or more images chosen from the disk for an image set: each arrives as a PNG (ImageChosen), then ImagesChosen says that was all.
        // "target" is the DotNetObjectReference of the editor of the set
        chooseImages(target) {
            const input = document.createElement('input');
            input.type = 'file';
            input.accept = 'image/*';
            input.multiple = true;
            input.addEventListener('change', async () => {
                for (const file of input.files) await target.invokeMethodAsync('ImageChosen', file.name, await toPng(file));
                await target.invokeMethodAsync('ImagesChosen');
            });
            input.click();
        },

        // Asks the C64 for all its memory (the answer comes as a 'memory' message). False if there is no C64 to ask
        memoryComputer() {
            const target = computerTarget();
            if (!target || !ready.has(target)) return false;
            target.postMessage({ type: 'memory' }, location.origin);
            clearTimeout(memoryTimer);
            memoryTimer = setTimeout(() => page?.invokeMethodAsync('ComputerStatus',
                'The C64 does not answer: it was published before the memory viewer existed. Publish it again: dotnet publish src/Thing64.WebComputer -c Release -o artifacts/web'), 5000);
            return true;
        },

        async copyText(text) {
            await navigator.clipboard.writeText(text);
        },

        downloadText(name, text) {
            const link = document.createElement('a');
            link.href = URL.createObjectURL(new Blob([text], { type: 'text/plain' }));
            link.download = name;
            link.click();
            setTimeout(() => URL.revokeObjectURL(link.href), 1000);
        },

        async copyImage(base64) {
            const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
            await navigator.clipboard.write([new ClipboardItem({ 'image/png': new Blob([bytes], { type: 'image/png' }) })]);
        },

        animate(elementId, frames, milliseconds) {
            clearInterval(animations.get(elementId));
            const image = document.getElementById(elementId);
            if (!image || frames.length === 0) return;
            let frame = 0;
            image.src = frames[0];
            animations.set(elementId, setInterval(() => {
                if (!document.body.contains(image)) { clearInterval(animations.get(elementId)); return; }
                frame = (frame + 1) % frames.length;
                image.src = frames[frame];
            }, milliseconds));
        },

        // The chat: Enter sends (a click on the send button), Shift+Enter is a new line
        chatInput(inputId, sendId) {
            document.getElementById(inputId)?.addEventListener('keydown', e => {
                if (e.key !== 'Enter' || e.shiftKey || e.isComposing) return;
                e.preventDefault();
                document.getElementById(sendId)?.click();
            });
        },

        // Attachments for RalfAI: files dropped on the chat, pictures pasted in its text (not passed on to the
        // sprite editor's paste). Pictures over 3 MB are made smaller first: a message to the server has a limit
        chatAttachments(panelId, inputId, dotnet) {
            const panel = document.getElementById(panelId), input = document.getElementById(inputId);
            if (!panel || !input) return;
            const send = async files => {
                for (const file of files) {
                    let bytes = new Uint8Array(await file.arrayBuffer());
                    let name = file.name || 'pasted.png';
                    if (bytes.length > 3_000_000) {
                        if (!file.type.startsWith('image/')) { alert(`${name} is too large to drop: use the Attach button`); continue; }
                        bytes = await toPng(file, 1600);
                        name = name.replace(/\.[^.]*$/, '') + '.png';
                    }
                    await dotnet.invokeMethodAsync('AttachDropped', name, bytes);
                }
            };
            panel.addEventListener('dragover', e => e.preventDefault());
            panel.addEventListener('drop', e => {
                e.preventDefault();
                send([...e.dataTransfer.files]);
            });
            input.addEventListener('paste', e => {
                const files = [...(e.clipboardData?.files ?? [])];
                if (files.length === 0) return;
                e.preventDefault();
                e.stopPropagation();
                send(files);
            });
        },

        scrollTo(elementId, top) {
            const element = document.getElementById(elementId);
            if (element) element.scrollTop = Math.max(0, top - element.clientHeight / 3);
        },

        scrollToEnd(elementId) {
            const element = document.getElementById(elementId);
            if (element) element.scrollTop = element.scrollHeight;
        },

        saved(path) {
            const entry = models.get(path);
            if (!entry) return;
            entry.saved = entry.model.getAlternativeVersionId();
            entry.dirty = false;
        },

        markers(path, markers) {
            const model = models.get(path)?.model;
            if (!model) return;
            monaco.editor.setModelMarkers(model, 'thing64', markers.map(marker => ({
                severity: monaco.MarkerSeverity.Error,
                message: marker.message,
                startLineNumber: marker.line,
                startColumn: model.getLineFirstNonWhitespaceColumn(marker.line) || 1,
                endLineNumber: marker.line,
                endColumn: model.getLineMaxColumn(marker.line),
            })));
        },

        clearMarkers() {
            for (const { model } of models.values()) monaco.editor.setModelMarkers(model, 'thing64', []);
        },

        reveal(path, line) {
            if (editor.getModel() !== models.get(path)?.model) return;
            editor.revealLineInCenter(line);
            editor.setPosition({ lineNumber: line, column: 1 });
            editor.focus();
        },

        // A program to the C64; it waits for the C64 to be ready, then the C64 has the keyboard
        run(name, bytes) {
            const target = computerTarget();
            const message = { type: 'load', name, bytes };
            // No target yet: the C64 is being turned on, the first one to say 'ready' gets the program
            if (!target) pendingRun = { target: null, message };
            else if (ready.has(target)) target.postMessage(message, location.origin);
            else pendingRun = { target, message };
            thing64Studio.focusComputer();
        },

        // A moment later: the tab of the C64 may become visible only with the next render from the server
        focusComputer() {
            setTimeout(() => {
                const target = computerTarget();
                if (!target) return;
                if (target === computerWindow) computerWindow.focus();
                else document.getElementById('computer')?.focus();
                target.postMessage({ type: 'focus' }, location.origin);
            }, 100);
        },

        closeComputerWindow() {
            clearInterval(windowWatch);
            computerWindow?.close();
            computerWindow = null;
        },

        // The C64 in a window of its own (the page then removes the iframe); when the window closes, the page
        // brings the C64 back into the tab
        openComputerWindow() {
            computerWindow = window.open('computer/', 'thing64-computer', 'popup,width=900,height=760');
            if (!computerWindow) return false;
            clearInterval(windowWatch);
            windowWatch = setInterval(() => {
                if (!computerWindow.closed) return;
                clearInterval(windowWatch);
                ready.delete(computerWindow);
                computerWindow = null;
                page?.invokeMethodAsync('ComputerWindowClosed');
            }, 500);
            return true;
        },

        dispose() {
            window.removeEventListener('keydown', keys, true);
            window.removeEventListener('message', messages);
            for (const { model } of models.values()) model.dispose();
            models.clear();
            editor?.dispose();
            editor = null;
            page = null;
            ready.clear();
            clearInterval(windowWatch);
            computerWindow?.close();
            computerWindow = null;
        },
    };
})();
