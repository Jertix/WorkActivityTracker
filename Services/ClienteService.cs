using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione della lista clienti.
/// Include logging di tutte le modifiche per tracciabilità.
/// </summary>
public class ClienteService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly UserService _userService;

    public ClienteService(IDbContextFactory<AppDbContext> contextFactory, UserService userService)
    {
        _contextFactory = contextFactory;
        _userService = userService;
    }

    /// <summary>
    /// Recupera tutti i clienti attivi ordinati alfabeticamente.
    /// </summary>
    public async Task<List<ClienteDto>> GetClientiAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Clienti
            .Where(c => c.Attivo)
            .OrderBy(c => c.Nome)
            .Select(c => new ClienteDto { Id = c.Id, Nome = c.Nome, Attivo = c.Attivo })
            .ToListAsync();
    }

    /// <summary>
    /// Recupera tutti i clienti (inclusi non attivi) per l'editor.
    /// </summary>
    public async Task<List<ClienteDto>> GetAllClientiAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Clienti
            .OrderBy(c => c.Nome)
            .Select(c => new ClienteDto { Id = c.Id, Nome = c.Nome, Attivo = c.Attivo })
            .ToListAsync();
    }

    /// <summary>
    /// Aggiunge un nuovo cliente con logging.
    /// </summary>
    public async Task<ClienteDto> AddClienteAsync(ClienteDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var exists = await context.Clienti.AnyAsync(c => c.Nome == dto.Nome);
        if (exists)
            throw new InvalidOperationException($"Esiste già un cliente con nome '{dto.Nome}'");

        var cliente = new Cliente { Nome = dto.Nome, Attivo = true };
        context.Clienti.Add(cliente);

        context.ClientiLog.Add(new ClienteLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Nuovo",
            NomeCliente = dto.Nome,
            NuovoValore = $"Nome: {dto.Nome}; Attivo: Sì",
            Timestamp = DateTime.Now
        });

        await context.SaveChangesAsync();
        dto.Id = cliente.Id;
        dto.Attivo = true;
        return dto;
    }

    /// <summary>
    /// Aggiorna un cliente esistente con logging.
    /// </summary>
    public async Task<ClienteDto> UpdateClienteAsync(ClienteDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var cliente = await context.Clienti.FindAsync(dto.Id)
            ?? throw new InvalidOperationException("Cliente non trovato");

        var nomeExists = await context.Clienti.AnyAsync(c => c.Nome == dto.Nome && c.Id != dto.Id);
        if (nomeExists)
            throw new InvalidOperationException($"Esiste già un altro cliente con nome '{dto.Nome}'");

        var vecchioValore = $"Nome: {cliente.Nome}; Attivo: {(cliente.Attivo ? "Sì" : "No")}";

        cliente.Nome = dto.Nome;
        cliente.Attivo = dto.Attivo;

        context.ClientiLog.Add(new ClienteLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Modifica",
            NomeCliente = dto.Nome,
            VecchioValore = vecchioValore,
            NuovoValore = $"Nome: {dto.Nome}; Attivo: {(dto.Attivo ? "Sì" : "No")}",
            Timestamp = DateTime.Now
        });

        await context.SaveChangesAsync();
        return dto;
    }

    /// <summary>
    /// Disattiva un cliente (non elimina fisicamente per preservare le relazioni).
    /// </summary>
    public async Task DeleteClienteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var cliente = await context.Clienti.FindAsync(id)
            ?? throw new InvalidOperationException("Cliente non trovato");

        context.ClientiLog.Add(new ClienteLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Elimina",
            NomeCliente = cliente.Nome,
            VecchioValore = $"Nome: {cliente.Nome}; Attivo: {(cliente.Attivo ? "Sì" : "No")}",
            Timestamp = DateTime.Now
        });

        cliente.Attivo = false;
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Recupera lo storico delle modifiche ai clienti.
    /// </summary>
    public async Task<List<ClienteLog>> GetLogAsync(int maxRecords = 100)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ClientiLog
            .OrderByDescending(l => l.Timestamp)
            .Take(maxRecords)
            .ToListAsync();
    }
}
