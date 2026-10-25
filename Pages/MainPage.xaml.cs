using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using PasswordManager.Models;
using PasswordManager.Services;
using System.Security.Cryptography;
using System.Text.Json;

namespace PasswordManager.Pages;

public partial class MainPage : ContentPage
{
    private static readonly string[] FolderDots =
        ["#17B26A", "#F04438", "#7A5AF8", "#2E90FA", "#F79009", "#06AED4", "#EE46BC"];

    private readonly DatabaseService _db;
    private readonly CryptoService _crypto;
    private readonly SessionService _session;
    private readonly IServiceProvider _services;

    private enum NavSection { Passwords, Servers, Generator, Backup }
    private NavSection _currentSection = NavSection.Passwords;

    // Sidebar items: border → its labels, plus which ones are highlighted (hover skips those)
    private sealed record NavItem(Border Border, Label? Glyph, Label Text, Label? Count);
    private readonly Dictionary<Border, NavItem> _navItems = new();
    private readonly HashSet<Border> _activeNav = [];

    // Passwords
    private List<PasswordEntry> _allEntries = [];
    private List<PasswordCard> _allCards = [];
    private List<Folder> _folders = [];
    private int? _currentFolderId;
    private string _currentSearch = "";
    private int? _selectedPasswordId;

    // Servers
    private List<ServerEntry> _allServers = [];
    private List<ServerCard> _serverCards = [];
    private string _serverSearch = "";
    private int? _selectedServerId;

    // Generator
    private bool _useUpper = true, _useLower = true, _useDigits = true, _useSymbols;
    private string _generatedPassword = "";

    private bool _initialized;
    private int _toastVersion;

