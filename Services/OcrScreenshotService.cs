using System.Text;
using Microsoft.EntityFrameworkCore;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using WorkActivityTracker.Data;

namespace WorkActivityTracker.Services;

/// <summary>Il riconoscimento del testo di Windows non è disponibile su questo PC (lingua non installata).</summary>
public sealed class OcrNonDisponibileException(string messaggio) : Exception(messaggio);

/// <summary>
/// Lettura del testo negli screenshot incollati negli editor (v5.7), con il riconoscimento del testo
/// integrato in Windows 10/11: gratuito, senza internet. Porting di <c>OcrWindows</c> del progetto
/// DocumentaleMarta, adattato a immagini in memoria (i base64 dell'HTML) invece che su file.
///
/// <see cref="RiconosciHtmlAsync"/> è indipendente da <c>Attivita</c>: lo usano anche gli screenshot di
/// Gestione Clienti. <see cref="ElaboraLottoAsync"/> è il lavoro in background sulle attività,
/// <see cref="ElaboraLottoClientiAmbientiAsync"/> quello sugli ambienti cliente.
/// </summary>
public sealed class OcrScreenshotService
{
    /// <summary>
    /// Ingrandimento applicato prima della lettura. Gli screenshot di interfacce hanno caratteri da
    /// 9-12 px, al limite per l'OCR. Misurato sugli screenshot reali: a 1× date e parole venivano
    /// storpiate ("13.cro" per "lavoro"), a 3× si leggono date, URL completi e testo piccolo, in
    /// 50-300 ms per immagine. Il fattore dipende dalla dimensione dei caratteri, non dell'immagine:
    /// per questo è fisso (ridotto solo se si supererebbe OcrEngine.MaxImageDimension).
    /// </summary>
    internal const double FattoreIngrandimento = 3.0;

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly UserService _userService;

    private readonly Lazy<OcrEngine?> _motore = new(() =>
        OcrEngine.TryCreateFromLanguage(new Language("it-IT")) ?? OcrEngine.TryCreateFromUserProfileLanguages());

    /// <summary>
    /// Una lettura alla volta: il pulsante 📷 del form e il lavoro in background possono chiedere
    /// l'OCR nello stesso momento, e la documentazione non garantisce l'uso concorrente del motore.
    /// </summary>
    private readonly SemaphoreSlim _unaLetturaAllaVolta = new(1, 1);

    public OcrScreenshotService(IDbContextFactory<AppDbContext> contextFactory, UserService userService)
    {
        _contextFactory = contextFactory;
        _userService = userService;
    }

    /// <summary>True se Windows ha il riconoscimento del testo per l'italiano o per una lingua dell'utente.</summary>
    public bool Disponibile => _motore.Value is not null;

    public string? MotivoNonDisponibile => Disponibile
        ? null
        : "Windows non ha il riconoscimento del testo (OCR) per l'italiano. Per attivarlo: Impostazioni → Ora e lingua → " +
          "Lingua e area geografica → Italiano → Opzioni lingua → Riconoscimento del testo.";

    private OcrEngine Motore() =>
        _motore.Value ?? throw new OcrNonDisponibileException(MotivoNonDisponibile!);

    #region Lettura

    /// <summary>Legge il testo di un'immagine (PNG, JPG, ...): una riga di testo per ogni riga riconosciuta.</summary>
    public async Task<string> RiconosciAsync(byte[] immagine, CancellationToken cancellation = default)
    {
        var motore = Motore();

        using var memoria = new MemoryStream(immagine);
        using var stream = memoria.AsRandomAccessStream();
        var decodificatore = await BitmapDecoder.CreateAsync(stream).AsTask(cancellation);
        using var bitmap = await ComeImmagineLeggibileAsync(decodificatore, cancellation);

        await _unaLetturaAllaVolta.WaitAsync(cancellation);
        try
        {
            var risultato = await motore.RecognizeAsync(bitmap).AsTask(cancellation);
            // Righe separate (OcrResult.Text le unirebbe con spazi): il testo resta leggibile nella
            // popup del form e nel dettaglio della ricerca.
            return string.Join("\n", risultato.Lines.Select(r => r.Text));
        }
        finally
        {
            _unaLetturaAllaVolta.Release();
        }
    }

