using System.Text.Json;
using WorkActivityTracker.Models;

namespace WorkActivityTracker.Services;

/// <summary>
/// Carica i template dei file Readme dal file ReadmeTmplates.json nella cartella dell'eseguibile.
/// I template vengono usati per generare i file .txt di istruzioni dentro la struttura patch.
/// </summary>
public class ReadmeTemplateService
{
    private const string NomeFile = "ReadmeTmplates.json";

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Legge e deserializza i template dal JSON. Ritorna lista vuota se il file manca o è illeggibile.
    /// </summary>
    public List<ReadmeTemplate> GetTemplates()
    {
        try
        {
            var percorso = System.IO.Path.Combine(AppContext.BaseDirectory, NomeFile);
            if (!System.IO.File.Exists(percorso))
                return new List<ReadmeTemplate>();

            var json = System.IO.File.ReadAllText(percorso);
            return JsonSerializer.Deserialize<List<ReadmeTemplate>>(json, Opzioni)
                   ?? new List<ReadmeTemplate>();
        }
        catch
        {
            // File corrotto o non valido: nessun template disponibile.
            return new List<ReadmeTemplate>();
        }
    }
}
