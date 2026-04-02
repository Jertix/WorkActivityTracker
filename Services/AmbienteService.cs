using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione degli ambienti/congelati.
/// Include logging di tutte le modifiche per tracciabilità.
/// </summary>
public class AmbienteService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly UserService _userService;

    public AmbienteService(IDbContextFactory<AppDbContext> contextFactory, UserService userService)
    {
        _contextFactory = contextFactory;
        _userService = userService;
    }

    /// <summary>
    /// Recupera tutti gli ambienti attivi ordinati per codice decrescente.
    /// </summary>
    public async Task<List<AmbienteDto>> GetAmbientiAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var results = await context.Ambienti
            .Where(a => a.Attivo)
            .OrderByDescending(a => a.Codice)
            .ToListAsync();

        return results.Select(a => new AmbienteDto
        {
            Id = a.Id,
            Codice = a.Codice,
            Descrizione = a.Descrizione,
            DataCongelamento = a.DataCongelamento,
            DataDismissione = a.DataDismissione,
            Attivo = a.Attivo
        }).ToList();
    }

    /// <summary>
    /// Recupera tutti gli ambienti (inclusi non attivi) per l'editor.
    /// </summary>
    public async Task<List<AmbienteDto>> GetAllAmbientiAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var results = await context.Ambienti
            .OrderByDescending(a => a.Codice)
            .ToListAsync();

        return results.Select(a => new AmbienteDto
        {
            Id = a.Id,
            Codice = a.Codice,
            Descrizione = a.Descrizione,
            DataCongelamento = a.DataCongelamento,
            DataDismissione = a.DataDismissione,
            Attivo = a.Attivo
        }).ToList();
    }

    /// <summary>
    /// Aggiunge un nuovo ambiente/congelato.
    /// </summary>
    public async Task<AmbienteDto> AddAmbienteAsync(AmbienteDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        // Verifica che il codice non esista già
        var exists = await context.Ambienti.AnyAsync(a => a.Codice == dto.Codice);
        if (exists)
            throw new InvalidOperationException($"Esiste già un ambiente con codice '{dto.Codice}'");

        var ambiente = new Ambiente
        {
            Codice = dto.Codice,
            Descrizione = dto.Descrizione,
            DataCongelamento = dto.DataCongelamento,
            DataDismissione = null,
            Attivo = true
        };

        context.Ambienti.Add(ambiente);

        // Log dell'azione
        var log = new AmbienteLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Nuovo",
            Codice = dto.Codice,
            Descrizione = dto.Descrizione,
            DataCongelamento = dto.DataCongelamento,
            Timestamp = DateTime.Now
        };
        context.AmbientiLog.Add(log);

        await context.SaveChangesAsync();

        dto.Id = ambiente.Id;
        dto.Attivo = true;

        return dto;
    }

    /// <summary>
    /// Aggiorna un ambiente esistente.
    /// </summary>
    public async Task<AmbienteDto> UpdateAmbienteAsync(AmbienteDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var ambiente = await context.Ambienti.FindAsync(dto.Id);
        if (ambiente == null)
            throw new InvalidOperationException("Ambiente non trovato");

        // Verifica che il nuovo codice non sia già usato da un altro ambiente
        var codeExists = await context.Ambienti
            .AnyAsync(a => a.Codice == dto.Codice && a.Id != dto.Id);
        if (codeExists)
            throw new InvalidOperationException($"Esiste già un altro ambiente con codice '{dto.Codice}'");

        // Cattura i valori PRIMA della modifica per il log
        var vecchioValore = $"Codice: {ambiente.Codice}; Versione: {ambiente.Descrizione ?? "-"}; DataCongelamento: {ambiente.DataCongelamento?.ToString("dd/MM/yyyy") ?? "-"}; DataDismissione: {ambiente.DataDismissione?.ToString("dd/MM/yyyy") ?? "-"}; Attivo: {(ambiente.Attivo ? "Sì" : "No")}";

        ambiente.Codice = dto.Codice;
        ambiente.Descrizione = dto.Descrizione;
        ambiente.DataCongelamento = dto.DataCongelamento;
        ambiente.DataDismissione = dto.DataDismissione;
        ambiente.Attivo = dto.Attivo;

        var nuovoValore = $"Codice: {dto.Codice}; Versione: {dto.Descrizione ?? "-"}; DataCongelamento: {dto.DataCongelamento?.ToString("dd/MM/yyyy") ?? "-"}; DataDismissione: {dto.DataDismissione?.ToString("dd/MM/yyyy") ?? "-"}; Attivo: {(dto.Attivo ? "Sì" : "No")}";

        // Log dell'azione con vecchio e nuovo valore
        var log = new AmbienteLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Modifica",
            Codice = dto.Codice,
            Descrizione = dto.Descrizione,
            DataCongelamento = dto.DataCongelamento,
            Timestamp = DateTime.Now,
            VecchioValore = vecchioValore,
            NuovoValore = nuovoValore
        };
        context.AmbientiLog.Add(log);

        await context.SaveChangesAsync();

        return dto;
    }

    /// <summary>
    /// Elimina (disattiva) un ambiente.
    /// Non elimina fisicamente per preservare le relazioni esistenti.
    /// </summary>
    public async Task DeleteAmbienteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var ambiente = await context.Ambienti.FindAsync(id);
        if (ambiente == null)
            throw new InvalidOperationException("Ambiente non trovato");

        // Log dell'azione PRIMA di disattivare
        var log = new AmbienteLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Elimina",
            Codice = ambiente.Codice,
            Descrizione = ambiente.Descrizione,
            DataCongelamento = ambiente.DataCongelamento,
            Timestamp = DateTime.Now
        };
        context.AmbientiLog.Add(log);

        // Disattiva invece di eliminare fisicamente
        ambiente.Attivo = false;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Recupera lo storico delle modifiche agli ambienti.
    /// </summary>
    public async Task<List<AmbienteLog>> GetLogAsync(int maxRecords = 100)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        return await context.AmbientiLog
            .OrderByDescending(l => l.Timestamp)
            .Take(maxRecords)
            .ToListAsync();
    }
}