    public MainPage(DatabaseService db, CryptoService crypto, SessionService session, IServiceProvider services)
    {
        InitializeComponent();
        _db = db;
        _crypto = crypto;
        _session = session;
        _services = services;

        _session.SessionExpired += OnSessionExpired;
        ThemeService.Changed += OnThemeChanged;

        FieldFocus.Attach(SearchEntry, SearchField);
        FieldFocus.Attach(ServerSearchEntry, ServerSearchField);
        FieldFocus.Attach(KeywordsEntry, KeywordsField);
        FieldFocus.Attach(ExportPasswordEntry, ExportPasswordField);
        FieldFocus.Attach(ImportPasswordEntry, ImportPasswordField);

        foreach (var nav in new[] { NavPasswords, NavServers, NavGenerator, NavBackup })
            RegisterNav(nav);

        PasswordDrawer.CloseRequested  += (_, _) => CloseDrawer();
        PasswordDrawer.EditRequested   += async (_, entry) => await OpenEditPasswordAsync(entry);
        PasswordDrawer.DeleteRequested += async (_, entry) => await DeletePasswordAsync(entry);
        PasswordDrawer.Notify          += (_, msg) => ShowToast(msg);
        ServerDrawer.CloseRequested    += (_, _) => CloseDrawer();
        ServerDrawer.EditRequested     += async (_, server) => await OpenEditServerAsync(server);
        ServerDrawer.DeleteRequested   += async (_, server) => await DeleteServerAsync(server);
        ServerDrawer.Notify            += (_, msg) => ShowToast(msg);

        PasswordList.SizeChanged += (_, _) => UpdateSpan(PasswordList);
        ServerList.SizeChanged   += (_, _) => UpdateSpan(ServerList);

        UpdateThemeButton();
        UpdateGeneratorToggles();
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

    // ─── Session / theme ──────────────────────────────────────────────────────

    private void OnSessionExpired(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(LockVault);

    private void OnLock_Clicked(object? sender, EventArgs e) => LockVault();

    private void LockVault()
    {
        _session.SessionExpired -= OnSessionExpired;
        ThemeService.Changed -= OnThemeChanged;
        PasswordDrawer.Clear();
        ServerDrawer.Clear();
        _crypto.ClearKey();
        _session.Logout();
        if (Application.Current is App app) app.NavigateToLogin();
    }

    private void OnToggleTheme_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        ThemeService.Toggle();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateThemeButton();
        // Card tones / strokes are resolved per theme, so rebuild them (no DB round-trip)
        _allCards = _allEntries.Select(BuildCard).ToList();
        ApplyPasswordFilter();
        _serverCards = _allServers.Select(BuildServerCard).ToList();
        ApplyServerFilter();
    }

    private void UpdateThemeButton()
        => ThemeButton.Text = ThemeService.IsDark ? Glyphs.Sun : Glyphs.Moon;

    // ─── Navigation ───────────────────────────────────────────────────────────

    private async void OnNavPasswords_Tapped(object? sender, TappedEventArgs e)
    {
        _session.ResetActivity();
        _currentFolderId = null;
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
        if (string.IsNullOrEmpty(_generatedPassword))
            Generate();
    }

    private void OnNavBackup_Tapped(object? sender, TappedEventArgs e)
    {
        _session.ResetActivity();
        SetNavActive(NavSection.Backup);
    }

    private void SetNavActive(NavSection section)
    {
        if (section != _currentSection)
            CloseDrawer();

        _currentSection = section;
        PasswordsView.IsVisible = section == NavSection.Passwords;
        ServersView.IsVisible   = section == NavSection.Servers;
        GeneratorView.IsVisible = section == NavSection.Generator;
        BackupView.IsVisible    = section == NavSection.Backup;

        PrimaryActionButton.Text = section == NavSection.Servers ? "Yangi server" : "Yangi parol";
        UpdateNavHighlight();
    }

    private void UpdateNavHighlight()
    {
        bool passwords = _currentSection == NavSection.Passwords;
        HighlightNav(NavPasswords, passwords && _currentFolderId == null);
        HighlightNav(NavServers,   _currentSection == NavSection.Servers);
        HighlightNav(NavGenerator, _currentSection == NavSection.Generator);
        HighlightNav(NavBackup,    _currentSection == NavSection.Backup);

        foreach (var (border, item) in _navItems)
            if (item.Glyph == null && border.BindingContext is Folder f)
                HighlightNav(border, passwords && f.Id == _currentFolderId);
    }

    private void RegisterNav(Border border)
    {
        if (border.Content is not Grid grid) return;
        var labels = grid.Children.OfType<Label>().ToList();
        // XAML items: glyph, text, [count]
        _navItems[border] = new NavItem(border, labels[0], labels[1], labels.Count > 2 ? labels[2] : null);
        AddHover(border, hover => SetNavBackground(border, hover));
    }

    private void SetNavBackground(Border border, bool hover)
    {
        if (_activeNav.Contains(border)) return;
        border.SetDynamicResource(BackgroundColorProperty, hover ? "SurfaceMuted" : "Surface");
    }

    private void HighlightNav(Border border, bool active)
    {
        if (!_navItems.TryGetValue(border, out var item)) return;

        if (active) _activeNav.Add(border); else _activeNav.Remove(border);
        border.SetDynamicResource(BackgroundColorProperty, active ? "AccentSoft" : "Surface");
        item.Glyph?.SetDynamicResource(Label.TextColorProperty, active ? "AccentOnSoft" : "TextSecondary");
        item.Text.SetDynamicResource(Label.TextColorProperty, active ? "AccentOnSoft" : "TextNav");
        item.Text.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        item.Count?.SetDynamicResource(Label.TextColorProperty, active ? "AccentOnSoft" : "TextMuted");
    }

    private static void AddHover(View view, Action<bool> onHover)
    {
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => onHover(true);
        pointer.PointerExited  += (_, _) => onHover(false);
        view.GestureRecognizers.Add(pointer);
    }

    private static void UpdateSpan(CollectionView list)
    {
        if (list.ItemsLayout is GridItemsLayout grid && list.Width > 0)
        {
            int span = Math.Clamp((int)(list.Width / 260), 1, 6);
            if (grid.Span != span) grid.Span = span;
        }
    }

    // ─── Folders ──────────────────────────────────────────────────────────────

    private void BuildFolderNav()
    {
        foreach (var border in FolderNavContainer.Children.OfType<Border>())
        {
            _navItems.Remove(border);
            _activeNav.Remove(border);
        }
        FolderNavContainer.Children.Clear();

        for (int i = 0; i < _folders.Count; i++)
        {
            var folder = _folders[i];
            int count = _allEntries.Count(e => e.FolderId == folder.Id);
            FolderNavContainer.Children.Add(CreateFolderNavItem(folder, i, count));
        }

        NoFoldersLabel.IsVisible = _folders.Count == 0;
        UpdateNavHighlight();
    }

    private Border CreateFolderNavItem(Folder folder, int index, int count)
    {
        var dot = new BoxView
        {
            WidthRequest = 10, HeightRequest = 10, CornerRadius = 3,
            Color = Color.FromArgb(FolderDots[index % FolderDots.Length]),
            VerticalOptions = LayoutOptions.Center
        };
        var name = new Label
        {
            Text = folder.Name,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        var countLabel = new Label { Text = count.ToString(), VerticalOptions = LayoutOptions.Center };
        var deleteBtn = new Button
        {
            Text = Glyphs.Delete,
            Style = (Style)Application.Current!.Resources["IconButton"],
            FontSize = 12, WidthRequest = 28, HeightRequest = 28,
            IsVisible = false
        };
        ToolTipProperties.SetText(deleteBtn, "Papkani o'chirish");
        deleteBtn.Clicked += async (_, _) => await DeleteFolderAsync(folder);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        grid.Add(dot, 0);
        grid.Add(name, 1);
        grid.Add(countLabel, 2);
        grid.Add(deleteBtn, 3);

        var border = new Border
        {
            BindingContext = folder,
            Content = grid,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(12, 0, 4, 0),
            HeightRequest = 38
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => SelectFolder(folder.Id);
        border.GestureRecognizers.Add(tap);

        _navItems[border] = new NavItem(border, null, name, countLabel);
        AddHover(border, hover =>
        {
            deleteBtn.IsVisible = hover;
            countLabel.IsVisible = !hover;
            SetNavBackground(border, hover);
        });
        return border;
    }

    private void BuildChips()
    {
        ChipsContainer.Children.Clear();
        ChipsContainer.IsVisible = _folders.Count > 0;
        if (_folders.Count == 0) return;

        ChipsContainer.Children.Add(CreateChip("Hammasi", null));
        foreach (var f in _folders)
            ChipsContainer.Children.Add(CreateChip(f.Name, f.Id));
    }

    private Border CreateChip(string text, int? folderId)
    {
        bool active = _currentFolderId == folderId;
        var label = new Label
        {
            Text = text,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };
        label.SetDynamicResource(Label.TextColorProperty, active ? "OnInverse" : "TextNav");

        var chip = new Border
        {
            Content = label,
            HeightRequest = 34,
            Padding = new Thickness(14, 0),
            Margin = new Thickness(0, 0, 8, 0),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 17 }
        };
        chip.SetDynamicResource(BackgroundColorProperty, active ? "Inverse" : "Surface");
        chip.SetDynamicResource(Border.StrokeProperty, active ? "Inverse" : "Border");

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => SelectFolder(folderId);
        chip.GestureRecognizers.Add(tap);
        return chip;
    }

    private void SelectFolder(int? folderId)
    {
        _session.ResetActivity();
        bool reload = _currentSection != NavSection.Passwords;
        _currentFolderId = folderId;
        SetNavActive(NavSection.Passwords);
        if (reload)
        {
            _ = LoadPasswordsAsync();
            return;
        }
        BuildChips();
        ApplyPasswordFilter();
    }

    private string? FolderName(int? folderId)
        => folderId.HasValue ? _folders.FirstOrDefault(f => f.Id == folderId.Value)?.Name : null;

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

        var folder = new Folder { Name = name.Trim(), CreatedAt = DateTime.UtcNow };
        folder.Id = await _db.AddFolderAsync(folder);
        await LoadPasswordsAsync();
        ShowToast($"«{folder.Name}» papkasi yaratildi");
    }

    private async Task DeleteFolderAsync(Folder folder)
    {
        bool confirm = await DisplayAlertAsync(
            "Papkani o'chirish",
            $"«{folder.Name}» papkasini o'chirasizmi?\nUndagi parollar o'chmaydi, ular papkasiz qoladi.",
            "O'chirish", "Bekor qilish");
        if (!confirm) return;

        await _db.DeleteFolderAsync(folder.Id);
        if (_currentFolderId == folder.Id)
            _currentFolderId = null;

        await LoadPasswordsAsync();
    }

    // ─── Passwords ────────────────────────────────────────────────────────────

    private async Task LoadPasswordsAsync()
    {
        _allEntries = await _db.GetAllAsync();
        _folders = await _db.GetFoldersAsync();
        _allServers = await _db.GetAllServersAsync();

        if (_currentFolderId.HasValue && _folders.All(f => f.Id != _currentFolderId.Value))
            _currentFolderId = null;

        _allCards = _allEntries.Select(BuildCard).ToList();

        BuildFolderNav();
        BuildChips();
        UpdateCounts();
        ApplyPasswordFilter();

        // Keep the drawer in sync with what is now in the database
        if (_selectedPasswordId is int id)
        {
            var entry = _allEntries.FirstOrDefault(x => x.Id == id);
            if (entry == null) CloseDrawer();
            else if (PasswordDrawer.IsVisible) ShowPasswordDrawer(entry);
        }
    }

    private PasswordCard BuildCard(PasswordEntry e)
    {
        string subtitle;
        int score;
        try
        {
            subtitle = DecryptFirst(e.EncryptedUsername, e.EncryptedEmail, e.EncryptedPhone);
            score = Strength.Score(_crypto.Decrypt(e.EncryptedPassword));
        }
        catch
        {
            subtitle = "Shifrni ochib bo'lmadi";
            score = 0;
        }

        int tone = Monogram.Tone(e.ServiceName);
        return new PasswordCard
        {
            Entry         = e,
            Id            = e.Id,
            Monogram      = Monogram.For(e.ServiceName),
            ToneBg        = ThemeService.Color($"Tone{tone}Bg"),
            ToneFg        = ThemeService.Color($"Tone{tone}Fg"),
            Subtitle      = string.IsNullOrEmpty(subtitle) ? "Login ko'rsatilmagan" : subtitle,
            FolderName    = FolderName(e.FolderId) ?? "",
            StrengthScore = score,
            StrengthColor = ThemeService.Color(Strength.ColorKey(score)),
            IsSelected    = e.Id == _selectedPasswordId
        };
    }

    private string DecryptFirst(params string?[] encrypted)
    {
        foreach (var value in encrypted)
            if (value != null)
                return _crypto.Decrypt(value);
        return "";
    }

    private void UpdateCounts()
    {
        NavPasswordsCount.Text = _allEntries.Count.ToString();
        NavServersCount.Text = _allServers.Count.ToString();
        StatTotalLabel.Text = _allEntries.Count.ToString();
        StatServersLabel.Text = _allServers.Count.ToString();

        int weak = _allCards.Count(c => Strength.IsWeak(c.StrengthScore));
        StatWeakLabel.Text = weak.ToString();
        StatWeakCaption.Text = weak > 0 ? "Kuchsiz parollar" : "Kuchsiz parol yo'q";
        StatWeakCard.SetDynamicResource(BackgroundColorProperty, weak > 0 ? "WarningSoft" : "SuccessSoft");
        StatWeakCard.SetDynamicResource(Border.StrokeProperty, weak > 0 ? "WarningBorder" : "SuccessBorder");
        StatWeakCaption.SetDynamicResource(Label.TextColorProperty, weak > 0 ? "WarningText" : "Success");
        StatWeakLabel.SetDynamicResource(Label.TextColorProperty, weak > 0 ? "WarningText" : "Success");
    }

    private void ApplyPasswordFilter()
    {
        IEnumerable<PasswordCard> query = _allCards;
        if (_currentFolderId is int folderId)
            query = query.Where(c => c.Entry.FolderId == folderId);

        bool searching = !string.IsNullOrWhiteSpace(_currentSearch);
        if (searching)
        {
            var s = _currentSearch.Trim();
            query = query.Where(c => c.ServiceName.Contains(s, StringComparison.OrdinalIgnoreCase)
                                  || c.Subtitle.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.ToList();
        PasswordList.ItemsSource = list;

        if (_currentFolderId.HasValue)
        {
            PasswordsHeaderLabel.Text = FolderName(_currentFolderId) ?? "Papka";
            EntryCountLabel.Text = $"{_allCards.Count(c => c.Entry.FolderId == _currentFolderId)} ta parol";
        }
        else
        {
            var firstName = _session.UserFullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            PasswordsHeaderLabel.Text = string.IsNullOrEmpty(firstName) ? Greeting.Now() : $"{Greeting.Now()}, {firstName}";
            EntryCountLabel.Text = $"{_allEntries.Count} ta parol shifrlangan holda saqlanmoqda";
        }
        if (searching)
            EntryCountLabel.Text = $"«{_currentSearch.Trim()}» bo'yicha {list.Count} ta natija";

        (EmptyTitleLabel.Text, EmptyHintLabel.Text) = searching
            ? ("Hech narsa topilmadi", "Boshqa so'z bilan qidirib ko'ring")
            : _currentFolderId.HasValue
                ? ("Bu papka bo'sh", "Yangi parol qo'shishda shu papkani tanlang")
                : ("Hali parol saqlanmagan", "\"Yangi parol\" tugmasi bilan birinchisini qo'shing");
    }

    private void OnSearch_Changed(object? sender, TextChangedEventArgs e)
    {
        _session.ResetActivity();
        _currentSearch = e.NewTextValue ?? "";
        ApplyPasswordFilter();
    }

    private void OnPasswordCard_Tapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not PasswordCard card) return;
        _session.ResetActivity();

        _selectedPasswordId = card.Id;
        foreach (var c in _allCards) c.IsSelected = c.Id == card.Id;
        ShowPasswordDrawer(card.Entry);
    }

    private void ShowPasswordDrawer(PasswordEntry entry)
    {
        ServerDrawer.IsVisible = false;
        PasswordDrawer.IsVisible = true;
        DrawerHost.IsVisible = true;
        PasswordDrawer.Show(entry, FolderName(entry.FolderId), _crypto);
    }

    private void CloseDrawer()
    {
        DrawerHost.IsVisible = false;
        PasswordDrawer.IsVisible = false;
        ServerDrawer.IsVisible = false;
        PasswordDrawer.Clear();
        ServerDrawer.Clear();

        _selectedPasswordId = null;
        _selectedServerId = null;
        foreach (var c in _allCards) c.IsSelected = false;
        foreach (var c in _serverCards) c.IsSelected = false;
    }

    private async void OnCopyLogin_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is not Button { CommandParameter: int id }) return;
        var entry = _allEntries.FirstOrDefault(x => x.Id == id);
        if (entry == null) return;

