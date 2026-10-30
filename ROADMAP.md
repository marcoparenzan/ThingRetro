# Atari / common peripherals roadmap

## Implemented in this increment
- ThingVCS logical cartridge connector and immutable raw linear ROM.
- ThingVCS7800.A7800 board with removable CPU socket, cartridge connector, RAM and development CPU execution.
- Shared ThingRetroEmu video/audio ports; NES video adapter and frame presentation.

## Next
- Implemented functional RIOT 6532 ports, RAM, interval timer and IRQ in ThingVCS; removable board socket, register decode and CPU IRQ wiring.
- Implemented shared DE-9 control ports, RIOT direction-input wiring and shared keyboard/USB selector in the unchanged Computer window organization. Analog/Commodore mouse choices are disabled for Atari only; existing Thing64 defaults are unchanged.
- Implemented partial TIA digital input chip in its own socket: single-button INPT4/5, VBLANK D6 latch, D7-only bus output and reset/clock wiring. Synthetic coverage only.
- Implemented clock-sampled RIOT PA7 positive/negative edge detection, address-selected IRQ enable, independent edge/timer acknowledgements and board IRQ integration. Synthetic regression coverage; no sub-cycle race validation.
- Pending full board mirrors, diagnostic validation, native two-button/analog inputs and manual frontend/controller verification.
- Implemented first MARIA register/raster stage in a removable socket: BACKGRND output, MSTAT vblank, DPP/CTRL latches, WSYNC-to-RDY and shared video presentation. CPU-driven backdrop test ROM is available in the Computer Cartridge menu.
- Implemented functional normal-header direct 160A object traversal, DLL zones and line RAM with transparency/palette lookup; cartridge-backed CPU demo in Computer. Up to 64 objects per line; unsupported modes are not reinterpreted as 160A.
- Implemented extended headers, indirect 160A map/character fetches, CHARBASE and CWIDTH, with a CPU-driven indirect test cartridge in Computer.
- Implemented 160B write-mode cell composition, palette selection and persistent extended-header write-mode latch; CPU-driven 160B demo in Computer.
- Implemented DLL holey-DMA masks for direct and indirect graphics, CWIDTH-pair suppression and diagnostic counters, with synthetic bus-read tests.
- Implemented functional zone-load DLI pulses and MARIA NMI pin connected through the board CPU socket, including reset/disconnection tests and handler execution in a flat-memory test harness.
- Implemented low RAM/stack aliases backed by canonical main RAM, with board-level push/pull, JSR/RTS and repeated DLI/RTI tests. DLI demo is now available in Computer.
- Implemented 320 read-mode half-cell palette decoding, combined 320A/B/C/D functional coverage and a CPU-driven 320C demo in Computer.
- Implemented Sally HALT adapter using the core's bus-disable gate while retaining NMI sampling; Computer installs it by default. Board connects HALT to MARIA DmaBusy/WSYNC. Estimated startup/header/data budget creates a bounded busy window; synthetic HALT/NMI tests exist.
- Implemented bounded 426-clock functional DMA traversal with pre-fetch cost checks, CWIDTH-pair budgeting and HALT/WSYNC until next line on saturation; overloaded direct/indirect lists have bus-read regressions.
- Implemented functional 4/6-master-clock CPU slots for decoded TIA/RIOT accesses, separate master-clock/slow-access counters and Computer pacing based on elapsed clocks. Bus accesses from MARIA or direct inspection do not charge slow CPU slots.
- Pending full low I/O mirroring/deep-stack cases, exact DLI timing/width, interleaved cycle-exact DMA fetches, precise visible timing/palette and SALLY phase-accurate clocks/electrical handoff. Fixed 4:1 clock ratio and backdrop-only output are provisional, not game rendering.
- Hardware documentation validation and TIA audio/video remain pending.
- Implemented conservative A78 legacy v1-v3 parser and Computer loading/title integration for linear NTSC images; unsupported metadata is rejected. Reference: a7800 cartridge loader https://github.com/7800-devtools/a7800/blob/master/src/devices/bus/a7800/a78_slot.cpp. Coverage uses synthetic headers, not externally validated games.
- Pending A78 v4, board-specific decoding, BIOS/control latch and cartridge mappers.
- Common monitor and speaker wiring in NES desktop and Atari Computer frontend.
- Implemented: ThingVCS7800.Computer WinForms shell using Thing64's shared monitor/video components and window organization; cartridge loading, pause/reset and bounded development CPU pacing.
- Next for Computer: real Atari chips, video/audio and controller wiring; manual Windows UI verification.
- ThingVCS7800.WebStudio: shared 6502 assembler, cartridge build/export, debugger, source mapping and project workspace after the core is usable.
- Diagnostic ROM validation before game compatibility claims.

