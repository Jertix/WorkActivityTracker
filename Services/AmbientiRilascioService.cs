using Microsoft.EntityFrameworkCore;
using WorkActivityTracker.Data;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per la gestione dei tipi ambienti e versioni di rilascio,
/// e per le coppie ambiente/versione associate alle attività.
/// Include logging di tutte le modifiche per tracciabilità.
/// </summary>
public class AmbientiRilascioService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly UserService _userService;

    public AmbientiRilascioService(IDbContextFactory<AppDbContext> contextFactory, UserService userService)
    {
        _contextFactory = contextFactory;
        _userService = userService;
    }

    #region Tipi Ambienti Rilascio

    /// <summary>
    /// Recupera tutti i tipi ambienti attivi, ordinati alfabeticamente in modo decrescente.
    /// </summary>
    public async Task<List<TipoAmbienteRilascio>> GetTipiAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.TipiAmbientiRilascio
            .Where(t => t.Attivo)
            .OrderByDescending(t => t.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// Se il tipo non esiste nella lista, lo aggiunge con log.
    /// Restituisce il nome normalizzato (trim).
    /// </summary>
    public async Task EnsureTipoExistsAsync(string nome)
    {
        var nomeTrim = nome.Trim();
        if (string.IsNullOrEmpty(nomeTrim)) return;

        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var esiste = await context.TipiAmbientiRilascio
            .AnyAsync(t => t.Nome == nomeTrim);

        if (!esiste)
        {
            context.TipiAmbientiRilascio.Add(new TipoAmbienteRilascio { Nome = nomeTrim, Attivo = true });

            context.TipiAmbientiRilascioLog.Add(new TipoAmbienteRilascioLog
            {
                NomeUtente = currentUser.WindowsUsername,
                AzioneSvolta = "Nuovo",
                NomeValore = nomeTrim,
                Timestamp = DateTime.Now
            });

            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Aggiunge un nuovo tipo ambiente con log.
    /// </summary>
    public async Task<TipoAmbienteRilascio> AddTipoAsync(string nome)
    {
        var nomeTrim = nome.Trim();
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var esiste = await context.TipiAmbientiRilascio.AnyAsync(t => t.Nome == nomeTrim);
        if (esiste)
            throw new InvalidOperationException($"Esiste già un tipo ambiente con nome '{nomeTrim}'");

        var tipo = new TipoAmbienteRilascio { Nome = nomeTrim, Attivo = true };
        context.TipiAmbientiRilascio.Add(tipo);

        context.TipiAmbientiRilascioLog.Add(new TipoAmbienteRilascioLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Nuovo",
            NomeValore = nomeTrim,
            Timestamp = DateTime.Now
        });

        await context.SaveChangesAsync();
        return tipo;
    }

    /// <summary>
    /// Aggiorna il nome di un tipo ambiente con log.
    /// </summary>
    public async Task UpdateTipoAsync(int id, string nuovoNome)
    {
        var nomeTrim = nuovoNome.Trim();
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var tipo = await context.TipiAmbientiRilascio.FindAsync(id)
            ?? throw new InvalidOperationException("Tipo ambiente non trovato");

        var vecchioNome = tipo.Nome;

        var esiste = await context.TipiAmbientiRilascio
            .AnyAsync(t => t.Nome == nomeTrim && t.Id != id);
        if (esiste)
            throw new InvalidOperationException($"Esiste già un tipo ambiente con nome '{nomeTrim}'");

        tipo.Nome = nomeTrim;

        context.TipiAmbientiRilascioLog.Add(new TipoAmbienteRilascioLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Modifica",
            NomeValore = nomeTrim,
            VecchioValore = $"Nome: {vecchioNome}",
            NuovoValore = $"Nome: {nomeTrim}",
            Timestamp = DateTime.Now
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Disattiva un tipo ambiente con log.
    /// </summary>
    public async Task DeleteTipoAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var tipo = await context.TipiAmbientiRilascio.FindAsync(id)
            ?? throw new InvalidOperationException("Tipo ambiente non trovato");

        context.TipiAmbientiRilascioLog.Add(new TipoAmbienteRilascioLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Elimina",
            NomeValore = tipo.Nome,
            Timestamp = DateTime.Now
        });

        tipo.Attivo = false;
        await context.SaveChangesAsync();
    }

    #endregion

    #region Versioni Rilascio

    /// <summary>
    /// Recupera tutte le versioni di rilascio attive, ordinate alfabeticamente in modo decrescente.
    /// </summary>
    public async Task<List<VersioneRilascio>> GetVersioniAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.VersioniRilascio
            .Where(v => v.Attivo)
            .OrderByDescending(v => v.Versione)
            .ToListAsync();
    }

    /// <summary>
    /// Se la versione non esiste nella lista, la aggiunge con log.
    /// </summary>
    public async Task EnsureVersioneExistsAsync(string versione)
    {
        var verTrim = versione.Trim();
        if (string.IsNullOrEmpty(verTrim)) return;

        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var esiste = await context.VersioniRilascio
            .AnyAsync(v => v.Versione == verTrim);

        if (!esiste)
        {
            context.VersioniRilascio.Add(new VersioneRilascio { Versione = verTrim, Attivo = true });

            context.VersioniRilascioLog.Add(new VersioneRilascioLog
            {
                NomeUtente = currentUser.WindowsUsername,
                AzioneSvolta = "Nuovo",
                ValoreVersione = verTrim,
                Timestamp = DateTime.Now
            });

            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Aggiunge una nuova versione di rilascio con log.
    /// </summary>
    public async Task<VersioneRilascio> AddVersioneAsync(string versione)
    {
        var verTrim = versione.Trim();
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var esiste = await context.VersioniRilascio.AnyAsync(v => v.Versione == verTrim);
        if (esiste)
            throw new InvalidOperationException($"Esiste già una versione '{verTrim}'");

        var ver = new VersioneRilascio { Versione = verTrim, Attivo = true };
        context.VersioniRilascio.Add(ver);

        context.VersioniRilascioLog.Add(new VersioneRilascioLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Nuovo",
            ValoreVersione = verTrim,
            Timestamp = DateTime.Now
        });

        await context.SaveChangesAsync();
        return ver;
    }

    /// <summary>
    /// Aggiorna una versione di rilascio con log.
    /// </summary>
    public async Task UpdateVersioneAsync(int id, string nuovaVersione)
    {
        var verTrim = nuovaVersione.Trim();
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var ver = await context.VersioniRilascio.FindAsync(id)
            ?? throw new InvalidOperationException("Versione non trovata");

        var vecchiaVersione = ver.Versione;

        var esiste = await context.VersioniRilascio
            .AnyAsync(v => v.Versione == verTrim && v.Id != id);
        if (esiste)
            throw new InvalidOperationException($"Esiste già una versione '{verTrim}'");

        ver.Versione = verTrim;

        context.VersioniRilascioLog.Add(new VersioneRilascioLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Modifica",
            ValoreVersione = verTrim,
            VecchioValore = $"Versione: {vecchiaVersione}",
            NuovoValore = $"Versione: {verTrim}",
            Timestamp = DateTime.Now
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Disattiva una versione di rilascio con log.
    /// </summary>
    public async Task DeleteVersioneAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        var ver = await context.VersioniRilascio.FindAsync(id)
            ?? throw new InvalidOperationException("Versione non trovata");

        context.VersioniRilascioLog.Add(new VersioneRilascioLog
        {
            NomeUtente = currentUser.WindowsUsername,
            AzioneSvolta = "Elimina",
            ValoreVersione = ver.Versione,
            Timestamp = DateTime.Now
        });

        ver.Attivo = false;
        await context.SaveChangesAsync();
    }

    #endregion

    #region Coppie Ambiente/Versione per Attività

    /// <summary>
    /// Recupera le coppie ambiente/versione associate a un'attività.
    /// Restituisce sempre 3 elementi (Posizione 1, 2, 3), con valori null se non impostati.
    /// </summary>
    public async Task<List<AmbienteRilascioDto>> GetAmbientiRilascioAsync(int attivitaId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var righe = await context.AttivitaAmbientiRilascio
            .Where(r => r.AttivitaId == attivitaId)
            .OrderBy(r => r.Posizione)
            .ToListAsync();

        // Garantisce sempre 3 coppie
        var result = new List<AmbienteRilascioDto>
        {
            new() { Posizione = 1 },
            new() { Posizione = 2 },
            new() { Posizione = 3 }
        };

        foreach (var riga in righe)
        {
            var idx = riga.Posizione - 1;
            if (idx >= 0 && idx < 3)
            {
                result[idx].TipoAmbiente = riga.TipoAmbiente;
                result[idx].Versione = riga.Versione;
            }
        }

        return result;
    }

    /// <summary>
    /// Salva le coppie ambiente/versione per un'attività.
    /// Rimuove le vecchie e inserisce le nuove.
    /// Aggiunge automaticamente ai lookup i valori nuovi (con log).
    /// </summary>
    public async Task SalvaAmbientiRilascioAsync(int attivitaId, List<AmbienteRilascioDto> coppie)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // Rimuovi le vecchie coppie
        var vecchie = context.AttivitaAmbientiRilascio.Where(r => r.AttivitaId == attivitaId);
        context.AttivitaAmbientiRilascio.RemoveRange(vecchie);

        // Inserisci le nuove (solo quelle con almeno un valore)
        foreach (var coppia in coppie)
        {
            var haValore = !string.IsNullOrWhiteSpace(coppia.TipoAmbiente)
                        || !string.IsNullOrWhiteSpace(coppia.Versione);
            if (!haValore) continue;

            context.AttivitaAmbientiRilascio.Add(new AttivitaAmbienteRilascio
            {
                AttivitaId = attivitaId,
                Posizione = coppia.Posizione,
                TipoAmbiente = string.IsNullOrWhiteSpace(coppia.TipoAmbiente) ? null : coppia.TipoAmbiente.Trim(),
                Versione = string.IsNullOrWhiteSpace(coppia.Versione) ? null : coppia.Versione.Trim()
            });
        }

        await context.SaveChangesAsync();

        // Aggiunge ai lookup i valori nuovi (fuori dalla transazione principale)
        foreach (var coppia in coppie)
        {
            if (!string.IsNullOrWhiteSpace(coppia.TipoAmbiente))
                await EnsureTipoExistsAsync(coppia.TipoAmbiente);
            if (!string.IsNullOrWhiteSpace(coppia.Versione))
                await EnsureVersioneExistsAsync(coppia.Versione);
        }
    }

    /// <summary>
    /// Recupera in UNA sola query le coppie ambiente/versione di più attività (usato dagli export,
    /// per non fare una query per riga). Include solo le posizioni effettivamente compilate:
    /// le attività senza ambienti non compaiono nel dizionario.
    /// </summary>
    public async Task<Dictionary<int, List<AmbienteRilascioDto>>> GetAmbientiRilascioBatchAsync(IEnumerable<int> attivitaIds)
    {
        var ids = attivitaIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, List<AmbienteRilascioDto>>();

        using var context = await _contextFactory.CreateDbContextAsync();

        var righe = await context.AttivitaAmbientiRilascio
            .Where(r => ids.Contains(r.AttivitaId)
                        && (r.TipoAmbiente != null || r.Versione != null))
            .OrderBy(r => r.AttivitaId)
            .ThenBy(r => r.Posizione)
            .ToListAsync();

        return righe
            .GroupBy(r => r.AttivitaId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new AmbienteRilascioDto
                {
                    Posizione = r.Posizione,
                    TipoAmbiente = r.TipoAmbiente,
                    Versione = r.Versione
                }).ToList());
    }

    /// <summary>
    /// Coppie ambiente/versione recuperate dall'ultimo ticket lavorato per un cliente,
    /// con i riferimenti all'attività di provenienza (per il messaggio all'utente).
    /// </summary>
    public record UltimiAmbientiRilascio(
        int AttivitaId,
        DateTime Data,
        string? NumeroTicket,
        string? Descrizione,
        string? UtenteUsername,
        List<AmbienteRilascioDto> Coppie);

    /// <summary>
    /// Recupera le coppie ambiente/versione dell'ULTIMA attività del cliente indicato che abbia
    /// almeno un ambiente di rilascio compilato. La ricerca è fatta su TUTTI gli utenti (come in
    /// modalità admin), escludendo però le attività degli utenti in PrivacyMode, coerentemente
    /// con quanto fa la griglia in modalità admin.
    /// Restituisce null se il cliente non ha ticket precedenti con ambienti di rilascio.
    /// </summary>
    /// <param name="clienteId">Cliente per cui cercare</param>
    /// <param name="escludiAttivitaId">Attività da escludere (tipicamente quella aperta nel form)</param>
    public async Task<UltimiAmbientiRilascio?> GetUltimiAmbientiPerClienteAsync(int clienteId, int escludiAttivitaId = 0)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var currentUser = await _userService.GetOrCreateCurrentUserAsync();

        // Gli utenti in PrivacyMode restano invisibili agli altri anche qui.
        var utentiPrivacyIds = await context.Utenti
            .Where(u => u.PrivacyMode && u.Id != currentUser.Id)
            .Select(u => u.Id)
            .ToListAsync();

        var ultima = await context.Attivita
            .Where(a => a.ClienteId == clienteId
                        && a.Id != escludiAttivitaId
                        && !utentiPrivacyIds.Contains(a.UtenteId)
                        && context.AttivitaAmbientiRilascio.Any(r =>
                               r.AttivitaId == a.Id
                               && (r.TipoAmbiente != null || r.Versione != null)))
            .OrderByDescending(a => a.Data)
            .ThenByDescending(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Data,
                a.NumeroTicket,
                a.Descrizione,
                Username = a.Utente != null ? a.Utente.WindowsUsername : null
            })
            .FirstOrDefaultAsync();

        if (ultima == null) return null;

        var coppie = await GetAmbientiRilascioAsync(ultima.Id);
        return new UltimiAmbientiRilascio(
            ultima.Id, ultima.Data, ultima.NumeroTicket, ultima.Descrizione, ultima.Username, coppie);
    }

    #endregion
}
