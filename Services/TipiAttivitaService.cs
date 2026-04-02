using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione della lista dei tipi di attività (personalizzabile).
/// Gestisce le operazioni CRUD su TipiAttivita con logging delle modifiche.
/// </summary>
public class TipiAttivitaService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly UserService _userService;

    public TipiAttivitaService(IDbContextFactory<AppDbContext> dbFactory, UserService userService)
    {
        _dbFactory = dbFactory;
        _userService = userService;
    }

    /// <summary>
    /// Restituisce tutti i tipi attività attivi, ordinati per Ordine poi per Nome.
    /// </summary>
    public async Task<List<TipoAttivitaItem>> GetTipiAsync()
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TipiAttivita
            .Where(t => t.Attivo)
            .OrderBy(t => t.Ordine)
            .ThenBy(t => t.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// Restituisce tutti i tipi attività (inclusi non attivi), per la gestione nella modale.
    /// </summary>
    public async Task<List<TipoAttivitaItem>> GetTuttiAsync()
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        return await db.TipiAttivita
            .OrderBy(t => t.Ordine)
            .ThenBy(t => t.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// Aggiunge un nuovo tipo attività e registra il log.
    /// </summary>
    public async Task<TipoAttivitaItem> AddTipoAsync(TipoAttivitaItem tipo)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        db.TipiAttivita.Add(tipo);
        await db.SaveChangesAsync();

        var utente = await _userService.GetOrCreateCurrentUserAsync();
        db.TipiAttivitaLog.Add(new TipoAttivitaLog
        {
            NomeUtente = utente.WindowsUsername,
            AzioneSvolta = "Nuovo",
            NomeValore = tipo.Nome,
            NuovoValore = tipo.Nome
        });
        await db.SaveChangesAsync();

        return tipo;
    }

    /// <summary>
    /// Modifica un tipo attività esistente e registra il log.
    /// </summary>
    public async Task UpdateTipoAsync(TipoAttivitaItem tipo)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.TipiAttivita.FindAsync(tipo.Id)
            ?? throw new InvalidOperationException($"Tipo attività ID {tipo.Id} non trovato");

        var vecchioNome = existing.Nome;
        existing.Nome = tipo.Nome;
        existing.Ordine = tipo.Ordine;
        existing.Attivo = tipo.Attivo;
        await db.SaveChangesAsync();

        var utente = await _userService.GetOrCreateCurrentUserAsync();
        db.TipiAttivitaLog.Add(new TipoAttivitaLog
        {
            NomeUtente = utente.WindowsUsername,
            AzioneSvolta = "Modifica",
            NomeValore = tipo.Nome,
            VecchioValore = vecchioNome,
            NuovoValore = tipo.Nome
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Disattiva un tipo attività (soft delete) e registra il log.
    /// I tipi base (Lavoro, Permesso, Ferie) non possono essere eliminati.
    /// </summary>
    public async Task DisattivaTipoAsync(int id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.TipiAttivita.FindAsync(id)
            ?? throw new InvalidOperationException($"Tipo attività ID {id} non trovato");

        // I tipi base non possono essere eliminati
        if (existing.Nome == TipiAttivita.Lavoro || existing.Nome == TipiAttivita.Permesso || existing.Nome == TipiAttivita.Ferie)
            throw new InvalidOperationException($"Il tipo '{existing.Nome}' è un tipo base e non può essere eliminato.");

        existing.Attivo = false;
        await db.SaveChangesAsync();

        var utente = await _userService.GetOrCreateCurrentUserAsync();
        db.TipiAttivitaLog.Add(new TipoAttivitaLog
        {
            NomeUtente = utente.WindowsUsername,
            AzioneSvolta = "Elimina",
            NomeValore = existing.Nome,
            VecchioValore = existing.Nome
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Assicura che i tipi base (Lavoro, Permesso, Ferie) esistano nel DB.
    /// Chiamato all'avvio dell'applicazione.
    /// </summary>
    public async Task EnsureTipiBaseAsync()
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var esistenti = await db.TipiAttivita.Select(t => t.Nome).ToListAsync();

        var tipiBase = new[]
        {
            new TipoAttivitaItem { Nome = TipiAttivita.Lavoro, Ordine = 1 },
            new TipoAttivitaItem { Nome = TipiAttivita.Permesso, Ordine = 2 },
            new TipoAttivitaItem { Nome = TipiAttivita.Ferie, Ordine = 3 }
        };

        foreach (var tipo in tipiBase)
        {
            if (!esistenti.Contains(tipo.Nome))
                db.TipiAttivita.Add(tipo);
        }

        await db.SaveChangesAsync();
    }
}
