using System.Security.Cryptography;
using System.Text;

namespace AzureDevOpsAgents.Api.Services;

/// <summary>
/// Encrypts and decrypts sensitive values (OAuth tokens) using AES-GCM.
/// The key is derived from the ENCRYPTION_KEY environment variable / appsettings.
/// </summary>
public class TokenEncryptionService
{
    private readonly byte[] _key;

    public TokenEncryptionService(IConfiguration configuration)
    {
        var raw = configuration["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption:Key is not configured.");
        // Derive a 32-byte AES key from the config value
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
    }

    public string Encrypt(string plainText)
    {
        var data = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var ciphertext = new byte[data.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, data, ciphertext, tag);

        // Store as base64(nonce):base64(ciphertext):base64(tag)
        return $"{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(ciphertext)}:{Convert.ToBase64String(tag)}";
    }

    public string Decrypt(string cipherText)
    {
        var parts = cipherText.Split(':');
        if (parts.Length != 3) throw new FormatException("Invalid encrypted token format.");

        var nonce = Convert.FromBase64String(parts[0]);
        var cipherBytes = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var plainText = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainText);

        return Encoding.UTF8.GetString(plainText);
    }
}
