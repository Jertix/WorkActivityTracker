using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.EmphasisExtras;

namespace WorkActivityTracker.Services;

/// <summary>
/// Conversione Markdown → HTML per l'editor "Descrizione dettagliata lavoro svolto" (v5.8).
///
/// Serve a importare i piani in Markdown (tipicamente quelli preparati da Claude) senza passare
/// da un visualizzatore esterno. L'HTML di Markdig non va bene così com'è per l'editor:
/// - l'editor ha <c>white-space: pre</c>, quindi gli a capo che Markdig mette fra un tag di blocco
///   e l'altro diventerebbero righe vuote: vanno tolti (tranne dentro i blocchi di codice);
/// - gli stili sono scritti nei tag e non affidati a classi CSS, perché lo stesso HTML finisce nel DB
///   e poi nel PDF, nelle anteprime e nella Ricerca Avanzata, dove app.css non sempre c'è.
///   I colori sono fissi: l'editor resta un "foglio" bianco anche nel tema scuro;
/// - le checkbox delle liste di controllo diventano ☐ / ☑ (una vera casella in un contenteditable
///   sarebbe cliccabile, e nel testo semplice sparirebbe);
/// - le immagini diventano testo: un percorso locale non si vedrebbe nell'app;
/// - i link restano solo se http/https/mailto.
/// Tutti i tag che queste regex trattano li genera Markdig: l'HTML scritto nel file è disattivato
/// (<c>DisableHtml</c>) e arriva come testo, con i caratteri &lt; &gt; già convertiti in entità.
/// </summary>
public static class MarkdownHelper
{
    /// <summary>Estensioni accettate dalla scelta del file e dal trascinamento (passate anche al JS).</summary>
    public static readonly string[] EstensioniAmmesse = { ".md", ".markdown" };

    /// <summary>Oltre questa dimensione il file non viene importato: un piano pesa pochi KB.</summary>
    public const int DimensioneMassimaByte = 2 * 1024 * 1024;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()       // l'eventuale intestazione "---" in testa al file non viene mostrata
        .UsePipeTables()
        // Solo ~~barrato~~: con il pedice attivo, "~/.claude/plans e ~/x" diventerebbe testo in basso
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseTaskLists()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

    // Stili applicati ai tag di Markdig. white-space: normal sui blocchi di testo: l'editor è in
    // "pre", e senza un paragrafo lungo resterebbe su una sola riga con lo scorrimento orizzontale.
    private const string Testo = "white-space: normal;";
    private const string Bordo = "1px solid #c8ccd0";
    private const string Monospazio = "font-family: Consolas, 'Courier New', monospace; font-size: 0.92em;";

    private static readonly Dictionary<string, string> Stili = new()
    {
        ["h1"] = $"font-size: 1.45em; font-weight: 700; margin: 0.6em 0 0.3em; padding-bottom: 0.15em; border-bottom: 1px solid #d0d7de; {Testo}",
        ["h2"] = $"font-size: 1.3em; font-weight: 700; margin: 0.6em 0 0.3em; padding-bottom: 0.15em; border-bottom: 1px solid #d0d7de; {Testo}",
        ["h3"] = $"font-size: 1.15em; font-weight: 700; margin: 0.5em 0 0.25em; {Testo}",
        ["h4"] = $"font-size: 1em; font-weight: 700; margin: 0.5em 0 0.2em; {Testo}",
        ["h5"] = $"font-size: 1em; font-weight: 700; margin: 0.5em 0 0.2em; {Testo}",
        ["h6"] = $"font-size: 1em; font-weight: 700; margin: 0.5em 0 0.2em; color: #57606a; {Testo}",
        ["p"] = $"margin: 0 0 0.5em; {Testo}",
        ["ul"] = $"margin: 0 0 0.5em; padding-left: 1.6em; {Testo}",
        ["ol"] = $"margin: 0 0 0.5em; padding-left: 1.6em; {Testo}",
        ["li"] = $"margin: 0.1em 0; {Testo}",
        ["blockquote"] = $"margin: 0 0 0.5em; padding: 0.1em 0.8em; border-left: 4px solid #d0d7de; color: #57606a; {Testo}",
        ["table"] = $"border-collapse: collapse; margin: 0 0 0.5em; {Testo}",
        ["th"] = $"border: {Bordo}; padding: 2px 8px; background-color: #f0f2f4; font-weight: 700; text-align: left; vertical-align: top; {Testo}",
        ["td"] = $"border: {Bordo}; padding: 2px 8px; vertical-align: top; {Testo}",
        ["hr"] = $"border: 0; border-top: {Bordo}; opacity: 1; margin: 0.6em 0;",
        ["code"] = $"{Monospazio} color: #b4234a; background-color: #f3f4f6; border: 1px solid #e1e4e8; border-radius: 3px; padding: 0 3px;",
    };

    private const string StilePre =
        $"{Monospazio} color: #24292f; background-color: #f6f8fa; border: 1px solid #d0d7de; border-radius: 4px; padding: 6px 8px; margin: 0 0 0.5em; white-space: pre; overflow-x: auto;";

    private const string StileLink = "color: #0b5cad; text-decoration: underline;";

