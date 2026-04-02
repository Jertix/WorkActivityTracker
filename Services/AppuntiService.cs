using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione degli Appunti (Knowledge Base personale).
/// Ogni utente ha i propri appunti. In modalità admin si vedono quelli di tutti.
/// </summary>
public class AppuntiService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly UserService _userService;

    public AppuntiService(IDbContextFactory<AppDbContext> contextFactory, UserService userService)
    {
        _contextFactory = contextFactory;
        _userService = userService;
    }

    /// <summary>
    /// Recupera gli appunti. In modalità admin restituisce quelli di tutti gli utenti.
    /// </summary>
    /// <param name="adminMode">Se true, restituisce gli appunti di tutti gli utenti</param>
    /// <param name="ricerca">Testo da cercare in titolo e descrizione</param>
    /// <param name="tagFiltro">Tag da filtrare (null = tutti)</param>
    public async Task<List<AppuntoItemDto>> GetAppuntiAsync(bool adminMode = false, string? ricerca = null, string? tagFiltro = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var query = context.Appunti
            .Include(a => a.Tags)
            .Include(a => a.Utente)
            .AsQueryable();

        // Filtro per utente (se non admin)
        if (!adminMode)
        {
            query = query.Where(a => a.UtenteId == currentUser.Id);
        }

        // Filtro per tag
        if (!string.IsNullOrWhiteSpace(tagFiltro))
        {
            query = query.Where(a => a.Tags.Any(t => t.NomeTag == tagFiltro));
        }

        // Filtro per testo di ricerca
        if (!string.IsNullOrWhiteSpace(ricerca))
        {
            var search = ricerca.ToLower();
            query = query.Where(a => 
                a.Titolo.ToLower().Contains(search) || 
                a.Descrizione.ToLower().Contains(search) ||
                a.Tags.Any(t => t.NomeTag.ToLower().Contains(search)));
        }

        var results = await query
            .OrderByDescending(a => a.DataModifica)
            .ToListAsync();

        return results.Select(a => new AppuntoItemDto
        {
            Id = a.Id,
            UtenteId = a.UtenteId,
            UtenteUsername = a.Utente?.WindowsUsername,
            Titolo = a.Titolo,
            Descrizione = a.Descrizione,
            Tags = a.Tags.Select(t => t.NomeTag).OrderBy(t => t).ToList(),
            TagsInput = string.Join(", ", a.Tags.Select(t => t.NomeTag).OrderBy(t => t)),
            DataCreazione = a.DataCreazione,
            DataModifica = a.DataModifica
        }).ToList();
    }

    /// <summary>
    /// Conta gli appunti dell'utente corrente.
    /// </summary>
    public async Task<int> GetAppuntiCountAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        return await context.Appunti
            .Where(a => a.UtenteId == currentUser.Id)
            .CountAsync();
    }

    /// <summary>
    /// Recupera tutti i tag distinti dell'utente (o di tutti in admin mode).
    /// </summary>
    public async Task<List<string>> GetAllTagsAsync(bool adminMode = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var query = context.AppuntiTags
            .Include(t => t.Appunto)
            .AsQueryable();

        if (!adminMode)
        {
            query = query.Where(t => t.Appunto!.UtenteId == currentUser.Id);
        }

        return await query
            .Select(t => t.NomeTag)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }

    /// <summary>
    /// Aggiunge un nuovo appunto con i relativi tag.
    /// </summary>
    public async Task<AppuntoItemDto> AddAppuntoAsync(AppuntoItemDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var appunto = new AppuntoItem
        {
            UtenteId = currentUser.Id,
            Titolo = dto.Titolo,
            Descrizione = dto.Descrizione,
            DataCreazione = DateTime.Now,
            DataModifica = DateTime.Now
        };

        // Parsing dei tag dalla stringa separata da virgole
        var tagNames = ParseTags(dto.TagsInput);
        foreach (var tag in tagNames)
        {
            appunto.Tags.Add(new AppuntoTag { NomeTag = tag });
        }

        context.Appunti.Add(appunto);
        await context.SaveChangesAsync();

        dto.Id = appunto.Id;
        dto.UtenteId = appunto.UtenteId;
        dto.Tags = tagNames;
        dto.DataCreazione = appunto.DataCreazione;
        dto.DataModifica = appunto.DataModifica;

        return dto;
    }

    /// <summary>
    /// Aggiorna un appunto esistente (solo se di proprietà dell'utente corrente).
    /// </summary>
    public async Task<AppuntoItemDto> UpdateAppuntoAsync(AppuntoItemDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var appunto = await context.Appunti
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Id == dto.Id && a.UtenteId == currentUser.Id);

        if (appunto == null)
            throw new InvalidOperationException("Appunto non trovato o non autorizzato alla modifica");

        appunto.Titolo = dto.Titolo;
        appunto.Descrizione = dto.Descrizione;
        appunto.DataModifica = DateTime.Now;

        // Rimuovi tutti i tag esistenti e ricrea
        context.AppuntiTags.RemoveRange(appunto.Tags);
        
        var tagNames = ParseTags(dto.TagsInput);
        foreach (var tag in tagNames)
        {
            appunto.Tags.Add(new AppuntoTag { NomeTag = tag, AppuntoId = appunto.Id });
        }

        await context.SaveChangesAsync();

        dto.Tags = tagNames;
        dto.DataModifica = appunto.DataModifica;
        return dto;
    }

    /// <summary>
    /// Elimina un appunto (solo se di proprietà dell'utente corrente).
    /// </summary>
    public async Task DeleteAppuntoAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var appunto = await context.Appunti
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Id == id && a.UtenteId == currentUser.Id);

        if (appunto != null)
        {
            context.AppuntiTags.RemoveRange(appunto.Tags);
            context.Appunti.Remove(appunto);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Parsing dei tag dalla stringa separata da virgole.
    /// Rimuove spazi, duplicati e stringhe vuote.
    /// </summary>
    private List<string> ParseTags(string? tagsInput)
    {
        if (string.IsNullOrWhiteSpace(tagsInput))
            return new List<string>();

        return tagsInput
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
