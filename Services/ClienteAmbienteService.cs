using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione degli ambienti per cliente (Application Server,
/// Database Server, persone di riferimento, istruzioni di collegamento).
/// I dati sono CONDIVISI tra tutti gli utenti dell'applicazione.
/// Include logging di tutte le modifiche per tracciabilità.
/// </summary>
public class ClienteAmbienteService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly UserService _userService;

    public ClienteAmbienteService(IDbContextFactory<AppDbContext> contextFactory, UserService userService)
    {
        _contextFactory = contextFactory;
        _userService = userService;
    }

    /// <summary>
    /// Recupera tutti gli ambienti per tutti i clienti, ordinati per nome cliente e ambiente.
    /// </summary>
    public async Task<List<ClienteAmbienteDto>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var query = from ca in context.ClientiAmbienti
                    join c in context.Clienti on ca.ClienteId equals c.Id
                    orderby c.Nome, ca.Ambiente
                    select new ClienteAmbienteDto
                    {
                        Id = ca.Id,
                        ClienteId = ca.ClienteId,
                        NomeCliente = c.Nome,
                        Ambiente = ca.Ambiente,
                        ApplicationServer = ca.ApplicationServer,
                        DatabaseServer = ca.DatabaseServer,
                        PersoneRiferimento = ca.PersoneRiferimento,
                        ComeCollegarsi = ca.ComeCollegarsi,
                        DatiAmbiente = ca.DatiAmbiente,
                        DirectoryInstallazione = ca.DirectoryInstallazione,
                        InformazioniPool = ca.InformazioniPool,
                        TipoVersione = ca.TipoVersione,
                        NumeroVersione = ca.NumeroVersione,
                        DataModifica = ca.DataModifica
                    };

        var list = await query.ToListAsync();

        // Recupera l'ultimo utente che ha modificato ogni (ClienteId, Ambiente) dal log
        var ultimiUtenti = await context.ClientiAmbientiLog
            .GroupBy(l => new { l.ClienteId, l.Ambiente })
            .Select(g => new
            {
                g.Key.ClienteId,
                g.Key.Ambiente,
                UltimoUtente = g.OrderByDescending(x => x.Timestamp).First().NomeUtente
            })
            .ToListAsync();

        var dict = ultimiUtenti.ToDictionary(
            k => (k.ClienteId ?? 0, k.Ambiente ?? string.Empty),
            v => v.UltimoUtente);

        foreach (var dto in list)
        {
            if (dict.TryGetValue((dto.ClienteId, dto.Ambiente), out var utente))
                dto.UltimoUtente = utente;
        }

        return list;
    }

    /// <summary>
    /// Recupera la lista dei nomi ambiente distinti già usati (per datalist di suggerimento).
    /// </summary>
    public async Task<List<string>> GetAmbientiDistintiAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ClientiAmbienti
            .Select(ca => ca.Ambiente)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }

    /// <summary>
    /// Aggiunge un nuovo ambiente per cliente con logging.
    /// </summary>
    public async Task<ClienteAmbienteDto> AddAsync(ClienteAmbienteDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        if (dto.ClienteId <= 0)
            throw new InvalidOperationException("Selezionare un cliente");
        if (string.IsNullOrWhiteSpace(dto.Ambiente))
            throw new InvalidOperationException("L'ambiente è obbligatorio");

        var exists = await context.ClientiAmbienti
            .AnyAsync(x => x.ClienteId == dto.ClienteId && x.Ambiente == dto.Ambiente);
        if (exists)
            throw new InvalidOperationException(
                $"Esiste già un ambiente '{dto.Ambiente}' per il cliente selezionato.");

        var entity = new ClienteAmbiente
        {
            ClienteId = dto.ClienteId,
            Ambiente = dto.Ambiente.Trim(),
            ApplicationServer = string.IsNullOrWhiteSpace(dto.ApplicationServer) ? null : dto.ApplicationServer.Trim(),
            DatabaseServer = string.IsNullOrWhiteSpace(dto.DatabaseServer) ? null : dto.DatabaseServer.Trim(),
            PersoneRiferimento = string.IsNullOrWhiteSpace(dto.PersoneRiferimento) ? null : dto.PersoneRiferimento,
            ComeCollegarsi = string.IsNullOrWhiteSpace(dto.ComeCollegarsi) ? null : dto.ComeCollegarsi,
            DatiAmbiente = string.IsNullOrWhiteSpace(dto.DatiAmbiente) ? null : dto.DatiAmbiente,
            DirectoryInstallazione = string.IsNullOrWhiteSpace(dto.DirectoryInstallazione) ? null : dto.DirectoryInstallazione,
            InformazioniPool = string.IsNullOrWhiteSpace(dto.InformazioniPool) ? null : dto.InformazioniPool,
            TipoVersione = string.IsNullOrWhiteSpace(dto.TipoVersione) ? null : dto.TipoVersione.Trim(),
            NumeroVersione = string.IsNullOrWhiteSpace(dto.NumeroVersione) ? null : dto.NumeroVersione.Trim(),
            DataModifica = DateTime.Now
        };
        context.ClientiAmbienti.Add(entity);

        // Risolve il nome cliente per il DTO di ritorno e per il log
        var nomeCliente = await context.Clienti
            .Where(c => c.Id == dto.ClienteId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync() ?? string.Empty;
        dto.NomeCliente = nomeCliente;

        context.ClientiAmbientiLog.Add(new ClienteAmbienteLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Nuovo",
            ClienteId = dto.ClienteId,
            Ambiente = entity.Ambiente,
            NuovoValore = Serialize(dto),
            Timestamp = DateTime.Now
        });

        await context.SaveChangesAsync();

        dto.Id = entity.Id;
        dto.DataModifica = entity.DataModifica;
        return dto;
    }

    /// <summary>
    /// Aggiorna un ambiente per cliente esistente con logging.
    /// </summary>
    public async Task<ClienteAmbienteDto> UpdateAsync(ClienteAmbienteDto dto)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var entity = await context.ClientiAmbienti.FindAsync(dto.Id)
            ?? throw new InvalidOperationException("Ambiente cliente non trovato");

        if (dto.ClienteId <= 0)
            throw new InvalidOperationException("Selezionare un cliente");
        if (string.IsNullOrWhiteSpace(dto.Ambiente))
            throw new InvalidOperationException("L'ambiente è obbligatorio");

        var duplicato = await context.ClientiAmbienti
            .AnyAsync(x => x.ClienteId == dto.ClienteId
                        && x.Ambiente == dto.Ambiente
                        && x.Id != dto.Id);
        if (duplicato)
            throw new InvalidOperationException(
                $"Esiste già un ambiente '{dto.Ambiente}' per il cliente selezionato.");

        // Carica il nome cliente (per log + DTO ritorno)
        var nomeClienteCorrente = await context.Clienti
            .Where(c => c.Id == entity.ClienteId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync() ?? string.Empty;

        var vecchioDto = new ClienteAmbienteDto
        {
            Id = entity.Id,
            ClienteId = entity.ClienteId,
            NomeCliente = nomeClienteCorrente,
            Ambiente = entity.Ambiente,
            ApplicationServer = entity.ApplicationServer,
            DatabaseServer = entity.DatabaseServer,
            PersoneRiferimento = entity.PersoneRiferimento,
            ComeCollegarsi = entity.ComeCollegarsi,
            DatiAmbiente = entity.DatiAmbiente,
            DirectoryInstallazione = entity.DirectoryInstallazione,
            InformazioniPool = entity.InformazioniPool,
            TipoVersione = entity.TipoVersione,
            NumeroVersione = entity.NumeroVersione,
            DataModifica = entity.DataModifica
        };
        var vecchioValore = Serialize(vecchioDto);

        var nuovoNomeCliente = await context.Clienti
            .Where(c => c.Id == dto.ClienteId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync() ?? string.Empty;

        entity.ClienteId = dto.ClienteId;
        entity.Ambiente = dto.Ambiente.Trim();
        entity.ApplicationServer = string.IsNullOrWhiteSpace(dto.ApplicationServer) ? null : dto.ApplicationServer.Trim();
        entity.DatabaseServer = string.IsNullOrWhiteSpace(dto.DatabaseServer) ? null : dto.DatabaseServer.Trim();
        entity.PersoneRiferimento = string.IsNullOrWhiteSpace(dto.PersoneRiferimento) ? null : dto.PersoneRiferimento;
        entity.ComeCollegarsi = string.IsNullOrWhiteSpace(dto.ComeCollegarsi) ? null : dto.ComeCollegarsi;
        entity.DatiAmbiente = string.IsNullOrWhiteSpace(dto.DatiAmbiente) ? null : dto.DatiAmbiente;
        entity.DirectoryInstallazione = string.IsNullOrWhiteSpace(dto.DirectoryInstallazione) ? null : dto.DirectoryInstallazione;
        entity.InformazioniPool = string.IsNullOrWhiteSpace(dto.InformazioniPool) ? null : dto.InformazioniPool;
        entity.TipoVersione = string.IsNullOrWhiteSpace(dto.TipoVersione) ? null : dto.TipoVersione.Trim();
        entity.NumeroVersione = string.IsNullOrWhiteSpace(dto.NumeroVersione) ? null : dto.NumeroVersione.Trim();
        entity.DataModifica = DateTime.Now;

        dto.NomeCliente = nuovoNomeCliente;
        dto.DataModifica = entity.DataModifica;

        context.ClientiAmbientiLog.Add(new ClienteAmbienteLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Modifica",
            ClienteId = dto.ClienteId,
            Ambiente = entity.Ambiente,
            VecchioValore = vecchioValore,
            NuovoValore = Serialize(dto),
            Timestamp = DateTime.Now
        });

        await context.SaveChangesAsync();
        return dto;
    }

    /// <summary>
    /// Elimina fisicamente un ambiente per cliente con logging.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var entity = await context.ClientiAmbienti.FindAsync(id)
            ?? throw new InvalidOperationException("Ambiente cliente non trovato");

        var nomeCliente = await context.Clienti
            .Where(c => c.Id == entity.ClienteId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync() ?? string.Empty;

        var vecchio = new ClienteAmbienteDto
        {
            Id = entity.Id,
            ClienteId = entity.ClienteId,
            NomeCliente = nomeCliente,
            Ambiente = entity.Ambiente,
            ApplicationServer = entity.ApplicationServer,
            DatabaseServer = entity.DatabaseServer,
            PersoneRiferimento = entity.PersoneRiferimento,
            ComeCollegarsi = entity.ComeCollegarsi,
            DatiAmbiente = entity.DatiAmbiente,
            DirectoryInstallazione = entity.DirectoryInstallazione,
            InformazioniPool = entity.InformazioniPool,
            TipoVersione = entity.TipoVersione,
            NumeroVersione = entity.NumeroVersione,
            DataModifica = entity.DataModifica
        };

        context.ClientiAmbientiLog.Add(new ClienteAmbienteLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Elimina",
            ClienteId = entity.ClienteId,
            Ambiente = entity.Ambiente,
            VecchioValore = Serialize(vecchio),
            Timestamp = DateTime.Now
        });

        context.ClientiAmbienti.Remove(entity);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Recupera lo storico delle modifiche agli ambienti cliente.
    /// </summary>
    public async Task<List<ClienteAmbienteLog>> GetLogAsync(int maxRecords = 100)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ClientiAmbientiLog
            .OrderByDescending(l => l.Timestamp)
            .Take(maxRecords)
            .ToListAsync();
    }

    /// <summary>
    /// Serializza un DTO in una stringa compatta per il log (Vecchio/NuovoValore).
    /// </summary>
    private static string Serialize(ClienteAmbienteDto d) =>
        $"Cliente: {d.NomeCliente} | Ambiente: {d.Ambiente} | "
      + $"AppServer: {d.ApplicationServer} | DbServer: {d.DatabaseServer} | "
      + $"Persone: {d.PersoneRiferimento} | ComeCollegarsi: {d.ComeCollegarsi} | "
      + $"DirInstallazione: {d.DirectoryInstallazione} | InfoPool: {d.InformazioniPool} | "
      + $"TipoVersione: {d.TipoVersione} | NumeroVersione: {d.NumeroVersione}";
}
