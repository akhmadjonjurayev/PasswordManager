using System.ComponentModel;
using PasswordManager.Services;

namespace PasswordManager.Models;

/// <summary>Base for the selectable cards shown in the grids (selection drives the border).</summary>
public abstract class CardBase : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; init; }
    public string Monogram { get; init; } = "";
    public Color ToneBg { get; init; } = Colors.Transparent;
    public Color ToneFg { get; init; } = Colors.Black;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(Stroke));
            OnPropertyChanged(nameof(StrokeThickness));
        }
    }

    public Brush Stroke => new SolidColorBrush(IsSelected ? ThemeService.Color("Accent") : ThemeService.Color("Border"));
    public double StrokeThickness => IsSelected ? 2 : 1;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class PasswordCard : CardBase
{
    public required PasswordEntry Entry { get; init; }
    public string ServiceName => Entry.ServiceName;

    /// <summary>Decrypted login / email / phone shown under the name (first that exists).</summary>
    public string Subtitle { get; init; } = "";
    public string FolderName { get; init; } = "";
    public bool HasFolder => !string.IsNullOrEmpty(FolderName);

    public int StrengthScore { get; init; }
    public string StrengthLabel => Strength.Label(StrengthScore);
    public Color StrengthColor { get; init; } = Colors.Gray;
    public Brush StrengthBrush => new SolidColorBrush(StrengthColor);
}

public sealed class FileCard : CardBase
{
    public required SecureFile File { get; init; }
    public string Name { get; init; } = "";
    public string Note { get; init; } = "";
    public string Badge { get; init; } = "";
    public string SizeText => FileVaultService.FormatSize(File.SizeBytes);
    /// <summary>Second line on the card: the note, or when the file was last changed.</summary>
    public string Caption => string.IsNullOrWhiteSpace(Note)
        ? $"{File.UpdatedAt.ToLocalTime():dd.MM.yyyy} da yangilangan"
        : Note.ReplaceLineEndings(" ");
    public bool IsTextual { get; init; }
}

public sealed class ServerCard : CardBase
{
    public required ServerEntry Server { get; init; }
    public string Name => Server.Name;
    public string DisplayHost => Server.DisplayHost;
    public string TypeGlyph => Server.TypeGlyph;
    public string TypeLabel => Server.HasType ? Server.ServerType! : "Server";
    public string Subtitle { get; init; } = "";
}
