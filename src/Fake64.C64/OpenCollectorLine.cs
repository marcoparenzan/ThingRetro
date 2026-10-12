namespace Fake64.C64;

// Pista a collettore aperto (/IRQ, /NMI): una resistenza la tiene alta e ogni uscita collegata
// può solo tirarla giù. È bassa (attiva) se almeno un dispositivo la sta tirando giù
public sealed class OpenCollectorLine
{
    readonly Func<bool>[] drivers;

    // drivers: le uscite collegate alla pista; ognuna restituisce true quando tira giù la linea
    public OpenCollectorLine(string name, params Func<bool>[] drivers)
    {
        Name = name;
        this.drivers = drivers;
    }

    public string Name { get; }

    public bool IsLow
    {
        get
        {
            foreach (var driver in drivers)
                if (driver()) return true;
            return false;
        }
    }
}
