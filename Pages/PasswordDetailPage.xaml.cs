using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Pages;

public partial class PasswordDetailPage : ContentPage
{
    private readonly CryptoService _crypto;
    private PasswordEntry? _entry;

    private string _decryptedPassword = "";
    private string _decryptedUsername = "";
    private string _decryptedEmail    = "";
    private string _decryptedPhone    = "";
    private bool _passwordRevealed;

    public PasswordDetailPage(CryptoService crypto)
    {
        InitializeComponent();
        _crypto = crypto;
    }

    public void Configure(PasswordEntry entry)
    {
        _entry = entry;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_entry == null) return;

        ServiceNameLabel.Text = _entry.ServiceName;
        DateLabel.Text = $"Qo'shilgan: {_entry.CreatedAt.ToLocalTime():dd.MM.yyyy}";
        ErrorLabel.Text = "";
        _passwordRevealed = false;
        PasswordDisplayLabel.Text = "••••••••••••";
        PasswordDisplayLabel.TextColor = Color.FromArgb("#8A8FAB");
        RevealBtn.Text = "👁";

        try
        {
            _decryptedPassword = _crypto.Decrypt(_entry.EncryptedPassword);

            if (_entry.EncryptedUsername != null)
            {
                _decryptedUsername = _crypto.Decrypt(_entry.EncryptedUsername);
                UsernameDisplayLabel.Text = _decryptedUsername;
                UsernameSection.IsVisible = true;
            }
            else { UsernameSection.IsVisible = false; }

            if (_entry.EncryptedEmail != null)
            {
                _decryptedEmail = _crypto.Decrypt(_entry.EncryptedEmail);
                EmailDisplayLabel.Text = _decryptedEmail;
                EmailSection.IsVisible = true;
            }
            else
            {
                EmailSection.IsVisible = false;
            }

            if (_entry.EncryptedPhone != null)
            {
                _decryptedPhone = _crypto.Decrypt(_entry.EncryptedPhone);
                PhoneDisplayLabel.Text = _decryptedPhone;
                PhoneSection.IsVisible = true;
            }
            else
            {
                PhoneSection.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Shifrni ochib bo'lmadi: {ex.Message}";
        }
    }

    private void OnRevealPassword_Clicked(object? sender, EventArgs e)
    {
        _passwordRevealed = !_passwordRevealed;
        if (_passwordRevealed)
        {
            PasswordDisplayLabel.Text = _decryptedPassword;
            PasswordDisplayLabel.TextColor = Color.FromArgb("#F0F2FF");
            RevealBtn.Text = "🙈";
        }
        else
        {
            PasswordDisplayLabel.Text = "••••••••••••";
            PasswordDisplayLabel.TextColor = Color.FromArgb("#8A8FAB");
            RevealBtn.Text = "👁";
        }
    }

    private async void OnCopyUsername_Clicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_decryptedUsername))
        {
            await Clipboard.SetTextAsync(_decryptedUsername);
            await DisplayAlertAsync("Nusxalandi", "Foydalanuvchi nomi clipboard ga nusxalandi.", "OK");
        }
    }

    private async void OnCopyPassword_Clicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_decryptedPassword))
        {
            await Clipboard.SetTextAsync(_decryptedPassword);
            await DisplayAlertAsync("Nusxalandi", "Parol clipboard ga nusxalandi.", "OK");
        }
    }

    private async void OnCopyEmail_Clicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_decryptedEmail))
        {
            await Clipboard.SetTextAsync(_decryptedEmail);
            await DisplayAlertAsync("Nusxalandi", "Email clipboard ga nusxalandi.", "OK");
        }
    }

    private async void OnCopyPhone_Clicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_decryptedPhone))
        {
            await Clipboard.SetTextAsync(_decryptedPhone);
            await DisplayAlertAsync("Nusxalandi", "Telefon raqam clipboard ga nusxalandi.", "OK");
        }
    }

    private async void OnClose_Clicked(object? sender, EventArgs e)
    {
        _decryptedPassword = "";
        _decryptedUsername = "";
        _decryptedEmail    = "";
        _decryptedPhone    = "";
        await Navigation.PopModalAsync();
    }
}
