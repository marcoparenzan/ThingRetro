# Esempi

Programmi da trascinare sulla finestra di Fake64 (o da passare da riga di comando), ciascuno dedicato a una
funzione dell'emulatore. I programmi BASIC partono da soli con `RUN`; accanto al `.prg` c'è il sorgente `.bas`.

| File | Cosa mostra |
|---|---|
| `hello.prg` | BASIC e colori del testo: la stessa riga in 15 colori (`POKE 646` è il colore del cursore) |
| `rasterbars.prg` | Raster interrupt: il bordo diventa rosso fra le righe raster 100 e 150. Il BASIC resta utilizzabile |
| `sprite.prg` | Uno sprite espanso (24 x 21 pixel, disegnato nel generatore) che rimbalza sullo schermo |
| `keys.prg` | Tasti funzione e tasti cursore: scrive il nome del tasto premuto (F1-F8 sono i codici 133-140). `Q` per uscire |
| `bitmap.prg` | Modo bitmap hires in linguaggio macchina: uno zig-zag disegnato cella per cella. Resta in bitmap: per uscire, reset |
| `hello.crt` | Cartuccia 8K con avvio automatico (firma `CBM80`): inizializza il sistema e scrive un messaggio |

`rasterbars.prg` e `bitmap.prg` sono in linguaggio macchina con uno stub BASIC `10 SYS2061`: il codice inizia a
`$080D`, subito dopo la riga BASIC.

## Rigenerare

I file sono prodotti dal progetto `src/Fake64.Samples`, che contiene i sorgenti di tutti gli esempi: il BASIC come
testo (trasformato nel formato del C64 da un tokenizzatore BASIC V2), il codice macchina come byte con il listato
assembly accanto, le cartucce con un writer del formato `.crt`.

```powershell
dotnet run --project src/Fake64.Samples            # scrive in samples/
dotnet run --project src/Fake64.Samples -- cartella
```

I test in `src/Fake64.Tests/TestSamples.cs` eseguono ogni esempio nell'emulatore e verificano che i file di questa
cartella coincidano con quelli prodotti dal generatore.
