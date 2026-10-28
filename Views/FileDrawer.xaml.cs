using System.Security.Cryptography;
using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Views;

/// <summary>Right-hand panel for one encrypted file: decrypts it, shows .env variables or text, saves to disk.</summary>
public partial class FileDrawer : ContentView
{
    private const string Mask = "••••••••••";
    private const int MaxPreviewChars = 200_000;

    private FileVaultService? _vault;
    private SecureFile? _file;
    private string _name = "";
    private byte[]? _content;
    private string _text = "";
    private List<KeyValuePair<string, string>> _env = [];
    private readonly List<(Label Label, string Value)> _envValues = [];
    private bool _revealed;
    private int _version;

    public event EventHandler? CloseRequested;
    public event EventHandler<SecureFile>? EditRequested;
    public event EventHandler<SecureFile>? DeleteRequested;
    public event EventHandler<string>? Notify;

    public FileDrawer()
    {
        InitializeComponent();
    }

    public int? FileId => _file?.Id;

    public async Task ShowAsync(FileCard card, FileVaultService vault)
    {
        Clear();
        int version = ++_version;
        _vault = vault;
        _file = card.File;
        _name = card.Name;

        int tone = Monogram.Tone(card.Badge);
        TileBorder.SetDynamicResource(BackgroundColorProperty, $"Tone{tone}Bg");
        BadgeLabel.SetDynamicResource(Label.TextColorProperty, $"Tone{tone}Fg");
        BadgeLabel.Text = card.Badge;
        NameLabel.Text = card.Name;
        MetaLabel.Text = $"{card.SizeText} · {card.File.UpdatedAt.ToLocalTime():dd.MM.yyyy} da yangilangan";
        SizeLabel.Text = $"{card.SizeText} ({card.File.SizeBytes:N0} bayt)";
        CreatedLabel.Text = card.File.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        UpdatedLabel.Text = card.File.UpdatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        BlobLabel.Text = vault.BlobFileName(card.File);
        NoteLabel.Text = card.Note;
        NoteSection.IsVisible = !string.IsNullOrWhiteSpace(card.Note);
        SetStatus("Shifr ochilmoqda…", "SurfaceAlt", "Border", "TextSecondary", Glyphs.Lock);

        byte[] content;
        try
        {
            content = await vault.ReadAsync(card.File);
        }
        catch (CryptographicException)
        {
            if (version == _version)
                SetStatus("Fayl buzilgan yoki volume'da o'zgartirilgan — shifrni ochib bo'lmadi.", "DangerSoft", "DangerBorder", "Danger", Glyphs.Warning);
            return;
        }
        catch (Exception ex)
        {
            if (version == _version)
                SetStatus(ex.Message, "DangerSoft", "DangerBorder", "Danger", Glyphs.Warning);
            return;
        }

        // Another file was opened while this one was decrypting
        if (version != _version)
        {
            CryptographicOperations.ZeroMemory(content);
            return;
        }

        _content = content;
        SetStatus("AES-256-GCM bilan shifrlangan · butunligi tasdiqlandi", "SuccessSoft", "SuccessBorder", "Success", Glyphs.Lock);

        if (!FileKinds.TryDecodeText(content, out _text))
        {
            BinarySection.IsVisible = true;
            return;
        }

        _env = FileKinds.ParseEnv(_text);
        if (_env.Count > 0)
        {
            BuildEnvRows();
            EnvTitleLabel.Text = $"O'zgaruvchilar ({_env.Count})";
            EnvSection.IsVisible = true;
        }
        else
        {
            int lines = _text.Count(c => c == '\n') + 1;
            TextTitleLabel.Text = $"Tarkib ({lines} qator)";
            TextSection.IsVisible = true;
        }
        UpdateReveal();
    }

    /// <summary>Forget the decrypted content (on close / lock / switching files).</summary>
    public void Clear()
    {
        _version++;
        if (_content != null) CryptographicOperations.ZeroMemory(_content);
        _content = null;
        _text = "";
        _env = [];
        _envValues.Clear();
        _revealed = false;
        _file = null;

        EnvRows.Children.Clear();
        ContentLabel.Text = "";
        EnvSection.IsVisible = TextSection.IsVisible = BinarySection.IsVisible = false;
    }