    // Liste di controllo: Markdig scrive <input disabled="disabled" type="checkbox" [checked="checked"] />
    private static readonly Regex RegexCheckbox =
        new(@"<input\b[^>]*\btype=""checkbox""[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Blocchi di codice: il <code> interno non serve (e prenderebbe lo stile del codice in linea)
    private static readonly Regex RegexInizioPre =
        new(@"<pre><code\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexFinePre =
        new(@"\n?</code></pre>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexImmagine =
        new(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexAlt =
        new(@"\balt=""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Markdig non annida i link, quindi il primo </a> chiude quello aperto
    private static readonly Regex RegexLink =
        new(@"<a\b[^>]*?\bhref=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex RegexTagConStile =
        new(@"<(h[1-6]|p|ul|ol|li|blockquote|table|th|td|hr|code)\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexClasseId =
        new(@"\s(?:class|id)=""[^""]*""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexAttributoStile =
        new(@"\sstyle=""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexSegmentiPre =
        new(@"(<pre\b[\s\S]*?</pre>)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A capo subito prima o subito dopo un tag di blocco
    private static readonly Regex RegexACapoBlocchi =
        new(@"\n(?=</?(?:p|h[1-6]|ul|ol|li|blockquote|table|thead|tbody|tr|th|td|hr|pre)\b)"
          + @"|(?<=</?(?:p|h[1-6]|ul|ol|li|blockquote|table|thead|tbody|tr|th|td|hr|br|pre)\b[^>]*>)\n",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Converte il Markdown nell'HTML da inserire nell'editor Note. Stringa vuota se non c'è nulla
    /// da mostrare (file vuoto o con la sola intestazione YAML).
    /// </summary>
    public static string ToHtmlPerEditor(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        var html = Markdown.ToHtml(markdown.Replace("\r\n", "\n"), Pipeline);

        html = RegexCheckbox.Replace(html, m =>
            m.Value.Contains("checked", StringComparison.OrdinalIgnoreCase) ? "☑" : "☐");

        html = RegexInizioPre.Replace(html, $"<pre style=\"{StilePre}\">");
        html = RegexFinePre.Replace(html, "</pre>");

        html = RegexImmagine.Replace(html, m =>
        {
            var alt = RegexAlt.Match(m.Value).Groups[1].Value;
            return alt.Length > 0 ? $"[immagine: {alt}]" : "[immagine]";
        });

        html = RegexLink.Replace(html, m =>
            IsLinkSicuro(m.Groups[1].Value)
                ? $"<a href=\"{m.Groups[1].Value}\" style=\"{StileLink}\">{m.Groups[2].Value}</a>"
                : m.Groups[2].Value);

        html = RegexTagConStile.Replace(html, ApplicaStile);

        // Gli a capo si tolgono solo fuori dai blocchi di codice: lì sono le righe del codice.
        // Quelli che restano sono gli a capo "morbidi" dentro un paragrafo, che in Markdown valgono
        // uno spazio: lasciati come "\n" il testo semplice (export TXT, ricerca) andrebbe a capo.
        var segmenti = RegexSegmentiPre.Split(html);
        for (int i = 0; i < segmenti.Length; i += 2)
        {
            segmenti[i] = RegexACapoBlocchi.Replace(segmenti[i], string.Empty).Replace('\n', ' ');
        }

        return string.Concat(segmenti).Trim();
    }

    /// <summary>
    /// Testo del file: UTF-8 (con o senza BOM) o UTF-16 con BOM; se non è UTF-8 valido lo si legge
    /// come ANSI occidentale (Windows-1252), la codifica "ANSI" di Notepad++ su un Windows italiano.
    /// </summary>
    public static string Decodifica(byte[] contenuto)
    {
        try
        {
            using var lettore = new StreamReader(new MemoryStream(contenuto),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);
            return lettore.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetString(contenuto);
        }
    }

    /// <summary>True se il nome del file ha una delle <see cref="EstensioniAmmesse"/>.</summary>
    public static bool IsFileMarkdown(string? nomeFile) =>
        EstensioniAmmesse.Contains(Path.GetExtension(nomeFile ?? string.Empty), StringComparer.OrdinalIgnoreCase);

    // href è già codificato da Markdig (&amp; ecc.), basta guardare lo schema
    private static bool IsLinkSicuro(string href) =>
        href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Aggiunge lo stile al tag, togliendo class/id (senza app.css non significano nulla).
    /// Uno stile già presente — l'allineamento delle colonne delle tabelle — va in coda, così vince.
    /// </summary>
    private static string ApplicaStile(Match m)
    {
        var tag = m.Groups[1].Value.ToLowerInvariant();
        var attributi = m.Groups[2].Value;
        var stile = Stili[tag];

        // Voce di una lista di controllo: il pallino accanto a ☐ / ☑ sarebbe di troppo
        if (tag == "li" && attributi.Contains("task-list-item", StringComparison.OrdinalIgnoreCase))
            stile = "list-style: none; " + stile;

        attributi = RegexClasseId.Replace(attributi, string.Empty);
        var esistente = RegexAttributoStile.Match(attributi);
        if (esistente.Success)
        {
            stile += " " + esistente.Groups[1].Value;
            attributi = attributi.Remove(esistente.Index, esistente.Length);
        }

        // Restano gli attributi utili (es. start="3" di un elenco numerato); "/" dei tag vuoti come <hr />
        attributi = attributi.TrimEnd().TrimEnd('/').TrimEnd();
        return $"<{tag}{attributi} style=\"{stile}\">";
    }
}
