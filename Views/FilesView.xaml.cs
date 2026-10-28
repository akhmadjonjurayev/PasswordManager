using System.Security.Cryptography;
using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Views;

/// <summary>"Shifrlangan fayllar" section: drop zone, search and the grid of encrypted files.</summary>
public partial class FilesView : ContentView
{
    private FileVaultService? _vault;
    private SessionService? _session;
    private List<FileCard> _cards = [];
    private string _search = "";
    private int? _selectedId;

    public event EventHandler<FileCard>? FileSelected;
    public event EventHandler<string>? Notify;
    /// <summary>Raised after files were added (counts in the sidebar need a refresh).</summary>
    public event EventHandler? FilesChanged;

    public FilesView()
    {
        InitializeComponent();
        FileList.SizeChanged += (_, _) =>
        {
            if (FileList.ItemsLayout is GridItemsLayout grid && FileList.Width > 0)
            {
                int span = Math.Clamp((int)(FileList.Width / 260), 1, 6);
                if (grid.Span != span) grid.Span = span;
            }
        };
    }

    public void Initialize(FileVaultService vault, SessionService session)
    {
        _vault = vault;
        _session = session;
        FieldFocus.Attach(SearchEntry, SearchField);
    }

    public int Count => _cards.Count;

    public FileCard? Find(int id) => _cards.FirstOrDefault(c => c.Id == id);

    public async Task ReloadAsync()
    {
        if (_vault == null) return;
        var files = await _vault.GetAllAsync();
        _cards = files.Select(BuildCard).ToList();
        Apply();
    }

    /// <summary>Theme changed: tones are resolved per theme, so rebuild the cards (no decryption of content).</summary>
    public void RebuildCards()
    {
        _cards = _cards.Select(c => BuildCard(c.File)).ToList();
        Apply();
    }

    public void SetSelected(int? id)
    {
        _selectedId = id;
        foreach (var c in _cards) c.IsSelected = c.Id == id;
    }

    private FileCard BuildCard(SecureFile f)
    {
        string name, note;
        try
        {
            name = _vault!.DecryptName(f);
            note = _vault.DecryptNote(f) ?? "";
        }
        catch
        {
            name = "Nomini ochib bo'lmadi";
            note = "";
        }

        var badge = FileKinds.Badge(name);
        int tone = Monogram.Tone(badge);
        return new FileCard
        {
            File       = f,
            Id         = f.Id,
            Name       = name,
            Note       = note,
            Badge      = badge,
            Monogram   = badge,
            ToneBg     = ThemeService.Color($"Tone{tone}Bg"),
            ToneFg     = ThemeService.Color($"Tone{tone}Fg"),
            IsTextual  = FileKinds.LooksTextual(name),
            IsSelected = f.Id == _selectedId
        };
    }

