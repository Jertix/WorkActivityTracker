using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione delle segnalazioni bug e richieste di modifica.
/// </summary>
public class SegnalazioneService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly UserService _userService;

    public SegnalazioneService(IDbContextFactory<AppDbContext> contextFactory, UserService userService)
    {
        _contextFactory = contextFactory;
        _userService = userService;
    }

    /// <summary>
    /// Recupera tutte le segnalazioni con utente e risposte, ordinate per data decrescente.
    /// </summary>
    public async Task<List<SegnalazioneDto>> GetSegnalazioniAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var segnalazioni = await context.Segnalazioni
            .Include(s => s.Utente)
            .Include(s => s.Risposte)
                .ThenInclude(r => r.Utente)
            .OrderByDescending(s => s.DataRichiesta)
            .ToListAsync();

        return segnalazioni.Select(s => new SegnalazioneDto
        {
            Id = s.Id,
            UtenteId = s.UtenteId,
            UtenteUsername = s.Utente?.WindowsUsername ?? s.Utente?.NomeCompleto ?? "?",
            TestoSegnalazione = s.TestoSegnalazione,
            DataRichiesta = s.DataRichiesta,
            Stato = s.Stato,
            Risposte = s.Risposte
                .OrderBy(r => r.DataRisposta)
                .Select(r => new SegnalazioneRispostaDto
                {
                    Id = r.Id,
                    SegnalazioneId = r.SegnalazioneId,
                    UtenteId = r.UtenteId,
                    UtenteUsername = r.Utente?.WindowsUsername ?? r.Utente?.NomeCompleto ?? "?",
                    TestoRisposta = r.TestoRisposta,
                    NuovoStato = r.NuovoStato,
                    DataRisposta = r.DataRisposta
                }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Restituisce il numero di segnalazioni con stato diverso da "Risolto".
    /// Usato per il badge sul pulsante Segnala nella toolbar principale.
    /// </summary>
    public async Task<int> GetSegnalazioniNonRisolteCountAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Segnalazioni
            .CountAsync(s => s.Stato != StatiSegnalazione.Risolto);
    }

    /// <summary>
    /// Recupera tutti gli utenti del sistema (per la combo nel form segnalazioni).
    /// </summary>
    public async Task<List<Utente>> GetUtentiAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Utenti
            .Where(u => u.Attivo)
            .OrderBy(u => u.WindowsUsername)
            .ToListAsync();
    }

    /// <summary>
    /// Inserisce una nuova segnalazione.
    /// </summary>
    public async Task<SegnalazioneDto> AddSegnalazioneAsync(int utenteId, string testo)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var segnalazione = new Segnalazione
        {
            UtenteId = utenteId,
            TestoSegnalazione = testo,
            DataRichiesta = DateTime.Now,
            Stato = StatiSegnalazione.InAttesa
        };

        context.Segnalazioni.Add(segnalazione);
        await context.SaveChangesAsync();

        var utente = await context.Utenti.FindAsync(utenteId);
        return new SegnalazioneDto
        {
            Id = segnalazione.Id,
            UtenteId = utenteId,
            UtenteUsername = utente?.WindowsUsername ?? "?",
            TestoSegnalazione = testo,
            DataRichiesta = segnalazione.DataRichiesta,
            Stato = segnalazione.Stato
        };
    }

    /// <summary>
    /// Aggiunge una risposta a una segnalazione e aggiorna lo stato.
    /// </summary>
    public async Task<SegnalazioneRispostaDto> AddRispostaAsync(int segnalazioneId, string testo, string nuovoStato)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var segnalazione = await context.Segnalazioni.FindAsync(segnalazioneId)
            ?? throw new InvalidOperationException("Segnalazione non trovata");

        var risposta = new SegnalazioneRisposta
        {
            SegnalazioneId = segnalazioneId,
            UtenteId = currentUser.Id,
            TestoRisposta = testo,
            NuovoStato = nuovoStato,
            DataRisposta = DateTime.Now
        };

        segnalazione.Stato = nuovoStato;

        context.SegnalazioniRisposte.Add(risposta);
        await context.SaveChangesAsync();

        return new SegnalazioneRispostaDto
        {
            Id = risposta.Id,
            SegnalazioneId = segnalazioneId,
            UtenteId = currentUser.Id,
            UtenteUsername = currentUser.WindowsUsername,
            TestoRisposta = testo,
            NuovoStato = nuovoStato,
            DataRisposta = risposta.DataRisposta
        };
    }
}
