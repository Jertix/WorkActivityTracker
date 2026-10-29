using System.Text.RegularExpressions;

namespace WorkActivityTracker.Services;

/// <summary>
/// Conversione HTML → testo semplice, condivisa fra UI e servizi.
///
/// Esisteva in due copie quasi identiche (Home.razor e SearchModal.razor). È stata centralizzata
/// qui perché ora serve anche lato servizio, per alimentare le colonne di testo ricercabile
/// (`Attivita.NoteTesto` / `Attivita.ChangesetTesto`): se lo strip fosse diverso, la ricerca
/// troverebbe cose diverse da quello che l'utente vede a schermo.
///
/// Nota importante: rimuovendo i tag si eliminano anche gli `&lt;img src="data:image/...;base64,…"&gt;`,
/// cioè gli screenshot incollati. È esattamente ciò che serve: il testo ricercabile resta di pochi KB
/// invece delle centinaia di KB dell'HTML originale.
/// </summary>
public static class HtmlTextHelper
{
    // Oltre a <br>, </p>, </div> degli editor: voci di elenco, titoli, righe di tabella e blocchi di
    // codice (Markdown importato v5.8, o HTML incollato da una pagina web). L'apertura di un elenco
    // va a capo per separare la voce madre dal primo punto di un elenco annidato.
    private static readonly Regex RegexACapo =
        new(@"<br\s*/?>|</p>|</div>|</li>|</h[1-6]>|</tr>|</pre>|<(?:ul|ol)\b[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Fra due celle della stessa riga: senza separatore il testo delle colonne finirebbe attaccato
    private static readonly Regex RegexFraCelle =
        new(@"</t[dh]>(?=\s*<t[dh]\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexTag =
        new(@"<[^>]+>", RegexOptions.Compiled);

    private static readonly Regex RegexRigheVuote =
        new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>
    /// Restituisce il testo semplice di un frammento HTML: gli a capo strutturali diventano "\n",
    /// i tag vengono rimossi, le entità decodificate e le righe vuote multiple collassate.
    /// </summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // <br>, </p>, </div> → newline (i contenteditable usano <div> per gli a capo), ecc.
        var testo = RegexACapo.Replace(html, "\n");
        testo = RegexFraCelle.Replace(testo, " | ");

        // Rimuove tutti i restanti tag, immagini base64 comprese
        testo = RegexTag.Replace(testo, string.Empty);

        // Decodifica le entità HTML (&amp;, &nbsp;, &#39;, ...)
        testo = System.Net.WebUtility.HtmlDecode(testo);

        // &nbsp; diventa U+00A0 (spazio unificatore): lo si riporta a spazio normale.
        // Gli editor ne producono in abbondanza (l'indentazione inserisce 4 &nbsp; per livello) e
        // un U+00A0 nel testo ricercabile impedirebbe di ritrovare la frase digitandola con spazi
        // normali. È anche il comportamento della vecchia implementazione in Home.razor.
        testo = testo.Replace('\u00A0', ' ');

        // Normalizza le righe vuote consecutive
        testo = RegexRigheVuote.Replace(testo, "\n\n");

        return testo.Trim();
    }
}
