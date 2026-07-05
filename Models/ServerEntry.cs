namespace PasswordManager.Models;

public class ServerEntry
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public string? Port { get; set; }
    public string? ServerType { get; set; }
    public string? EncryptedUsername { get; set; }
    public string? EncryptedPassword { get; set; }
    public string? EncryptedNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool HasUsername => EncryptedUsername != null;
    public bool HasPassword => EncryptedPassword != null;
    public bool HasNotes    => EncryptedNotes    != null;
    public bool HasType     => !string.IsNullOrEmpty(ServerType);

    public string DisplayHost => string.IsNullOrEmpty(Port) ? Host : $"{Host}:{Port}";

    public string TypeIcon => ServerType switch
    {
        "SSH"        => "🔐",
        "SFTP"       => "📁",
        "FTP"        => "📁",
        "HTTP/HTTPS" => "🌐",
        "RDP"        => "🖥️",
        "VNC"        => "📺",
        "MySQL"      => "🗄️",
        "PostgreSQL" => "🗄️",
        "MongoDB"    => "🗄️",
        _            => "🖥️"
    };
}
