using WorkActivityTracker.Services;

namespace WorkActivityTracker;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = "Work Activity Tracker",
            Width = 1200,
            Height = 800
        };

        // Hooking all'evento di cambio handler per accedere alla finestra nativa WinUI
        // e intercettare la chiusura prima che avvenga (per avvisare l'utente di modifiche non salvate)
        window.HandlerChanged += OnWindowHandlerChanged;

        return window;
    }

    private void OnWindowHandlerChanged(object? sender, EventArgs e)
    {
#if WINDOWS
        if (sender is Microsoft.Maui.Controls.Window mauiWindow)
        {
            if (mauiWindow.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWin)
            {
                nativeWin.AppWindow.Closing += OnNativeWindowClosing;
            }
        }
#endif
    }

#if WINDOWS
    private async void OnNativeWindowClosing(
        Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        var service = IPlatformApplication.Current?.Services.GetService<UnsavedChangesService>();
        if (service?.HasUnsavedChanges == true)
        {
            // Cancella la chiusura per poter mostrare il dialogo
            args.Cancel = true;

            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
            {
                bool esci = await page.DisplayAlert(
                    "Modifiche non salvate",
                    "Ci sono modifiche non salvate nel form attività. Vuoi uscire senza salvare?",
                    "Sì, esci senza salvare",
                    "Rimani"
                );

                if (esci)
                {
                    service.HasUnsavedChanges = false;
                    Application.Current?.Quit();
                }
            }
        }
    }
#endif
}