## Allineamento ThingNES.Computer

- Rinominato Desktop in Computer sotto ThingNES/src e nelle soluzioni. ComputerForm con MonitorControl/VideoMenu comuni, menu Peripherals/Video e contestuale, senza toolbar/status/help personalizzati.
- Video e audio via contratti comuni; registrazione/ascolto simultanei con AudioSplitter/WaveRecorder/PcSpeakers. PCM APU adattato ad AudioPort, CLI WAV invariato. 97 test NES superati, inclusi tre nuovi test di cablaggio.
- Verifica manuale Windows ancora da fare; nessun cambio ai mapper/CPU/PPU o nuova certificazione giochi. NesMachine resta API esistente, zoccoli chip NES e controller comuni nativi sono futuri; nessun WebComputer/Studio NES aggiunto in questa fase.

## WebComputer e WebStudio — base avviata

- Due host Blazor Server, RCL condivisa e test sotto ThingVCS7800/src/, aggiunti alla soluzione ThingRetro.slnx: sessioni isolate, caricamento cartucce/dimostrazione, monitor RGB via cavi comuni, joystick browser, step/reset e run limitato a 10 frame/s.
- Studio con sorgente 6502 singolo, template, build reale ThingMOS, cartuccia lineare 16 KiB/A78 v3, download sorgente e ultima build, prova sullo stesso monitor. Non ancora uno Studio equivalente a Thing64 completo.
- Da completare: persistenza/import progetti, debugger/source mapping, editor grafici, audio browser, pacing/trasporto più efficiente, menu video/BIOS/periferiche completi e validazione manuale browser. App locali senza autenticazione; non pubblicate.

## BIOS / control lock / 2600

- BIOS ROM rimovibile 4/16 KiB, overlay delle letture in alto e installazione/rimozione dalla form esistente. Nessun firmware Atari distribuito. Avvio diretto rimane default; HardwareBoot abilita BIOS e reset del latch.
- INPTCTRL funzionale D0 lock, D2 BIOS disable, D1 selezione 7800/2600; alias delle scritture TIA prima del lock. Test CPU BIOS sintetico → trampoline RAM → cartuccia, reset/relock e validazione senza sostituzione su errore.
- Base 2600: bus 13 bit, RAM/registri RIOT specchiati, CPU 6 master clock, MARIA esclusa, cartucce lineari 2/4 KiB via API esplicita. NON giochi 2600: mancano video/collisioni/WSYNC TIA, bank switching, timing TV e profilo PAL 2600. Altri bit controllo/avvio/autenticazione BIOS reali da validare.

## POKEY periferiche e oscillatore indipendente

- Pin logici opzionali POT, matrice tastiera/modificatori/Break, seriale con clock esterno e uscita. POTGO/ALLPOT con conversione lenta/rapida, KBCODE e debounce funzionale, SERIN/SEROUT 8N1 ed errori/IRQ, SKREST e SKCTL break/reset.
- STIMER ritardato quattro clock senza cancellare IRQ pendenti. Oscillatore cartuccia nominale configurabile con accumulatore razionale guidato dai master clock, indipendente da PAL e slot CPU lenti. Dieci nuove regressioni, 339 test Atari superati.
- Ancora aperti: seriale con clock interno/asynchronous/two-tone (`UnsupportedSkctlModes`), rollover tastiera e circuito analogico POT, cablaggio esterno delle periferiche, validazione elettrica delle fasi timer/IRQ, calibrazione oscillatori reali, TIA completa a due fasi e risposta analogica. Questo incremento NON chiude tutto l'obiettivo né certifica POKEY completo.

## Accuratezza audio TIA / POKEY

- TIA: confronto AUDF e wrap a 31 su cambio frequenza in corso; divisore per tre separato; AUDC $0B costante; regressioni dei periodi polinomiali 15/31/511.
- POKEY: rumore 4/5/9/17 bit e gating, RANDOM, AUDCTL clock 28/114 e fast +4, concatenazione 16 bit (+7 fast), filtri CH3/4, STIMER funzionale e timer IRQ 1/2/4 attivi bassi. IRQ esposto sul chip opzionale, non collegato alla CPU della scheda in assenza di un cablaggio documentato.
- 15 nuovi casi, totale 329 test Atari. Da completare: seriale, tastiera, POT, SKCTL esteso, fasi timer/IRQ/STIMER misurate, oscillatore cartuccia indipendente, TIA a due fasi e confronto analogico/hardware. Non POKEY completo, non ciclo-esatto.

## Controller nativi a due pulsanti

