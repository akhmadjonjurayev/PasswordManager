namespace PasswordManager.Models;

public class PasswordEntry
{
    public int Id { get; set; }
    public string ServiceName { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";
    public string? EncryptedUsername { get; set; }
    public string? EncryptedEmail { get; set; }
    public string? EncryptedPhone { get; set; }
    public int? FolderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool HasUsername => EncryptedUsername != null;
    public bool HasEmail    => EncryptedEmail    != null;
    public bool HasPhone    => EncryptedPhone    != null;
}
