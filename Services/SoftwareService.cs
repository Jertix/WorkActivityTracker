using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la lista dei software (combo "Software" del form attività, v5.6).
/// La lista è CONDIVISA fra tutti gli utenti; ogni modifica viene registrata in Software_Log
/// con utente e data. Il software di default è <c>AppSettings:NomeProgramma</c>.
/// </summary>
public class SoftwareService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly UserService _userService;

    public SoftwareService(IDbContextFactory<AppDbContext> dbFactory, UserService userService)
    {
        _dbFactory = dbFactory;
        _userService = userService;
    }

    /// <summary>
    /// Restituisce i software attivi, ordinati per Ordine poi per Nome (per la combo).
    /// </summary>
    public async Task<List<SoftwareItem>> GetSoftwareAsync()
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Software
            .Where(s => s.Attivo)
            .OrderBy(s => s.Ordine)
            .ThenBy(s => s.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// Restituisce tutti i software (inclusi non attivi), per la gestione nella modale.
    /// </summary>
    public async Task<List<SoftwareItem>> GetTuttiAsync()
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Software
            .OrderBy(s => s.Ordine)
            .ThenBy(s => s.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// Aggiunge un nuovo software e registra il log.
    /// </summary>
    public async Task<SoftwareItem> AddAsync(SoftwareItem software)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        software.Nome = software.Nome.Trim();

        var esistente = await db.Software.FirstOrDefaultAsync(s => s.Nome == software.Nome);
        if (esistente != null)
        {
            throw new InvalidOperationException(esistente.Attivo
                ? $"Il software '{esistente.Nome}' esiste già."
                : $"Il software '{esistente.Nome}' esiste già ma è disattivato: selezionalo nella griglia e spunta 'Attivo'.");
        }

        db.Software.Add(software);
        await db.SaveChangesAsync();

        await ScriviLogAsync(db, "Nuovo", software.Nome, null, Descrivi(software));
        return software;
    }

    /// <summary>
    /// Modifica un software esistente e registra il log (stato prima e dopo).
    /// Il nome nuovo compare anche sulle attività già salvate, che lo referenziano per Id.
    /// </summary>
    public async Task UpdateAsync(SoftwareItem software)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Software.FindAsync(software.Id)
            ?? throw new InvalidOperationException($"Software ID {software.Id} non trovato");

        var nuovoNome = software.Nome.Trim();
        if (await db.Software.AnyAsync(s => s.Id != software.Id && s.Nome == nuovoNome))
            throw new InvalidOperationException($"Esiste già un altro software chiamato '{nuovoNome}'.");

        var vecchioValore = Descrivi(existing);
        existing.Nome = nuovoNome;
        existing.Ordine = software.Ordine;
        existing.Attivo = software.Attivo;
        await db.SaveChangesAsync();

        await ScriviLogAsync(db, "Modifica", existing.Nome, vecchioValore, Descrivi(existing));
    }

    /// <summary>
    /// Disattiva un software (soft delete) e registra il log. Le attività che lo usano
    /// continuano a mostrarne il nome; semplicemente non compare più nella combo.
    /// </summary>
    public async Task DisattivaAsync(int id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Software.FindAsync(id)
            ?? throw new InvalidOperationException($"Software ID {id} non trovato");

        var vecchioValore = Descrivi(existing);
        existing.Attivo = false;
        await db.SaveChangesAsync();

        await ScriviLogAsync(db, "Elimina", existing.Nome, vecchioValore, Descrivi(existing));
    }

    /// <summary>
    /// Assicura che il software di default (<c>NomeProgramma</c> di appsettings) esista e sia
    /// attivo, e ne restituisce l'Id. Chiamato all'avvio di Home. Se l'inserimento collide con
    /// un altro utente che l'ha appena creato (indice unico), rilegge il record esistente.
    /// </summary>
    public async Task<int?> EnsureSoftwareDefaultAsync(string nomeProgramma)
    {
        if (string.IsNullOrWhiteSpace(nomeProgramma)) return null;
        var nome = nomeProgramma.Trim();

        using var db = await _dbFactory.CreateDbContextAsync();
        var esistente = await db.Software.FirstOrDefaultAsync(s => s.Nome == nome);

        if (esistente == null)
        {
            var nuovo = new SoftwareItem { Nome = nome, Ordine = 1, Attivo = true };
            db.Software.Add(nuovo);
            try
            {
                await db.SaveChangesAsync();
                await ScriviLogAsync(db, "Nuovo", nome, null, Descrivi(nuovo) + " (default da appsettings)");
                return nuovo.Id;
            }
            catch (DbUpdateException)
            {
                using var db2 = await _dbFactory.CreateDbContextAsync();
                return (await db2.Software.FirstOrDefaultAsync(s => s.Nome == nome))?.Id;
            }
        }

        if (!esistente.Attivo)
        {
            var vecchioValore = Descrivi(esistente);
            esistente.Attivo = true;
            await db.SaveChangesAsync();
            await ScriviLogAsync(db, "Ripristina", nome, vecchioValore, Descrivi(esistente) + " (default da appsettings)");
        }

        return esistente.Id;
    }

    private static string Descrivi(SoftwareItem s) =>
        $"Nome: {s.Nome} | Ordine: {s.Ordine} | Attivo: {(s.Attivo ? "Sì" : "No")}";

    private async Task ScriviLogAsync(AppDbContext db, string azione, string nome, string? vecchio, string? nuovo)
    {
        var utente = await _userService.GetOrCreateCurrentUserAsync();
        db.SoftwareLog.Add(new SoftwareLog
        {
            NomeUtente = utente.WindowsUsername,
            AzioneSvolta = azione,
            NomeValore = nome,
            VecchioValore = vecchio,
            NuovoValore = nuovo,
            Timestamp = DateTime.Now
        });
        await db.SaveChangesAsync();
    }
}
