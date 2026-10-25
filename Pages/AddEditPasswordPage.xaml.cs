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

        FieldFocus.Attach(ServiceEntry, ServiceField);
        FieldFocus.Attach(PasswordEntry, PasswordField);
        FieldFocus.Attach(FolderPicker, FolderField);
        FieldFocus.Attach(UsernameEntry, UsernameField);
        FieldFocus.Attach(EmailEntry, EmailField);
        FieldFocus.Attach(PhoneEntry, PhoneField);
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
        ShowError(null);

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

                SelectFolder(_existing.FolderId);
            }
            catch
            {
                ShowError("Ma'lumotlarni ochib bo'lmadi.");
            }
        }
        else
        {
            PageTitleLabel.Text = "Yangi parol qo'shish";
            SaveButton.Text = "Saqlash";
            SelectFolder(_defaultFolderId);
            ServiceEntry.Focus();
        }
    }

    private async Task LoadFolderPickerAsync()
    {
        var folders = await _db.GetFoldersAsync();
        _pickerFolders = [null, .. folders];

        FolderPicker.Items.Clear();
        FolderPicker.Items.Add("Papkasiz");
        foreach (var f in folders)
            FolderPicker.Items.Add(f.Name);

        FolderPicker.SelectedIndex = 0;
    }

    private void SelectFolder(int? folderId)
    {
        if (!folderId.HasValue) return;
        var idx = _pickerFolders.FindIndex(f => f?.Id == folderId.Value);
        if (idx >= 0) FolderPicker.SelectedIndex = idx;
    }

    private async void OnSave_Clicked(object? sender, EventArgs e)
    {
        ShowError(null);
        var service  = ServiceEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";
        var username = UsernameEntry.Text?.Trim();
        var email    = EmailEntry.Text?.Trim();
        var phone    = PhoneEntry.Text?.Trim();

        if (string.IsNullOrEmpty(service))
        {
            ShowError("Xizmat nomini kiriting.");
            ServiceEntry.Focus();
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            ShowError("Parolni kiriting.");
            PasswordEntry.Focus();
            return;
        }

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
            ShowError($"Xato: {ex.Message}");
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
        TogglePwBtn.Text = PasswordEntry.IsPassword ? Glyphs.View : Glyphs.Hide;
    }

    private void OnQuickGenerate_Clicked(object? sender, EventArgs e)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var pw = new char[20];
        for (int i = 0; i < pw.Length; i++)
            pw[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        PasswordEntry.Text = new string(pw);
        PasswordEntry.IsPassword = false;
        TogglePwBtn.Text = Glyphs.Hide;
    }

    private void OnPasswordTextChanged(object? sender, TextChangedEventArgs e)
        => FieldFocus.ShowStrength(e.NewTextValue ?? "", [PwStrength1, PwStrength2, PwStrength3, PwStrength4], PwStrengthLabel);

    private void ShowError(string? msg)
    {
        ErrorLabel.Text = msg ?? "";
        ErrorBox.IsVisible = !string.IsNullOrEmpty(msg);
    }

    private void SetLoading(bool loading)
    {
        LoadingOverlay.IsVisible = loading;
        SaveButton.IsEnabled = !loading;
    }

    private Task CloseModal()
        => Navigation.PopModalAsync();
}
