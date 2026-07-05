using Microsoft.Extensions.DependencyInjection;
using PasswordManager.Pages;
using PasswordManager.Services;

namespace PasswordManager;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<CryptoService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<SessionService>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<AddEditPasswordPage>();
        builder.Services.AddTransient<PasswordDetailPage>();
        builder.Services.AddTransient<AddEditServerPage>();
        builder.Services.AddTransient<ServerDetailPage>();

        return builder.Build();
    }
}