        var login = DecryptFirst(entry.EncryptedUsername, entry.EncryptedEmail, entry.EncryptedPhone);
        if (string.IsNullOrEmpty(login))
        {
            ShowToast("Bu yozuvda login yo'q", ok: false);
            return;
        }
        await Clipboard.SetTextAsync(login);
        ShowToast($"{entry.ServiceName}: login nusxalandi");
    }

    private async void OnCopyPassword_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is not Button { CommandParameter: int id }) return;
        var entry = _allEntries.FirstOrDefault(x => x.Id == id);
        if (entry == null) return;

        await Clipboard.SetTextAsync(_crypto.Decrypt(entry.EncryptedPassword));
        ShowToast($"{entry.ServiceName}: parol nusxalandi");
    }

    private async void OnPrimaryAction_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (_currentSection == NavSection.Servers)
        {
            await OpenEditServerAsync(null);
            return;
        }
        if (_currentSection != NavSection.Passwords)
        {
            SetNavActive(NavSection.Passwords);
            await LoadPasswordsAsync();
        }
        await OpenEditPasswordAsync(null);
    }

    private async Task OpenEditPasswordAsync(PasswordEntry? entry)
    {
        _session.ResetActivity();
        try
        {
            var page = _services.GetRequiredService<AddEditPasswordPage>();
            page.Configure(entry, async () => await LoadPasswordsAsync(), _currentFolderId);
            await Navigation.PushModalAsync(page);
        }
        catch (Exception ex) { await DisplayAlertAsync("Xato", ex.Message, "OK"); }
    }

    private async Task DeletePasswordAsync(PasswordEntry entry)
    {
        _session.ResetActivity();
        bool confirm = await DisplayAlertAsync(
            "O'chirishni tasdiqlash",
            $"«{entry.ServiceName}» ni o'chirishni xohlaysizmi?",
            "O'chirish", "Bekor qilish");
        if (!confirm) return;

        await _db.DeleteAsync(entry.Id);
        CloseDrawer();
        await LoadPasswordsAsync();
        ShowToast($"«{entry.ServiceName}» o'chirildi");
    }

    // ─── Servers ──────────────────────────────────────────────────────────────

    private async Task LoadServersAsync()
    {
        _allServers = await _db.GetAllServersAsync();
        _serverCards = _allServers.Select(BuildServerCard).ToList();
        NavServersCount.Text = _allServers.Count.ToString();
        ApplyServerFilter();

        if (_selectedServerId is int id)
        {
            var server = _allServers.FirstOrDefault(s => s.Id == id);
            if (server == null) CloseDrawer();
            else if (ServerDrawer.IsVisible) ShowServerDrawer(server);
        }
    }

    private ServerCard BuildServerCard(ServerEntry s)
    {
        string user = "";
        try
        {
            if (s.EncryptedUsername != null) user = _crypto.Decrypt(s.EncryptedUsername);
        }
        catch { user = ""; }

        int tone = Monogram.Tone(s.Name);
        return new ServerCard
        {
            Server     = s,
            Id         = s.Id,
            Monogram   = Monogram.For(s.Name),
            ToneBg     = ThemeService.Color($"Tone{tone}Bg"),
            ToneFg     = ThemeService.Color($"Tone{tone}Fg"),
            Subtitle   = string.IsNullOrEmpty(user) ? "Foydalanuvchi ko'rsatilmagan" : user,
            IsSelected = s.Id == _selectedServerId
        };
    }

    private void ApplyServerFilter()
    {
        var s = _serverSearch.Trim();
        var list = string.IsNullOrEmpty(s)
            ? _serverCards
            : _serverCards.Where(c =>
                c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                c.DisplayHost.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();

        ServerList.ItemsSource = list;
        ServerCountLabel.Text = string.IsNullOrEmpty(s)
            ? $"{_allServers.Count} ta server"
            : $"«{s}» bo'yicha {list.Count} ta natija";
    }

    private void OnServerSearch_Changed(object? sender, TextChangedEventArgs e)
    {
        _session.ResetActivity();
        _serverSearch = e.NewTextValue ?? "";
        ApplyServerFilter();
    }

    private void OnServerCard_Tapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not ServerCard card) return;
        _session.ResetActivity();

        _selectedServerId = card.Id;
        foreach (var c in _serverCards) c.IsSelected = c.Id == card.Id;
        ShowServerDrawer(card.Server);
    }

    private void ShowServerDrawer(ServerEntry server)
    {
        PasswordDrawer.IsVisible = false;
        ServerDrawer.IsVisible = true;
        DrawerHost.IsVisible = true;
        ServerDrawer.Show(server, _crypto);
    }

    private async void OnCopyHost_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is not Button { CommandParameter: int id }) return;
        var server = _allServers.FirstOrDefault(s => s.Id == id);
        if (server == null) return;

        await Clipboard.SetTextAsync(server.DisplayHost);
        ShowToast($"{server.Name}: host nusxalandi");
    }

    private async void OnCopyServerPassword_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (sender is not Button { CommandParameter: int id }) return;
        var server = _allServers.FirstOrDefault(s => s.Id == id);
        if (server == null) return;

        if (server.EncryptedPassword == null)
        {
            ShowToast("Bu serverda parol saqlanmagan", ok: false);
            return;
        }
        await Clipboard.SetTextAsync(_crypto.Decrypt(server.EncryptedPassword));
        ShowToast($"{server.Name}: parol nusxalandi");
    }

    private async Task OpenEditServerAsync(ServerEntry? server)
    {
        _session.ResetActivity();
        try
        {
            var page = _services.GetRequiredService<AddEditServerPage>();
            page.Configure(server, async () => await LoadServersAsync());
            await Navigation.PushModalAsync(page);
        }
        catch (Exception ex) { await DisplayAlertAsync("Xato", ex.Message, "OK"); }
    }

    private async Task DeleteServerAsync(ServerEntry server)
    {
        _session.ResetActivity();
        bool confirm = await DisplayAlertAsync(
            "O'chirishni tasdiqlash",
            $"«{server.Name}» serverni o'chirishni xohlaysizmi?",
            "O'chirish", "Bekor qilish");
        if (!confirm) return;

        await _db.DeleteServerAsync(server.Id);
        CloseDrawer();
        await LoadServersAsync();
        ShowToast($"«{server.Name}» o'chirildi");
    }

    // ─── Generator ────────────────────────────────────────────────────────────

    private void OnLengthChanged(object? sender, ValueChangedEventArgs e)
    {
        _session.ResetActivity();
        LengthValueLabel.Text = ((int)e.NewValue).ToString();
    }

    private void OnToggleOption_Tapped(object? sender, TappedEventArgs e)
    {
        _session.ResetActivity();
        switch (e.Parameter as string)
        {
            case "upper":   _useUpper   = !_useUpper;   break;
            case "lower":   _useLower   = !_useLower;   break;
            case "digits":  _useDigits  = !_useDigits;  break;
            case "symbols": _useSymbols = !_useSymbols; break;
        }
        UpdateGeneratorToggles();
    }

    private void UpdateGeneratorToggles()
    {
        UpdateToggle(UpperTile, UpperBox, UpperCheck, _useUpper);
        UpdateToggle(LowerTile, LowerBox, LowerCheck, _useLower);
        UpdateToggle(DigitsTile, DigitsBox, DigitsCheck, _useDigits);
        UpdateToggle(SymbolsTile, SymbolsBox, SymbolsCheck, _useSymbols);
    }

    private static void UpdateToggle(Border tile, Border box, Label check, bool on)
    {
        tile.SetDynamicResource(BackgroundColorProperty, on ? "AccentSoft" : "Surface");
        tile.SetDynamicResource(Border.StrokeProperty, on ? "Accent" : "Border");
        box.SetDynamicResource(BackgroundColorProperty, on ? "Accent" : "Surface");
        box.SetDynamicResource(Border.StrokeProperty, on ? "Accent" : "BorderStrong");
        check.SetDynamicResource(Label.TextColorProperty, "OnAccent");
        check.IsVisible = on;
    }

    private void OnGenerate_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        Generate();
    }

    private void Generate()
    {
        var keywords = KeywordsEntry.Text ?? "";
        if (!_useUpper && !_useLower && !_useDigits && !_useSymbols && string.IsNullOrWhiteSpace(keywords))
        {
            ShowToast("Kamida bitta belgi turini tanlang", ok: false);
            return;
        }

        _generatedPassword = GeneratePassword((int)LengthSlider.Value, _useUpper, _useLower, _useDigits, _useSymbols, keywords);
        GeneratedPasswordLabel.Text = _generatedPassword;
        FieldFocus.ShowStrength(_generatedPassword, [GenStrength1, GenStrength2, GenStrength3, GenStrength4], GenStrengthLabel);
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

    private async void OnCopyGenerated_Clicked(object? sender, EventArgs e)
    {
        _session.ResetActivity();
        if (string.IsNullOrEmpty(_generatedPassword)) return;
        await Clipboard.SetTextAsync(_generatedPassword);
        ShowToast("Parol nusxalandi");
    }

    // ─── Export ───────────────────────────────────────────────────────────────

    private void OnToggleExportPw_Clicked(object? sender, EventArgs e)
    {
        ExportPasswordEntry.IsPassword = !ExportPasswordEntry.IsPassword;
        ToggleExportPwBtn.Text = ExportPasswordEntry.IsPassword ? Glyphs.View : Glyphs.Hide;
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
            SetStatus(ExportStatusLabel, $"{payload.Entries.Count} ta parol saqlandi:\n{path}", true);
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
        ToggleImportPwBtn.Text = ImportPasswordEntry.IsPassword ? Glyphs.View : Glyphs.Hide;
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
            SetStatus(ImportStatusLabel, "Fayl topilmadi. Iltimos, fayl tanlang.", false);
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
            _allEntries = await _db.GetAllAsync();
            NavPasswordsCount.Text = _allEntries.Count.ToString();
            SetStatus(ImportStatusLabel, $"{count} ta parol import qilindi.", true);
        }
        catch (Exception ex)
        {
            SetStatus(ImportStatusLabel, $"Xato: {ex.Message}", false);
        }
    }

    private static void SetStatus(Label label, string msg, bool success)
    {
        label.Text = msg;
        label.IsVisible = true;
        label.SetDynamicResource(Label.TextColorProperty, success ? "Success" : "Danger");
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // ─── Toast ────────────────────────────────────────────────────────────────

    private async void ShowToast(string message, bool ok = true)
    {
        int version = ++_toastVersion;
        ToastLabel.Text = message;
        ToastGlyph.Text = ok ? Glyphs.Check : Glyphs.Info;
        Toast.IsVisible = true;
        await Toast.FadeToAsync(1, 120);
        await Task.Delay(1800);
        if (version != _toastVersion) return;
        await Toast.FadeToAsync(0, 200);
        if (version == _toastVersion) Toast.IsVisible = false;
    }

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