    /// <summary>
    /// Legge il testo di tutti gli screenshot incorporati in un frammento HTML, separati da una riga
    /// vuota. Un'immagine illeggibile (formato non supportato, dati corrotti) viene saltata senza
    /// far fallire le altre. Stringa vuota se non ci sono screenshot.
    /// </summary>
    public async Task<string> RiconosciHtmlAsync(string? html, CancellationToken cancellation = default)
    {
        var testi = new List<string>();
        foreach (var immagine in ScreenshotHtmlHelper.EstraiImmagini(html))
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                var testo = await RiconosciAsync(immagine, cancellation);
                if (!string.IsNullOrWhiteSpace(testo)) testi.Add(testo.Trim());
            }
            catch (OcrNonDisponibileException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch { /* immagine non leggibile: la si salta */ }
        }
        return string.Join("\n\n", testi);
    }

    /// <summary>
    /// L'immagine nel formato che l'OCR vuole, ingrandita di <see cref="FattoreIngrandimento"/> senza
    /// superare la dimensione massima accettata dal motore.
    /// </summary>
    private static async Task<SoftwareBitmap> ComeImmagineLeggibileAsync(BitmapDecoder frame, CancellationToken cancellation)
    {
        var lato = Math.Max(frame.PixelWidth, frame.PixelHeight);
        var scala = Math.Min(FattoreIngrandimento, (double)OcrEngine.MaxImageDimension / lato);

        var trasformazione = new BitmapTransform();
        if (Math.Abs(scala - 1.0) > 0.01)
        {
            trasformazione.ScaledWidth = (uint)(frame.PixelWidth * scala);
            trasformazione.ScaledHeight = (uint)(frame.PixelHeight * scala);
            trasformazione.InterpolationMode = scala > 1 ? BitmapInterpolationMode.Cubic : BitmapInterpolationMode.Fant;
        }

        return await frame.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, trasformazione,
                ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage)
            .AsTask(cancellation);
    }

    #endregion

    #region Lavoro in background sulle attività

    /// <summary>Quante attività dell'utente corrente hanno screenshot ancora da leggere.</summary>
    public async Task<int> ContaDaLeggereAsync()
    {
        var utente = await _userService.GetOrCreateCurrentUserAsync();
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Attivita.CountAsync(a => a.UtenteId == utente.Id && a.OcrImpronta == null);
    }

    /// <summary>
    /// Legge gli screenshot di un lotto di attività dell'utente corrente ancora da leggere
    /// (<c>OcrImpronta</c> NULL), dalla più recente: gli ultimi mesi diventano cercabili per primi.
    /// Restituisce quante attività ha aggiornato. 0 = lavoro finito, oppure le attività del lotto sono
    /// state tutte risalvate durante la lettura: il chiamante si ferma, e le riprenderà il prossimo
    /// salvataggio o avvio (così una riga che continua a cambiare non fa girare il ciclo a vuoto).
    ///
    /// Solo le attività dell'utente corrente: ogni postazione legge le proprie, così due PC non fanno
    /// lo stesso lavoro e le attività degli utenti in PrivacyMode non passano da altri.
    /// Riprendibile: le attività lette non tornano NULL finché non cambiano i loro screenshot.
    /// </summary>
    public async Task<int> ElaboraLottoAsync(int dimensioneLotto = 5, CancellationToken cancellation = default)
    {
        Motore();   // se manca l'OCR lo si segnala subito, senza scaricare l'HTML
        var utente = await _userService.GetOrCreateCurrentUserAsync();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellation);

        // Solo i campi necessari: l'HTML con gli screenshot è la parte pesante, il resto non serve.
        var lotto = await context.Attivita
            .Where(a => a.UtenteId == utente.Id && a.OcrImpronta == null)
            .OrderByDescending(a => a.Data)
            .ThenByDescending(a => a.Id)
            .Take(dimensioneLotto)
            .Select(a => new { a.Id, a.Note, a.ChangesetCoinvolti, a.DataModifica })
            .ToListAsync(cancellation);

        var aggiornate = 0;
        foreach (var riga in lotto)
        {
            cancellation.ThrowIfCancellationRequested();

            var impronta = ScreenshotHtmlHelper.CalcolaImpronta(riga.Note, riga.ChangesetCoinvolti);
            var noteOcr = impronta.Length == 0 ? string.Empty : await RiconosciHtmlAsync(riga.Note, cancellation);
            var changesetOcr = impronta.Length == 0 ? string.Empty : await RiconosciHtmlAsync(riga.ChangesetCoinvolti, cancellation);

            // Si scrive solo se l'attività non è stata risalvata durante la lettura: altrimenti il
            // testo potrebbe riferirsi a screenshot non più presenti. In quel caso resta NULL e la
            // si rilegge al giro successivo, con il contenuto aggiornato.
            aggiornate += await context.Attivita
                .Where(a => a.Id == riga.Id && a.OcrImpronta == null && a.DataModifica == riga.DataModifica)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.NoteOcr, noteOcr)
                    .SetProperty(a => a.ChangesetOcr, changesetOcr)
                    .SetProperty(a => a.OcrImpronta, impronta), cancellation);
        }

        return aggiornate;
    }

    #endregion

    #region Lavoro in background su Gestione Clienti

    /// <summary>Quanti ambienti cliente (ClientiAmbienti) sono ancora da elaborare.</summary>
    public async Task<int> ContaClientiAmbientiDaLeggereAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ClientiAmbienti.CountAsync(ca => ca.OcrImpronta == null);
    }

    /// <summary>
    /// Elabora un lotto di ambienti cliente ancora da elaborare (<c>OcrImpronta</c> NULL): testo
    /// letto negli screenshot di "Come collegarsi" e "Dati ambiente" e, già che l'HTML è in mano,
    /// anche il testo ripulito dei due campi. Per gli ambienti esistenti prima della v5.7 è questo
    /// passaggio a calcolare il testo ripulito; dopo, lo calcola già il salvataggio.
    /// Restituisce quanti ambienti ha aggiornato (0 = lavoro finito: vedi <see cref="ElaboraLottoAsync"/>).
    ///
    /// I dati sono condivisi, quindi lo fa qualsiasi postazione, senza filtro per utente. Due PC che
    /// prendono lo stesso ambiente leggono le stesse immagini: il primo scrive, il secondo trova la
    /// riga non più NULL e non scrive nulla.
    /// </summary>
    public async Task<int> ElaboraLottoClientiAmbientiAsync(int dimensioneLotto = 5, CancellationToken cancellation = default)
    {
        Motore();   // se manca l'OCR lo si segnala subito, senza scaricare l'HTML

        await using var context = await _contextFactory.CreateDbContextAsync(cancellation);

        var lotto = await context.ClientiAmbienti
            .Where(ca => ca.OcrImpronta == null)
            .OrderBy(ca => ca.Id)
            .Take(dimensioneLotto)
            .Select(ca => new { ca.Id, ca.ComeCollegarsi, ca.DatiAmbiente, ca.DataModifica })
            .ToListAsync(cancellation);

        var aggiornati = 0;
        foreach (var riga in lotto)
        {
            cancellation.ThrowIfCancellationRequested();

            var impronta = ScreenshotHtmlHelper.CalcolaImpronta(riga.ComeCollegarsi, riga.DatiAmbiente);
            var collegarsiOcr = impronta.Length == 0 ? string.Empty : await RiconosciHtmlAsync(riga.ComeCollegarsi, cancellation);
            var datiOcr = impronta.Length == 0 ? string.Empty : await RiconosciHtmlAsync(riga.DatiAmbiente, cancellation);
            var collegarsiTesto = HtmlTextHelper.ToPlainText(riga.ComeCollegarsi);
            var datiTesto = HtmlTextHelper.ToPlainText(riga.DatiAmbiente);

            // Come per le attività: si scrive solo se l'ambiente non è stato risalvato durante la
            // lettura, altrimenti il testo potrebbe riferirsi a screenshot non più presenti.
            aggiornati += await context.ClientiAmbienti
                .Where(ca => ca.Id == riga.Id && ca.OcrImpronta == null && ca.DataModifica == riga.DataModifica)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ca => ca.ComeCollegarsiOcr, collegarsiOcr)
                    .SetProperty(ca => ca.DatiAmbienteOcr, datiOcr)
                    .SetProperty(ca => ca.ComeCollegarsiTesto, collegarsiTesto)
                    .SetProperty(ca => ca.DatiAmbienteTesto, datiTesto)
                    .SetProperty(ca => ca.OcrImpronta, impronta), cancellation);
        }

        return aggiornati;
    }

    #endregion
}
