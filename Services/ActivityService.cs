using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione delle attività lavorative.
/// Fornisce metodi CRUD e query per le attività, con supporto per modalità admin.
/// </summary>
public class ActivityService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly UserService _userService;
    private readonly AmbientiRilascioService _ambientiRilascioService;

    public ActivityService(IDbContextFactory<AppDbContext> contextFactory, UserService userService,
        AmbientiRilascioService ambientiRilascioService)
    {
        _contextFactory = contextFactory;
        _userService = userService;
        _ambientiRilascioService = ambientiRilascioService;
    }

    #region Helper Methods

    /// <summary>
    /// Normalizza una stringa rimuovendo spazi multipli e trasformando in lowercase.
    /// Esempio: "oggi   sono    andato" diventa "oggi sono andato"
    /// </summary>
    private static string NormalizeForSearch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        
        // Rimuove spazi multipli e trasforma in lowercase
        return Regex.Replace(text.Trim(), @"\s+", " ").ToLower();
    }

    /// <summary>
    /// Verifica se un testo normalizzato contiene la stringa di ricerca normalizzata.
    /// </summary>
    private static bool ContainsNormalized(string? text, string normalizedSearch)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(normalizedSearch))
            return false;
        
        return NormalizeForSearch(text).Contains(normalizedSearch);
    }

    #endregion

    #region Metodi Lookup

    /// <summary>
    /// Recupera la lista di tutti i clienti attivi dal database.
    /// </summary>
    /// <returns>Lista ordinata alfabeticamente dei clienti attivi</returns>
    public async Task<List<Cliente>> GetClientiAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Clienti
            .Where(c => c.Attivo)
            .OrderBy(c => c.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// Recupera la lista di tutti gli ambienti attivi dal database.
    /// </summary>
    /// <returns>Lista ordinata per codice DECRESCENTE degli ambienti attivi (versione più alta prima)</returns>
    public async Task<List<Ambiente>> GetAmbientiAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Ambienti
            .Where(a => a.Attivo)
            .OrderByDescending(a => a.Codice)
            .ToListAsync();
    }

    /// <summary>
    /// Recupera la lista degli anni per cui esistono attività registrate.
    /// In modalità admin, include tutti gli anni di tutti gli utenti.
    /// </summary>
    /// <param name="adminMode">Se true, include gli anni di tutti gli utenti</param>
    /// <returns>Lista degli anni disponibili, ordinata in modo decrescente</returns>
    public async Task<List<int>> GetAnniDisponibiliAsync(bool adminMode = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();
        
        IQueryable<Attivita> query = context.Attivita;
        
        // Filtra per utente solo se non in modalità admin
        if (!adminMode)
        {
            query = query.Where(a => a.UtenteId == currentUser.Id);
        }
        
        var anni = await query
            .Select(a => a.Data.Year)
            .Distinct()
            .OrderByDescending(a => a)
            .ToListAsync();

        // Aggiungi l'anno corrente se non presente
        var annoCorrente = DateTime.Now.Year;
        if (!anni.Contains(annoCorrente))
            anni.Insert(0, annoCorrente);

        return anni;
    }

    #endregion

    #region Metodi CRUD

    /// <summary>
    /// Recupera le attività filtrate per data e/o testo di ricerca.
    /// La ricerca testuale viene effettuata su: Descrizione, URL Ticket, Vedere, Note, Changeset.
    /// </summary>
    /// <param name="anno">Anno da filtrare (opzionale)</param>
    /// <param name="mese">Mese da filtrare (opzionale)</param>
    /// <param name="giorno">Giorno da filtrare (opzionale)</param>
    /// <param name="ricerca">Testo da cercare in tutti i campi testuali (opzionale)</param>
    /// <param name="adminMode">Se true, mostra le attività di tutti gli utenti</param>
    /// <returns>Lista delle attività che soddisfano i criteri di ricerca</returns>
    public async Task<List<WorkActivityDto>> GetActivitiesAsync(
        int? anno = null, 
        int? mese = null, 
        int? giorno = null, 
        string? ricerca = null,
        bool adminMode = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        // Query base con Include per le navigation properties
        var query = context.Attivita
            .Include(a => a.Cliente)
            .Include(a => a.Utente)
            .Include(a => a.AttivitaAmbienti)
            .AsQueryable();

        // Filtra per utente solo se non in modalità admin
        if (!adminMode)
        {
            query = query.Where(a => a.UtenteId == currentUser.Id);
        }
        else
        {
            // In admin mode, escludi le attività di utenti con PrivacyMode = true
            // (eccetto quelle dell'utente corrente, sempre visibili)
            var utentiPrivacyIds = await context.Utenti
                .Where(u => u.PrivacyMode && u.Id != currentUser.Id)
                .Select(u => u.Id)
                .ToListAsync();

            if (utentiPrivacyIds.Any())
                query = query.Where(a => !utentiPrivacyIds.Contains(a.UtenteId));
        }

        // Applica filtri per data
        if (anno.HasValue)
            query = query.Where(a => a.Data.Year == anno.Value);

        if (mese.HasValue)
            query = query.Where(a => a.Data.Month == mese.Value);

        if (giorno.HasValue)
            query = query.Where(a => a.Data.Day == giorno.Value);

        // Esegui la query e ordina i risultati
        var results = await query
            .OrderByDescending(a => a.Data)
            .ThenByDescending(a => a.Id)
            .ToListAsync();

        // Ricerca testuale con normalizzazione degli spazi (eseguita in memoria)
        // Questo permette di cercare "oggi   sono  andato" e trovare "oggi sono andato"
        if (!string.IsNullOrWhiteSpace(ricerca))
        {
            var normalizedSearch = NormalizeForSearch(ricerca);
            
            results = results.Where(a =>
                ContainsNormalized(a.Descrizione, normalizedSearch) ||
                ContainsNormalized(a.UrlTicket, normalizedSearch) ||
                ContainsNormalized(a.NumeroTicket, normalizedSearch) ||
                ContainsNormalized(a.Vedere, normalizedSearch) ||
                ContainsNormalized(a.Note, normalizedSearch) ||
                ContainsNormalized(a.ChangesetCoinvolti, normalizedSearch) ||
                ContainsNormalized(a.UrlPatchRilasci, normalizedSearch) ||
                ContainsNormalized(a.Cliente?.Nome, normalizedSearch) ||
                ContainsNormalized(a.Versione, normalizedSearch) ||
                ContainsNormalized(a.CartellaDocumentazione, normalizedSearch) ||
                ContainsNormalized(a.TestoCheckIn, normalizedSearch)
            ).ToList();
        }

        // Mappa i risultati nel DTO
        var dtos = results.Select(a => new WorkActivityDto
        {
            Id = a.Id,
            Data = a.Data,
            TipoAttivita = a.TipoAttivita ?? TipiAttivita.Lavoro,
            Descrizione = a.Descrizione,
            UrlTicket = a.UrlTicket,
            NumeroTicket = a.NumeroTicket,
            ClienteId = a.ClienteId,
            ClienteNome = a.Cliente?.Nome,
            OreLavorate = a.OreLavorate,
            Versione = a.Versione,
            AmbientiSelezionatiIds = a.AttivitaAmbienti.Select(aa => aa.AmbienteId).ToList(),
            Vedere = a.Vedere,
            Note = a.Note,
            ChangesetCoinvolti = a.ChangesetCoinvolti,
            UrlPatchRilasci = a.UrlPatchRilasci,
            TestoCheckIn = a.TestoCheckIn,
            CartellaDocumentazione = a.CartellaDocumentazione,
            UtenteId = a.UtenteId,
            UtenteUsername = a.Utente?.WindowsUsername
        }).ToList();

        // Carica i nomi ambienti di rilascio per tutte le attività restituite (per la colonna griglia)
        if (dtos.Count > 0)
        {
            var ids = dtos.Select(d => d.Id).ToList();
            var ambienti = await context.AttivitaAmbientiRilascio
                .Where(r => ids.Contains(r.AttivitaId) && r.TipoAmbiente != null)
                .OrderBy(r => r.AttivitaId)
                .ThenBy(r => r.Posizione)
                .Select(r => new { r.AttivitaId, r.TipoAmbiente })
                .ToListAsync();

            var ambientiPerAttivita = ambienti
                .GroupBy(r => r.AttivitaId)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(r => r.TipoAmbiente)));

            foreach (var dto in dtos)
                dto.AmbientiRilascioNomi = ambientiPerAttivita.TryGetValue(dto.Id, out var nomi) ? nomi : null;
        }

        return dtos;
    }

    /// <summary>
    /// Recupera una singola attività per ID.
    /// In modalità normale, verifica che l'attività appartenga all'utente corrente.
    /// </summary>
    /// <param name="id">ID dell'attività da recuperare</param>
    /// <param name="adminMode">Se true, permette di recuperare attività di altri utenti</param>
    /// <returns>DTO dell'attività trovata, o null se non esiste</returns>
    public async Task<WorkActivityDto?> GetActivityByIdAsync(int id, bool adminMode = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var query = context.Attivita
            .Include(a => a.Cliente)
            .Include(a => a.Utente)
            .Include(a => a.AttivitaAmbienti)
            .AsQueryable();

        // Filtra per utente solo se non in modalità admin
        if (!adminMode)
        {
            query = query.Where(a => a.UtenteId == currentUser.Id);
        }

        var attivita = await query.FirstOrDefaultAsync(a => a.Id == id);

        if (attivita == null)
            return null;

        return new WorkActivityDto
        {
            Id = attivita.Id,
            Data = attivita.Data,
            TipoAttivita = attivita.TipoAttivita ?? TipiAttivita.Lavoro,
            Descrizione = attivita.Descrizione,
            UrlTicket = attivita.UrlTicket,
            NumeroTicket = attivita.NumeroTicket,
            ClienteId = attivita.ClienteId,
            ClienteNome = attivita.Cliente?.Nome,
            OreLavorate = attivita.OreLavorate,
            Versione = attivita.Versione,
            AmbientiSelezionatiIds = attivita.AttivitaAmbienti.Select(aa => aa.AmbienteId).ToList(),
            Vedere = attivita.Vedere,
            Note = attivita.Note,
            ChangesetCoinvolti = attivita.ChangesetCoinvolti,
            UrlPatchRilasci = attivita.UrlPatchRilasci,
            TestoCheckIn = attivita.TestoCheckIn,
            CartellaDocumentazione = attivita.CartellaDocumentazione,
            UtenteId = attivita.UtenteId,
            UtenteUsername = attivita.Utente?.WindowsUsername
        };
    }

    /// <summary>
    /// Aggiunge una nuova attività al database.
    /// L'attività viene automaticamente associata all'utente corrente.
    /// </summary>
    /// <param name="dto">DTO con i dati dell'attività da creare</param>
    /// <returns>DTO dell'attività creata con l'ID assegnato</returns>
    public async Task<WorkActivityDto> AddActivityAsync(WorkActivityDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        // Crea l'entità Attivita dal DTO
        var attivita = new Attivita
        {
            UtenteId = currentUser.Id,
            Data = dto.Data,
            TipoAttivita = dto.TipoAttivita ?? TipiAttivita.Lavoro,
            Descrizione = dto.Descrizione,
            UrlTicket = dto.UrlTicket,
            NumeroTicket = dto.NumeroTicket,
            ClienteId = dto.ClienteId,
            OreLavorate = dto.OreLavorate,
            Versione = dto.Versione,
            Vedere = dto.Vedere,
            Note = dto.Note,
            ChangesetCoinvolti = dto.ChangesetCoinvolti,
            UrlPatchRilasci = dto.UrlPatchRilasci,
            TestoCheckIn = dto.TestoCheckIn,
            CartellaDocumentazione = dto.CartellaDocumentazione,
            DataCreazione = DateTime.Now,
            DataModifica = DateTime.Now
        };

        context.Attivita.Add(attivita);
        await context.SaveChangesAsync();

        // Aggiungi gli ambienti selezionati (relazione N:N)
        foreach (var ambienteId in dto.AmbientiSelezionatiIds)
        {
            context.AttivitaAmbienti.Add(new AttivitaAmbiente
            {
                AttivitaId = attivita.Id,
                AmbienteId = ambienteId
            });
        }
        await context.SaveChangesAsync();

        // Salva le coppie ambiente/versione di rilascio
        await _ambientiRilascioService.SalvaAmbientiRilascioAsync(attivita.Id, dto.AmbientiRilascio);

        dto.Id = attivita.Id;
        dto.UtenteId = currentUser.Id;
        dto.UtenteUsername = currentUser.WindowsUsername;
        return dto;
    }

    /// <summary>
    /// Aggiorna un'attività esistente nel database.
    /// Verifica che l'attività appartenga all'utente corrente prima di modificarla.
    /// </summary>
    /// <param name="dto">DTO con i dati aggiornati dell'attività</param>
    /// <returns>DTO dell'attività aggiornata</returns>
    /// <exception cref="InvalidOperationException">Se l'attività non esiste o non appartiene all'utente</exception>
    public async Task<WorkActivityDto> UpdateActivityAsync(WorkActivityDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        // Recupera l'attività esistente con i suoi ambienti
        var attivita = await context.Attivita
            .Include(a => a.AttivitaAmbienti)
            .FirstOrDefaultAsync(a => a.Id == dto.Id && a.UtenteId == currentUser.Id);

        if (attivita == null)
            throw new InvalidOperationException("Attività non trovata o non autorizzato alla modifica");

        // Aggiorna i campi
        attivita.Data = dto.Data;
        attivita.TipoAttivita = dto.TipoAttivita ?? TipiAttivita.Lavoro;
        attivita.Descrizione = dto.Descrizione;
        attivita.UrlTicket = dto.UrlTicket;
        attivita.NumeroTicket = dto.NumeroTicket;
        attivita.ClienteId = dto.ClienteId;
        attivita.OreLavorate = dto.OreLavorate;
        attivita.Versione = dto.Versione;
        attivita.Vedere = dto.Vedere;
        attivita.Note = dto.Note;
        attivita.ChangesetCoinvolti = dto.ChangesetCoinvolti;
        attivita.UrlPatchRilasci = dto.UrlPatchRilasci;
        attivita.TestoCheckIn = dto.TestoCheckIn;
        attivita.CartellaDocumentazione = dto.CartellaDocumentazione;
        attivita.DataModifica = DateTime.Now;

        // Aggiorna gli ambienti: rimuovi i vecchi e aggiungi i nuovi
        context.AttivitaAmbienti.RemoveRange(attivita.AttivitaAmbienti);

        foreach (var ambienteId in dto.AmbientiSelezionatiIds)
        {
            context.AttivitaAmbienti.Add(new AttivitaAmbiente
            {
                AttivitaId = attivita.Id,
                AmbienteId = ambienteId
            });
        }

        await context.SaveChangesAsync();

        // Aggiorna le coppie ambiente/versione di rilascio
        await _ambientiRilascioService.SalvaAmbientiRilascioAsync(attivita.Id, dto.AmbientiRilascio);

        return dto;
    }

    /// <summary>
    /// Elimina un'attività dal database.
    /// Verifica che l'attività appartenga all'utente corrente prima di eliminarla.
    /// La cancellazione è a cascata per gli ambienti associati.
    /// </summary>
    /// <param name="id">ID dell'attività da eliminare</param>
    public async Task DeleteActivityAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var attivita = await context.Attivita
            .FirstOrDefaultAsync(a => a.Id == id && a.UtenteId == currentUser.Id);

        if (attivita != null)
        {
            context.Attivita.Remove(attivita);
            await context.SaveChangesAsync();
        }
    }

    #endregion
}
