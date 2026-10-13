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
        }
    }

    // Where programs go: the window of the C64 while it is open, otherwise the iframe of the tab
    function computerTarget() {
        if (computerWindow && !computerWindow.closed) return computerWindow;
        return document.getElementById('computer')?.contentWindow ?? null;
    }

    // An image of any format the browser can show becomes a PNG here (the converter reads PNG only), no larger than
    // 1280 pixels: plenty for a C64 screen, and small enough to travel to the server
    async function importImage(e) {
        const file = e.target.files[0];
        e.target.value = '';
        if (!file || !page) return;
        const bitmap = await createImageBitmap(file);
        const scale = Math.min(1, 1280 / Math.max(bitmap.width, bitmap.height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(bitmap.width * scale);
        canvas.height = Math.round(bitmap.height * scale);
        canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        const png = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
        await page.invokeMethodAsync('ImageImported', file.name, new Uint8Array(await png.arrayBuffer()));
    }

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

        // The chat: Enter sends (a click on the send button), Shift+Enter is a new line
        chatInput(inputId, sendId) {
            document.getElementById(inputId)?.addEventListener('keydown', e => {
                if (e.key !== 'Enter' || e.shiftKey || e.isComposing) return;
                e.preventDefault();
                document.getElementById(sendId)?.click();
            });
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
