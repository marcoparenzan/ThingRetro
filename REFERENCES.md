# Fonti tecniche

## Archivio locale riproducibile

Dalla radice del repository, in PowerShell:

```powershell
./ops/download-reference-docs.ps1
```

Lo script scarica i file integrali in `ops/references/` e genera `manifest.json` con URL, data UTC e SHA256. Il primo download è stato eseguito con successo dall'assistente tramite l'utility .NET `src/ReferenceDownloader/ReferenceDownloader`; tutti e quattro i file e il manifest sono presenti. Per ripetere il download si può usare anche `dotnet run --project src/ReferenceDownloader/ReferenceDownloader -- ops/references` dalla radice del repository. I download usano branch mobili: il manifest identifica il contenuto scaricato, ma esecuzioni successive possono ottenere revisioni diverse. Un errore HTTP interrompe lo script senza dichiarare il download completato.

## Aggiornamento MARIA/SALLY e DLI

L'utility .NET scarica ora anche il driver completo Atari 7800, il catalogo software e la logica delle cartucce, oltre ai loader, MARIA e licenza: sette file, con URL/data/hash nel manifest. Usare `dotnet run --project src/ReferenceDownloader/ReferenceDownloader -- ops/references` per la raccolta aggiornata; lo script PowerShell storico può contenere un elenco più ristretto.

- Driver MAME: https://raw.githubusercontent.com/mamedev/mame/master/src/mame/atari/a7800.cpp — copia integrale `ops/references/mame-a7800-driver.cpp`. Consultato per callback HALT/DLI e note sul clock TIA/RIOT.
- MARIA MAME: https://raw.githubusercontent.com/mamedev/mame/master/src/mame/atari/maria.cpp — consultato nuovamente per startup DMA, DLL e overhead DLI.
- Il driver usa `pulse_input_line(INPUT_LINE_NMI, attotime::zero)`: è un evento del simulatore, non una misura della durata elettrica di NMI. Il riferimento MARIA conteggia 17 clock di overhead DLI. Questi sorgenti non certificano la precisione delle nostre fasi PHI1/PHI2 o del nostro impulso provvisorio di otto clock.
- Nessuna diagnostica esterna dedicata al timing DLI eseguita in questo incremento; i nuovi test sono sintetici.

## Secondo riferimento: ProSystem/libretro

Archiviate integralmente `prosystem-maria.c`, `prosystem-sally.c` e `prosystem-license.txt` in `ops/references`, con URL/data/SHA256 nel manifest. L'utility .NET scarica ora dieci file. Materiale GPL-2.0-or-later separato e non compilato nel progetto; nessun codice ProSystem copiato nel core.

- https://raw.githubusercontent.com/libretro/prosystem-libretro/master/core/Maria.c
- https://raw.githubusercontent.com/libretro/prosystem-libretro/master/core/Sally.c
- https://raw.githubusercontent.com/libretro/prosystem-libretro/master/License.txt

ProSystem chiama `sally_ExecuteNMI()` direttamente, con push dello stack e caricamento vettore, restituendo 7 cicli CPU: non modella la durata di un pin NMI. MARIA conteggia 31 clock base e 7 di inizializzazione, emette NMI all'acquisizione iniziale e decrementa l'offset nella stessa chiamata. Differisce dal guard `frame_scanline > 16` di MAME e dal nostro sequenziatore. Questi sono modelli software diversi, non due misure hardware concordi. Non è stata eseguita una comparazione binaria tra emulatori né una ROM diagnostica esterna. Il bootstrap attuale è coerente con il riferimento MAME; la durata elettrica NMI rimane aperta.

La matrice locale ora registra anche i clock di osservazione degli incrementi dell'handler. Latenza sintetica osservata: 76–84 master clock senza startup, 74–84 con startup, nei profili NTSC e PAL. Il campionamento avviene dopo `Board.Tick`, non sul bus elettrico: non rappresenta né la sola latenza di ingresso NMI né il completamento di RTI.

## Fonti confermate nella sessione corrente

