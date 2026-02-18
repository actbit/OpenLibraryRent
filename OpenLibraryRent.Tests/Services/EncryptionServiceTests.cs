using Microsoft.Extensions.Logging;
using Moq;
using OpenLibraryRent.Services;
using Xunit;

namespace OpenLibraryRent.Tests.Services;

public class EncryptionServiceTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly string _validKey;
    private readonly EncryptionService _service;

    public EncryptionServiceTests()
    {
        _loggerMock = new Mock<ILogger>();
        _validKey = EncryptionService.GenerateNewKey();
        _service = new EncryptionService(_validKey, _loggerMock.Object);
    }

    [Fact]
    public void GenerateNewKey_Returns_Valid_Base64_Key()
    {
        // Act
        var key = EncryptionService.GenerateNewKey();

        // Assert
        Assert.NotNull(key);
        Assert.NotEmpty(key);

        // Should be valid base64
        var keyBytes = Convert.FromBase64String(key);
        Assert.Equal(32, keyBytes.Length); // 256 bits
    }

    [Fact]
    public void GenerateNewKey_Generates_Different_Keys()
    {
        // Act
        var key1 = EncryptionService.GenerateNewKey();
        var key2 = EncryptionService.GenerateNewKey();

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Constructor_With_Valid_Key_Succeeds()
    {
        // Arrange & Act
        var service = new EncryptionService(_validKey, _loggerMock.Object);

        // Assert - no exception thrown
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_With_Invalid_Base64_Key_Throws()
    {
        // Arrange & Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new EncryptionService("not-valid-base64!", _loggerMock.Object));
    }

    [Fact]
    public void Constructor_With_Wrong_Length_Key_Throws()
    {
        // Arrange
        var shortKey = Convert.ToBase64String(new byte[16]); // 128 bits instead of 256

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new EncryptionService(shortKey, _loggerMock.Object));
    }

    [Fact]
    public void Encrypt_Returns_NonEmpty_String()
    {
        // Arrange
        var plaintext = "test-secret";

        // Act
        var encrypted = _service.Encrypt(plaintext);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted);
    }

    [Fact]
    public void Encrypt_Returns_Different_Values_For_Same_Input()
    {
        // Arrange
        var plaintext = "test-secret";

        // Act
        var encrypted1 = _service.Encrypt(plaintext);
        var encrypted2 = _service.Encrypt(plaintext);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2); // Different due to random nonce
    }

    [Fact]
    public void Encrypt_Produces_Expected_Format()
    {
        // Arrange
        var plaintext = "test-secret";

        // Act
        var encrypted = _service.Encrypt(plaintext);

        // Assert
        var parts = encrypted.Split(':');
        Assert.Equal(3, parts.Length);

        // Each part should be valid base64
        Convert.FromBase64String(parts[0]); // nonce
        Convert.FromBase64String(parts[1]); // ciphertext
        Convert.FromBase64String(parts[2]); // tag
    }

    [Fact]
    public void Encrypt_With_Null_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.Encrypt(null!));
    }

    [Fact]
    public void Encrypt_With_Empty_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.Encrypt(string.Empty));
    }

    [Fact]
    public void Decrypt_RoundTrip_Returns_Original_Plaintext()
    {
        // Arrange
        var plaintext = "my-secret-password-123";

        // Act
        var encrypted = _service.Encrypt(plaintext);
        var decrypted = _service.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_With_Null_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.Decrypt(null!));
    }

    [Fact]
    public void Decrypt_With_Empty_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.Decrypt(string.Empty));
    }

    [Fact]
    public void Decrypt_With_Invalid_Format_Throws()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _service.Decrypt("invalid-format"));
    }

    [Fact]
    public void Decrypt_With_Wrong_Key_Throws()
    {
        // Arrange
        var plaintext = "secret-data";
        var encrypted = _service.Encrypt(plaintext);

        var differentKey = EncryptionService.GenerateNewKey();
        var differentService = new EncryptionService(differentKey, _loggerMock.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            differentService.Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_With_Tampered_Data_Throws()
    {
        // Arrange
        var plaintext = "secret-data";
        var encrypted = _service.Encrypt(plaintext);

        // Tamper with the ciphertext
        var parts = encrypted.Split(':');
        var tamperedCiphertext = Convert.FromBase64String(parts[1]);
        tamperedCiphertext[0] ^= 0xFF; // Flip some bits
        var tamperedEncrypted = $"{parts[0]}:{Convert.ToBase64String(tamperedCiphertext)}:{parts[2]}";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _service.Decrypt(tamperedEncrypted));
    }

    [Fact]
    public void DecryptWithTenantKey_RoundTrip_Succeeds()
    {
        // Arrange
        var tenantKey = EncryptionService.GenerateNewKey();
        var masterService = new EncryptionService(_validKey, _loggerMock.Object);
        var encryptedTenantKey = masterService.Encrypt(tenantKey);

        var tenantService = new EncryptionService(tenantKey, _loggerMock.Object);
        var secretData = "oidc-client-secret";
        var encryptedData = tenantService.Encrypt(secretData);

        // Act
        var decrypted = masterService.DecryptWithTenantKey(encryptedTenantKey, encryptedData);

        // Assert
        Assert.Equal(secretData, decrypted);
    }

    [Fact]
    public void DecryptWithTenantKey_With_Null_EncryptedTenantKey_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.DecryptWithTenantKey(null!, "some-encrypted-data"));
    }

    [Fact]
    public void DecryptWithTenantKey_With_Null_EncryptedData_Throws()
    {
        // Arrange
        var encryptedTenantKey = _service.Encrypt(EncryptionService.GenerateNewKey());

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.DecryptWithTenantKey(encryptedTenantKey, null!));
    }

    [Fact]
    public void DecryptWithPlainTenantKey_RoundTrip_Succeeds()
    {
        // Arrange
        var tenantKey = EncryptionService.GenerateNewKey();
        var tenantService = new EncryptionService(tenantKey, _loggerMock.Object);
        var secretData = "another-secret";
        var encryptedData = tenantService.Encrypt(secretData);

        // Act
        var decrypted = _service.DecryptWithPlainTenantKey(tenantKey, encryptedData);

        // Assert
        Assert.Equal(secretData, decrypted);
    }

    [Fact]
    public void Encryption_Works_With_Unicode_Characters()
    {
        // Arrange
        var plaintext = "日本語パスワード🔐🔒";

        // Act
        var encrypted = _service.Encrypt(plaintext);
        var decrypted = _service.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encryption_Works_With_Long_Text()
    {
        // Arrange
        var plaintext = new string('A', 10000);

        // Act
        var encrypted = _service.Encrypt(plaintext);
        var decrypted = _service.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }
}
