using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Pages;

public partial class ServerDetailPage : ContentPage
{
    private readonly CryptoService _crypto;
    private ServerEntry? _entry;

    private string _decryptedPassword = "";
    private string _decryptedUsername = "";
    private bool _passwordRevealed;

    public ServerDetailPage(CryptoService crypto)
    {
        InitializeComponent();
        _crypto = crypto;
    }

    public void Configure(ServerEntry entry)
    {
        _entry = entry;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_entry == null) return;

        ServerNameLabel.Text = _entry.Name;
        DateLabel.Text = $"Qo'shilgan: {_entry.CreatedAt.ToLocalTime():dd.MM.yyyy}";
        TypeIconLabel.Text = _entry.TypeIcon;
        ErrorLabel.Text = "";

        _passwordRevealed = false;

        // Type badge
        if (_entry.HasType)
        {
            TypeBadgeLabel.Text = _entry.ServerType;
            TypeBadge.IsVisible = true;
        }
        else
        {
            TypeBadge.IsVisible = false;
        }

        // Host display (host + port combined)
        HostDisplayLabel.Text = _entry.DisplayHost;

        try
        {
            // Password
            if (_entry.EncryptedPassword != null)
            {
                _decryptedPassword = _crypto.Decrypt(_entry.EncryptedPassword);
                PasswordDisplayLabel.Text = "......";
                PasswordDisplayLabel.TextColor = Color.FromArgb("#8A8FAB");
                RevealBtn.Text = "O";
                PasswordSection.IsVisible = true;
            }
            else { PasswordSection.IsVisible = false; }

            // Username
            if (_entry.EncryptedUsername != null)
            {
                _decryptedUsername = _crypto.Decrypt(_entry.EncryptedUsername);
                UsernameDisplayLabel.Text = _decryptedUsername;
                UsernameSection.IsVisible = true;
            }
            else { UsernameSection.IsVisible = false; }

            // Notes
            if (_entry.EncryptedNotes != null)
            {
                NotesDisplayLabel.Text = _crypto.Decrypt(_entry.EncryptedNotes);
                NotesSection.IsVisible = true;
            }
            else { NotesSection.IsVisible = false; }
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
            RevealBtn.Text = "H";
        }
        else
        {
            PasswordDisplayLabel.Text = "......";
            PasswordDisplayLabel.TextColor = Color.FromArgb("#8A8FAB");
            RevealBtn.Text = "O";
        }
    }

    private async void OnCopyHost_Clicked(object? sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(_entry?.DisplayHost ?? "");
        await DisplayAlertAsync("Nusxalandi", "Host manzil clipboard ga nusxalandi.", "OK");
    }

    private async void OnCopyPassword_Clicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_decryptedPassword))
        {
            await Clipboard.SetTextAsync(_decryptedPassword);
            await DisplayAlertAsync("Nusxalandi", "Parol clipboard ga nusxalandi.", "OK");
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

    private async void OnClose_Clicked(object? sender, EventArgs e)
    {
        _decryptedPassword = "";
        _decryptedUsername = "";
        await Navigation.PopModalAsync();
    }
}
