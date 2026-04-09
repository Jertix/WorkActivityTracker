using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione dello storico versioni dei campi editor
/// (Note e ChangesetCoinvolti). Ogni salvataggio di un'attività produce
/// una voce nel log, consentendo la navigazione undo/redo e il recupero
/// di versioni precedenti.
/// </summary>
public class EditorHistoryService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public EditorHistoryService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Salva una nuova voce nello storico per il campo specificato.
    /// Viene chiamato automaticamente dopo ogni salvataggio riuscito di un'attività.
    /// </summary>
    /// <param name="attivitaId">ID dell'attività salvata</param>
    /// <param name="utenteId">ID dell'utente che ha eseguito il salvataggio</param>
    /// <param name="campo">Nome del campo: 'Note' o 'ChangesetCoinvolti'</param>
    /// <param name="contenuto">Contenuto HTML del campo al momento del salvataggio</param>
    /// <param name="dataSalvataggio">Timestamp del salvataggio</param>
    public async Task SalvaAsync(
        int attivitaId,
        int utenteId,
        string campo,
        string? contenuto,
        DateTime dataSalvataggio)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.EditorHistory.Add(new EditorHistoryEntry
        {
            AttivitaId      = attivitaId,
            UtenteId        = utenteId,
            Campo           = campo,
            Contenuto       = contenuto,
            DataSalvataggio = dataSalvataggio
        });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Restituisce lo storico versioni per un'attività e un campo specifici,
    /// ordinato dalla versione più recente alla più vecchia.
    /// </summary>
    /// <param name="attivitaId">ID dell'attività</param>
    /// <param name="campo">Nome del campo: 'Note' o 'ChangesetCoinvolti'</param>
    /// <returns>Lista di voci di storico ordinate per data discendente</returns>
    public async Task<List<EditorHistoryEntry>> GetStoricoAsync(int attivitaId, string campo)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.EditorHistory
            .Where(e => e.AttivitaId == attivitaId && e.Campo == campo)
            .OrderByDescending(e => e.DataSalvataggio)
            .ToListAsync();
    }

    /// <summary>
    /// Restituisce il contenuto dell'ultima voce salvata per l'attività e il campo specificati,
    /// oppure null se non esiste nessuna voce. Usato per evitare di salvare duplicati consecutivi.
    /// </summary>
    /// <param name="attivitaId">ID dell'attività</param>
    /// <param name="campo">Nome del campo: 'Note' o 'ChangesetCoinvolti'</param>
    public async Task<string?> GetUltimoContenutoAsync(int attivitaId, string campo)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.EditorHistory
            .Where(e => e.AttivitaId == attivitaId && e.Campo == campo)
            .OrderByDescending(e => e.DataSalvataggio)
            .Select(e => e.Contenuto)
            .FirstOrDefaultAsync();
    }
}
