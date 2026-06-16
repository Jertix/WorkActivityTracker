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

    /// <summary>
    /// Pattern LIKE per rilevare la parola isolata "TODO" (case-insensitive grazie alla collation SQL).
    /// Il testo viene confrontato con un padding di spazi (" " + campo + " ") per gestire i confini a
    /// inizio/fine stringa. Approssima la regex word-boundary usata in memoria.
    /// </summary>
    private const string TodoLikePattern = "%[^A-Za-z]TODO[^A-Za-z]%";

    /// <summary>Regex per "TODO" come parola isolata (usata nella modalità completa, in memoria).</summary>
    private static readonly Regex TodoWordRegex =
        new(@"(?<![A-Za-z])TODO(?![A-Za-z])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Verifica approssimata di presenza TODO nei campi (HTML grezzo, best effort).</summary>
    private static bool HasTodoApprossimato(params string?[] campi) =>
        campi.Any(c => !string.IsNullOrEmpty(c) && TodoWordRegex.IsMatch(c!));

    /// <summary>Esegue l'escape dei caratteri speciali LIKE ([ % _) per una ricerca letterale.</summary>
    private static string EscapeLike(string input) =>
        input.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

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
        bool adminMode = false,
        int? settimana = null,
        bool includeContenuto = false)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        // Query base. Le navigation property vengono risolte da Include (modalità completa)
        // oppure dalla proiezione .Select (modalità leggera/griglia).
        IQueryable<Attivita> query = context.Attivita;

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

        // Applica i filtri per data. Quando l'anno è presente (e non c'è il filtro settimana) si usa
        // un intervallo [inizio, fine): è SARGable e l'indice su Data può fare seek invece di scan.
        // Con il filtro settimana l'anno viene gestito in memoria (ISOWeek), quindi qui si saltano.
        if (!settimana.HasValue && anno.HasValue)
        {
            DateTime inizio, fine;
            if (mese.HasValue && giorno.HasValue)
            {
                inizio = new DateTime(anno.Value, mese.Value, giorno.Value);
                fine = inizio.AddDays(1);
            }
            else if (mese.HasValue)
            {
                inizio = new DateTime(anno.Value, mese.Value, 1);
                fine = inizio.AddMonths(1);
            }
            else
            {
                inizio = new DateTime(anno.Value, 1, 1);
                fine = inizio.AddYears(1);
            }
            query = query.Where(a => a.Data >= inizio && a.Data < fine);
        }
        else
        {
            // Anno assente, oppure filtro settimana attivo (l'anno viene saltato come in passato):
            // applica mese/giorno singolarmente.
            if (mese.HasValue)
                query = query.Where(a => a.Data.Month == mese.Value);
            if (giorno.HasValue)
                query = query.Where(a => a.Data.Day == giorno.Value);
        }

        // Ricerca testuale: nella modalità leggera (griglia) viene spostata su SQL con LIKE, così i campi
        // pesanti Note/Changeset vengono filtrati lato server SENZA essere trasferiti. Nella modalità
        // completa la ricerca è fatta in memoria più sotto (con normalizzazione degli spazi multipli).
        if (!includeContenuto && !string.IsNullOrWhiteSpace(ricerca))
        {
            var termine = Regex.Replace(ricerca.Trim(), @"\s+", " ");
            var like = "%" + EscapeLike(termine) + "%";
            query = query.Where(a =>
                EF.Functions.Like(a.Descrizione, like) ||
                (a.UrlTicket != null && EF.Functions.Like(a.UrlTicket, like)) ||
                (a.NumeroTicket != null && EF.Functions.Like(a.NumeroTicket, like)) ||
                (a.Vedere != null && EF.Functions.Like(a.Vedere, like)) ||
                (a.Note != null && EF.Functions.Like(a.Note, like)) ||
                (a.ChangesetCoinvolti != null && EF.Functions.Like(a.ChangesetCoinvolti, like)) ||
                (a.UrlPatchRilasci != null && EF.Functions.Like(a.UrlPatchRilasci, like)) ||
                (a.Versione != null && EF.Functions.Like(a.Versione, like)) ||
                (a.CartellaDocumentazione != null && EF.Functions.Like(a.CartellaDocumentazione, like)) ||
                (a.TestoCheckIn != null && EF.Functions.Like(a.TestoCheckIn, like)) ||
                (a.Cliente != null && EF.Functions.Like(a.Cliente.Nome, like)));
        }

        List<WorkActivityDto> dtos;

        if (includeContenuto)
        {
            // ===== MODALITÀ COMPLETA: carica anche Note/Changeset (ricerca avanzata, export TXT) =====
            var results = await query
                .Include(a => a.Cliente)
                .Include(a => a.Utente)
                .Include(a => a.AttivitaAmbienti)
                .OrderByDescending(a => a.Data)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            // Filtro settimana (ISO 8601) in memoria — EF non traduce ISOWeek
            if (settimana.HasValue)
            {
                results = results.Where(a =>
                    System.Globalization.ISOWeek.GetWeekOfYear(a.Data) == settimana.Value
                    && (!anno.HasValue || System.Globalization.ISOWeek.GetYear(a.Data) == anno.Value)
                ).ToList();
            }

            // Ricerca testuale con normalizzazione degli spazi (in memoria)
            // Permette di cercare "oggi   sono  andato" e trovare "oggi sono andato"
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

            dtos = results.Select(a => new WorkActivityDto
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
                UtenteUsername = a.Utente?.WindowsUsername,
                HasTodo = HasTodoApprossimato(a.Descrizione, a.UrlTicket, a.Note, a.ChangesetCoinvolti)
            }).ToList();
        }
        else
        {
            // ===== MODALITÀ LEGGERA (griglia): NON carica Note/Changeset (campi pesanti con screenshot) =====
            // I contenuti pesanti restano null e vengono caricati on-demand (selezione/copia/export).
            // HasTodo è calcolato lato SQL con un LIKE word-boundary (best effort sull'HTML grezzo).
            var lista = await query
                .OrderByDescending(a => a.Data)
                .ThenByDescending(a => a.Id)
                .Select(a => new WorkActivityDto
                {
                    Id = a.Id,
                    Data = a.Data,
                    TipoAttivita = a.TipoAttivita ?? TipiAttivita.Lavoro,
                    Descrizione = a.Descrizione,
                    UrlTicket = a.UrlTicket,
                    NumeroTicket = a.NumeroTicket,
                    ClienteId = a.ClienteId,
                    ClienteNome = a.Cliente != null ? a.Cliente.Nome : null,
                    OreLavorate = a.OreLavorate,
                    Versione = a.Versione,
                    AmbientiSelezionatiIds = a.AttivitaAmbienti.Select(aa => aa.AmbienteId).ToList(),
                    Vedere = a.Vedere,
                    UrlPatchRilasci = a.UrlPatchRilasci,
                    TestoCheckIn = a.TestoCheckIn,
                    CartellaDocumentazione = a.CartellaDocumentazione,
                    UtenteId = a.UtenteId,
                    UtenteUsername = a.Utente != null ? a.Utente.WindowsUsername : null
                    // Note/ChangesetCoinvolti NON caricati; HasTodo calcolato sotto con una query
                    // batch dedicata (più semplice da tradurre per EF di un bool nella proiezione).
                })
                .ToListAsync();

            // Filtro settimana (ISO 8601) in memoria
            if (settimana.HasValue)
            {
                lista = lista.Where(a =>
                    System.Globalization.ISOWeek.GetWeekOfYear(a.Data) == settimana.Value
                    && (!anno.HasValue || System.Globalization.ISOWeek.GetYear(a.Data) == anno.Value)
                ).ToList();
            }

            // HasTodo calcolato con una query batch lato SQL (LIKE word-boundary) sugli Id restituiti:
            // evita di trasferire Note/Changeset ma consente comunque di evidenziare/filtrare i TODO.
            if (lista.Count > 0)
            {
                var idsLista = lista.Select(d => d.Id).ToList();
                var idsConTodo = (await context.Attivita
                    .Where(a => idsLista.Contains(a.Id) && (
                        EF.Functions.Like(" " + a.Descrizione + " ", TodoLikePattern) ||
                        (a.UrlTicket != null && EF.Functions.Like(" " + a.UrlTicket + " ", TodoLikePattern)) ||
                        (a.Note != null && EF.Functions.Like(" " + a.Note + " ", TodoLikePattern)) ||
                        (a.ChangesetCoinvolti != null && EF.Functions.Like(" " + a.ChangesetCoinvolti + " ", TodoLikePattern))))
                    .Select(a => a.Id)
                    .ToListAsync()).ToHashSet();

                foreach (var d in lista)
                    d.HasTodo = idsConTodo.Contains(d.Id);
            }

            dtos = lista;
        }

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
