# The C64 in WebAssembly: build, deployment and use

`src/Thing65.WebComputer` is the C64 that runs in the browser. It is a **static site** that you publish by hand; the
web studio only serves it. This page says how it is built, where it ends up, how the studio uses it and what to do
when something does not reach the browser.

## What it is

A Blazor WebAssembly project (`net10.0`) with the same board and chips as the desktop app (`Thing65.C65`,
`Thing65.Crt`, `Thing65.Prg`). The browser has no thread for the emulation: at every animation frame
`wwwroot/js/computer.js` calls `WebComputer.Frame` with the elapsed time, the board runs the matching cycles and the
frame is painted on a canvas; sound goes through an AudioWorklet.

| Build | Code | Speed (Edge, development PC) | ROMs |
| --- | --- | --- | --- |
| Debug | interpreted | about 0.18 times a real C64 | embedded in the assembly (development only) |
| Release | **ahead-of-time compiled** to WebAssembly (`RunAOTCompilation`) | about 2.9 times a real C64 | never included |

Only Release is fast enough for a cycle-exact C64. The ROMs are copyrighted: the published site does not contain
them.

## Publishing

```powershell
dotnet workload install wasm-tools-net10        # once per machine (AOT needs it: error NETSDK1147 otherwise)
dotnet publish src/Thing65.WebComputer -c Release -o artifacts/web
```

AOT takes a few minutes. The result:

```
artifacts/web/
  wwwroot/                     the site
    index.html                 <base href="./">: it works under any path
    css/  js/  img/
    _framework/                about 100 MB: .wasm files with a hash in their names, each also as .br and .gz
  dotnet.js  web.config  Thing65.WebComputer.runtimeconfig.json   (not part of the site)
```

Notes:

- `dotnet publish -o` **does not delete** what an earlier publish left: old `.wasm` files with other hashes stay in
  `_framework`. They are never loaded (the boot manifest names the current ones); delete them if you want.
- `-p:RunAOTCompilation=false` publishes without the workload, in a minute: useful to try something, far too slow to
  play (the interpreter). Publish it to another folder, not over `artifacts/web`.
- `artifacts/` is outside git, like the packages of the command line tool.

## How the studio serves it

The web studio (Blazor Server) never builds the C64. `Program.cs` reads `Studio:ComputerFolder` (default
`../../artifacts/web/wwwroot`, relative to `src/Thing65.WebStudio`; `appsettings.json` or the environment variable
`Studio__ComputerFolder`) **when it starts**:

- the folder exists: it is served at `/computer/` with static files, the same origin as the studio;
- it does not: a warning is logged and the C64 tab explains how to publish it.

So after the first publish, restart the studio. The files are served with `Cache-Control: no-cache`: the browser asks
again every time (and gets a `304` when nothing changed), because `computer.js` and the boot manifest have no hash in
their names and a cached copy would hide a newly published C64.

Same origin matters twice: the studio and the C64 talk with `postMessage`, and they share the `localStorage` where
the ROMs are kept.

## How the studio talks to it

The studio shows the C64 in an iframe (`computer/?embedded`: only the screen and its controls) or in a window of its
own. Messages, all between pages of the same origin:

| Direction | Message | Meaning |
| --- | --- | --- |
| C64 → studio | `{ type: 'ready' }` | the page is loaded and can receive |
| C64 → studio | `{ type: 'status', text }` | after each load |
| studio → C64 | `{ type: 'load', name, bytes }` | a `.prg`/`.crt`; it waits for the KERNAL to reach `READY.`, then runs it |
| studio → C64 | `{ type: 'focus' }` | the keyboard goes to the C64 |
| studio → C64 | `{ type: 'capture' }` | send the video memory |
| C64 → studio | `{ type: 'capture', bytes }` | the 17,472 bytes of `Board.CaptureVideo`: the 16 KB of the VIC-II's bank (the character ROM where the chip reads it), the Color RAM, the registers |
| studio → C64 | `{ type: 'memory' }` | send all the memory |
| C64 → studio | `{ type: 'memory', bytes }` | the 70,722 bytes of `Board.CaptureMemory`: the 64 KB of RAM (also under the ROMs and the I/O), Color RAM, the VIC-II registers, the 4 KB of character ROM, the VIC-II bank and the CPU port |

`WebComputer.cs` exposes the C# side to JavaScript with `[JSExport]` (`Frame`, `Load`, `KeyDown`, `CaptureVideo`...)
and `computer.js` calls it.

## The ROMs

The first time, the page asks for the three files (`kernal`, `basic`, `chargen`) and keeps them in the browser's
`localStorage` as `thing65.rom.kernal`, `thing65.rom.basic` and `thing65.rom.chargen`. The studio's page reads the
same ones. Clearing the site data makes the page ask again.

## When to publish again

Whenever something the C64 is made of changes: the chips, the board (`Thing65.C65`), `WebComputer.cs`,
`computer.js`, the keyboard layout. Work on the studio itself (editors, panels, assembler) does not need it.

Typical case: a feature adds a message (the capture and the memory viewer did). Until the C64 is published again it ignores the new message
and the studio waits for an answer that never comes; the capture button says so after five seconds.

## When something does not reach the browser

The capture shows *"The C64 does not answer the capture: it was published before captures existed"*, or the C64 does
not do what the last change says:

1. **Is it published?** The served file must have the change:
   `curl http://localhost:5065/computer/js/computer.js | findstr capture` (for the capture) must find lines. If not,
   publish again (see above) and restart the studio.
2. **Is the browser using the old one?** Reload the page with **Ctrl+F5** (or clear the site data of the studio). A
   browser may have kept `computer.js` or the C64 from before the `no-cache` header existed, and only a hard reload
   replaces them. Turn the C64 off and on (the ✕ and the tab) so that the iframe loads again.
3. **Is the C64 running?** The capture and the programs go to a C64 that has said `ready`: turn it on (F5 or the tab)
   and, for a capture, run the program first. In *Own window* mode the window must still be open.
4. **Is it the right folder?** `Studio:ComputerFolder` may point somewhere else than where you published: the log of
   the studio at start says which folder it serves.
5. **The publish fails with NETSDK1147**: the `wasm-tools-net10` workload is not installed for the SDK in use
   (`dotnet --version`, `dotnet workload list`).

The browser console (F12) shows errors of the page; a `404` for
`_framework/dotnet-browser-tools/hot-reload-settings.json` is harmless (the development tools ask for it).

## The same site anywhere else

`artifacts/web/wwwroot` is a plain static site: any web server can serve it (it needs the `application/wasm` type for
`.wasm` and, for the compressed files, the usual `Content-Encoding`). Without the studio it is only the C64: drag a
`.prg` or `.crt` on the screen, or use the *Load* button; the ROMs are asked for, as above.
