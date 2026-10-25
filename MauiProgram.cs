using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.LifecycleEvents;
using PasswordManager.Pages;
using PasswordManager.Services;

namespace PasswordManager;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Fonts: Segoe UI Variable, Segoe Fluent Icons and Consolas ship with Windows 11 — nothing to bundle.
        builder.UseMauiApp<App>();

#if WINDOWS
        // Open maximized (full desktop); title bar colors follow the active theme
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(windows => windows.OnWindowCreated(window =>
            {
                var appWindow = window.AppWindow;
                ThemeService.AttachWindow(appWindow);

                // MAUI sizes and shows the window after this callback, which would undo an
                // early Maximize(). So maximize once the launch work has run (queued), and again
                // on first activation in case the window only got focus later.
                void Maximize()
                {
                    if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter &&
                        presenter.State != Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
                        presenter.Maximize();
                }
                void MaximizeOnce(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
                {
                    window.Activated -= MaximizeOnce;
                    Maximize();
                }
                window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, Maximize);
                window.Activated += MaximizeOnce;
            }));
        });
#endif

        builder.Services.AddSingleton<CryptoService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<SessionService>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<AddEditPasswordPage>();
        builder.Services.AddTransient<AddEditServerPage>();

        return builder.Build();
    }
}
