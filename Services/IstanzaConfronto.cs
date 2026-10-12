using System.Diagnostics;

namespace WorkActivityTracker.Services;

/// <summary>
/// Finestra di confronto (v5.6): una seconda istanza del programma, avviata
///  - dal pulsante "Ricerca in nuova finestra" accanto alla Descrizione (parte già filtrata sul
///    testo cercato), oppure
///  - dal pulsante "Apri selezionato in nuova finestra" della Ricerca Avanzata (parte con
///    l'attività selezionata aperta nel form, sul mese di quell'attività).
/// Serve a confrontare con l'attività su cui si sta lavorando nella finestra principale,
/// che resta intatta.
///
/// L'istanza di confronto è in SOLA CONSULTAZIONE per l'attività (Salva/Elimina disattivati:
/// aprendo la stessa attività in entrambe le finestre, un salvataggio sovrascriverebbe l'altro)
/// e non esegue i lavori di avvio (pulizia storico, backfill testo ricercabile, bozza di
/// emergenza), che restano compito della finestra principale.
///
/// Gli argomenti viaggiano sulla riga di comando: possibile perché l'app NON è un pacchetto
/// MSIX (WindowsPackageType=None), quindi l'eseguibile si può avviare direttamente e più volte.
/// </summary>
public static class IstanzaConfronto
{
    private const string ArgConfronto = "--confronto";
    private const string ArgCerca = "--cerca";
    private const string ArgApri = "--apri";
    private const string ArgAdmin = "--admin";

    private sealed record Argomenti(bool IsConfronto, string? Testo, int? AttivitaId, bool Admin);

    private static readonly Lazy<Argomenti> _argomenti = new(Leggi);

    /// <summary>True se questa istanza è una finestra di confronto.</summary>
    public static bool IsConfronto => _argomenti.Value.IsConfronto;

    /// <summary>Testo da cercare all'avvio (null se non indicato).</summary>
    public static string? TestoRicerca => _argomenti.Value.Testo;

    /// <summary>Id dell'attività da aprire nel form all'avvio (null se non indicato).</summary>
    public static int? AttivitaDaAprire => _argomenti.Value.AttivitaId;

    /// <summary>
    /// True se la finestra di origine era in modalità Admin: serve per aprire un'attività di
    /// un altro utente, che senza Admin non verrebbe trovata.
    /// </summary>
    public static bool AvviaInAdmin => _argomenti.Value.Admin;

    /// <summary>
    /// Avvia una nuova istanza in modalità confronto con la ricerca indicata
    /// (pulsante "Ricerca in nuova finestra" accanto alla Descrizione).
    /// </summary>
    public static void Avvia(string testoRicerca) =>
        AvviaConArgomenti(ArgCerca, testoRicerca);

    /// <summary>
    /// Avvia una nuova istanza in modalità confronto con l'attività indicata già aperta nel form
    /// (pulsante "Apri selezionato in nuova finestra" della Ricerca Avanzata).
    /// </summary>
    public static void AvviaConAttivita(int attivitaId, bool admin)
    {
        var args = new List<string> { ArgApri, attivitaId.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        if (admin) args.Add(ArgAdmin);
        AvviaConArgomenti(args.ToArray());
    }

    /// <summary>
    /// <c>ArgumentList</c> si occupa delle virgolette: il testo può contenere spazi e apici.
    /// </summary>
    private static void AvviaConArgomenti(params string[] argomenti)
    {
        var exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Percorso dell'eseguibile non disponibile.");

        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        psi.ArgumentList.Add(ArgConfronto);
        foreach (var a in argomenti)
            psi.ArgumentList.Add(a);

        Process.Start(psi);
    }

    private static Argomenti Leggi()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();

            bool Presente(string nome) =>
                args.Any(a => string.Equals(a, nome, StringComparison.OrdinalIgnoreCase));

            string? Valore(string nome)
            {
                var i = Array.FindIndex(args, a => string.Equals(a, nome, StringComparison.OrdinalIgnoreCase));
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }

            int? id = int.TryParse(Valore(ArgApri), System.Globalization.NumberStyles.Integer,
                                   System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0
                ? n : null;

            return new Argomenti(Presente(ArgConfronto), Valore(ArgCerca), id, Presente(ArgAdmin));
        }
        catch
        {
            return new Argomenti(false, null, null, false);
        }
    }
}
