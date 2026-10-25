using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Views;

/// <summary>Right-hand panel with the decrypted details of one password entry.</summary>
public partial class PasswordDrawer : ContentView
{
    private const string Mask = "••••••••••••";

    private CryptoService? _crypto;
    private PasswordEntry? _entry;
    private string _password = "";
    private string _username = "";
    private string _email = "";
    private string _phone = "";
    private bool _revealed;

    public event EventHandler? CloseRequested;
    public event EventHandler<PasswordEntry>? EditRequested;
    public event EventHandler<PasswordEntry>? DeleteRequested;
    /// <summary>Short confirmation text for the toast ("Parol nusxalandi").</summary>
    public event EventHandler<string>? Notify;

    public PasswordDrawer()
    {
        InitializeComponent();
    }

    public int? EntryId => _entry?.Id;

    public void Show(PasswordEntry entry, string? folderName, CryptoService crypto)
    {
        _entry = entry;
        _crypto = crypto;
        _revealed = false;
        ErrorLabel.IsVisible = false;

        int tone = Monogram.Tone(entry.ServiceName);
        TileBorder.SetDynamicResource(BackgroundColorProperty, $"Tone{tone}Bg");
        MonogramLabel.SetDynamicResource(Label.TextColorProperty, $"Tone{tone}Fg");
        MonogramLabel.Text = Monogram.For(entry.ServiceName);
        NameLabel.Text = entry.ServiceName;
        MetaLabel.Text = $"{(string.IsNullOrEmpty(folderName) ? "Papkasiz" : folderName)} · {entry.UpdatedAt.ToLocalTime():dd.MM.yyyy} da yangilangan";
        CreatedLabel.Text = entry.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy");
        UpdatedLabel.Text = entry.UpdatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        try
        {
            _password = crypto.Decrypt(entry.EncryptedPassword);
            _username = entry.EncryptedUsername != null ? crypto.Decrypt(entry.EncryptedUsername) : "";
            _email    = entry.EncryptedEmail    != null ? crypto.Decrypt(entry.EncryptedEmail)    : "";
            _phone    = entry.EncryptedPhone    != null ? crypto.Decrypt(entry.EncryptedPhone)    : "";
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Shifrni ochib bo'lmadi: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }

        UsernameLabel.Text = _username;
        EmailLabel.Text    = _email;
        PhoneLabel.Text    = _phone;
        UsernameSection.IsVisible = _username.Length > 0;
        EmailSection.IsVisible    = _email.Length > 0;
        PhoneSection.IsVisible    = _phone.Length > 0;

        UpdatePasswordDisplay();
        UpdateStrength();
    }

    /// <summary>Forget decrypted values (on close / lock).</summary>
    public void Clear()
    {
        _entry = null;
        _password = _username = _email = _phone = "";
        PasswordLabel.Text = UsernameLabel.Text = EmailLabel.Text = PhoneLabel.Text = "";
    }

    private void UpdatePasswordDisplay()
    {
        PasswordLabel.Text = _revealed ? _password : Mask;
        PasswordLabel.CharacterSpacing = _revealed ? 0 : 2;
        RevealButton.Text = _revealed ? Glyphs.Hide : Glyphs.View;
    }

    private void UpdateStrength()
    {
        int score = Strength.Score(_password);
        string soft = score switch { 1 => "DangerSoft", 2 => "WarningSoft", _ => "SuccessSoft" };
        string line = score switch { 1 => "DangerBorder", 2 => "WarningBorder", _ => "SuccessBorder" };

        StrengthBox.SetDynamicResource(BackgroundColorProperty, soft);
        StrengthBox.SetDynamicResource(Border.StrokeProperty, line);
        StrengthTitle.Text = $"Parol kuchi: {Strength.Label(score).ToLowerInvariant()}";
        StrengthLength.Text = $"{_password.Length} belgi";
        StrengthTitle.SetDynamicResource(Label.TextColorProperty, Strength.ColorKey(score));
        StrengthLength.SetDynamicResource(Label.TextColorProperty, Strength.ColorKey(score));

        var bars = new[] { Bar1, Bar2, Bar3, Bar4 };
        for (int i = 0; i < bars.Length; i++)
            bars[i].SetDynamicResource(BoxView.ColorProperty, i < score ? Strength.BarKey(score) : "StrengthEmpty");
    }

    private async Task CopyAsync(string value, string message)
    {
        if (string.IsNullOrEmpty(value)) return;
        await Clipboard.SetTextAsync(value);
        Notify?.Invoke(this, message);
    }

    private void OnClose_Clicked(object? sender, EventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnEdit_Clicked(object? sender, EventArgs e)
    {
        if (_entry != null) EditRequested?.Invoke(this, _entry);
    }

    private void OnDelete_Clicked(object? sender, EventArgs e)
    {
        if (_entry != null) DeleteRequested?.Invoke(this, _entry);
    }

    private void OnReveal_Clicked(object? sender, EventArgs e)
    {
        _revealed = !_revealed;
        UpdatePasswordDisplay();
    }

    private async void OnCopyPassword_Clicked(object? sender, EventArgs e) => await CopyAsync(_password, "Parol nusxalandi");
    private async void OnCopyUsername_Clicked(object? sender, EventArgs e) => await CopyAsync(_username, "Foydalanuvchi nomi nusxalandi");
    private async void OnCopyEmail_Clicked(object? sender, EventArgs e) => await CopyAsync(_email, "Email nusxalandi");
    private async void OnCopyPhone_Clicked(object? sender, EventArgs e) => await CopyAsync(_phone, "Telefon raqam nusxalandi");
}
