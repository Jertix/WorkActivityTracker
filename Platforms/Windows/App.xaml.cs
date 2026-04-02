using Microsoft.UI.Xaml;

namespace WorkActivityTracker.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => WorkActivityTracker.MauiProgram.CreateMauiApp();
}
