using System.Text;
using System.Text.RegularExpressions;

namespace PasswordManager.Services;

/// <summary>Helpers for showing stored files: type badge, text detection and .env parsing.</summary>
public static partial class FileKinds
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".txt", ".json", ".yml", ".yaml", ".xml", ".ini", ".conf", ".cfg", ".config", ".toml",
        ".properties", ".pem", ".key", ".crt", ".cer", ".csr", ".pub", ".sh", ".ps1", ".bat", ".cmd",
        ".sql", ".md", ".csv", ".log", ".npmrc", ".gitconfig", ".htpasswd", ".tf", ".tfvars"
    };

    public static bool IsEnvName(string name)
        => name.StartsWith(".env", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".env", StringComparison.OrdinalIgnoreCase);

    /// <summary>Short type label for the card tile: ENV, PEM, JSON, KEY…</summary>
    public static string Badge(string name)
    {
        if (IsEnvName(name)) return "ENV";
        var ext = Path.GetExtension(name);   // for dotfiles like ".npmrc" this is the whole name
        if (ext.Length <= 1) return "FILE";
        var label = ext.TrimStart('.').ToUpperInvariant();
        return label.Length > 4 ? label[..4] : label;
    }

    /// <summary>Guess from the name alone (used on cards, before anything is decrypted).</summary>
    public static bool LooksTextual(string name)
        => IsEnvName(name) || TextExtensions.Contains(Path.GetExtension(name)) || (name.StartsWith('.') && !name[1..].Contains('.'));

    /// <summary>Strict UTF-8 decode; false for binary content (NULs or invalid sequences).</summary>
    public static bool TryDecodeText(byte[] bytes, out string text)
    {
        text = "";
        if (bytes.AsSpan(0, Math.Min(bytes.Length, 8192)).IndexOf((byte)0) >= 0) return false;
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            if (text.Length > 0 && text[0] == '﻿') text = text[1..];
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_.\-]*)\s*=\s*(.*)$")]
    private static partial Regex EnvLine();

    /// <summary>
    /// KEY=VALUE pairs of a dotenv file. Returns an empty list when the text does not look like one
    /// (fewer than 60% of the meaningful lines are assignments).
    /// </summary>
    public static List<KeyValuePair<string, string>> ParseEnv(string text)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        int meaningful = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            meaningful++;
            var m = EnvLine().Match(line);
            if (!m.Success) continue;

            var value = m.Groups[2].Value.Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                value = value[1..^1];
            else if (value.IndexOf(" #", StringComparison.Ordinal) is int hash and > 0)
                value = value[..hash].TrimEnd();
            pairs.Add(new(m.Groups[1].Value, value));
        }
        return meaningful > 0 && pairs.Count >= Math.Ceiling(meaningful * 0.6) ? pairs : [];
    }
}
