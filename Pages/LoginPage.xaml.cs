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
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isFirstTime = !await _db.IsInitializedAsync();
        SetupUI();
        ErrorLabel.Text = "";
    }

    private void SetupUI()
    {
        if (_isFirstTime)
        {
            TitleLabel.Text = "Xush kelibsiz!";
            SubtitleLabel.Text = "Birinchi marta sozlash";
            ActionButton.Text = "Hisob yaratish";
            SetupSection.IsVisible = true;
            LoginSection.IsVisible = false;
        }
        else
        {
            TitleLabel.Text = "SecureVault";
            SubtitleLabel.Text = "Parollaringiz xavfsiz saqlanadi";
            ActionButton.Text = "Kirish";
            SetupSection.IsVisible = false;
            LoginSection.IsVisible = true;
            LoginPasswordEntry.Focus();
        }
    }

    private async void OnActionButtonClicked(object? sender, EventArgs e)
    {
        ErrorLabel.Text = "";
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
        var password = SetupPasswordEntry.Text ?? "";
        var confirm = ConfirmPasswordEntry.Text ?? "";

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

        var (hash, salt) = _crypto.HashPassword(password);
        var profile = new UserProfile
        {
            FirstName    = firstName,
            LastName     = lastName,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt    = DateTime.UtcNow
        };

        await _db.SaveUserProfileAsync(profile);
        _crypto.InitializeKey(password, salt);
        _session.Login(profile.FullName, profile.Initials);

        GoToMain();
    }

    private async Task Login()
    {
        var password = LoginPasswordEntry.Text ?? "";
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

        if (!_crypto.VerifyPassword(password, profile.PasswordHash, profile.PasswordSalt))
        {
            ShowError("Noto'g'ri parol.");
            LoginPasswordEntry.Text = "";
            return;
        }

        _crypto.InitializeKey(password, profile.PasswordSalt);
        _session.Login(profile.FullName, profile.Initials);

        GoToMain();
    }

    private void GoToMain()
    {
        if (Application.Current is App app)
            app.NavigateToMain();
    }

    private void ShowError(string msg) => ErrorLabel.Text = msg;

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        ActionButton.IsEnabled = !loading;
    }

    private void OnTogglePasswordVisibility(object? sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            var target = btn.CommandParameter as string;
            if (target == "setup")
            {
                SetupPasswordEntry.IsPassword = !SetupPasswordEntry.IsPassword;
                btn.Text = SetupPasswordEntry.IsPassword ? "👁" : "🙈";
            }
            else
            {
                LoginPasswordEntry.IsPassword = !LoginPasswordEntry.IsPassword;
                btn.Text = LoginPasswordEntry.IsPassword ? "👁" : "🙈";
            }
        }
    }

    private void OnPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        var pw = e.NewTextValue ?? "";
        var score = GetPasswordStrength(pw);
        UpdateStrengthBar(score);
    }

    private static int GetPasswordStrength(string pw)
    {
        if (pw.Length == 0) return 0;
        int score = 0;
        if (pw.Length >= 8) score++;
        if (pw.Length >= 12) score++;
        if (pw.Any(char.IsUpper) && pw.Any(char.IsLower)) score++;
        if (pw.Any(char.IsDigit)) score++;
        if (pw.Any(c => !char.IsLetterOrDigit(c))) score++;
        return Math.Min(score, 4);
    }

    private void UpdateStrengthBar(int score)
    {
        var bars = new[] { Strength1, Strength2, Strength3, Strength4 };
        var colors = new[]
        {
            Color.FromArgb("#F56565"),
            Color.FromArgb("#F6C343"),
            Color.FromArgb("#4FD1A5"),
            Color.FromArgb("#6C63FF")
        };
        var labels = new[] { "", "Zaif", "O'rtacha", "Yaxshi", "Kuchli" };
        var border = Color.FromArgb("#2A2F42");

        for (int i = 0; i < 4; i++)
            bars[i].Color = i < score ? colors[score - 1] : border;

        StrengthLabel.Text = score > 0 ? labels[score] : "";
        StrengthLabel.TextColor = score > 0 ? colors[score - 1] : Colors.Transparent;
    }
}