- Collegato ITwoButtonJoystick comune alla TIA: INPT0/1 = P1 fire2/fire1, INPT2/3 = P2 fire2/fire1; D7 attivo alto, bit bassi open bus. Modalità controllata da RIOT PB2/PB4 e DDR, fallback a un pulsante con fire combinati.
- Computer abilita l'opzione twoButtons già presente nei simulatori comuni; nessun nuovo pin universale, nessuna toolbar. Otto nuovi casi sintetici, totale 314 test Atari e 50 comuni superati. Tastiera/USB e giochi da validare manualmente; LightGun e ingressi analogici ancora da collegare.

## Diagnostica audio e registratore comune

- ROM 6502 TIA tone/noise disponibile nel menu Cartridge esistente e via CLI `CartridgeInspector --tia-audio-diagnostic`. Esportati WAV NTSC/PAL da un secondo e risultato JSON; entrambi 44100 campioni, registri programmati dalla CPU verificati.
- `ThingRetroEmu.Audio.WaveRecorder` implementa ISpeaker e si collega tramite AudioCable comune: PCM16 mono, header RIFF finalizzato su Dispose, validazione stream/rate e limite dimensioni RIFF. Copertura xUnit nella suite Atari anche per il componente comune. Integrato come registrazione arbitraria nella form tramite `AudioSplitter` comune, senza sostituire implicitamente il writer NES. Menu Peripherals/contestuale Audio recording; ascolto e WAV simultanei, registrazione disponibile anche senza dispositivo audio Windows. Stop/cambio cartuccia/reset/cambio standard/chiusura finalizzano il file; pausa non aggiunge campioni. UI da verificare manualmente.
- Nessun ascolto manuale o confronto con hardware: è verifica sintetica del percorso completo. AudioPort consegna blocchi da 10 ms, coda parziale non esportata a durate arbitrarie.

## Audio TIA funzionale

- Due canali nello zoccolo TIA esistente; registri AUDC/AUDF/AUDV, volume-only, toni puri/divisi e rumore polinomiale/gated funzionale. Clock master/228 indipendente da HALT e raster MARIA. Campioni verso AudioPort comune ogni quattro master clock, con miscela TIA/POKEY e headroom fisso.
- Da validare/completare: fasi audio e polinomi hardware, transizioni dei registri, risposta analogica, guadagni e confronto con registrazioni/diagnostiche esterne. Audio TIA implementato funzionalmente, non ciclo-esatto; video TIA resta assente. Ascolto Windows non ancora verificato.

## Decodifica e mirroring scheda 7800

- Decoder unico di chip-select/offset condiviso da letture, scritture e preview degli slot CPU: mirror TIA/MARIA su A8/A9; mirror registri RIOT su A5/A6/A8; RAM RIOT $0480–$04FF/$0580–$05FF collegata al chip nello zoccolo.
- RAM principale $1800–$27FF e alias bassi con overlay I/O; RAM alta sottostante non alterata da scritture ai mirror dei registri. RIOT/TIA, incluse finestre specchiate e RAM RIOT, seguono il modello lento 6 clock.
- 14 nuovi casi automatici, inclusa scansione dei 65536 indirizzi, tutte le combinazioni dei mirror RIOT, rimozione chip/open bus e accessi CPU lenti. Totale 271 test superati. Matrice DLI invariata; Summer Games rieseguito per 180 frame senza troncamenti/clamp.
- Limiti espliciti: BIOS/lock di controllo e modalità 2600 non implementati; alias $2800 dipendente da revisione non inventato; spazio $2800–$3FFF non assegnato in assenza di future periferiche. Non una decodifica elettrica completa di tutte le revisioni 7800.

## Consolidamento MARIA / SALLY

