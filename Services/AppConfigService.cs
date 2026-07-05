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
