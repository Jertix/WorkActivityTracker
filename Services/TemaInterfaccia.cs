using Microsoft.JSInterop;

namespace WorkActivityTracker.Services;

/// <summary>
/// Tema (chiaro/scuro) e colore principale dell'interfaccia (v5.7).
///
/// Il tema scuro è quello di Bootstrap 5.3 (attributo <c>data-bs-theme</c> su &lt;html&gt;); il colore
/// principale sono le variabili <c>--wat-primario*</c> di app.css, che <c>temaHelper</c> (index.html)
/// calcola dal colore qui scelto. Le palette stanno SOLO qui: il JS riceve già il colore, e i pallini
/// della finestra Impostazioni usano gli stessi valori.
/// Usato da Home (all'avvio e quando appsettings.json viene ricaricato) e dall'anteprima delle Impostazioni.
/// </summary>
public static class TemaInterfaccia
{
    /// <summary>
    /// Colori principali offerti nelle Impostazioni, nello stesso ordine di
    /// <see cref="AppConfigService.ColoriAmmessi"/>. "Classico" è il blu Bootstrap di sempre; le altre
    /// tinte vengono dal DocumentaleMarta e reggono il testo bianco sui pulsanti (contrasto ≥ 4,5).
    /// "ComeWindows" non è qui: si legge dal colore d'accento di Windows.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Palette = new Dictionary<string, string>
    {
        ["Classico"] = "#0D6EFD",
        ["Acciaio"] = "#1F6AA5",
        ["Fucina"] = "#B8500A",
        ["Foresta"] = "#2A7A4B",
        ["Prugna"] = "#7B4BA8",
        ["Grafite"] = "#475569"
    };

    /// <summary>Nomi mostrati nelle Impostazioni</summary>
    public static string NomeColore(string colore) => colore == "ComeWindows" ? "Come Windows" : colore;

    /// <summary>Il colore vero e proprio ("#RRGGBB"): quello della palette o, per "ComeWindows", l'accento di Windows.</summary>
    public static string Esadecimale(string colore) =>
        colore == "ComeWindows"
            ? ColoreAccentoWindows() ?? Palette["Classico"]
            : Palette.TryGetValue(colore, out var esa) ? esa : Palette["Classico"];

    /// <summary>
    /// Applica tema e colore alla pagina e alla finestra (barra del titolo). Non solleva eccezioni:
    /// se il JS non è ancora pronto resta l'aspetto che lo script in &lt;head&gt; ha già ripristinato.
    /// </summary>
    public static async Task ApplicaAsync(IJSRuntime js, string tema, string colore)
    {
        try { await js.InvokeVoidAsync("temaHelper.applica", tema, Esadecimale(colore)); }
        catch { /* pagina non ancora pronta */ }

        // Barra del titolo e controlli nativi: "Come Windows" lascia decidere al sistema
        var temaFinestra = tema switch
        {
            "Scuro" => AppTheme.Dark,
            "Chiaro" => AppTheme.Light,
            _ => AppTheme.Unspecified
        };
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current is { } app && app.UserAppTheme != temaFinestra)
                app.UserAppTheme = temaFinestra;
        });
    }

    /// <summary>Il colore d'accento scelto in Windows (Impostazioni → Personalizzazione → Colori), null se non leggibile.</summary>
    public static string? ColoreAccentoWindows()
    {
        try
        {
            var c = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        catch
        {
            return null;
        }
    }
}