    private void Apply()
    {
        var s = _search.Trim();
        var list = string.IsNullOrEmpty(s)
            ? _cards
            : _cards.Where(c => c.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                             || c.Note.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        FileList.ItemsSource = list;

        long total = _cards.Sum(c => c.File.SizeBytes);
        SubtitleLabel.Text = string.IsNullOrEmpty(s)
            ? $"{_cards.Count} ta fayl · {FileVaultService.FormatSize(total)} · har biri alohida shifrlangan"
            : $"«{s}» bo'yicha {list.Count} ta natija";

        (EmptyTitleLabel.Text, EmptyHintLabel.Text) = string.IsNullOrEmpty(s)
            ? ("Hali fayl saqlanmagan", "Masalan, loyihangizning .env faylini yuqoridagi zonaga tashlang")
            : ("Hech narsa topilmadi", "Boshqa so'z bilan qidirib ko'ring");
    }

    private void OnSearch_Changed(object? sender, TextChangedEventArgs e)
    {
        _session?.ResetActivity();
        _search = e.NewTextValue ?? "";
        Apply();
    }

    private void OnCard_Tapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not FileCard card) return;
        _session?.ResetActivity();
        SetSelected(card.Id);
        FileSelected?.Invoke(this, card);
    }

    // ─── Adding ───────────────────────────────────────────────────────────────

    private async void OnPick_Clicked(object? sender, EventArgs e) => await PickAndAddAsync();

    public async Task PickAndAddAsync()
    {
        _session?.ResetActivity();
        try
        {
            var picked = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Shifrlab saqlash uchun fayl tanlang"
            });
            var paths = picked?.Where(p => p != null).Select(p => p!.FullPath).ToList() ?? [];
            if (paths.Count > 0) await AddPathsAsync(paths);
        }
        catch (Exception ex)
        {
            await Alert("Xato", ex.Message);
        }
    }

    public async Task AddPathsAsync(IReadOnlyList<string> paths)
    {
        if (_vault == null) return;
        _session?.ResetActivity();
        SetBusy(true);

        var added = new List<string>();
        var problems = new List<string>();
        try
        {
            foreach (var path in paths)
            {
                var info = new FileInfo(path);
                if (!info.Exists) { problems.Add($"{info.Name}: fayl topilmadi"); continue; }
                if (info.Length > FileVaultService.MaxFileSize)
                {
                    problems.Add($"{info.Name}: {FileVaultService.FormatSize(info.Length)} — 25 MB dan katta");
                    continue;
                }
                try
                {
                    var bytes = await File.ReadAllBytesAsync(path);
                    await _vault.AddAsync(info.Name, bytes, null);
                    CryptographicOperations.ZeroMemory(bytes);
                    added.Add(path);
                }
                catch (Exception ex)
                {
                    problems.Add($"{info.Name}: {ex.Message}");
                }
            }
        }
        finally
        {
            SetBusy(false);
        }

        await ReloadAsync();
        if (added.Count > 0)
        {
            FilesChanged?.Invoke(this, EventArgs.Empty);
            Notify?.Invoke(this, added.Count == 1
                ? $"«{Path.GetFileName(added[0])}» shifrlab saqlandi"
                : $"{added.Count} ta fayl shifrlab saqlandi");
        }
        if (problems.Count > 0)
            await Alert("Ba'zi fayllar qo'shilmadi", string.Join("\n", problems));
        if (added.Count > 0)
            await OfferDeleteOriginalsAsync(added);
    }

    /// <summary>The plaintext originals are still on disk — offer to remove them.</summary>
    private async Task OfferDeleteOriginalsAsync(List<string> paths)
    {
        var page = FileExport.PageOf(this);
        if (page == null) return;

        var list = string.Join("\n", paths.Take(5).Select(p => "• " + p)) + (paths.Count > 5 ? $"\n… va yana {paths.Count - 5} ta" : "");
        bool delete = await page.DisplayAlertAsync(
            "Asl nusxani o'chirasizmi?",
            $"Fayl omborga shifrlab saqlandi, lekin asl nusxasi diskda ochiq holda qoldi:\n\n{list}\n\nAsl nusxani o'chirish xavfsizroq.",
            "O'chirish", "Qoldirish");
        if (!delete) return;

        int removed = 0;
        foreach (var p in paths)
        {
            try { File.Delete(p); removed++; } catch { /* in use / no access — leave it */ }
        }
        Notify?.Invoke(this, removed == paths.Count
            ? "Asl nusxa diskdan o'chirildi"
            : $"{removed} / {paths.Count} ta asl nusxa o'chirildi");
    }

    private void SetBusy(bool busy)
    {
        BusyIndicator.IsVisible = busy;
        BusyIndicator.IsRunning = busy;
    }

    // ─── Drag & drop (Windows Explorer) ───────────────────────────────────────

    private void OnDragOver(object? sender, DragEventArgs e)
    {
#if WINDOWS
        if (e.PlatformArgs?.DragEventArgs is { } args &&
            args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            args.DragUIOverride.Caption = "Shifrlab saqlash";
        }
#endif
        e.AcceptedOperation = DataPackageOperation.Copy;
        SetDropHighlight(true);
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => SetDropHighlight(false);

    private async void OnDrop(object? sender, DropEventArgs e)
    {
        SetDropHighlight(false);
#if WINDOWS
        var view = e.PlatformArgs?.DragEventArgs?.DataView;
        if (view == null || !view.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            return;

        var items = await view.GetStorageItemsAsync();
        var paths = items.OfType<Windows.Storage.StorageFile>().Select(f => f.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (items.OfType<Windows.Storage.StorageFolder>().Any())
            Notify?.Invoke(this, "Papkalar qo'shilmaydi — faqat fayllar");
        if (paths.Count > 0)
            await AddPathsAsync(paths);
#else
        await Task.CompletedTask;
#endif
    }

    private void SetDropHighlight(bool on)
    {
        DropZone.SetDynamicResource(BackgroundColorProperty, on ? "AccentSoft" : "Surface");
        DropZone.SetDynamicResource(Border.StrokeProperty, on ? "Accent" : "BorderStrong");
        DropTitleLabel.Text = on ? "Qo'yib yuboring — fayl shifrlanib saqlanadi" : "Faylni shu yerga tashlang yoki tanlang";
    }

    // ─── Card buttons ─────────────────────────────────────────────────────────

    private async void OnCopy_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: FileCard card } || _vault == null) return;
        _session?.ResetActivity();
        try
        {
            var bytes = await _vault.ReadAsync(card.File);
            if (!FileKinds.TryDecodeText(bytes, out var text))
            {
                Notify?.Invoke(this, "Bu fayl matn emas — uni diskka saqlang");
                return;
            }
            await Clipboard.SetTextAsync(text);
            Notify?.Invoke(this, $"«{card.Name}» tarkibi nusxalandi");
        }
        catch (Exception ex)
        {
            await Alert("Faylni ochib bo'lmadi", ex is CryptographicException
                ? "Fayl buzilgan yoki volume'da o'zgartirilgan."
                : ex.Message);
        }
    }

    private async void OnSave_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: FileCard card } || _vault == null) return;
        _session?.ResetActivity();
        try
        {
            var bytes = await _vault.ReadAsync(card.File);
            var path = await FileExport.SaveAsync(card.Name, bytes, FileExport.PageOf(this));
            CryptographicOperations.ZeroMemory(bytes);
            if (path != null) Notify?.Invoke(this, $"Saqlandi: {path}");
        }
        catch (Exception ex)
        {
            await Alert("Faylni saqlab bo'lmadi", ex is CryptographicException
                ? "Fayl buzilgan yoki volume'da o'zgartirilgan."
                : ex.Message);
        }
    }

    private Task Alert(string title, string message)
        => FileExport.PageOf(this)?.DisplayAlertAsync(title, message, "OK") ?? Task.CompletedTask;
}
