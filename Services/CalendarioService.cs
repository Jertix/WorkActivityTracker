using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione degli eventi del calendario con avvisi configurabili.
/// </summary>
public class CalendarioService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly UserService _userService;

    public CalendarioService(IDbContextFactory<AppDbContext> dbFactory, UserService userService)
    {
        _dbFactory = dbFactory;
        _userService = userService;
    }

    /// <summary>
    /// Restituisce tutti gli eventi dell'utente corrente (inclusi risolti).
    /// </summary>
    public async Task<List<EventoCalendarioDto>> GetEventiAsync()
    {
        var utente = await _userService.GetOrCreateCurrentUserAsync();
        using var db = await _dbFactory.CreateDbContextAsync();
        var eventi = await db.EventiCalendario
            .Where(e => e.UtenteId == utente.Id)
            .OrderBy(e => e.DataEvento)
            .ToListAsync();

        return eventi.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Restituisce gli eventi di un giorno specifico dell'utente corrente.
    /// </summary>
    public async Task<List<EventoCalendarioDto>> GetEventiDelGiornoAsync(DateTime giorno)
    {
        var utente = await _userService.GetOrCreateCurrentUserAsync();
        using var db = await _dbFactory.CreateDbContextAsync();
        var data = giorno.Date;
        var eventi = await db.EventiCalendario
            .Where(e => e.UtenteId == utente.Id && e.DataEvento.Date == data)
            .OrderBy(e => e.DataEvento)
            .ToListAsync();

        return eventi.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Restituisce gli eventi imminenti (non risolti) che rientrano nella finestra di avviso.
    /// Usato per la barra di notifica in Home.razor.
    /// </summary>
    public async Task<List<EventoCalendarioDto>> GetEventiImminentiAsync()
    {
        var utente = await _userService.GetOrCreateCurrentUserAsync();
        using var db = await _dbFactory.CreateDbContextAsync();
        var oggi = DateTime.Today;
        var eventi = await db.EventiCalendario
            .Where(e => e.UtenteId == utente.Id && !e.Risolto)
            .OrderBy(e => e.DataEvento)
            .ToListAsync();

        // Filtra: mostra solo eventi entro la finestra di avviso (giorni rimanenti <= GiorniPrimaAvviso)
        // Oppure eventi già passati (per mostrare il grigio "X giorni fa")
        return eventi
            .Select(MapToDto)
            .Where(e => e.GiorniRimanenti <= e.GiorniPrimaAvviso)
            .ToList();
    }

    /// <summary>
    /// Restituisce i giorni del mese che hanno almeno un evento (per evidenziarli nel calendario).
    /// </summary>
    public async Task<HashSet<DateTime>> GetGiorniConEventiAsync(int anno, int mese)
    {
        var utente = await _userService.GetOrCreateCurrentUserAsync();
        using var db = await _dbFactory.CreateDbContextAsync();
        var giorni = await db.EventiCalendario
            .Where(e => e.UtenteId == utente.Id
                     && e.DataEvento.Year == anno
                     && e.DataEvento.Month == mese)
            .Select(e => e.DataEvento.Date)
            .Distinct()
            .ToListAsync();

        return new HashSet<DateTime>(giorni);
    }

    /// <summary>
    /// Aggiunge un nuovo evento al calendario.
    /// </summary>
    public async Task<EventoCalendarioDto> AddEventoAsync(EventoCalendarioDto dto)
    {
        var utente = await _userService.GetOrCreateCurrentUserAsync();
        using var db = await _dbFactory.CreateDbContextAsync();

        var evento = new EventoCalendario
        {
            UtenteId = utente.Id,
            Descrizione = dto.Descrizione,
            DataEvento = dto.DataEvento,
            GiorniPrimaAvviso = dto.GiorniPrimaAvviso,
            Risolto = false
        };

        db.EventiCalendario.Add(evento);
        await db.SaveChangesAsync();

        return MapToDto(evento);
    }

    /// <summary>
    /// Aggiorna un evento esistente.
    /// </summary>
    public async Task UpdateEventoAsync(EventoCalendarioDto dto)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var evento = await db.EventiCalendario.FindAsync(dto.Id)
            ?? throw new InvalidOperationException($"Evento ID {dto.Id} non trovato");

        evento.Descrizione = dto.Descrizione;
        evento.DataEvento = dto.DataEvento;
        evento.GiorniPrimaAvviso = dto.GiorniPrimaAvviso;
        evento.Risolto = dto.Risolto;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Segna un evento come risolto.
    /// </summary>
    public async Task SegnaRisoltoAsync(int id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var evento = await db.EventiCalendario.FindAsync(id)
            ?? throw new InvalidOperationException($"Evento ID {id} non trovato");

        evento.Risolto = true;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Elimina un evento dal calendario.
    /// </summary>
    public async Task DeleteEventoAsync(int id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var evento = await db.EventiCalendario.FindAsync(id);
        if (evento != null)
        {
            db.EventiCalendario.Remove(evento);
            await db.SaveChangesAsync();
        }
    }

    private static EventoCalendarioDto MapToDto(EventoCalendario e) => new()
    {
        Id = e.Id,
        UtenteId = e.UtenteId,
        Descrizione = e.Descrizione,
        DataEvento = e.DataEvento,
        GiorniPrimaAvviso = e.GiorniPrimaAvviso,
        Risolto = e.Risolto
    };
}