- Implementato sequenziatore DMA a master clock: acquisizione DLL/header/grafica distribuita, dati letti dal bus al momento del fetch, HALT con avvio/rilascio allineati a quattro clock, deadline di riga e cancellazione su reset/rimozione bus.
- DLI della DLL successiva dopo acquisizione post-render; NMI conservata durante HALT. Il DLI iniziale ora viene emesso dopo grafica e terminatore, con overhead conteggiato di 17 master clock. Test dedicati coprono ordine dei fetch, reset durante overhead, livello NMI mantenuto e nuovo fronte dopo RTI. Corretto bootstrap DLL: la prima riga non decrementa l'offset né acquisisce il successore. Il fixture denso ora serve tutte le richieste su righe distinte, senza introdurre una coda NMI artificiale. Confronto con MAME come riferimento funzionale, non certificazione hardware. Validazione elettrica/diagnostiche esterne ancora aperte.
- Summer Games e Winter Games: esecuzioni corrette da 180 frame ancora senza righe troncate.
- Implementate fasi logiche PHI1/PHI2: preview senza side effect del prossimo accesso SALLY, selezione anticipata dello slot 4/6 clock, accesso all'ultimo clock PHI2 e pin di consenso MARIA al confine PHI1. Test su tutti i 256 opcode e su handoff/ritorno di accessi lenti.
- Driver MAME completo archiviato e consultato per DLI: impulso NMI come evento scheduler, non misura elettrica. Nessuna diagnostica DLI esterna eseguita in questo incremento.
- Matrice sintetica NTSC/PAL con/senza DLI iniziale esportata: su due frame NTSC 480/480 senza startup e 482/482 con startup; PAL 580/580 senza startup e 582/582 con startup. Zero richieste non servite, distanza minima 452 clock. Il precedente doppio evento a 23 clock è eliminato correggendo il bootstrap. Tutti i casi passano. Dettagli in `ops/references/dli-matrix/README.md`; non validazione hardware esterna.
- Implementati caricamento DLL successiva nei sei clock post-render e overhead DLI di 17 master clock, con cache, conteggio dei clock trascorsi, deadline e WSYNC su clamp. Diagnostica NTSC post-render aggiornata: 480 eventi serviti/RTI su due frame, 8160 clock di overhead, zero clamp/troncamenti; bootstrap e ultimo successore invisibile esclusi dal conteggio delle transizioni.
- Secondo riferimento ProSystem archiviato e confrontato: diverge da MAME sul bootstrap e usa NMI come chiamata immediata, non pin. Nessuna misura elettrica ricavabile. Matrice estesa con latenza osservata dell'incremento handler: 76–84 clock senza startup, 74–84 con startup in NTSC/PAL; osservazioni dopo Board.Tick, non RTI né timestamp elettrici. Quattro regressioni verificano ordine e completamento dell'incremento prima del prossimo DLI.
- Da completare: validazione elettrica delle fasi/clock stretching, temporizzazione DMA, durata dell'impulso NMI, confronto indipendente del bootstrap e regole di overflow/defer della DLL; diagnostiche esterne. La nuova diagnostica è sintetica, non una prova hardware indipendente. Non dichiarato ciclo-esatto.

## PAL / NTSC comune — DA RIVEDERE

**Promemoria richiesto dall'utente:** il supporto attuale è provvisorio; rivedere architettura e comportamento Auto/PAL/NTSC prima di considerarlo consolidato. Verificare la distinzione tra standard della macchina, segnale video, cavo e monitor, oltre a rilevamento automatico, override e conseguenze del cambio di modalità.

- Implementati Auto/PAL/NTSC nel VideoPort comune, Composite Auto nel VideoMenu e switch console opt-in nel Computer 7800.
- A78 PAL accettato; clock, raster MARIA e pacing cambiano in base alla regione; il cambio effettivo riavvia la macchina. Il cavo composito rimane distinto dalla configurazione hardware.
- Da completare: palette PAL misurata, area attiva calibrata, timing DMA/DLI e audio precisi, prove con ROM PAL reali e verifica manuale dei menu/cavi Windows. Nessuna modifica ai clock di Thing64/NES implicita in questo incremento.

## Da vedere dopo — Atari Development Card

- [ ] Documentare ed emulare la scheda Atari 7800 Development Card (C300358), prima di tentare l'esecuzione di **Symbolic Debugger & Downloader (1988) (Atari)**.
- ROM allegata identificata come `Devcard`: A78 v1, 16 KiB, flag originali `$0006`, NTSC. SHA1 del payload: `4df3657f57e3086bee26fb5c6ea946c509d08553`.
- Riferimento archiviato: `ops/references/mame-a7800-software.xml`, voce `devcard`, dichiarata `supported="no"`; il tipo POKEY assegnato è annotato come errato. Non usare questa voce per inferire il cablaggio.
- [ ] Reperire schemi/manuali e verificare decodifica ROM/RAM, registri e protocollo dell'interfaccia di download.
- [ ] Implementare l'hardware verificato dietro il connettore cartuccia comune, mantenendo pin e zoccoli separati dalla form.
- [ ] Aggiungere test e valutare in seguito l'integrazione con lo Studio per debugger e download di programmi.
- Stato: riconoscimento per impronta e diagnostica specifica implementati; esecuzione e hardware non supportati. Nessun override o modifica del file ROM. Attività rinviata, non prioritaria rispetto al core della console.

## Not claimed
Playable 7800 emulation, 2600 compatibility, electrical pin-number simulation, native composite output, working Studio or Atari audio/video synthesis.
