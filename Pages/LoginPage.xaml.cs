using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Pages;

public partial class LoginPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly CryptoService _crypto;
    private readonly SessionService _session;
    private bool _isFirstTime;

    public LoginPage(DatabaseService db, CryptoService crypto, SessionService session)
    {
        InitializeComponent();
        _db = db;
        _crypto = crypto;
        _session = session;

        FieldFocus.Attach(FirstNameEntry, FirstNameField);
        FieldFocus.Attach(LastNameEntry, LastNameField);
        FieldFocus.Attach(SetupPasswordEntry, SetupPasswordField);
        FieldFocus.Attach(ConfirmPasswordEntry, ConfirmPasswordField);
        FieldFocus.Attach(LoginPasswordEntry, LoginPasswordField);

        UpdateThemeButton();
        SizeChanged += (_, _) => UpdateLayoutForWidth();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isFirstTime = !await _db.IsInitializedAsync();
        await SetupUIAsync();
        ShowError(null);
    }

    private async Task SetupUIAsync()
    {
        if (_isFirstTime)
        {
            InitialsLabel.FontFamily = (string)Application.Current!.Resources["IconFont"];
            InitialsLabel.Text = Glyphs.Key;
            TitleLabel.Text = "Hisob yaratish";
            SubtitleLabel.Text = "Birinchi marta sozlash: ismingiz va asosiy parolni kiriting.";
            ActionButton.Text = "Hisob yaratish";
            SetupSection.IsVisible = true;
            LoginSection.IsVisible = false;
            FirstNameEntry.Focus();
        }
        else
        {
            var profile = await _db.GetUserProfileAsync();
            InitialsLabel.FontFamily = (string)Application.Current!.Resources["TextFont"];
            InitialsLabel.Text = profile?.Initials ?? "";
            TitleLabel.Text = profile != null ? $"Xush kelibsiz, {profile.FirstName}" : "Xush kelibsiz";
            SubtitleLabel.Text = "Omborni ochish uchun asosiy parolingizni kiriting.";
            ActionButton.Text = "Omborni ochish";
            SetupSection.IsVisible = false;
            LoginSection.IsVisible = true;
            LoginPasswordEntry.Focus();
        }
    }

    private void UpdateLayoutForWidth()
    {
        // Brand panel only when there is room for both halves
        bool wide = Width >= 960;
        BrandPanel.IsVisible = wide;
        RootGrid.ColumnDefinitions[0].Width = wide ? GridLength.Star : new GridLength(0);
    }

    private async void OnActionButtonClicked(object? sender, EventArgs e)
    {
        ShowError(null);
        SetLoading(true);

        try
        {
            if (_isFirstTime)
                await SetupAccount();
            else
                await Login();
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task SetupAccount()
    {
        var firstName = FirstNameEntry.Text?.Trim() ?? "";
        var lastName = LastNameEntry.Text?.Trim() ?? "";
        var password = await SettledTextAsync(SetupPasswordEntry);
        var confirm = await SettledTextAsync(ConfirmPasswordEntry);

        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
        {
            ShowError("Ism va familiyani kiriting.");
            return;
        }
        if (password.Length < 8)
        {
            ShowError("Parol kamida 8 ta belgidan iborat bo'lishi kerak.");
            return;
        }
        if (password != confirm)
        {
            ShowError("Parollar mos kelmadi.");
            return;
        }

        // PBKDF2 is slow on purpose — keep it off the UI thread so the spinner animates
        var (hash, salt) = await Task.Run(() => _crypto.HashPassword(password));
        var profile = new UserProfile
        {
            FirstName    = firstName,
            LastName     = lastName,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt    = DateTime.UtcNow
        };

        await _db.SaveUserProfileAsync(profile);
        await Task.Run(() => _crypto.InitializeKey(password, salt));
        _session.Login(profile.FullName, profile.Initials);

        GoToMain();
    }

    private async Task Login()
    {
        var password = await SettledTextAsync(LoginPasswordEntry);
        if (string.IsNullOrEmpty(password))
        {
            ShowError("Parolni kiriting.");
            return;
        }

        var profile = await _db.GetUserProfileAsync();
        if (profile == null)
        {
            ShowError("Hisob topilmadi. Qayta ishga tushiring.");
            return;
        }

        bool ok = await Task.Run(() => _crypto.VerifyPassword(password, profile.PasswordHash, profile.PasswordSalt));
        if (!ok)
        {
            ShowError("Noto'g'ri parol.");
            LoginPasswordEntry.Text = "";
            LoginPasswordEntry.Focus();
            return;
        }

        await Task.Run(() => _crypto.InitializeKey(password, profile.PasswordSalt));
        _session.Login(profile.FullName, profile.Initials);

        GoToMain();
    }

    /// <summary>
    /// On Windows, keystrokes typed quickly before Enter (password-manager auto-type, scripted input)
    /// reach the password box after the Enter key, so reading right away gave an empty value.
    /// Wait until the value stops changing (≈80 ms when it is already settled).
    /// </summary>
    private static async Task<string> SettledTextAsync(Entry entry)
    {
        string previous = CurrentText(entry);
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(80);
            string current = CurrentText(entry);
            if (current == previous && current.Length > 0) return current;
            previous = current;
        }
        return previous;
    }

    private static string CurrentText(Entry entry)
    {
#if WINDOWS
        if (entry.Handler?.PlatformView is Microsoft.Maui.Platform.MauiPasswordTextBox box)
            return box.Password ?? "";
#endif
        return entry.Text ?? "";
    }

    private void GoToMain()
    {
        if (Application.Current is App app)
            app.NavigateToMain();
    }

    private void ShowError(string? msg)
    {
        ErrorLabel.Text = msg ?? "";
        ErrorBox.IsVisible = !string.IsNullOrEmpty(msg);
    }

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        ActionButton.IsEnabled = !loading;
    }

    private void OnTogglePasswordVisibility(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        var entry = btn.CommandParameter as string == "setup" ? SetupPasswordEntry : LoginPasswordEntry;
        entry.IsPassword = !entry.IsPassword;
        btn.Text = entry.IsPassword ? Glyphs.View : Glyphs.Hide;
    }

    private void OnPasswordChanged(object? sender, TextChangedEventArgs e)
        => FieldFocus.ShowStrength(e.NewTextValue ?? "", [Strength1, Strength2, Strength3, Strength4], StrengthLabel);

    private void OnToggleTheme_Clicked(object? sender, EventArgs e)
    {
        ThemeService.Toggle();
        UpdateThemeButton();
    }

    private void UpdateThemeButton()
        => ThemeButton.Text = ThemeService.IsDark ? Glyphs.Sun : Glyphs.Moon;
}
