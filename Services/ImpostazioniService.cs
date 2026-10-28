using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Lettura e scrittura delle impostazioni della finestra ⚙️ Impostazioni (v5.7).
///
/// Scrive direttamente in appsettings.json (scelta dell'utente: è lo stesso file che va già
/// preservato agli aggiornamenti per la stringa di connessione). Tocca SOLO le chiavi della sezione
/// AppSettings che la finestra gestisce: tutto il resto del file, ConnectionStrings compreso, resta
/// com'è. Dopo il salvataggio ricarica la configurazione, e siccome <see cref="AppConfigService"/>
/// legge sempre dal vivo i valori nuovi valgono subito (tranne quelli letti solo all'avvio).
/// </summary>
public sealed class ImpostazioniService
{
    private readonly IConfiguration _configuration;
    private readonly AppConfigService _appConfig;

    public ImpostazioniService(IConfiguration configuration, AppConfigService appConfig)
    {
        _configuration = configuration;
        _appConfig = appConfig;
    }

    /// <summary>False se all'avvio appsettings.json non è stato trovato: non c'è un file da riscrivere.</summary>
    public bool PuoSalvare => AppConfigService.PercorsoFile is not null;

    /// <summary>I valori attuali, letti dalla configurazione in uso.</summary>
    public ImpostazioniModificabili Leggi() => new()
    {
        Tema = _appConfig.Tema,
        ColorePrincipale = _appConfig.ColorePrincipale,
        NomeProgramma = _appConfig.NomeProgramma,
        NomeCompagnia = _appConfig.NomeCompagnia,
        TmpPatchDir = _appConfig.TmpPatchDir,
        GiorniPreavvisoScadenzaArancione = _appConfig.GiorniPreavvisoScadenzaArancione,
        GiorniPreavvisoScadenzaRosso = _appConfig.GiorniPreavvisoScadenzaRosso,
        EsportaAttivitaCollegateInPdf = _appConfig.EsportaAttivitaCollegateInPdf,
        NumeroMesiMantenimentoHistory = _appConfig.NumeroMesiMantenimentoHistory,
        TimeoutQuerySecondi = _appConfig.TimeoutQuerySecondi
    };

    /// <summary>
    /// Scrive i valori in appsettings.json e ricarica la configurazione. Il file viene scritto prima
    /// in un file temporaneo e poi sostituito, così un errore a metà non lo lascia troncato.
    /// Solleva un'eccezione con un messaggio comprensibile se il file non si può scrivere.
    /// </summary>
    public void Salva(ImpostazioniModificabili valori)
    {
        var percorso = AppConfigService.PercorsoFile
            ?? throw new InvalidOperationException("appsettings.json non è stato trovato all'avvio: non c'è un file in cui salvare.");

        var opzioniLettura = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        if (JsonNode.Parse(File.ReadAllText(percorso), documentOptions: opzioniLettura) is not JsonObject radice)
            throw new InvalidOperationException("appsettings.json non contiene un oggetto JSON valido.");

        if (radice["AppSettings"] is not JsonObject sezione)
        {
            sezione = new JsonObject();
            radice["AppSettings"] = sezione;
        }

        sezione["Tema"] = valori.Tema;
        sezione["ColorePrincipale"] = valori.ColorePrincipale;
        sezione["NomeProgramma"] = valori.NomeProgramma.Trim();
        sezione["NomeCompagnia"] = valori.NomeCompagnia.Trim();
        sezione["TmpPatchDir"] = valori.TmpPatchDir.Trim();
        sezione["GiorniPreavvisoScadenzaArancione"] = valori.GiorniPreavvisoScadenzaArancione;
        sezione["GiorniPreavvisoScadenzaRosso"] = valori.GiorniPreavvisoScadenzaRosso;
        sezione["EsportaAttivitaCollegateInPdf"] = valori.EsportaAttivitaCollegateInPdf;
        sezione["NumeroMesiMantenimentoHistory"] = valori.NumeroMesiMantenimentoHistory;
        sezione["TimeoutQuerySecondi"] = valori.TimeoutQuerySecondi;

        // Accenti e simboli scritti come sono (non è): il file resta leggibile e modificabile a mano
        var json = radice.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        var temporaneo = percorso + ".tmp";
        try
        {
            File.WriteAllText(temporaneo, json);
            File.Move(temporaneo, percorso, overwrite: true);
        }
        catch (UnauthorizedAccessException)
        {
            try { File.Delete(temporaneo); } catch { /* il temporaneo può non esistere */ }
            throw new InvalidOperationException(
                $"Non ho i permessi per scrivere {percorso}. Se il programma è in una cartella protetta " +
                "(es. Programmi), spostalo in una cartella dell'utente o modifica il file a mano.");
        }

        // Il FileSystemWatcher di reloadOnChange ricaricherebbe comunque, ma con qualche centinaio di
        // millisecondi di ritardo: ricaricando subito, chi rilegge AppConfigService vede già i valori nuovi.
        (_configuration as IConfigurationRoot)?.Reload();
    }

    /// <summary>
    /// Descrizione della connessione per la sezione in sola lettura: server, database e tipo di
    /// autenticazione. La password, se c'è, non viene mai mostrata.
    /// </summary>
    public string DescriviConnessione()
    {
        var stringa = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(stringa)) return "(non configurata)";
        try
        {
            var b = new SqlConnectionStringBuilder(stringa);
            var autenticazione = b.IntegratedSecurity ? "autenticazione di Windows" : $"utente SQL «{b.UserID}»";
            return $"Server «{b.DataSource}», database «{b.InitialCatalog}», {autenticazione}";
        }
        catch (ArgumentException)
        {
            return "(stringa di connessione non leggibile)";
        }
    }
}
