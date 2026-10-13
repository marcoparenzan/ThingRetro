// The 6502 assembly language for Monaco: colors (Monarch), hover help, go to definition, completion, and the
// 'thing64' theme. Shared by the desktop studio (WebView2) and the web studio; both call
//   registerAsm6502(mnemonics, directives, descriptions)
// with the lists of the assembler (Assembler.Mnemonics, Assembler.Directives, InstructionSet.Descriptions)
'use strict';

// Labels ("name:", or ACME style "name" in column 0) and constants ("NAME = value") defined in the source
let mnemonicSet = new Set();
let asm6502Registered = false;

function definitions(model) {
    const found = new Map();
    const lines = model.getLinesContent();
    lines.forEach((line, index) => {
        const code = line.replace(/;.*$/, '');
        const constant = /^\s*([A-Za-z_.@]\w*)\s*=(?!=)/.exec(code);
        let label = /^\s*([A-Za-z_.@]\w*):/.exec(code);
        if (!label && !constant) {
            const bare = /^([A-Za-z_.@]\w*)(?=\s|$)/.exec(code);
            if (bare && !mnemonicSet.has(bare[1].toUpperCase())) label = bare;
        }
        const name = constant ? constant[1] : label ? label[1] : null;
        if (name && !found.has(name)) {
            found.set(name, { line: index + 1, column: code.indexOf(name) + 1, kind: label ? 'label' : 'constant', text: line.trim() });
        }
    });
    return found;
}

function registerAsm6502(mnemonics, directives, descriptions) {
    if (asm6502Registered) return;
    asm6502Registered = true;
    mnemonicSet = new Set(mnemonics);
    monaco.languages.register({ id: 'asm6502', extensions: ['.asm'] });

    // Mnemonics are accepted in any case; symbols are case-sensitive (CONSTANTS in upper case by convention)
    const mnemonicCases = mnemonics.concat(mnemonics.map(m => m.toLowerCase()));
    monaco.languages.setMonarchTokensProvider('asm6502', {
        mnemonics: mnemonicCases,
        directives: directives,
        tokenizer: {
            root: [
                [/;@.*$/, 'region'],                                                                  // ;@ editor regions
                [/;.*$/, 'comment'],
                [/^\s*[A-Za-z_.@]\w*(?=:)/, 'label'],
                [/^[A-Za-z_.@]\w*/, { cases: { '@mnemonics': 'mnemonic', '@default': 'label' } }],   // ACME: labels without colon
                [/^[-+]+(?=\s|$)/, 'label'],                                                          // anonymous labels
                [/[.!][A-Za-z0-9]+/, { cases: { '@directives': 'directive', '@default': 'identifier' } }],
                [/\+[A-Za-z_]\w*/, 'directive'],                                                      // macro call
                [/(,)(\s*)([XYxy])\b/, ['delimiter', '', 'register']],
                [/[A-Za-z_]\w*/, {
                    cases: {
                        '@mnemonics': 'mnemonic',
                        '~[A-Z][A-Z0-9_]*': 'constant',
                        '@default': 'identifier'
                    }
                }],
                [/\$[0-9A-Fa-f]+/, 'number.hex'],
                [/%[01]+/, 'number.binary'],
                [/\d+/, 'number'],
                [/'.'/, 'string'],
                [/"[^"]*$/, 'string.invalid'],
                [/"[^"]*"/, 'string'],
                [/#/, 'immediate'],
                [/[<>]/, 'operator'],
                [/[=+\-*\/()]/, 'operator'],
                [/[,:]/, 'delimiter'],
            ]
        }
    });

    monaco.languages.setLanguageConfiguration('asm6502', {
        comments: { lineComment: ';' },
        brackets: [['(', ')']],
        autoClosingPairs: [{ open: '(', close: ')' }, { open: '"', close: '"' }],
        wordPattern: /([.!@]?[A-Za-z_]\w*)|(\$[0-9A-Fa-f]+)/,
    });

    monaco.editor.defineTheme('thing64', {
        base: 'vs-dark',
        inherit: true,
        rules: [
            { token: 'comment', foreground: '6A9955', fontStyle: 'italic' },
            { token: 'region', foreground: '7C8AF7', fontStyle: 'bold' },
            { token: 'mnemonic', foreground: '569CD6', fontStyle: 'bold' },
            { token: 'directive', foreground: 'C586C0' },
            { token: 'label', foreground: 'DCDCAA' },
            { token: 'constant', foreground: '4FC1FF' },
            { token: 'identifier', foreground: '9CDCFE' },
            { token: 'register', foreground: 'D7BA7D' },
            { token: 'number', foreground: 'B5CEA8' },
            { token: 'number.hex', foreground: 'B5CEA8' },
            { token: 'number.binary', foreground: 'B5CEA8' },
            { token: 'string', foreground: 'CE9178' },
            { token: 'immediate', foreground: 'D16969' },
            { token: 'operator', foreground: 'D4D4D4' },
            { token: 'invalid', foreground: 'F44747', fontStyle: 'underline' },
        ],
        colors: {                               // the night blue of the logo
            'editor.background': '#05083a',
            'editor.lineHighlightBackground': '#0d1257',
            'editorLineNumber.foreground': '#4a528f',
            'editorLineNumber.activeForeground': '#7c8af7',
            'editor.selectionBackground': '#2b3590',
            'editorCursor.foreground': '#7c8af7',
            'minimap.background': '#05083a',
        },
    });

    // Hover: what an instruction does, or the line where a symbol is defined
    monaco.languages.registerHoverProvider('asm6502', {
        provideHover(model, position) {
            const word = model.getWordAtPosition(position);
            if (!word) return null;
            const text = word.word;
            const range = new monaco.Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn);
            const description = descriptions[text.toUpperCase()];
            if (description) return { range, contents: [{ value: '**' + text.toUpperCase() + '**' }, { value: description }] };
            const definition = definitions(model).get(text);
            if (definition) return { range, contents: [{ value: '```asm\n' + definition.text + '\n```' }, { value: `${definition.kind}, line ${definition.line}` }] };
            return null;
        }
    });

    // F12 / Ctrl+click on a symbol: where it is defined
    monaco.languages.registerDefinitionProvider('asm6502', {
        provideDefinition(model, position) {
            const word = model.getWordAtPosition(position);
            const definition = word && definitions(model).get(word.word);
            if (!definition) return null;
            return { uri: model.uri, range: new monaco.Range(definition.line, definition.column, definition.line, definition.column + word.word.length) };
        }
    });

    // Completion: instructions (with their description), directives, symbols of the source
    monaco.languages.registerCompletionItemProvider('asm6502', {
        triggerCharacters: ['.', '!'],
        provideCompletionItems(model, position) {
            const word = model.getWordUntilPosition(position);
            const range = new monaco.Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn);
            // A lone "." is not a word yet: the directive replaces it
            const line = model.getLineContent(position.lineNumber);
            const dotRange = word.word === '' && '.!'.includes(line[position.column - 2])
                ? new monaco.Range(position.lineNumber, position.column - 1, position.lineNumber, position.column)
                : range;
            const kinds = monaco.languages.CompletionItemKind;
            const suggestions = mnemonics.map(m => ({ label: m, kind: kinds.Keyword, insertText: m, detail: descriptions[m] || '', range }))
                .concat(directives.map(d => ({ label: d, kind: kinds.Module, insertText: d, range: dotRange })))
                .concat([...definitions(model)].map(([name, d]) => ({
                    label: name, kind: d.kind === 'label' ? kinds.Function : kinds.Constant, insertText: name, detail: d.text, range
                })));
            return { suggestions };
        }
    });
}
