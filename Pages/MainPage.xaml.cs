using Microsoft.Extensions.DependencyInjection;
using PasswordManager.Models;
using PasswordManager.Services;
using System.Security.Cryptography;
using System.Text.Json;

namespace PasswordManager.Pages;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly CryptoService _crypto;
    private readonly SessionService _session;
    private readonly IServiceProvider _services;

    private enum NavSection { Passwords, Servers, Generator, Backup }
    private NavSection _currentSection = NavSection.Passwords;

    // Passwords
    private List<PasswordEntry> _allEntries = [];
    private List<Folder> _folders = [];
    private readonly Dictionary<int, Border> _folderNavBorders = new();
    private int? _currentFolderId = null;
    private string _currentSearch = "";

    // Servers
    private List<ServerEntry> _allServers = [];
    private string _serverSearch = "";

    private bool _initialized;

    public MainPage(DatabaseService db, CryptoService crypto, SessionService session, IServiceProvider services)
    {
        InitializeComponent();
        _db = db;
        _crypto = crypto;
        _session = session;
        _services = services;

        _session.SessionExpired += OnSessionExpired;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UserNameLabel.Text = _session.UserFullName;
        InitialsLabel.Text = _session.UserInitials;

        if (!_initialized)
        {
            _initialized = true;
            _currentFolderId = null;
            _folders = await _db.GetFoldersAsync();
            BuildFolderNav(_folders);
            SetNavActive(NavSection.Passwords);
        }

        await RefreshCurrentSectionAsync();

#if WINDOWS
        AttachWindowActivityTracking();
#endif
    }

    private async Task RefreshCurrentSectionAsync()
    {
        switch (_currentSection)
        {
            case NavSection.Passwords:
                await LoadPasswordsAsync();
                break;
            case NavSection.Servers:
                await LoadServersAsync();
                break;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _session.SessionExpired -= OnSessionExpired;
    }

    // ─── Session ──────────────────────────────────────────────────────────────

    private void OnSessionExpired(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _crypto.ClearKey();
            _session.Logout();
            if (Application.Current is App app) app.NavigateToLogin();
        });
    }

    private void OnLock_Tapped(object? sender, TappedEventArgs e)
    {
        _crypto.ClearKey();
        _session.Logout();
        if (Application.Current is App app) app.NavigateToLogin();
    }

    // ─── Navigation ───────────────────────────────────────────────────────────

    private async void OnNavPasswords_Tapped(object? sender, TappedEventArgs e)
    {
        _session.ResetActivity();
        _currentFolderId = null;
        PasswordsHeaderLabel.Text = "Parollar";
        SetNavActive(NavSection.Passwords);
        await LoadPasswordsAsync();
    }

    private async void OnNavServers_Tapped(object? sender, TappedEventArgs e)
    {
        _session.ResetActivity();
        SetNavActive(NavSection.Servers);
        await LoadServersAsync();
    }

    private void OnNavGenerator_Tapped(object? sender, TappedEventArgs e)
    {
        _session.ResetActivity();
        SetNavActive(NavSection.Generator);
    }

    private void OnNavBackup_Tapped(object? sender, TappedEventArgs e)
    {
        _session.ResetActivity();
        SetNavActive(NavSection.Backup);
    }

    private void SetNavActive(NavSection section)
    {
        _currentSection = section;
        PasswordsView.IsVisible = section == NavSection.Passwords;
        ServersView.IsVisible   = section == NavSection.Servers;
        GeneratorView.IsVisible = section == NavSection.Generator;
        BackupView.IsVisible    = section == NavSection.Backup;

        HighlightNav(NavServers,   section == NavSection.Servers);
        HighlightNav(NavGenerator, section == NavSection.Generator);
        HighlightNav(NavBackup,    section == NavSection.Backup);
        HighlightPasswordsNav();
    }

    private static void HighlightNav(Border nav, bool active)
    {
        nav.Background = active ? Color.FromArgb("#1E2540") : Colors.Transparent;
        if (nav.Content is HorizontalStackLayout stack && stack.Children[1] is Label label)
        {
            label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
            label.TextColor = active ? Color.FromArgb("#F0F2FF") : Color.FromArgb("#8A8FAB");
        }
    }

    private void HighlightPasswordsNav()
    {
        bool hammasi = _currentSection == NavSection.Passwords && _currentFolderId == null;
        HighlightNav(NavPasswords, hammasi);

        // Also update the icon label in NavServers header
        if (NavServers.Content is HorizontalStackLayout srvStack && srvStack.Children[0] is Label srvIcon)
        {
            bool srvActive = _currentSection == NavSection.Servers;
            srvIcon.TextColor = srvActive ? Color.FromArgb("#F0F2FF") : Color.FromArgb("#8A8FAB");
        }

        foreach (var (folderId, border) in _folderNavBorders)
        {
            bool active = _currentSection == NavSection.Passwords && folderId == _currentFolderId;
            border.Background = active ? Color.FromArgb("#1E2540") : Colors.Transparent;
            if (border.Content is Grid grid && grid.Children.Count >= 2 && grid.Children[1] is Label lbl)
            {
                lbl.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
                lbl.TextColor = active ? Color.FromArgb("#F0F2FF") : Color.FromArgb("#8A8FAB");
            }
        }
    }

    // ─── Folder management ────────────────────────────────────────────────────

    private void BuildFolderNav(List<Folder> folders)
    {
        FolderNavContainer.Children.Clear();
        _folderNavBorders.Clear();

        foreach (var folder in folders)
        {
            var border = CreateFolderNavItem(folder);
            _folderNavBorders[folder.Id] = border;
            FolderNavContainer.Children.Add(border);
        }

        HighlightPasswordsNav();
    }

    private Border CreateFolderNavItem(Folder folder)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var icon = new Label { Text = "F", FontSize = 14, VerticalOptions = LayoutOptions.Center,
                               TextColor = Color.FromArgb("#8A8FAB") };

        var nameLabel = new Label
        {
            Text = folder.Name,
            FontSize = 13,
            TextColor = Color.FromArgb("#8A8FAB"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var deleteBtn = new Button
        {
            Text = "x",
            FontSize = 11,
            TextColor = Color.FromArgb("#4A4F6A"),
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0,
            HeightRequest = 28,
            WidthRequest = 28,
            Padding = new Thickness(0),
            VerticalOptions = LayoutOptions.Center
        };
        deleteBtn.Clicked += async (_, _) => await DeleteFolderAsync(folder);

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(nameLabel, 1);
        Grid.SetColumn(deleteBtn, 2);

        grid.Children.Add(icon);
        grid.Children.Add(nameLabel);
        grid.Children.Add(deleteBtn);

        var border = new Border
        {
            Background = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
            Stroke = Colors.Transparent,
            Padding = new Thickness(14, 9)
        };
        border.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            _session.ResetActivity();
            _currentFolderId = folder.Id;
            PasswordsHeaderLabel.Text = folder.Name;
            SetNavActive(NavSection.Passwords);
            await LoadPasswordsAsync();
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private async void OnAddFolder_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        var name = await DisplayPromptAsync(
            "Yangi papka",
            "Papka nomini kiriting:",
            "Yaratish", "Bekor qilish",
            "Masalan: Ijtimoiy tarmoqlar",
            maxLength: 50);

        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();

        var folder = new Folder { Name = name, CreatedAt = DateTime.UtcNow };
        folder.Id = await _db.AddFolderAsync(folder);

        _folders.Add(folder);
        _folders.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        BuildFolderNav(_folders);
    }

    private async Task DeleteFolderAsync(Folder folder)
    {
        bool confirm = await DisplayAlertAsync(
            "Papkani o'chirish",
            $"«{folder.Name}» papkasini o'chirasizmi?\nUndagi parollar 'Hammasi' bo'limida saqlanib qoladi.",
            "O'chirish", "Bekor qilish");
        if (!confirm) return;

        await _db.DeleteFolderAsync(folder.Id);
        _folders.Remove(folder);

        if (_currentFolderId == folder.Id)
        {
            _currentFolderId = null;
            PasswordsHeaderLabel.Text = "Parollar";
        }

        BuildFolderNav(_folders);
        await LoadPasswordsAsync();
    }

    // ─── Password list ────────────────────────────────────────────────────────

    private async Task LoadPasswordsAsync()
    {
        _allEntries = _currentFolderId.HasValue
            ? await _db.GetByFolderAsync(_currentFolderId.Value)
            : await _db.GetAllAsync();
        ApplyPasswordFilter();
    }

    private void ApplyPasswordFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(_currentSearch)
            ? _allEntries
            : _allEntries.Where(e => e.ServiceName.Contains(_currentSearch, StringComparison.OrdinalIgnoreCase)).ToList();

        PasswordList.ItemsSource = filtered;
        EntryCountLabel.Text = $"{filtered.Count} ta yozuv";
    }

    private void OnSearch_Changed(object? sender, TextChangedEventArgs e)
    {
        _session.ResetActivity();
        _currentSearch = e.NewTextValue ?? "";
        ApplyPasswordFilter();
    }

    // ─── Password CRUD ────────────────────────────────────────────────────────

    private async void OnAddPassword_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        try
        {
            var page = _services.GetRequiredService<AddEditPasswordPage>();
            page.Configure(null, async () => await LoadPasswordsAsync(), _currentFolderId);
            await Navigation.PushModalAsync(page);
        }
        catch (Exception ex) { await DisplayAlertAsync("Xato", ex.Message, "OK"); }
    }

    private async void OnViewEntry_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is Button btn && btn.CommandParameter is int id)
        {
            var entry = await _db.GetByIdAsync(id);
            if (entry == null) return;
            try
            {
                var page = _services.GetRequiredService<PasswordDetailPage>();
                page.Configure(entry);
                await Navigation.PushModalAsync(page);
            }
            catch (Exception ex) { await DisplayAlertAsync("Xato", ex.Message, "OK"); }
        }
    }

    private async void OnEditEntry_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is Button btn && btn.CommandParameter is int id)
        {
            var entry = await _db.GetByIdAsync(id);
            if (entry == null) return;
            try
            {
                var page = _services.GetRequiredService<AddEditPasswordPage>();
                page.Configure(entry, async () => await LoadPasswordsAsync());
                await Navigation.PushModalAsync(page);
            }
            catch (Exception ex) { await DisplayAlertAsync("Xato", ex.Message, "OK"); }
        }
    }

    private async void OnDeleteEntry_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is Button btn && btn.CommandParameter is int id)
        {
            var entry = _allEntries.FirstOrDefault(x => x.Id == id);
            if (entry == null) return;

            bool confirm = await DisplayAlertAsync(
                "O'chirishni tasdiqlash",
                $"«{entry.ServiceName}» ni o'chirishni xohlaysizmi?",
                "O'chirish", "Bekor qilish");

            if (!confirm) return;
            await _db.DeleteAsync(id);
            await LoadPasswordsAsync();
        }
    }

    // ─── Server list ──────────────────────────────────────────────────────────

    private async Task LoadServersAsync()
    {
        _allServers = await _db.GetAllServersAsync();
        ApplyServerFilter();
    }

    private void ApplyServerFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(_serverSearch)
            ? _allServers
            : _allServers.Where(s =>
                s.Name.Contains(_serverSearch, StringComparison.OrdinalIgnoreCase) ||
                s.Host.Contains(_serverSearch, StringComparison.OrdinalIgnoreCase)).ToList();

        ServerList.ItemsSource = filtered;
        ServerCountLabel.Text = $"{filtered.Count} ta server";
    }

    private void OnServerSearch_Changed(object? sender, TextChangedEventArgs e)
    {
        _session.ResetActivity();
        _serverSearch = e.NewTextValue ?? "";
        ApplyServerFilter();
    }

    // ─── Server CRUD ──────────────────────────────────────────────────────────

    private async void OnAddServer_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        try
        {
            var page = _services.GetRequiredService<AddEditServerPage>();
            page.Configure(null, async () => await LoadServersAsync());
            await Navigation.PushModalAsync(page);
        }
        catch (Exception ex) { await DisplayAlertAsync("Xato", ex.Message, "OK"); }
    }

    private async void OnViewServer_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is Button btn && btn.CommandParameter is int id)
        {
            var server = await _db.GetServerByIdAsync(id);
            if (server == null) return;
            try
            {
                var page = _services.GetRequiredService<ServerDetailPage>();
                page.Configure(server);
                await Navigation.PushModalAsync(page);
            }
            catch (Exception ex) { await DisplayAlertAsync("Xato", ex.Message, "OK"); }
        }
    }

    private async void OnEditServer_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is Button btn && btn.CommandParameter is int id)
        {
            var server = await _db.GetServerByIdAsync(id);
            if (server == null) return;
            try
            {
                var page = _services.GetRequiredService<AddEditServerPage>();
                page.Configure(server, async () => await LoadServersAsync());
                await Navigation.PushModalAsync(page);
            }
            catch (Exception ex) { await DisplayAlertAsync("Xato", ex.Message, "OK"); }
        }
    }

    private async void OnDeleteServer_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is Button btn && btn.CommandParameter is int id)
        {
            var server = _allServers.FirstOrDefault(s => s.Id == id);
            if (server == null) return;

            bool confirm = await DisplayAlertAsync(
                "O'chirishni tasdiqlash",
                $"«{server.Name}» serverni o'chirishni xohlaysizmi?",
                "O'chirish", "Bekor qilish");

            if (!confirm) return;
            await _db.DeleteServerAsync(id);
            await LoadServersAsync();
        }
    }

    // ─── Generator ────────────────────────────────────────────────────────────

    private void OnLengthChanged(object? sender, ValueChangedEventArgs e)
    {
        _session.ResetActivity();
        LengthValueLabel.Text = ((int)e.NewValue).ToString();
    }

    private void OnGenerate_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        var length    = (int)LengthSlider.Value;
        var useUpper  = UppercaseCheck.IsChecked;
        var useLower  = LowercaseCheck.IsChecked;
        var useDigits = DigitsCheck.IsChecked;
        var useSymbols= SymbolsCheck.IsChecked;
        var keywords  = KeywordsEntry.Text ?? "";

        if (!useUpper && !useLower && !useDigits && !useSymbols && string.IsNullOrEmpty(keywords))
        {
            _ = DisplayAlertAsync("Xato", "Kamida bitta belgi turini tanlang.", "OK");
            return;
        }

        var pw = GeneratePassword(length, useUpper, useLower, useDigits, useSymbols, keywords);
        GeneratedPasswordLabel.Text = pw;
        GeneratedPasswordLabel.TextColor = Color.FromArgb("#F0F2FF");
        UpdateGenStrength(pw);
    }

    private static string GeneratePassword(int length, bool upper, bool lower, bool digits, bool symbols, string keywordsRaw)
    {
        const string uppers = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowers = "abcdefghijklmnopqrstuvwxyz";
        const string nums   = "0123456789";
        const string syms   = "!@#$%^&*()_+-=[]{}|;:,.?/";

        var keywords = keywordsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => k.Length > 0).ToList();

        var guaranteed = new List<char>();
        var charset    = "";
        if (upper)  { charset += uppers; guaranteed.Add(RandChar(uppers)); }
        if (lower)  { charset += lowers; guaranteed.Add(RandChar(lowers)); }
        if (digits) { charset += nums;   guaranteed.Add(RandChar(nums));   }
        if (symbols){ charset += syms;   guaranteed.Add(RandChar(syms));   }
        if (string.IsNullOrEmpty(charset) && keywords.Count == 0) charset = lowers + nums;

        string kwPart = string.Join("", keywords);
        var filler = new List<char>(guaranteed);
        int fillCount = Math.Max(0, length - kwPart.Length - filler.Count);
        if (!string.IsNullOrEmpty(charset))
            for (int i = 0; i < fillCount; i++) filler.Add(RandChar(charset));
        Shuffle(filler);

        if (keywords.Count == 0) return new string([.. filler]);

        int slots = keywords.Count + 1;
        var slotContent = Enumerable.Range(0, slots).Select(_ => new List<char>()).ToArray();
        foreach (var c in filler) slotContent[RandomNumberGenerator.GetInt32(slots)].Add(c);

        var sb = new System.Text.StringBuilder();
        for (int s = 0; s < slots; s++)
        {
            foreach (var c in slotContent[s]) sb.Append(c);
            if (s < keywords.Count) sb.Append(keywords[s]);
        }
        return sb.ToString();
    }

    private static char RandChar(string pool) => pool[RandomNumberGenerator.GetInt32(pool.Length)];

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void UpdateGenStrength(string pw)
    {
        int score = 0;
        if (pw.Length >= 8) score++;
        if (pw.Length >= 12) score++;
        if (pw.Any(char.IsUpper) && pw.Any(char.IsLower)) score++;
        if (pw.Any(char.IsDigit)) score++;
        if (pw.Any(c => !char.IsLetterOrDigit(c))) score++;
        score = Math.Min(score, 4);

        var bars = new[] { GenStrength1, GenStrength2, GenStrength3, GenStrength4 };
        var clrs = new[] { "#F56565", "#F6C343", "#4FD1A5", "#6C63FF" };
        for (int i = 0; i < 4; i++)
            bars[i].Color = Color.FromArgb(i < score ? clrs[score - 1] : "#2A2F42");
    }

    private async void OnCopyGenerated_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        var text = GeneratedPasswordLabel.Text;
        if (!string.IsNullOrEmpty(text) && text.StartsWith("Pastda") == false)
        {
            await Clipboard.SetTextAsync(text);
            await DisplayAlertAsync("Nusxalandi", "Parol clipboard ga nusxalandi.", "OK");
        }
    }

    // ─── Export ───────────────────────────────────────────────────────────────

    private void OnToggleExportPw_Clicked(object? sender, EventArgs e)
    {
        ExportPasswordEntry.IsPassword = !ExportPasswordEntry.IsPassword;
        ToggleExportPwBtn.Text = ExportPasswordEntry.IsPassword ? "O" : "H";
    }

    private async void OnExport_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        var exportPw = ExportPasswordEntry.Text?.Trim() ?? "";

        if (exportPw.Length < 6)
        {
            SetStatus(ExportStatusLabel, "Eksport paroli kamida 6 ta belgi bo'lishi kerak.", false);
            return;
        }

        try
        {
            var entries = await _db.GetAllAsync();
            var payload = new ExportPayload
            {
                Entries = entries.Select(en => new ExportEntry
                {
                    ServiceName = en.ServiceName,
                    Password    = _crypto.Decrypt(en.EncryptedPassword),
                    Username    = en.EncryptedUsername != null ? _crypto.Decrypt(en.EncryptedUsername) : null,
                    Email       = en.EncryptedEmail    != null ? _crypto.Decrypt(en.EncryptedEmail)    : null,
                    Phone       = en.EncryptedPhone    != null ? _crypto.Decrypt(en.EncryptedPhone)    : null,
                    CreatedAt   = en.CreatedAt.ToString("O"),
                    UpdatedAt   = en.UpdatedAt.ToString("O")
                }).ToList()
            };

            var innerJson = JsonSerializer.Serialize(payload, JsonOpts);
            var encrypted = _crypto.EncryptWithPassword(innerJson, exportPw, out string salt);

            var file = new ExportFile
            {
                ExportedAt = DateTime.UtcNow.ToString("O"),
                EntryCount = payload.Entries.Count,
                Salt       = salt,
                Data       = encrypted
            };

            var json = JsonSerializer.Serialize(file, JsonOpts);
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var name = $"SecureVault_{DateTime.Now:yyyyMMdd_HHmmss}.svault";
            var path = System.IO.Path.Combine(docs, name);

            await File.WriteAllTextAsync(path, json, System.Text.Encoding.UTF8);

            ExportPasswordEntry.Text = "";
            SetStatus(ExportStatusLabel, $"OK: {payload.Entries.Count} ta parol saqlandi:\n{path}", true);
        }
        catch (Exception ex)
        {
            SetStatus(ExportStatusLabel, $"Xato: {ex.Message}", false);
        }
    }

    // ─── Import ───────────────────────────────────────────────────────────────

    private void OnToggleImportPw_Clicked(object? sender, EventArgs e)
    {
        ImportPasswordEntry.IsPassword = !ImportPasswordEntry.IsPassword;
        ToggleImportPwBtn.Text = ImportPasswordEntry.IsPassword ? "O" : "H";
    }

    private async void OnBrowseImportFile_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "SecureVault eksport faylini tanlang",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".svault", ".json"] }
                })
            });
            if (result != null)
                ImportFilePathEntry.Text = result.FullPath;
        }
        catch (Exception ex)
        {
            SetStatus(ImportStatusLabel, $"Xato: {ex.Message}", false);
        }
    }

    private async void OnImport_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        var filePath = ImportFilePathEntry.Text?.Trim() ?? "";
        var importPw = ImportPasswordEntry.Text ?? "";

        if (!File.Exists(filePath))
        {
            SetStatus(ImportStatusLabel, "Fayl topilmadi. Iltimos fayl tanlang.", false);
            return;
        }
        if (string.IsNullOrEmpty(importPw))
        {
            SetStatus(ImportStatusLabel, "Eksport parolini kiriting.", false);
            return;
        }

        try
        {
            var fileContent = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
            var exportFile  = JsonSerializer.Deserialize<ExportFile>(fileContent, JsonOpts);

            if (exportFile == null || string.IsNullOrEmpty(exportFile.Data))
            {
                SetStatus(ImportStatusLabel, "Fayl formati noto'g'ri.", false);
                return;
            }

            string innerJson;
            try
            {
                innerJson = _crypto.DecryptWithPassword(exportFile.Data, importPw, exportFile.Salt);
            }
            catch
            {
                SetStatus(ImportStatusLabel, "Noto'g'ri parol yoki buzilgan fayl.", false);
                return;
            }

            var payload = JsonSerializer.Deserialize<ExportPayload>(innerJson, JsonOpts);
            if (payload?.Entries == null)
            {
                SetStatus(ImportStatusLabel, "Fayl tarkibi noto'g'ri.", false);
                return;
            }

            int count = 0;
            var now = DateTime.UtcNow;
            foreach (var en in payload.Entries)
            {
                if (string.IsNullOrEmpty(en.ServiceName) || string.IsNullOrEmpty(en.Password)) continue;

                var created = DateTime.TryParse(en.CreatedAt, out var dt) ? dt : now;
                await _db.AddAsync(new PasswordEntry
                {
                    ServiceName       = en.ServiceName,
                    EncryptedPassword = _crypto.Encrypt(en.Password),
                    EncryptedUsername = en.Username != null ? _crypto.Encrypt(en.Username) : null,
                    EncryptedEmail    = en.Email    != null ? _crypto.Encrypt(en.Email)    : null,
                    EncryptedPhone    = en.Phone    != null ? _crypto.Encrypt(en.Phone)    : null,
                    FolderId          = null,
                    CreatedAt         = created,
                    UpdatedAt         = now
                });
                count++;
            }

            ImportPasswordEntry.Text = "";
            ImportFilePathEntry.Text = "";
            await LoadPasswordsAsync();
            SetStatus(ImportStatusLabel, $"OK: {count} ta parol import qilindi.", true);
        }
        catch (Exception ex)
        {
            SetStatus(ImportStatusLabel, $"Xato: {ex.Message}", false);
        }
    }

    private static void SetStatus(Label label, string msg, bool success)
    {
        label.Text      = msg;
        label.TextColor = success ? Color.FromArgb("#4FD1A5") : Color.FromArgb("#F56565");
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // ─── Windows activity tracking ────────────────────────────────────────────

#if WINDOWS
    private bool _trackingAttached;

    private void AttachWindowActivityTracking()
    {
        if (_trackingAttached) return;
        _trackingAttached = true;
        var windows = Application.Current?.Windows;
        if (windows?.Count > 0 &&
            windows[0].Handler?.PlatformView is Microsoft.UI.Xaml.Window win &&
            win.Content is Microsoft.UI.Xaml.UIElement ui)
        {
            ui.PointerMoved += (_, _) => _session.ResetActivity();
            ui.KeyDown      += (_, _) => _session.ResetActivity();
        }
    }
#endif
}
