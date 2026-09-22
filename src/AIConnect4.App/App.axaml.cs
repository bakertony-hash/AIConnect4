using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AIConnect4.App.ViewModels;
using AIConnect4.App.Views;

namespace AIConnect4.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var arena = new ArenaViewModel();
            desktop.MainWindow = new MainWindow { DataContext = arena };
            desktop.Exit += (_, _) => arena.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
