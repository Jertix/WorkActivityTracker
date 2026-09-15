using Microsoft.Extensions.Configuration;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione delle configurazioni dell'applicazione.
/// Legge i valori da appsettings.json.
/// </summary>
public class AppConfigService
{
    private readonly IConfiguration _configuration;

    public AppConfigService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Cartella "Export" nella directory dell'eseguibile, creata se non esiste.
    /// Statica e condivisa: la usano sia il form principale sia le modali, così il pulsante
    /// "Apri Export" apre sempre la stessa cartella in cui vengono scritti gli export.
    /// </summary>
    public static string GetExportFolder()
    {
        var folder = System.IO.Path.Combine(AppContext.BaseDirectory, "Export");
        System.IO.Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// Indica se mostrare la checkbox "Modalità admin".
    /// Default: true (visibile)
    /// </summary>
    public bool MostraModalitaAdmin => 
        _configuration.GetValue<bool>("AppSettings:MostraModalitaAdmin", true);

    /// <summary>
    /// Nome dell'applicazione.
    /// </summary>
    public string AppName => 
        _configuration.GetValue<string>("AppSettings:AppName") ?? "Work Activity Tracker";

    /// <summary>
    /// Versione dell'applicazione.
    /// </summary>
    public string Version =>
        _configuration.GetValue<string>("AppSettings:Version") ?? "1.0";

    /// <summary>
    /// Se true, le attività di questo utente non saranno visibili agli admin di altre postazioni.
    /// Impostabile solo tramite appsettings.json manualmente (nessuna UI).
    /// Default: false.
    /// </summary>
    public bool PrivacyMode =>
        _configuration.GetValue<bool>("AppSettings:PrivacyMode", false);

    /// <summary>
    /// Numero di mesi di mantenimento dello storico modifiche (tabella EditorHistory).
    /// All'avvio le voci più vecchie di questo numero di mesi vengono eliminate automaticamente.
    /// Default: 3. Un valore <= 0 disattiva la pulizia automatica.
    /// </summary>
    public int NumeroMesiMantenimentoHistory =>
        _configuration.GetValue<int>("AppSettings:NumeroMesiMantenimentoHistory", 3);

    /// <summary>
    /// Timeout in secondi dei comandi SQL. Default 30 (lo stesso di SqlClient). Va aumentato sugli
    /// archivi grandi, dove la ricerca con LIKE su Note/ChangesetCoinvolti (NVARCHAR(MAX) con
    /// screenshot in base64) può superare i 30 secondi. Letto all'avvio in MauiProgram: per renderlo
    /// effettivo serve riavviare l'applicazione. Un valore <= 0 viene riportato a 30.
    /// </summary>
    public int TimeoutQuerySecondi =>
        _configuration.GetValue<int>("AppSettings:TimeoutQuerySecondi", 30);

    /// <summary>
    /// Se true, l'export PDF di una singola attività include anche le attività collegate dalla
    /// catena "**** Proseguo l'attività N ... ****" presente nelle Note. Se false esporta la sola
    /// attività selezionata. Default: true.
    /// </summary>
    public bool EsportaAttivitaCollegateInPdf =>
        _configuration.GetValue<bool>("AppSettings:EsportaAttivitaCollegateInPdf", true);

    /// <summary>
    /// Giorni di preavviso entro i quali un ticket in scadenza viene evidenziato in ARANCIONE
    /// nella griglia delle attività. Default: 5. Un valore <= 0 disattiva l'evidenziazione arancione.
    /// </summary>
    public int GiorniPreavvisoScadenzaArancione =>
        _configuration.GetValue<int>("AppSettings:GiorniPreavvisoScadenzaArancione", 5);

    /// <summary>
    /// Giorni di preavviso entro i quali un ticket in scadenza viene evidenziato in ROSSO
    /// (ha la precedenza sull'arancione). Default: 2. Le scadenze già passate usano un rosso più marcato.
    /// </summary>
    public int GiorniPreavvisoScadenzaRosso =>
        _configuration.GetValue<int>("AppSettings:GiorniPreavvisoScadenzaRosso", 2);

    /// <summary>
    /// Nome del programma usato per comporre il nome della cartella patch.
    /// Default: "MyProgram".
    /// </summary>
    public string NomeProgramma =>
        _configuration.GetValue<string>("AppSettings:NomeProgramma") ?? "MyProgram";

    /// <summary>
    /// Nome della compagnia usato nei percorsi/testi dei readme (token {NOMECOMPAGNIA}).
    /// Default: "MyCompany".
    /// </summary>
    public string NomeCompagnia =>
        _configuration.GetValue<string>("AppSettings:NomeCompagnia") ?? "MyCompany";

    /// <summary>
    /// Cartella temporanea radice dove viene creata la struttura patch.
    /// Default: C:\TmpPatch\
    /// </summary>
    public string TmpPatchDir =>
        _configuration.GetValue<string>("AppSettings:TmpPatchDir") ?? @"C:\TmpPatch\";
}
