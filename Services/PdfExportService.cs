using Microsoft.Web.WebView2.Core;

namespace WorkActivityTracker.Services;

/// <summary>
/// Servizio per l'esportazione in PDF di contenuti HTML.
/// Usa un CoreWebView2 off-screen e la stampa nativa (PrintToPdfAsync) per rendere
/// fedelmente HTML+CSS+immagini base64 (le stesse presenti nei campi Note/Changeset).
/// L'app è solo Windows e WebView2 è già disponibile via BlazorWebView.
/// </summary>
public class PdfExportService
{
    /// <summary>
    /// Environment WebView2 condiviso, catturato da BlazorWebView all'avvio (MainPage).
    /// Nello stesso processo tutti gli environment devono condividere cartella e opzioni:
    /// riusarlo evita l'errore HRESULT 0x8007139F ("resource is not in the correct state").
    /// </summary>
    public CoreWebView2Environment? SharedEnvironment { get; set; }

    /// <summary>
    /// Renderizza l'HTML fornito in un file PDF nel percorso indicato.
    /// </summary>
    public async Task ExportHtmlToPdfAsync(string html, string pdfPath)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // NavigateToString ha un limite ~2MB: le immagini base64 lo sforano facilmente.
            // Si scrive l'HTML su file temporaneo e si naviga via file://.
            var tempHtml = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"wat_pdf_{Guid.NewGuid():N}.html");
            await System.IO.File.WriteAllTextAsync(tempHtml, html, System.Text.Encoding.UTF8);

            CoreWebView2CompositionController? controller = null;
            try
            {
                // Riusa l'environment di BlazorWebView (stesse opzioni/cartella del processo).
                var env = SharedEnvironment
                    ?? throw new InvalidOperationException("Environment WebView2 non disponibile (BlazorWebView non inizializzato).");
                var hwnd = GetMainWindowHandle();
                var windowRef = CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)hwnd);
                // L'app è WinUI 3: WebView2 usa i composition controller. Un windowed controller
                // sullo stesso environment darebbe HRESULT 0x8007139F (stato non valido).
                // Non serve attaccare RootVisualTarget: per la stampa basta un viewport (Bounds).
                controller = await env.CreateCoreWebView2CompositionControllerAsync(windowRef);
                controller.Bounds = new Windows.Foundation.Rect(0, 0, 816, 1056); // ~Letter @96dpi
                var core = controller.CoreWebView2;

                // Attende il completamento della navigazione prima di stampare.
                var tcs = new TaskCompletionSource<bool>();
                void OnNav(object? s, CoreWebView2NavigationCompletedEventArgs e) => tcs.TrySetResult(e.IsSuccess);
                core.NavigationCompleted += OnNav;
                core.Navigate(new Uri(tempHtml).AbsoluteUri);
                var ok = await tcs.Task;
                core.NavigationCompleted -= OnNav;
                if (!ok)
                    throw new InvalidOperationException("Navigazione WebView2 fallita durante la generazione del PDF.");

                // Lascia completare il layout/decodifica delle immagini.
                await Task.Delay(200);

                var settings = env.CreatePrintSettings();
                settings.ShouldPrintBackgrounds = true;
                var stampato = await core.PrintToPdfAsync(pdfPath, settings);
                if (!stampato)
                    throw new InvalidOperationException("PrintToPdfAsync ha restituito esito negativo.");
            }
            finally
            {
                controller?.Close();
                try { System.IO.File.Delete(tempHtml); } catch { /* best effort */ }
            }
        });
    }

    /// <summary>
    /// Recupera l'HWND della finestra principale WinUI (necessario per il controller WebView2).
    /// </summary>
    private static IntPtr GetMainWindowHandle()
    {
        var win = Application.Current?.Windows.FirstOrDefault();
        var native = win?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        return WinRT.Interop.WindowNative.GetWindowHandle(native!);
    }
}
