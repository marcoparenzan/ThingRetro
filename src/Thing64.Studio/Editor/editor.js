// Monaco hosted in WebView2. The page talks to AsmEditorForm with messages:
//   page -> host: ready, changed {text}, command {name: save | assemble | run | close}
//   host -> page: init {text, language, mnemonics, directives, descriptions}, markers {markers: [{line, message}]},
//                 reveal {line}, focus
// The 6502 language (colors, hover help, completion, go to definition) is asm6502.js, shared with the web studio,
// built from the lists the host sends: they come from the assembler itself, so the editor and the assembler cannot
// disagree on what an instruction is
'use strict';

const MONACO = 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/';
const host = window.chrome && window.chrome.webview;
const post = message => host && host.postMessage(message);

let editor = null;
let loading = false;   // setValue from the host is not a user change

function showMessage(text) {
    document.getElementById('editor').style.display = 'none';
    const box = document.getElementById('message');
    box.style.display = 'block';
    box.textContent = text;
}

if (typeof require === 'undefined') {
    showMessage('The editor could not be loaded: Monaco comes from cdn.jsdelivr.net and needs an internet connection the first time.');
} else {
    // Monaco's web workers come from another origin: they are started through a data: URL that imports them
    window.MonacoEnvironment = {
        getWorkerUrl: () => 'data:text/javascript;charset=utf-8,' + encodeURIComponent(
            `self.MonacoEnvironment = { baseUrl: '${MONACO}' }; importScripts('${MONACO}vs/base/worker/workerMain.js');`)
    };
    require.config({ paths: { vs: MONACO + 'vs' } });
    require.onError = error => showMessage('The editor could not be loaded: ' + error);
    require(['vs/editor/editor.main'], () => post({ type: 'ready' }));
}

host && host.addEventListener('message', event => {
    const message = event.data;
    switch (message.type) {
        case 'init': init(message); break;
        case 'markers': setMarkers(message.markers); break;
        case 'reveal':
            editor.revealLineInCenter(message.line);
            editor.setPosition({ lineNumber: message.line, column: 1 });
            editor.focus();
            break;
        case 'focus': editor && editor.focus(); break;
    }
});

function init(message) {
    registerAsm6502(message.mnemonics, message.directives, message.descriptions);

    editor = monaco.editor.create(document.getElementById('editor'), {
        value: '',
        language: message.language,
        theme: 'thing64',
        fontFamily: 'Cascadia Mono, Consolas, monospace',
        fontSize: 14,
        tabSize: 4,
        insertSpaces: true,
        automaticLayout: true,
        minimap: { enabled: true },
        renderWhitespace: 'selection',
        rulers: [],
        mouseWheelZoom: true,
    });

    loading = true;
    editor.setValue(message.text);
    loading = false;

    editor.onDidChangeModelContent(() => {
        if (!loading) post({ type: 'changed', text: editor.getValue() });
    });

    // Keys handled by the host (WebView2 keeps the keyboard while the editor has the focus)
    const command = name => () => post({ type: 'command', name });
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, command('save'));
    editor.addCommand(monaco.KeyCode.F7, command('assemble'));
    editor.addCommand(monaco.KeyCode.F5, command('run'));
    editor.addCommand(monaco.KeyMod.Shift | monaco.KeyCode.F5, command('runwindow'));
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyW, command('close'));
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.F4, command('close'));
    editor.focus();
}

function setMarkers(markers) {
    const model = editor.getModel();
    monaco.editor.setModelMarkers(model, 'thing64', markers.map(marker => ({
        severity: monaco.MarkerSeverity.Error,
        message: marker.message,
        startLineNumber: marker.line,
        startColumn: model.getLineFirstNonWhitespaceColumn(marker.line) || 1,
        endLineNumber: marker.line,
        endColumn: model.getLineMaxColumn(marker.line),
    })));
}
