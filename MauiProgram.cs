using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using WorkActivityTracker.Data;
using WorkActivityTracker.Services;

namespace WorkActivityTracker;

/// <summary>
/// Classe di configurazione principale dell'applicazione MAUI.
/// Configura i servizi, il database e l'interfaccia Blazor Hybrid.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Crea e configura l'applicazione MAUI con tutti i servizi necessari.
    /// </summary>
    /// <returns>Istanza configurata di MauiApp</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()  // Abilita CommunityToolkit per FolderPicker
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // =============================================
        // CONFIGURAZIONE DA FILE ESTERNO
        // Il file appsettings.json viene cercato nella cartella dell'applicazione
        // =============================================
        var configuration = LoadConfiguration();
        builder.Services.AddSingleton<IConfiguration>(configuration);

        // Abilita Blazor WebView per l'interfaccia utente
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        // In debug, abilita gli strumenti di sviluppo Blazor
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // =============================================
        // CONFIGURAZIONE DATABASE SQL SERVER
        // La stringa di connessione viene letta da appsettings.json
        // =============================================
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json");
        
        // Registra il DbContext come factory (per supporto multi-thread)
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // =============================================
        // REGISTRAZIONE SERVIZI
        // =============================================
        
        // Configurazione: rende disponibile IConfiguration per i servizi
        builder.Services.AddSingleton<IConfiguration>(configuration);
        
        // AppConfigService: Singleton per le configurazioni dell'app
        builder.Services.AddSingleton<AppConfigService>();
        
        // UserService: Singleton perché l'utente non cambia durante l'esecuzione
        builder.Services.AddSingleton<UserService>();
        
        // ActivityService: Scoped perché crea nuovi DbContext per ogni operazione
        builder.Services.AddScoped<ActivityService>();
        
        // TodoService: Scoped per la gestione dei TODO
        builder.Services.AddScoped<TodoService>();
        
        // AmbienteService: Scoped per la gestione dei congelati
        builder.Services.AddScoped<AmbienteService>();
        
        // AppuntiService: Scoped per la gestione degli appunti (Knowledge Base)
        builder.Services.AddScoped<AppuntiService>();

        // ClienteService: Scoped per la gestione dei clienti con logging
        builder.Services.AddScoped<ClienteService>();

        // SegnalazioneService: Scoped per la gestione delle segnalazioni
        builder.Services.AddScoped<SegnalazioneService>();

        // AmbientiRilascioService: Scoped per la gestione dei tipi ambienti e versioni di rilascio
        builder.Services.AddScoped<AmbientiRilascioService>();

        // TipiAttivitaService: Scoped per la gestione dei tipi attività personalizzati
        builder.Services.AddScoped<TipiAttivitaService>();

        // CalendarioService: Scoped per la gestione degli eventi del calendario
        builder.Services.AddScoped<CalendarioService>();

        // UnsavedChangesService: Singleton per tracciare modifiche non salvate
        builder.Services.AddSingleton<UnsavedChangesService>();

        return builder.Build();
    }

    /// <summary>
    /// Carica la configurazione da appsettings.json.
    /// Cerca il file prima nella cartella dell'eseguibile, poi nella cartella corrente.
    /// </summary>
    private static IConfiguration LoadConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();
        
        // Percorso della cartella dell'applicazione
        var appDirectory = AppContext.BaseDirectory;
        var configPath = Path.Combine(appDirectory, "appsettings.json");
        
        // Se non trova nella cartella dell'app, prova la cartella corrente
        if (!File.Exists(configPath))
        {
            configPath = "appsettings.json";
        }
        
        // Se il file esiste, lo carica
        if (File.Exists(configPath))
        {
            configBuilder.AddJsonFile(configPath, optional: false, reloadOnChange: true);
        }
        else
        {
            // Se non trova il file, crea una configurazione con valori di default
            // Questo permette all'app di avviarsi anche senza il file (per debug)
            System.Diagnostics.Debug.WriteLine($"WARNING: appsettings.json not found at {configPath}");
            
            // Usa configurazione in-memory con valori di default
            var defaultConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=localhost;Initial Catalog=WorkActivityTracker;Integrated Security=True;TrustServerCertificate=True"
            };
            configBuilder.AddInMemoryCollection(defaultConfig);
        }
        
        return configBuilder.Build();
    }
}
