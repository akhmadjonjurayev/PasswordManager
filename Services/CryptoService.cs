using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Services;

// AES-256-CBC with PBKDF2-HMAC-SHA512 key derivation.
// Note: AES standard max key size is 256-bit ("AES-512" does not exist in the standard).
public class CryptoService
{
    private const int Iterations = 310_000; // OWASP 2023 recommendation for PBKDF2-HMAC-SHA512
    private const int SaltSize = 32;        // 256-bit salt
    private const int KeySize = 32;         // 256-bit AES key
    private const int HashSize = 64;        // 512-bit verification hash

    private byte[]? _encryptionKey;

    public bool IsKeyInitialized => _encryptionKey != null;

    public (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), saltBytes, Iterations, HashAlgorithmName.SHA512, HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(saltBytes));
    }

    public bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var saltBytes = Convert.FromBase64String(storedSalt);
        var computed = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), saltBytes, Iterations, HashAlgorithmName.SHA512, HashSize);
        return CryptographicOperations.FixedTimeEquals(computed, Convert.FromBase64String(storedHash));
    }

    // Uses a different purpose prefix so the encryption key differs from the verification hash
    public void InitializeKey(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var purposedPw = Encoding.UTF8.GetBytes("ENC_KEY:" + password);
        _encryptionKey = Rfc2898DeriveBytes.Pbkdf2(
            purposedPw, saltBytes, Iterations, HashAlgorithmName.SHA512, KeySize);
    }

    public void ClearKey()
    {
        if (_encryptionKey != null)
        {
            CryptographicOperations.ZeroMemory(_encryptionKey);
            _encryptionKey = null;
        }
    }

    public string Encrypt(string plainText)
    {
        if (_encryptionKey == null) throw new InvalidOperationException("Encryption key not initialized.");

        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);

        // Result = IV (16 bytes) + CipherText
        var combined = new byte[16 + cipher.Length];
        iv.CopyTo(combined, 0);
        cipher.CopyTo(combined, 16);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherTextBase64)
    {
        if (_encryptionKey == null) throw new InvalidOperationException("Encryption key not initialized.");

        var combined = Convert.FromBase64String(cipherTextBase64);
        var iv = combined[..16];
        var cipher = combined[16..];

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain);
    }

    // ─── Export / Import ────────────────────────────────────────────────────────
    // Encrypts arbitrary text with a standalone password (not the session key).
    // A fresh random salt is generated each time; pass it to DecryptWithPassword for decryption.

    public string EncryptWithPassword(string plainText, string password, out string saltBase64)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        saltBase64 = Convert.ToBase64String(saltBytes);

        var key = DeriveExportKey(password, saltBytes);
        return AesEncrypt(Encoding.UTF8.GetBytes(plainText), key);
    }

    // Throws CryptographicException if the password is wrong (padding error).
    public string DecryptWithPassword(string cipherTextBase64, string password, string saltBase64)
    {
        var saltBytes = Convert.FromBase64String(saltBase64);
        var key = DeriveExportKey(password, saltBytes);
        return Encoding.UTF8.GetString(AesDecrypt(cipherTextBase64, key));
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private byte[] DeriveExportKey(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes("EXPORT_KEY:" + password),
            salt, Iterations, HashAlgorithmName.SHA512, KeySize);

    private static string AesEncrypt(byte[] plain, byte[] key)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = key; aes.IV = iv;
        aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;

        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(plain, 0, plain.Length);

        var result = new byte[16 + cipher.Length];
        iv.CopyTo(result, 0);
        cipher.CopyTo(result, 16);
        return Convert.ToBase64String(result);
    }

    private static byte[] AesDecrypt(string cipherBase64, byte[] key)
    {
        var combined = Convert.FromBase64String(cipherBase64);
        using var aes = Aes.Create();
        aes.Key = key; aes.IV = combined[..16];
        aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;

        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(combined, 16, combined.Length - 16);
    }
}