    private void BuildEnvRows()
    {
        EnvRows.Children.Clear();
        _envValues.Clear();
        var iconButton = (Style)Application.Current!.Resources["IconButton"];
        var mono = (string)Application.Current.Resources["MonoFont"];

        for (int i = 0; i < _env.Count; i++)
        {
            var (key, value) = (_env[i].Key, _env[i].Value);
            if (i > 0)
            {
                var line = new BoxView { HeightRequest = 1 };
                line.SetDynamicResource(BoxView.ColorProperty, "Border");
                EnvRows.Children.Add(line);
            }

            var keyLabel = new Label
            {
                Text = key, FontFamily = mono, FontSize = 13, FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center
            };
            var valueLabel = new Label
            {
                FontFamily = mono, FontSize = 13,
                LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center
            };
            valueLabel.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
            _envValues.Add((valueLabel, value));

            var copy = new Button { Text = Glyphs.Copy, Style = iconButton };
            ToolTipProperties.SetText(copy, $"{key} qiymatini nusxalash");
            copy.Clicked += async (_, _) =>
            {
                await Clipboard.SetTextAsync(value);
                Notify?.Invoke(this, $"{key} nusxalandi");
            };

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(1.3, GridUnitType.Star)),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10,
                Padding = new Thickness(14, 4, 6, 4)
            };
            row.Add(keyLabel, 0);
            row.Add(valueLabel, 1);
            row.Add(copy, 2);
            EnvRows.Children.Add(row);
        }
    }

    private void UpdateReveal()
    {
        string glyph = _revealed ? Glyphs.Hide : Glyphs.View;
        EnvRevealButton.Text = TextRevealButton.Text = glyph;

        foreach (var (label, value) in _envValues)
            label.Text = !_revealed ? Mask : value.Length == 0 ? "(bo'sh)" : value;

        if (TextSection.IsVisible)
        {
            ContentLabel.Text = !_revealed
                ? "Tarkib yashirilgan — ko'rish uchun yuqoridagi ko'z tugmasini bosing."
                : _text.Length > MaxPreviewChars ? _text[..MaxPreviewChars] + "\n…" : _text;
            ContentLabel.SetDynamicResource(Label.TextColorProperty, _revealed ? "TextPrimary" : "TextMuted");
        }
    }

    private void SetStatus(string text, string bg, string stroke, string fg, string glyph)
    {
        StatusBox.SetDynamicResource(BackgroundColorProperty, bg);
        StatusBox.SetDynamicResource(Border.StrokeProperty, stroke);
        StatusLabel.Text = text;
        StatusLabel.SetDynamicResource(Label.TextColorProperty, fg);
        StatusGlyph.Text = glyph;
        StatusGlyph.SetDynamicResource(Label.TextColorProperty, fg);
    }

    private void OnReveal_Clicked(object? sender, EventArgs e)
    {
        _revealed = !_revealed;
        UpdateReveal();
    }

    private async void OnCopyAll_Clicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_text)) return;
        await Clipboard.SetTextAsync(_text);
        Notify?.Invoke(this, $"«{_name}» tarkibi nusxalandi");
    }

    private async void OnSave_Clicked(object? sender, EventArgs e)
    {
        var page = FileExport.PageOf(this);
        if (_content == null)
        {
            if (page != null) await page.DisplayAlertAsync("Saqlab bo'lmaydi", "Faylning shifri ochilmagan.", "OK");
            return;
        }
        try
        {
            var path = await FileExport.SaveAsync(_name, _content, page);
            if (path != null) Notify?.Invoke(this, $"Saqlandi: {path}");
        }
        catch (Exception ex)
        {
            if (page != null) await page.DisplayAlertAsync("Faylni saqlab bo'lmadi", ex.Message, "OK");
        }
    }

    private void OnClose_Clicked(object? sender, EventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnEdit_Clicked(object? sender, EventArgs e)
    {
        if (_file != null) EditRequested?.Invoke(this, _file);
    }

    private void OnDelete_Clicked(object? sender, EventArgs e)
    {
        if (_file != null) DeleteRequested?.Invoke(this, _file);
    }
}
