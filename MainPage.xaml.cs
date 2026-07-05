using Microsoft.AspNetCore.Components.WebView;
using WorkActivityTracker.Services;

namespace WorkActivityTracker;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Alla creazione del BlazorWebView cattura l'environment WebView2 e lo passa al
    /// PdfExportService, così l'export PDF riusa lo stesso environment del processo
    /// (evita il conflitto HRESULT 0x8007139F tra environment con opzioni diverse).
    /// </summary>
    private void OnBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
#if WINDOWS
        var env = e.WebView?.CoreWebView2?.Environment;
        if (env != null)
        {
            var svc = IPlatformApplication.Current?.Services.GetService<PdfExportService>();
            if (svc != null) svc.SharedEnvironment = env;
        }
#endif
    }
}
