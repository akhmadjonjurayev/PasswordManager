using Microsoft.Extensions.DependencyInjection;
using PasswordManager.Pages;
using PasswordManager.Services;

namespace PasswordManager;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private Window? _appWindow;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    // .NET 10 MAUI: CreateWindow is the correct entry point for window setup
    protected override Window CreateWindow(IActivationState? activationState)
    {
        _appWindow = new Window(new ContentPage
        {
            BackgroundColor = Color.FromArgb("#0F1117")
        })
        {
            Title = "SecureVault",
            MinimumWidth = 1000,
            MinimumHeight = 660
        };

        _ = InitAsync();
        return _appWindow;
    }

    private async Task InitAsync()
    {
        try
        {
            var db = _services.GetRequiredService<DatabaseService>();
            await db.InitializeAsync();
            MainThread.BeginInvokeOnMainThread(ShowLogin);
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_appWindow != null)
                    _appWindow.Page = new ContentPage
                    {
                        BackgroundColor = Color.FromArgb("#0F1117"),
                        Content = new Label
                        {
                            Text = $"Bazani ochishda xato: {ex.Message}",
                            TextColor = Colors.Red,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            FontSize = 16
                        }
                    };
            });
        }
    }

    private void ShowLogin()
    {
        var loginPage = _services.GetRequiredService<LoginPage>();
        SetPage(loginPage);
    }

    public void NavigateToMain()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var mainPage = _services.GetRequiredService<MainPage>();
            SetPage(mainPage);
        });
    }

    public void NavigateToLogin()
    {
        MainThread.BeginInvokeOnMainThread(ShowLogin);
    }

    private void SetPage(Page page)
    {
        if (_appWindow != null)
            _appWindow.Page = page;
    }
}
