using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Views;

/// <summary>Right-hand panel with the decrypted details of one server entry.</summary>
public partial class ServerDrawer : ContentView
{
    private const string Mask = "••••••••••••";

    private ServerEntry? _server;
    private string _password = "";
    private string _username = "";
    private bool _revealed;

    public event EventHandler? CloseRequested;
    public event EventHandler<ServerEntry>? EditRequested;
    public event EventHandler<ServerEntry>? DeleteRequested;
    public event EventHandler<string>? Notify;

    public ServerDrawer()
    {
        InitializeComponent();
    }

    public int? ServerId => _server?.Id;

    public void Show(ServerEntry server, CryptoService crypto)
    {
        _server = server;
        _revealed = false;
        ErrorLabel.IsVisible = false;

        int tone = Monogram.Tone(server.Name);
        TileBorder.SetDynamicResource(BackgroundColorProperty, $"Tone{tone}Bg");
        TypeGlyphLabel.SetDynamicResource(Label.TextColorProperty, $"Tone{tone}Fg");
        TypeGlyphLabel.Text = server.TypeGlyph;
        NameLabel.Text = server.Name;
        MetaLabel.Text = $"{(server.HasType ? server.ServerType : "Server")} · {server.UpdatedAt.ToLocalTime():dd.MM.yyyy} da yangilangan";
        HostLabel.Text = server.DisplayHost;
        CreatedLabel.Text = server.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy");
        UpdatedLabel.Text = server.UpdatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        string notes = "";
        try
        {
            _password = server.EncryptedPassword != null ? crypto.Decrypt(server.EncryptedPassword) : "";
            _username = server.EncryptedUsername != null ? crypto.Decrypt(server.EncryptedUsername) : "";
            notes     = server.EncryptedNotes    != null ? crypto.Decrypt(server.EncryptedNotes)    : "";
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Shifrni ochib bo'lmadi: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }

        UsernameLabel.Text = _username;
        NotesLabel.Text = notes;
        UsernameSection.IsVisible = _username.Length > 0;
        PasswordSection.IsVisible = _password.Length > 0;
        NotesSection.IsVisible    = notes.Length > 0;

        // Without a password the main action copies the host instead
        PrimaryCopyButton.Text = _password.Length > 0 ? "Parolni nusxalash" : "Hostni nusxalash";

        UpdatePasswordDisplay();
    }

    public void Clear()
    {
        _server = null;
        _password = _username = "";
        PasswordLabel.Text = UsernameLabel.Text = NotesLabel.Text = "";
    }

    private void UpdatePasswordDisplay()
    {
        PasswordLabel.Text = _revealed ? _password : Mask;
        PasswordLabel.CharacterSpacing = _revealed ? 0 : 2;
        RevealButton.Text = _revealed ? Glyphs.Hide : Glyphs.View;
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
        if (_server != null) EditRequested?.Invoke(this, _server);
    }

    private void OnDelete_Clicked(object? sender, EventArgs e)
    {
        if (_server != null) DeleteRequested?.Invoke(this, _server);
    }

    private void OnReveal_Clicked(object? sender, EventArgs e)
    {
        _revealed = !_revealed;
        UpdatePasswordDisplay();
    }

    private async void OnPrimaryCopy_Clicked(object? sender, EventArgs e)
    {
        if (_password.Length > 0) await CopyAsync(_password, "Parol nusxalandi");
        else await CopyAsync(_server?.DisplayHost ?? "", "Host nusxalandi");
    }

    private async void OnCopyHost_Clicked(object? sender, EventArgs e) => await CopyAsync(_server?.DisplayHost ?? "", "Host nusxalandi");
    private async void OnCopyUsername_Clicked(object? sender, EventArgs e) => await CopyAsync(_username, "Foydalanuvchi nomi nusxalandi");
    private async void OnCopyPassword_Clicked(object? sender, EventArgs e) => await CopyAsync(_password, "Parol nusxalandi");
}
