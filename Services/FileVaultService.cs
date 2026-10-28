using System.Security.Cryptography;
using PasswordManager.Models;

namespace PasswordManager.Services;

/// <summary>
/// Encrypted file storage. Metadata (name, note, size, dates — name and note encrypted) is a row
/// in the Files table; the content is an AES-GCM blob in the "volume" folder next to vault.db,
/// one blob per file, named by a random id so nothing about the original file leaks on disk.
/// </summary>
public class FileVaultService
{
    public const long MaxFileSize = 25L * 1024 * 1024;
    private const string BlobExtension = ".svf";

    private readonly DatabaseService _db;
    private readonly CryptoService _crypto;

    public FileVaultService(DatabaseService db, CryptoService crypto)
    {
        _db = db;
        _crypto = crypto;
    }

    public string VolumeDirectory
    {
        get
        {
            var dir = Path.Combine(_db.DataDirectory, "volume");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public string BlobFileName(SecureFile f) => f.BlobId + BlobExtension;

    private string BlobPath(string blobId) => Path.Combine(VolumeDirectory, blobId + BlobExtension);

    public Task<List<SecureFile>> GetAllAsync() => _db.GetAllFilesAsync();

    public string DecryptName(SecureFile f) => _crypto.Decrypt(f.EncryptedName);

    public string? DecryptNote(SecureFile f) => f.EncryptedNote != null ? _crypto.Decrypt(f.EncryptedNote) : null;

    public async Task<SecureFile> AddAsync(string name, byte[] content, string? note, DateTime? createdAt = null)
    {
        EnsureSize(content.LongLength);
        var blobId = NewBlobId();
        await WriteBlobAsync(blobId, content);

        var now = DateTime.UtcNow;
        var file = new SecureFile
        {
            EncryptedName = _crypto.Encrypt(name),
            BlobId        = blobId,
            SizeBytes     = content.LongLength,
            EncryptedNote = string.IsNullOrWhiteSpace(note) ? null : _crypto.Encrypt(note),
            CreatedAt     = createdAt ?? now,
            UpdatedAt     = now
        };
        try
        {
            file.Id = await _db.AddFileAsync(file);
        }
        catch
        {
            TryDelete(BlobPath(blobId));
            throw;
        }
        return file;
    }

    /// <summary>Decrypts the content; throws CryptographicException if the blob was tampered with.</summary>
    public async Task<byte[]> ReadAsync(SecureFile f)
    {
        var path = BlobPath(f.BlobId);
        if (!File.Exists(path))
            throw new FileNotFoundException("Shifrlangan fayl volume papkasida topilmadi.", path);
        var blob = await File.ReadAllBytesAsync(path);
        return _crypto.DecryptFile(blob, f.BlobId);
    }

    /// <summary>Updates name / note and, when <paramref name="newContent"/> is given, the content.</summary>
    public async Task UpdateAsync(SecureFile f, string name, string? note, byte[]? newContent)
    {
        string? oldBlob = null;
        if (newContent != null)
        {
            EnsureSize(newContent.LongLength);
            var blobId = NewBlobId();
            await WriteBlobAsync(blobId, newContent);
            oldBlob = f.BlobId;
            f.BlobId = blobId;
            f.SizeBytes = newContent.LongLength;
        }

        f.EncryptedName = _crypto.Encrypt(name);
        f.EncryptedNote = string.IsNullOrWhiteSpace(note) ? null : _crypto.Encrypt(note);
        f.UpdatedAt = DateTime.UtcNow;
        await _db.UpdateFileAsync(f);

        if (oldBlob != null) TryDelete(BlobPath(oldBlob));
    }

    public async Task DeleteAsync(SecureFile f)
    {
        await _db.DeleteFileAsync(f.Id);
        TryDelete(BlobPath(f.BlobId));
    }

    /// <summary>Removes blobs / temp files that no row points to (e.g. after a crash mid-write).</summary>
    public async Task<int> CleanupOrphansAsync()
    {
        var known = (await _db.GetAllBlobIdsAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int removed = 0;
        foreach (var path in Directory.EnumerateFiles(VolumeDirectory))
        {
            var isTemp = path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
            var id = Path.GetFileNameWithoutExtension(path);
            if (isTemp || !known.Contains(id))
            {
                TryDelete(path);
                removed++;
            }
        }
        return removed;
    }

    private async Task WriteBlobAsync(string blobId, byte[] content)
    {
        var blob = _crypto.EncryptFile(content, blobId);
        var final = BlobPath(blobId);
        var temp = final + ".tmp";
        await File.WriteAllBytesAsync(temp, blob);
        File.Move(temp, final, overwrite: true);
    }

    private static void EnsureSize(long size)
    {
        if (size > MaxFileSize)
            throw new InvalidOperationException($"Fayl juda katta: {FormatSize(size)}. Chegara — {FormatSize(MaxFileSize)}.");
    }

    private static string NewBlobId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* left for CleanupOrphansAsync */ }
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024        => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _             => $"{bytes / (1024.0 * 1024):0.#} MB"
    };
}
