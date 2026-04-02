using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione dell'utente corrente basato su Windows Authentication.
/// Gestisce il login automatico e la creazione utente al primo accesso.
/// </summary>
public class UserService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly AppConfigService _appConfig;
    private Utente? _currentUser;

    public UserService(IDbContextFactory<AppDbContext> contextFactory, AppConfigService appConfig)
    {
        _contextFactory = contextFactory;
        _appConfig = appConfig;
    }

    /// <summary>
    /// Username Windows dell'utente corrente (es: "mario.rossi")
    /// </summary>
    public string WindowsUsername => Environment.UserName;
    
    /// <summary>
    /// Nome del computer (es: "DESKTOP-ABC123")
    /// </summary>
    public string MachineName => Environment.MachineName;
    
    /// <summary>
    /// Identità completa nel formato "MACCHINA\utente"
    /// </summary>
    public string FullIdentity => $"{MachineName}\\{WindowsUsername}";

    /// <summary>
    /// Utente corrente memorizzato in cache
    /// </summary>
    public Utente? CurrentUser => _currentUser;

    /// <summary>
    /// Recupera l'utente corrente dal database o lo crea se non esiste.
    /// Al primo accesso, crea automaticamente un nuovo record nella tabella Utenti.
    /// </summary>
    /// <returns>Oggetto Utente con i dati dell'utente corrente</returns>
    public async Task<Utente> GetOrCreateCurrentUserAsync()
    {
        // Se già in cache, ritorna direttamente
        if (_currentUser != null)
            return _currentUser;

        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Cerca l'utente per Windows Username
        var user = await context.Utenti
            .FirstOrDefaultAsync(u => u.WindowsUsername == WindowsUsername);

        if (user == null)
        {
            // Primo accesso: crea nuovo utente
            user = new Utente
            {
                WindowsUsername = WindowsUsername,
                NomeCompleto = GetWindowsDisplayName(),
                DataPrimoAccesso = DateTime.Now,
                UltimoAccesso = DateTime.Now,
                Attivo = true,
                PrivacyMode = _appConfig.PrivacyMode
            };

            context.Utenti.Add(user);
            await context.SaveChangesAsync();
        }
        else
        {
            // Utente esistente: aggiorna ultimo accesso e sincronizza PrivacyMode da appsettings
            user.UltimoAccesso = DateTime.Now;
            user.PrivacyMode = _appConfig.PrivacyMode;
            await context.SaveChangesAsync();
        }

        _currentUser = user;
        return user;
    }

    /// <summary>
    /// Tenta di recuperare il nome completo dell'utente Windows.
    /// </summary>
    /// <returns>Nome completo se disponibile, altrimenti lo username</returns>
    private string GetWindowsDisplayName()
    {
        try
        {
            return System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        }
        catch
        {
            return WindowsUsername;
        }
    }
}
