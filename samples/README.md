# Esempi

Programmi da trascinare sulla finestra di Fake64 (o da passare da riga di comando), ciascuno dedicato a un
argomento del Commodore 64. I sorgenti sono in [src](src): BASIC (`.bas`) e assembly 6502 commentato (`.asm`),
pensati per essere letti. I programmi BASIC e quelli con lo stub `10 SYS2061` partono da soli con `RUN`.

## BASIC

| File | Sorgente | Cosa mostra |
| --- | --- | --- |
| `hello.prg` | [hello.bas](src/hello.bas) | Testo a colori: `POKE 646` è il colore del cursore |
| `math.prg` | [math.bas](src/math.bas) | Espressioni e funzioni in virgola mobile |
| `sprite.prg` | [sprite.bas](src/sprite.bas) | Uno sprite espanso che rimbalza |
| `keys.prg` | [keys.bas](src/keys.bas) | Tasti funzione e tasti cursore con `GET` (F1-F8 = codici 133-140). `Q` per uscire |
| `joystick.prg` | [joystick.bas](src/joystick.bas) | Joystick nella porta 2 (`PEEK(56320)`): lo smile si muove, con il fuoco cambia colore |
| `paddle.prg` | [paddle.bas](src/paddle.bas) | Paddle nella porta 1 (nell'app: "Paddle: mouse"): lo smile segue la manopola (`PEEK(54297)`) |

## Linguaggio macchina: CPU e interrupt

| File | Sorgente | Cosa mostra |
| --- | --- | --- |
| `sys.prg` | [sys.asm](src/sys.asm) | Una routine a `$C000`: si avvia con `SYS 49152` (non con `RUN`) |
| `rasterbars.prg` | [rasterbars.asm](src/rasterbars.asm) | Raster interrupt del VIC-II: una barra rossa ferma nel bordo, mentre il BASIC funziona |
| `irq.prg` | [irq.asm](src/irq.asm) | Agganciarsi all'IRQ del timer della CIA 1 (vettore `$0314`) senza fermare il KERNAL |
| `nmi.prg` | [nmi.asm](src/nmi.asm) | NMI dal timer della CIA 2 (vettore `$0318`): fronte di discesa e conferma dell'interrupt |
| `decimal.prg` | [decimal.asm](src/decimal.asm) | Modo decimale (BCD): un contatore a 4 cifre senza divisioni. Non torna al BASIC |

## Linguaggio macchina: VIC-II

| File | Sorgente | Cosa mostra |
| --- | --- | --- |
| `multicolor.prg` | [multicolor.asm](src/multicolor.asm) | Testo multicolor con un set di caratteri definito in RAM |
| `ecm.prg` | [ecm.asm](src/ecm.asm) | Extended color mode: 4 colori di sfondo scelti dai bit 6-7 del codice di schermo |
| `bitmap.prg` | [bitmap.asm](src/bitmap.asm) | Bitmap 320 x 200: la memoria organizzata in celle di 8 x 8 |
| `mcbitmap.prg` | [mcbitmap.asm](src/mcbitmap.asm) | Bitmap multicolor 160 x 200: le 4 sorgenti di colore di ogni cella |
| `sprites.prg` | [sprites.asm](src/sprites.asm) | Otto sprite: espansione, multicolor, priorità sulla grafica, collisioni (`$D01E`) |
| `scroll.prg` | [scroll.asm](src/scroll.asm) | Scroll fine di una riga con XSCROLL, cambiato con il raster. Non torna al BASIC |
| `vicbank.prg` | [vicbank.asm](src/vicbank.asm) | Banco del VIC-II dalla CIA 2, caratteri copiati dalla ROM in RAM e modificati |

I programmi che "non tornano al BASIC" restano in un ciclo: per uscire serve un reset (clic destro sul monitor).

## Cartucce

| File | Sorgente | Cosa mostra |
| --- | --- | --- |
| `hello.crt` | [cartridge.asm](src/cartridge.asm) | Cartuccia 8K con avvio automatico (firma `CBM80`) |
| `ultimax.crt` | [ultimax.asm](src/ultimax.asm) | Modo Ultimax: il C64 senza KERNAL, partendo dal vettore di reset della cartuccia |
| `banks.crt` | [banks0.asm](src/banks0.asm), [banks1.asm](src/banks1.asm) | Magic Desk con due banchi: il codice che cambia banco deve girare in RAM |

## L'assembler

I sorgenti `.asm` usano l'assembler del progetto `src/Fake64.Samples`, essenziale ma completo:

```asm
; commento
* = $0801               ; indirizzo di assemblaggio
.basic                  ; riga BASIC "10 SYS2061": il codice parte subito dopo
BORDO = $D020           ; costante (COSTANTI in maiuscolo, etichette in minuscolo: sono distinte)
ciclo:  LDA #<testo     ; etichetta; <valore e >valore sono il byte basso e alto
        STA SCHERMO+40*12,X
        BNE ciclo
testo:  .text "CIAO"    ; PETSCII, per CHROUT
        .screen "CIAO"  ; codici schermo, da scrivere direttamente nella memoria video
        .byte 13, $FF, %1010, 'A'
        .word ciclo
        .fill 8, 0
```

Tutti i modi di indirizzamento del 6502 con la sintassi standard (la pagina zero viene scelta da sola), gli opcode
non documentati compresi, espressioni con `+ - * /`, parentesi e `*` per l'indirizzo corrente.

## Rigenerare

```powershell
dotnet run --project src/Fake64.Samples            # scrive i .prg e i .crt in samples/
```

I test in `src/Fake64.Tests/TestSamples.cs` eseguono ogni esempio nell'emulatore e verificano che i file di questa
cartella coincidano con quelli prodotti dai sorgenti.
