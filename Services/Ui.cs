namespace PasswordManager.Services;

/// <summary>Segoe Fluent Icons code points used across the app.</summary>
public static class Glyphs
{
    public const string Copy      = "";
    public const string View      = "";
    public const string Hide      = "";
    public const string Edit      = "";
    public const string Delete    = "";
    public const string Add       = "";
    public const string Close     = "";
    public const string Search    = "";
    public const string Lock      = "";
    public const string Folder    = "";
    public const string Key       = "";
    public const string Bolt      = "";
    public const string Archive   = "";
    public const string Sun       = "";
    public const string Moon      = "";
    public const string Check     = "";
    public const string Shield    = "";
    public const string Network   = "";
    public const string Database  = "";
    public const string Monitor   = "";
    public const string Terminal  = "";
    public const string Globe     = "";
    public const string Person    = "";
    public const string Upload    = "";
    public const string Download  = "";
    public const string Warning   = "";
    public const string Info      = "";
}

/// <summary>Password strength scoring shared by all screens (0 = empty … 4 = strong).</summary>
public static class Strength
{
    public static int Score(string pw)
    {
        if (string.IsNullOrEmpty(pw)) return 0;
        int score = 0;
        if (pw.Length >= 8) score++;
        if (pw.Length >= 12) score++;
        if (pw.Any(char.IsUpper) && pw.Any(char.IsLower)) score++;
        if (pw.Any(char.IsDigit)) score++;
        if (pw.Any(c => !char.IsLetterOrDigit(c))) score++;
        return Math.Clamp(score, 1, 4);
    }

    public static string Label(int score) => score switch
    {
        1 => "Kuchsiz",
        2 => "O'rtacha",
        3 => "Yaxshi",
        4 => "Kuchli",
        _ => ""
    };

    /// <summary>Theme key of the text / bar color for a score.</summary>
    public static string ColorKey(int score) => score switch
    {
        1 => "Danger",
        2 => "Warning",
        _ => "Success"
    };

    public static string BarKey(int score) => score switch
    {
        1 => "Danger",
        2 => "Warning",
        _ => "SuccessBar"
    };

    public static bool IsWeak(int score) => score is > 0 and <= 2;
}

/// <summary>Deterministic monogram + color tone for a service name.</summary>
public static class Monogram
{
    public const int ToneCount = 7;

    public static string For(string name)
    {
        var words = name.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
        var w = words.Length == 1 ? words[0] : "?";
        return w.Length >= 2
            ? $"{char.ToUpperInvariant(w[0])}{char.ToLowerInvariant(w[1])}"
            : w.ToUpperInvariant();
    }

    public static int Tone(string name)
    {
        int h = 0;
        foreach (var c in name.ToLowerInvariant()) h = unchecked(h * 31 + c);
        return Math.Abs(h % ToneCount);
    }
}

public static class Greeting
{
    public static string Now() => DateTime.Now.Hour switch
    {
        >= 5 and < 12  => "Xayrli tong",
        >= 12 and < 18 => "Xayrli kun",
        >= 18 and < 23 => "Xayrli kech",
        _              => "Xayrli tun"
    };
}
