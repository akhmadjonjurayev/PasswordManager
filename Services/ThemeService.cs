using PasswordManager.Resources.Styles;

namespace PasswordManager.Services;

/// <summary>
/// Light / dark theme switching. The active palette is one merged ResourceDictionary
/// (LightTheme or DarkTheme) that is swapped at runtime, so every color must be
/// referenced with DynamicResource (or SetDynamicResource in code).
/// </summary>
public static class ThemeService
{
    private const string PreferenceKey = "theme";

    public static AppTheme Current { get; private set; } = AppTheme.Light;
    public static bool IsDark => Current == AppTheme.Dark;

    public static event EventHandler? Changed;

    private static Application? _app;
    private static Application? CurrentApp => _app ?? Application.Current;

    /// <summary>Called from the App constructor: restores the saved theme.</summary>
    public static void Initialize(Application app)
    {
        _app = app;
        var saved = Preferences.Default.Get(PreferenceKey, "light");
        Apply(saved == "dark" ? AppTheme.Dark : AppTheme.Light, persist: false);
    }

    public static void Toggle() => Apply(IsDark ? AppTheme.Light : AppTheme.Dark);

    public static void Apply(AppTheme theme, bool persist = true)
    {
        var app = CurrentApp;
        if (app == null) return;

        var merged = app.Resources.MergedDictionaries;
        var old = merged.FirstOrDefault(d => d is LightTheme or DarkTheme);
        if (old != null) merged.Remove(old);
        merged.Add(theme == AppTheme.Dark ? new DarkTheme() : new LightTheme());

        Current = theme;
        app.UserAppTheme = theme;

        if (persist)
            Preferences.Default.Set(PreferenceKey, theme == AppTheme.Dark ? "dark" : "light");

#if WINDOWS
        UpdateTitleBar();
#endif
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Current value of a theme color key (Bg, Accent, Tone3Fg…).</summary>
    public static Color Color(string key)
    {
        if (CurrentApp?.Resources.TryGetValue(key, out var value) == true && value is Color c)
            return c;
        return Colors.Magenta;
    }

#if WINDOWS
    private static Microsoft.UI.Windowing.AppWindow? _appWindow;

    public static void AttachWindow(Microsoft.UI.Windowing.AppWindow appWindow)
    {
        _appWindow = appWindow;
        UpdateTitleBar();
    }

    private static void UpdateTitleBar()
    {
        if (_appWindow == null) return;

        static global::Windows.UI.Color Win(Color c) => global::Windows.UI.Color.FromArgb(
            (byte)(c.Alpha * 255), (byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255));

        var bg    = Win(Color("Surface"));
        var fg    = Win(Color("TextPrimary"));
        var muted = Win(Color("TextMuted"));
        var hover = Win(Color("SurfaceMuted"));

        var titleBar = _appWindow.TitleBar;
        titleBar.BackgroundColor               = bg;
        titleBar.ForegroundColor               = fg;
        titleBar.InactiveBackgroundColor       = bg;
        titleBar.InactiveForegroundColor       = muted;
        titleBar.ButtonBackgroundColor         = bg;
        titleBar.ButtonForegroundColor         = fg;
        titleBar.ButtonHoverBackgroundColor    = hover;
        titleBar.ButtonHoverForegroundColor    = fg;
        titleBar.ButtonPressedBackgroundColor  = hover;
        titleBar.ButtonPressedForegroundColor  = fg;
        titleBar.ButtonInactiveBackgroundColor = bg;
        titleBar.ButtonInactiveForegroundColor = muted;
    }
#endif
}
