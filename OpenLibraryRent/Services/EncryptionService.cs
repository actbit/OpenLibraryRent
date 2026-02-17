using System.Security.Cryptography;
using System.Text;

namespace OpenLibraryRent.Services;

/// <summary>
/// AES-256-GCMを使用して機密データを暗号化・復号化するサービス
/// </summary>
public class EncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger _logger;

    public EncryptionService(string keyString, ILogger logger)
    {
        _logger = logger;

        try
        {
            _key = Convert.FromBase64String(keyString);

            if (_key.Length != 32)
            {
                throw new InvalidOperationException(
                    $"Encryption key must be 32 bytes (256 bits). Current length: {_key.Length} bytes.");
            }
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Encryption key must be base64-encoded.", ex);
        }
    }

    public EncryptionService(IConfiguration configuration, ILogger<EncryptionService> logger, IWebHostEnvironment env)
    {
        _logger = logger;

        var keyString = Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
            ?? configuration["Encryption:Key"];

        if (string.IsNullOrEmpty(keyString))
        {
            if (env.IsDevelopment())
            {
                var generatedKey = GenerateNewKey();
                _logger.LogWarning(
                    "No encryption key configured. Generated temporary key: {Key}. Set ENCRYPTION_KEY env var or Encryption:Key in appsettings.json for persistence.",
                    generatedKey);

                keyString = generatedKey;
            }
            else
            {
                throw new InvalidOperationException(
                    "Encryption key not configured in production. " +
                    "Set ENCRYPTION_KEY environment variable or Encryption:Key in appsettings.json.");
            }
        }

        try
        {
            _key = Convert.FromBase64String(keyString);

            if (_key.Length != 32)
            {
                throw new InvalidOperationException(
                    $"Encryption key must be 32 bytes (256 bits). Current length: {_key.Length} bytes.");
            }
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Encryption key must be base64-encoded.", ex);
        }
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

        try
        {
            const int tagSizeInBytes = 16;
            using (var aes = new AesGcm(_key, tagSizeInBytes))
            {
                var nonce = new byte[12];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(nonce);
                }

                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                var ciphertext = new byte[plaintextBytes.Length];
                var tag = new byte[tagSizeInBytes];

                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

                var nonceBase64 = Convert.ToBase64String(nonce);
                var ciphertextBase64 = Convert.ToBase64String(ciphertext);
                var tagBase64 = Convert.ToBase64String(tag);

                return $"{nonceBase64}:{ciphertextBase64}:{tagBase64}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting data");
            throw;
        }
    }

    public string DecryptWithTenantKey(string encryptedTenantKey, string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedTenantKey))
            throw new ArgumentException("Encrypted tenant key cannot be null or empty", nameof(encryptedTenantKey));

        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        var decryptedTenantKey = Decrypt(encryptedTenantKey);
        return DecryptWithPlainTenantKey(decryptedTenantKey, encryptedData);
    }

    public string DecryptWithPlainTenantKey(string tenantKeyString, string encryptedData)
    {
        if (string.IsNullOrEmpty(tenantKeyString))
            throw new ArgumentException("Tenant key cannot be null or empty", nameof(tenantKeyString));

        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        var tenantEncryption = new EncryptionService(tenantKeyString, _logger);
        return tenantEncryption.Decrypt(encryptedData);
    }

    public string Decrypt(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        try
        {
            var parts = encryptedData.Split(':');
            if (parts.Length != 3)
            {
                throw new InvalidOperationException(
                    "Invalid encrypted data format. Expected 'nonce:ciphertext:tag'");
            }

            var nonce = Convert.FromBase64String(parts[0]);
            var ciphertext = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);

            const int tagSizeInBytes = 16;
            using (var aes = new AesGcm(_key, tagSizeInBytes))
            {
                var plaintext = new byte[ciphertext.Length];

                aes.Decrypt(nonce, ciphertext, tag, plaintext);

                return Encoding.UTF8.GetString(plaintext);
            }
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Decryption failed - authentication tag verification failed or invalid key");
            throw new InvalidOperationException("Decryption failed - data may be corrupted or encrypted with different key", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting data");
            throw;
        }
    }

    public static string GenerateNewKey()
    {
        var key = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }
        return Convert.ToBase64String(key);
    }
}
