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
    ///
    /// Proiezione **LEGGERA**: `ComeCollegarsi` e `DatiAmbiente` (NVARCHAR(MAX) con gli screenshot
    /// incollati, in base64) NON vengono caricati. La griglia non li mostra, e trasferirli per tutte
    /// le righe significava spostare decine di MB a ogni apertura della finestra — su un database
    /// raggiunto via VPN erano i 30-60 secondi di attesa prima di vedere l'elenco.
    /// Il contenuto completo di una riga si ottiene on-demand con <see cref="GetByIdAsync"/>.
    ///
    /// Con <paramref name="testo"/> (casella di ricerca della griglia, v5.7) il filtro gira lato SQL
    /// sui campi semplici, sul testo ripulito dei due editor e, con
    /// <paramref name="cercaNegliScreenshot"/>, sul testo letto negli screenshot. Gli spazi del termine
    /// fanno da jolly (<see cref="ActivityService.BuildLikePattern"/>, come nella ricerca delle attività).
    /// </summary>
    public async Task<List<ClienteAmbienteDto>> GetAllAsync(string? testo = null, bool cercaNegliScreenshot = true)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var righe = from ca in context.ClientiAmbienti
                    join c in context.Clienti on ca.ClienteId equals c.Id
                    select new { ca, c.Nome };

        var like = string.IsNullOrWhiteSpace(testo) ? null : ActivityService.BuildLikePattern(testo);
        if (like != null)
        {
            righe = righe.Where(r =>
                EF.Functions.Like(r.Nome, like) ||
                EF.Functions.Like(r.ca.Ambiente, like) ||
                (r.ca.ApplicationServer != null && EF.Functions.Like(r.ca.ApplicationServer, like)) ||
                (r.ca.DatabaseServer != null && EF.Functions.Like(r.ca.DatabaseServer, like)) ||
                (r.ca.PersoneRiferimento != null && EF.Functions.Like(r.ca.PersoneRiferimento, like)) ||
                (r.ca.DirectoryInstallazione != null && EF.Functions.Like(r.ca.DirectoryInstallazione, like)) ||
                (r.ca.InformazioniPool != null && EF.Functions.Like(r.ca.InformazioniPool, like)) ||
                (r.ca.NumeroVersione != null && EF.Functions.Like(r.ca.NumeroVersione, like)) ||
                // Testo dei due editor: colonne ripulite. Finché un ambiente non è stato elaborato
                // (testo NULL) si ricade sull'HTML originale, così non si perdono risultati.
                (r.ca.ComeCollegarsiTesto != null && EF.Functions.Like(r.ca.ComeCollegarsiTesto, like)) ||
                (r.ca.ComeCollegarsiTesto == null && r.ca.ComeCollegarsi != null && EF.Functions.Like(r.ca.ComeCollegarsi, like)) ||
                (r.ca.DatiAmbienteTesto != null && EF.Functions.Like(r.ca.DatiAmbienteTesto, like)) ||
                (r.ca.DatiAmbienteTesto == null && r.ca.DatiAmbiente != null && EF.Functions.Like(r.ca.DatiAmbiente, like)) ||
                (cercaNegliScreenshot && r.ca.ComeCollegarsiOcr != null && EF.Functions.Like(r.ca.ComeCollegarsiOcr, like)) ||
                (cercaNegliScreenshot && r.ca.DatiAmbienteOcr != null && EF.Functions.Like(r.ca.DatiAmbienteOcr, like)));
        }

        var conScreenshot = like != null && cercaNegliScreenshot;
        var query = from r in righe
                    orderby r.Nome, r.ca.Ambiente
                    select new ClienteAmbienteDto
                    {
                        Id = r.ca.Id,
                        ClienteId = r.ca.ClienteId,
                        NomeCliente = r.Nome,
                        Ambiente = r.ca.Ambiente,
                        ApplicationServer = r.ca.ApplicationServer,
                        DatabaseServer = r.ca.DatabaseServer,
                        PersoneRiferimento = r.ca.PersoneRiferimento,
                        DirectoryInstallazione = r.ca.DirectoryInstallazione,
                        InformazioniPool = r.ca.InformazioniPool,
                        TipoVersione = r.ca.TipoVersione,
                        NumeroVersione = r.ca.NumeroVersione,
                        DataModifica = r.ca.DataModifica,
                        TrovatoNegliScreenshot = conScreenshot &&
                            ((r.ca.ComeCollegarsiOcr != null && EF.Functions.Like(r.ca.ComeCollegarsiOcr, like!)) ||
                             (r.ca.DatiAmbienteOcr != null && EF.Functions.Like(r.ca.DatiAmbienteOcr, like!)))
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

        // Ordinamento finale in memoria: la query ha già un ORDER BY, ma qui è indipendente
        // dalla collation del database e neutralizza eventuali spazi iniziali nei nomi
        // (un nome salvato come " Allianz" finirebbe altrimenti in cima all'elenco).
        return list
            .OrderBy(d => (d.NomeCliente ?? string.Empty).Trim(), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(d => (d.Ambiente ?? string.Empty).Trim(), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Recupera il record COMPLETO di un singolo ambiente cliente, campi ricchi inclusi.
    /// Da usare quando si seleziona una riga della griglia: si paga il trasferimento degli
    /// screenshot solo per la riga che serve davvero.
    /// </summary>
    public async Task<ClienteAmbienteDto?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        return await (from ca in context.ClientiAmbienti
                      join c in context.Clienti on ca.ClienteId equals c.Id
                      where ca.Id == id
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
                          DataModifica = ca.DataModifica,
                          // Testo degli screenshot già letto (v5.7): lo usa il pulsante 📷 senza rileggerli
                          ComeCollegarsiOcr = ca.ComeCollegarsiOcr,
                          DatiAmbienteOcr = ca.DatiAmbienteOcr,
                          OcrImpronta = ca.OcrImpronta
                      }).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Solo testo OCR e impronta di un ambiente (v5.7), senza l'HTML con gli screenshot. Usato dal
    /// pulsante 📷 quando la lettura in background è finita dopo l'apertura del record.
    /// </summary>
    public async Task<(string? ComeCollegarsiOcr, string? DatiAmbienteOcr, string? OcrImpronta)?> GetTestoOcrAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var riga = await context.ClientiAmbienti
            .Where(ca => ca.Id == id)
            .Select(ca => new { ca.ComeCollegarsiOcr, ca.DatiAmbienteOcr, ca.OcrImpronta })
            .FirstOrDefaultAsync();
        return riga == null ? null : (riga.ComeCollegarsiOcr, riga.DatiAmbienteOcr, riga.OcrImpronta);
    }

    /// <summary>
    /// Recupera i record COMPLETI (campi ricchi inclusi) di tutti gli ambienti di UN cliente,
    /// ordinati per nome ambiente. Usato dall'export PDF del cliente.
    /// </summary>
    public async Task<List<ClienteAmbienteDto>> GetByClienteAsync(int clienteId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        return await (from ca in context.ClientiAmbienti
                      join c in context.Clienti on ca.ClienteId equals c.Id
                      where ca.ClienteId == clienteId
                      orderby ca.Ambiente
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
                      }).ToListAsync();
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
        // Testo ricercabile e stato degli screenshot (v5.7): l'OCR li legge in background
        AggiornaTestoEStatoOcr(entity);
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
        CopiaStatoOcr(entity, dto);
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
        // Rilettura OCR solo se gli screenshot sono cambiati (stessa impronta = testo ancora valido)
        AggiornaTestoEStatoOcr(entity);

        dto.NomeCliente = nuovoNomeCliente;
        dto.DataModifica = entity.DataModifica;
        CopiaStatoOcr(entity, dto);

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
    /// Allinea testo ricercabile e stato OCR (v5.7) al contenuto appena impostato dei due campi ricchi.
    /// Il testo ripulito si calcola subito (pochi millisecondi); per gli screenshot si calcola solo
    /// l'impronta, come <c>ActivityService.AggiornaStatoOcr</c>: la lettura vera la fa
    /// <see cref="OcrScreenshotService"/> in background, così il salvataggio non aspetta l'OCR.
    /// - stessa impronta già salvata → testo OCR ancora valido, niente da rileggere;
    /// - nessuno screenshot → impronta e testi OCR vuoti;
    /// - screenshot cambiati → impronta NULL, da leggere. I vecchi testi OCR restano finché la
    ///   rilettura non li sostituisce, così nel frattempo la ricerca non ha buchi.
    /// </summary>
    private static void AggiornaTestoEStatoOcr(ClienteAmbiente entity)
    {
        entity.ComeCollegarsiTesto = HtmlTextHelper.ToPlainText(entity.ComeCollegarsi);
        entity.DatiAmbienteTesto = HtmlTextHelper.ToPlainText(entity.DatiAmbiente);

        var impronta = ScreenshotHtmlHelper.CalcolaImpronta(entity.ComeCollegarsi, entity.DatiAmbiente);
        if (entity.OcrImpronta == impronta) return;

        if (impronta.Length == 0)
        {
            entity.OcrImpronta = string.Empty;
            entity.ComeCollegarsiOcr = string.Empty;
            entity.DatiAmbienteOcr = string.Empty;
        }
        else
        {
            entity.OcrImpronta = null;
        }
    }

    /// <summary>Riporta nel DTO restituito al form lo stato OCR dopo il salvataggio (per il pulsante 📷).</summary>
    private static void CopiaStatoOcr(ClienteAmbiente entity, ClienteAmbienteDto dto)
    {
        dto.ComeCollegarsiOcr = entity.ComeCollegarsiOcr;
        dto.DatiAmbienteOcr = entity.DatiAmbienteOcr;
        dto.OcrImpronta = entity.OcrImpronta;
    }

    /// <summary>
    /// Serializza un DTO in una stringa compatta per il log (Vecchio/NuovoValore).
    /// </summary>
    private static string Serialize(ClienteAmbienteDto d) =>
        $"Cliente: {d.NomeCliente} | Ambiente: {d.Ambiente} | "
      + $"AppServer: {d.ApplicationServer} | DbServer: {d.DatabaseServer} | "
      + $"Persone: {d.PersoneRiferimento} | ComeCollegarsi: {TroncaPerLog(d.ComeCollegarsi)} | "
      + $"DirInstallazione: {d.DirectoryInstallazione} | InfoPool: {d.InformazioniPool} | "
      + $"TipoVersione: {d.TipoVersione} | NumeroVersione: {d.NumeroVersione}";

    /// <summary>
    /// Riduce un campo ricco a una riga breve per il log. `ComeCollegarsi` è HTML con gli
    /// screenshot in base64: scriverlo per intero in VecchioValore E NuovoValore duplicava
    /// tutte le immagini nella tabella di log a ogni singolo salvataggio.
    /// </summary>
    private static string TroncaPerLog(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return "-";
        var unaRiga = valore.Replace("\r\n", " ").Replace("\n", " ").Trim();
        return unaRiga.Length <= 200 ? unaRiga : unaRiga.Substring(0, 200) + "… [troncato]";
    }
}
