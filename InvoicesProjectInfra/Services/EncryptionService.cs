using System.Security.Cryptography;
using System.Text;
using InvoicesProjectApplication.Interfaces;
using Microsoft.Extensions.Configuration;

namespace InvoicesProjectInfra.Services;

/// <summary>
/// Serviço de criptografia usando AES
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionService(IConfiguration configuration)
    {
        // A chave de criptografia vem das configurações
        var encryptionKey = configuration["Encryption:Key"] 
            ?? throw new InvalidOperationException("Encryption:Key não configurada no appsettings.");
        
        // Deriva a chave e IV a partir da string de configuração
        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(encryptionKey));
        _iv = _key.Take(16).ToArray();
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        return Convert.ToBase64String(cipherBytes);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Se falhar a descriptografia, retorna vazio
            return string.Empty;
        }
    }
}
