namespace PasswordManager.Models;

/// <summary>
/// An encrypted file. The row (DB) keeps the metadata; the content lives in the
/// volume folder as an AES-GCM blob named after <see cref="BlobId"/>.
/// </summary>
public class SecureFile
{
    public int Id { get; set; }
    public string EncryptedName { get; set; } = "";
    public string BlobId { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? EncryptedNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
