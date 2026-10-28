using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WorkActivityTracker.Services;

/// <summary>
/// Screenshot incollati negli editor contenteditable (v5.7): vengono salvati nell'HTML come
/// <c>&lt;img src="data:image/png;base64,…"&gt;</c>. Qui si estraggono le immagini e se ne calcola
/// l'impronta, usata per non rileggere con l'OCR gli screenshot già letti.
///
/// Volutamente indipendente da <c>Attivita</c>: lo stesso schema vale per gli screenshot di
/// Gestione Clienti (ComeCollegarsi / DatiAmbiente).
/// </summary>
public static class ScreenshotHtmlHelper
{
    /// <summary>
    /// Immagine incorporata in base64. Il payload si ferma alle virgolette: il base64 non le contiene.
    /// Le immagini da URL (http, file) non sono incorporate e non vengono lette.
    /// </summary>
    private static readonly Regex RegexImmagine = new(
        @"<img\b[^>]*?\bsrc\s*=\s*[""']data:image/[a-zA-Z0-9.+-]+;base64,([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Payload base64 delle immagini incorporate nell'HTML, nell'ordine in cui compaiono.</summary>
    public static List<string> EstraiImmaginiBase64(string? html)
    {
        if (string.IsNullOrEmpty(html)) return new List<string>();
        return RegexImmagine.Matches(html).Select(m => m.Groups[1].Value).ToList();
    }

    /// <summary>Le immagini incorporate nell'HTML, già decodificate. Le immagini con base64 non valido vengono saltate.</summary>
    public static List<byte[]> EstraiImmagini(string? html)
    {
        var risultato = new List<byte[]>();
        foreach (var base64 in EstraiImmaginiBase64(html))
        {
            try { risultato.Add(Convert.FromBase64String(base64)); }
            catch (FormatException) { /* immagine corrotta: la si salta */ }
        }
        return risultato;
    }

    /// <summary>
    /// Impronta SHA-256 (esadecimale, 64 caratteri) degli screenshot di uno o più campi HTML, oppure
    /// stringa vuota se non ce ne sono. Cambia se si aggiunge, toglie, sostituisce o riordina uno
    /// screenshot, o se lo si sposta da un campo all'altro; resta uguale se cambia solo il testo.
    /// </summary>
    public static string CalcolaImpronta(params string?[] campiHtml)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var trovate = false;

        for (var i = 0; i < campiHtml.Length; i++)
        {
            // Separatore di campo: la stessa immagine nella Note o nei Changeset dà impronte diverse
            sha.AppendData(Encoding.ASCII.GetBytes($"|campo{i}|"));
            foreach (var base64 in EstraiImmaginiBase64(campiHtml[i]))
            {
                sha.AppendData(Encoding.ASCII.GetBytes(base64));
                sha.AppendData(Encoding.ASCII.GetBytes("|"));
                trovate = true;
            }
        }

        return trovate ? Convert.ToHexString(sha.GetHashAndReset()) : string.Empty;
    }
}