- MAME, loader cartucce Atari 7800: https://raw.githubusercontent.com/mamedev/mame/master/src/devices/bus/a7800/a78_slot.cpp
- Fork a7800, loader cartucce: https://raw.githubusercontent.com/7800-devtools/a7800/master/src/devices/bus/a7800/a78_slot.cpp

Questi due sorgenti sono stati consultati per verificare i flag legacy A78. Sono codice di riferimento, non specifiche normative del formato.

## Fonte citata nelle fasi precedenti

- MAME, MARIA: https://raw.githubusercontent.com/mamedev/mame/master/src/mame/atari/maria.cpp

La conversazione precedente cita MAME per il formato delle display list MARIA; il percorso è incluso come riferimento utile, senza pretendere di ricostruire l'esatta revisione allora consultata.

## Riferimenti audio TIA

Consultati e scaricati integralmente con l'utility .NET (ora 13 file):

- ProSystem `core/Tia.c`: `ops/references/prosystem-tia.c`, modello funzionale legacy di toni/gating/polinomi; conserva le intestazioni GPL e LGPL TiaSound.
- Stella `src/emucore/tia/AudioChannel.cxx`: `ops/references/stella-audio-channel.cxx`, modello a due fasi consultato per comprendere i limiti del modello funzionale.
- Licenza Stella: `ops/references/stella-license.txt`.

URL/data/hash nel manifest. Nessuna tabella polinomiale o traduzione del codice a due fasi incorporata nel core; l'implementazione funzionale usa contatori e LFSR propri. Questi riferimenti non costituiscono validazione dell'audio su hardware. Polinomi e fase iniziale restano approssimati.

## POKEY esteso

Consultato `https://raw.githubusercontent.com/libretro/prosystem-libretro/master/core/Pokey.c` e aggiunto al downloader come `ops/references/prosystem-pokey.c` (14 fonti totali). Le definizioni AUDCTL/AUDC e i periodi fast/concatenati guidano il modello funzionale. Conservate le intestazioni originali GPL/LGPL. Tabelle non copiate: generatori polinomiali calcolati nel core. Il riferimento audio non valida IRQ/STIMER, seriale o il clock della nostra cartuccia; questi limiti restano espliciti. Il generatore TIA a due fasi Stella resta un riferimento, non è stato tradotto integralmente nel core.

## POKEY periferiche / timer

Scaricati integralmente MAME `src/devices/sound/pokey.cpp` e `pokey.h` nell'archivio, con manifest aggiornato (16 file). Consultati definizioni IRQ/SKSTAT/SKCTL, POTGO/ALLPOT e STIMER. La nota STIMER indica quattro cicli di ritardo, ma lo stesso sorgente ha un FIXME sulla sua implementazione: non è prova elettrica. Serializzazione esterna 8N1 e scanner tastiera nel core restano modelli funzionali ridotti. Non sono state eseguite ROM esterne acid5200 citate dal sorgente né prove con periferiche reali. Il default dell'oscillatore configurabile non è una misura di ogni cartuccia.

## BIOS e controllo di avvio

Consultati `tia_w`/`a7800_mem` nel driver MAME archiviato per latch, alias pre-lock e vista BIOS; ProSystem `core/Memory.c` per esempi INPTCTRL, aggiunto al downloader come `prosystem-memory.c` (17 fonti totali). Il modello di boot rimane funzionale e non è validato con firmware Atari reale. Nessun BIOS o gioco è stato scaricato per questa implementazione. Il decoder 2600 riproduce selezioni logiche a 13 bit; non è una promessa di supporto grafico TIA o di clock PAL 2600 esatti.

## Licenze e limiti

Lo script scarica anche il file COPYING di MAME. Conservare le intestazioni di licenza dei singoli sorgenti: il codice MAME include licenze per file. Il materiale resta separato dal codice dell'applicazione e non viene compilato. La consultazione non implica che il core implementi tutti i comportamenti presenti nei sorgenti.

Questo è il primo nucleo dell'archivio, non una raccolta completa di tutte le fonti MOS, Commodore, NES e Atari citate nelle fasi precedenti. Le nuove fonti andranno aggiunte allo script con URL e provenienza, evitando di inventare uno storico di consultazione.
