using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Pages;

public partial class AddEditServerPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly CryptoService _crypto;

    private ServerEntry? _existing;
    private Func<Task>? _onSaved;

    private static readonly string[] ServerTypes =
    [
        "SSH", "SFTP", "FTP", "HTTP/HTTPS", "RDP", "VNC",
        "MySQL", "PostgreSQL", "MongoDB", "Boshqa"
    ];

    public AddEditServerPage(DatabaseService db, CryptoService crypto)
    {
        InitializeComponent();
        _db = db;
        _crypto = crypto;

        TypePicker.Items.Clear();
        foreach (var t in ServerTypes)
            TypePicker.Items.Add(t);
        TypePicker.SelectedIndex = -1;
    }

    public void Configure(ServerEntry? existing, Func<Task>? onSaved)
    {
        _existing = existing;
        _onSaved  = onSaved;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ErrorLabel.Text = "";

        if (_existing != null)
        {
            PageTitleLabel.Text = "Serverni tahrirlash";
            SaveButton.Text     = "Yangilash";
            NameEntry.Text      = _existing.Name;
            HostEntry.Text      = _existing.Host;
            PortEntry.Text      = _existing.Port ?? "";

            var typeIdx = Array.IndexOf(ServerTypes, _existing.ServerType ?? "");
            TypePicker.SelectedIndex = typeIdx;

            try
            {
                if (_existing.EncryptedUsername != null)
                    UsernameEntry.Text = _crypto.Decrypt(_existing.EncryptedUsername);
                if (_existing.EncryptedPassword != null)
                    PasswordEntry.Text = _crypto.Decrypt(_existing.EncryptedPassword);
                if (_existing.EncryptedNotes != null)
                    NotesEditor.Text = _crypto.Decrypt(_existing.EncryptedNotes);
            }
            catch
            {
                ErrorLabel.Text = "Ma'lumotlarni ochib bo'lmadi.";
            }
        }
        else
        {
            PageTitleLabel.Text = "Yangi server qo'shish";
            SaveButton.Text     = "Saqlash";
            NameEntry.Focus();
        }
    }

    private async void OnSave_Clicked(object? sender, EventArgs e)
    {
        ErrorLabel.Text = "";
        var name     = NameEntry.Text?.Trim() ?? "";
        var host     = HostEntry.Text?.Trim() ?? "";
        var port     = PortEntry.Text?.Trim();
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text;
        var notes    = NotesEditor.Text?.Trim();

        string? serverType = TypePicker.SelectedIndex >= 0
            ? ServerTypes[TypePicker.SelectedIndex]
            : null;

        if (string.IsNullOrEmpty(name))
        {
            ErrorLabel.Text = "Server nomini kiriting.";
            return;
        }
        if (string.IsNullOrEmpty(host))
        {
            ErrorLabel.Text = "Host yoki IP manzilni kiriting.";
            return;
        }

        SetLoading(true);
        try
        {
            var now     = DateTime.UtcNow;
            var encUser = string.IsNullOrEmpty(username) ? null : _crypto.Encrypt(username);
            var encPw   = string.IsNullOrEmpty(password) ? null : _crypto.Encrypt(password);
            var encNote = string.IsNullOrEmpty(notes)    ? null : _crypto.Encrypt(notes);

            if (_existing == null)
            {
                var entry = new ServerEntry
                {
                    Name              = name,
                    Host              = host,
                    Port              = string.IsNullOrEmpty(port) ? null : port,
                    ServerType        = serverType,
                    EncryptedUsername = encUser,
                    EncryptedPassword = encPw,
                    EncryptedNotes    = encNote,
                    CreatedAt         = now,
                    UpdatedAt         = now
                };
                await _db.AddServerAsync(entry);
            }
            else
            {
                _existing.Name              = name;
                _existing.Host              = host;
                _existing.Port              = string.IsNullOrEmpty(port) ? null : port;
                _existing.ServerType        = serverType;
                _existing.EncryptedUsername = encUser;
                _existing.EncryptedPassword = encPw;
                _existing.EncryptedNotes    = encNote;
                _existing.UpdatedAt         = now;
                await _db.UpdateServerAsync(_existing);
            }

            if (_onSaved != null)
                await _onSaved();

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Xato: {ex.Message}";
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnCancel_Clicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync();

    private void OnTogglePassword_Clicked(object? sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        TogglePwBtn.Text = PasswordEntry.IsPassword ? "O" : "H";
    }

    private void SetLoading(bool loading)
    {
        LoadingOverlay.IsVisible = loading;
        SaveButton.IsEnabled     = !loading;
    }
}
