using PasswordManager.Models;
using PasswordManager.Services;
using System.Security.Cryptography;

namespace PasswordManager.Pages;

public partial class AddEditPasswordPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly CryptoService _crypto;

    private PasswordEntry? _existing;
    private Func<Task>? _onSaved;
    private int? _defaultFolderId;
    private List<Folder?> _pickerFolders = [];

    public AddEditPasswordPage(DatabaseService db, CryptoService crypto)
    {
        InitializeComponent();
        _db = db;
        _crypto = crypto;
    }

    public void Configure(PasswordEntry? existing, Func<Task>? onSaved, int? defaultFolderId = null)
    {
        _existing = existing;
        _onSaved = onSaved;
        _defaultFolderId = defaultFolderId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadFolderPickerAsync();

        if (_existing != null)
        {
            PageTitleLabel.Text = "Parolni tahrirlash";
            SaveButton.Text = "Yangilash";
            ServiceEntry.Text = _existing.ServiceName;

            try
            {
                PasswordEntry.Text = _crypto.Decrypt(_existing.EncryptedPassword);
                if (_existing.EncryptedUsername != null)
                    UsernameEntry.Text = _crypto.Decrypt(_existing.EncryptedUsername);
                if (_existing.EncryptedEmail != null)
                    EmailEntry.Text = _crypto.Decrypt(_existing.EncryptedEmail);
                if (_existing.EncryptedPhone != null)
                    PhoneEntry.Text = _crypto.Decrypt(_existing.EncryptedPhone);

                // Pre-select current folder
                if (_existing.FolderId.HasValue)
                {
                    var idx = _pickerFolders.FindIndex(f => f?.Id == _existing.FolderId.Value);
                    if (idx >= 0) FolderPicker.SelectedIndex = idx;
                }
            }
            catch
            {
                ErrorLabel.Text = "Ma'lumotlarni ochib bo'lmadi.";
            }
        }
        else
        {
            PageTitleLabel.Text = "Yangi parol qo'shish";
            SaveButton.Text = "Saqlash";

            // Pre-select default folder (when adding from a folder view)
            if (_defaultFolderId.HasValue)
            {
                var idx = _pickerFolders.FindIndex(f => f?.Id == _defaultFolderId.Value);
                if (idx >= 0) FolderPicker.SelectedIndex = idx;
            }

            ServiceEntry.Focus();
        }

        ErrorLabel.Text = "";
    }

    private async Task LoadFolderPickerAsync()
    {
        var folders = await _db.GetFoldersAsync();
        _pickerFolders = new List<Folder?> { null };
        _pickerFolders.AddRange(folders.Cast<Folder?>());

        FolderPicker.Items.Clear();
        FolderPicker.Items.Add("(Papkasiz)");
        foreach (var f in folders)
            FolderPicker.Items.Add(f.Name);

        FolderPicker.SelectedIndex = 0;
    }

    private async void OnSave_Clicked(object? sender, EventArgs e)
    {
        ErrorLabel.Text = "";
        var service  = ServiceEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";
        var username = UsernameEntry.Text?.Trim();
        var email    = EmailEntry.Text?.Trim();
        var phone    = PhoneEntry.Text?.Trim();

        if (string.IsNullOrEmpty(service))
        {
            ErrorLabel.Text = "Xizmat nomini kiriting.";
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            ErrorLabel.Text = "Parolni kiriting.";
            return;
        }

        // Resolve selected folder
        int? folderId = null;
        var selIdx = FolderPicker.SelectedIndex;
        if (selIdx > 0 && selIdx < _pickerFolders.Count)
            folderId = _pickerFolders[selIdx]?.Id;

        SetLoading(true);
        try
        {
            var now      = DateTime.UtcNow;
            var encPw    = _crypto.Encrypt(password);
            var encUser  = string.IsNullOrEmpty(username) ? null : _crypto.Encrypt(username);
            var encEmail = string.IsNullOrEmpty(email)    ? null : _crypto.Encrypt(email);
            var encPhone = string.IsNullOrEmpty(phone)    ? null : _crypto.Encrypt(phone);

            if (_existing == null)
            {
                var entry = new PasswordEntry
                {
                    ServiceName         = service,
                    EncryptedPassword   = encPw,
                    EncryptedUsername   = encUser,
                    EncryptedEmail      = encEmail,
                    EncryptedPhone      = encPhone,
                    FolderId            = folderId,
                    CreatedAt           = now,
                    UpdatedAt           = now
                };
                await _db.AddAsync(entry);
            }
            else
            {
                _existing.ServiceName         = service;
                _existing.EncryptedPassword   = encPw;
                _existing.EncryptedUsername   = encUser;
                _existing.EncryptedEmail      = encEmail;
                _existing.EncryptedPhone      = encPhone;
                _existing.FolderId            = folderId;
                _existing.UpdatedAt           = now;
                await _db.UpdateAsync(_existing);
            }

            if (_onSaved != null)
                await _onSaved();

            await CloseModal();
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
        => await CloseModal();

    private void OnTogglePassword_Clicked(object? sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        TogglePwBtn.Text = PasswordEntry.IsPassword ? "👁" : "🙈";
    }

    private void OnQuickGenerate_Clicked(object? sender, EventArgs e)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var pw = new char[20];
        for (int i = 0; i < pw.Length; i++)
            pw[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        PasswordEntry.Text = new string(pw);
        PasswordEntry.IsPassword = false;
        TogglePwBtn.Text = "🙈";
    }

    private void OnPasswordTextChanged(object? sender, TextChangedEventArgs e)
    {
        var pw = e.NewTextValue ?? "";
        int score = 0;
        if (pw.Length >= 8) score++;
        if (pw.Length >= 12) score++;
        if (pw.Any(char.IsUpper) && pw.Any(char.IsLower)) score++;
        if (pw.Any(char.IsDigit)) score++;
        if (pw.Any(c => !char.IsLetterOrDigit(c))) score++;
        score = Math.Min(score, 4);

        var bars = new[] { PwStrength1, PwStrength2, PwStrength3, PwStrength4 };
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

        PwStrengthLabel.Text = score > 0 ? labels[score] : "";
        PwStrengthLabel.TextColor = score > 0 ? colors[score - 1] : Colors.Transparent;
    }

    private void SetLoading(bool loading)
    {
        LoadingOverlay.IsVisible = loading;
        SaveButton.IsEnabled = !loading;
    }

    private Task CloseModal()
        => Navigation.PopModalAsync();
}
