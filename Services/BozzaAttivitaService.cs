using System.Text.Json;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Salvataggio di emergenza dell'attività in lavorazione su file locale.
/// Serve quando il database non è raggiungibile (tipicamente per una caduta della VPN):
/// il salvataggio fallisce, ma il contenuto del form — Note e Changeset con gli screenshot
/// inclusi — viene messo al sicuro su disco e ripristinabile anche dopo un riavvio dell'app.
///
/// Un solo file alla volta: con il DB irraggiungibile non si può nemmeno cambiare attività,
/// quindi in pratica la bozza pendente è sempre una sola.
/// </summary>
public class BozzaAttivitaService
{
    private const string NomeFile = "bozza_attivita.json";

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Cartella delle bozze ({BaseDirectory}\Bozze), creata se non esiste.</summary>
    public static string GetCartella()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Bozze");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Percorso completo del file di bozza.</summary>
    public static string PercorsoFile => Path.Combine(GetCartella(), NomeFile);

    /// <summary>True se esiste una bozza salvata su disco.</summary>
    public bool Esiste() => File.Exists(PercorsoFile);

    /// <summary>Data/ora di scrittura della bozza, null se non esiste.</summary>
    public DateTime? DataBozza()
    {
        try
        {
            return File.Exists(PercorsoFile) ? File.GetLastWriteTime(PercorsoFile) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Scrive la bozza su file. Non solleva eccezioni: viene chiamata proprio quando qualcosa
    /// è già andato storto, quindi un fallimento qui non deve nascondere l'errore originale.
    /// Restituisce il percorso scritto, oppure null se la scrittura non è riuscita.
    /// </summary>
    public string? Salva(WorkActivityDto dto)
    {
        try
        {
            var json = JsonSerializer.Serialize(dto, Opzioni);
            File.WriteAllText(PercorsoFile, json, System.Text.Encoding.UTF8);
            return PercorsoFile;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Rilegge la bozza dal file. Restituisce null se il file manca o non è leggibile.
    /// </summary>
    public WorkActivityDto? Leggi()
    {
        try
        {
            if (!File.Exists(PercorsoFile)) return null;
            var json = File.ReadAllText(PercorsoFile);
            return JsonSerializer.Deserialize<WorkActivityDto>(json, Opzioni);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Elimina la bozza: va chiamata dopo un salvataggio a DB andato a buon fine.
    /// Silenziosa in caso di errore (il dato è comunque già al sicuro nel database).
    /// </summary>
    public void Elimina()
    {
        try
        {
            if (File.Exists(PercorsoFile)) File.Delete(PercorsoFile);
        }
        catch { /* best effort */ }
    }
}
