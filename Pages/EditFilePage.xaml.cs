using System.Security.Cryptography;
using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Pages;

public partial class EditFilePage : ContentPage
{
    private readonly FileVaultService _vault;

    private SecureFile? _file;
    private Func<Task>? _onSaved;
    private byte[]? _newContent;

    public EditFilePage(FileVaultService vault)
    {
        InitializeComponent();
        _vault = vault;
        FieldFocus.Attach(NameEntry, NameField);
        FieldFocus.Attach(NoteEditor, NoteField);
    }

    public void Configure(SecureFile file, Func<Task>? onSaved)
    {
        _file = file;
        _onSaved = onSaved;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_file == null) return;
        ShowError(null);

        try
        {
            NameEntry.Text = _vault.DecryptName(_file);
            NoteEditor.Text = _vault.DecryptNote(_file) ?? "";
        }
        catch
        {
            ShowError("Ma'lumotlarni ochib bo'lmadi.");
        }
        UpdateContentInfo(null);
        NameEntry.Focus();
    }

    private void UpdateContentInfo(string? newFileName)
    {
        if (_newContent == null)
        {
            ContentTitleLabel.Text = $"Joriy fayl · {FileVaultService.FormatSize(_file!.SizeBytes)}";
            ContentHintLabel.Text = "Tarkibni yangi versiyasi bilan almashtirish mumkin";
        }
        else
        {
            ContentTitleLabel.Text = $"Yangi: {newFileName} · {FileVaultService.FormatSize(_newContent.LongLength)}";
            ContentHintLabel.Text = "Saqlanganda eski shifrlangan nusxa volume'dan o'chiriladi";
        }
    }

    private async void OnReplace_Clicked(object? sender, EventArgs e)
    {
        ShowError(null);
        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Yangi versiyani tanlang" });
            if (picked == null) return;

            var info = new FileInfo(picked.FullPath);
            if (info.Length > FileVaultService.MaxFileSize)
            {
                ShowError($"Fayl juda katta: {FileVaultService.FormatSize(info.Length)} (chegara 25 MB).");
                return;
            }
            if (_newContent != null) CryptographicOperations.ZeroMemory(_newContent);
            _newContent = await File.ReadAllBytesAsync(info.FullName);
            UpdateContentInfo(info.Name);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnSave_Clicked(object? sender, EventArgs e)
    {
        ShowError(null);
        var name = NameEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            ShowError("Fayl nomini kiriting.");
            NameEntry.Focus();
            return;
        }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ShowError("Nomda \\ / : * ? \" < > | belgilari bo'lmasin.");
            return;
        }

        LoadingOverlay.IsVisible = true;
        SaveButton.IsEnabled = false;
        try
        {
            await _vault.UpdateAsync(_file!, name, NoteEditor.Text?.Trim(), _newContent);
            if (_onSaved != null) await _onSaved();
            await CloseAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Xato: {ex.Message}");
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnCancel_Clicked(object? sender, EventArgs e) => await CloseAsync();

    private Task CloseAsync()
    {
        if (_newContent != null) CryptographicOperations.ZeroMemory(_newContent);
        _newContent = null;
        return Navigation.PopModalAsync();
    }

    private void ShowError(string? msg)
    {
        ErrorLabel.Text = msg ?? "";
        ErrorBox.IsVisible = !string.IsNullOrEmpty(msg);
    }
}
