namespace InvoicesProjectApplication.Interfaces;

/// <summary>
/// Serviço para criptografia de dados sensíveis
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Criptografa uma string
    /// </summary>
    string Encrypt(string plainText);
    
    /// <summary>
    /// Descriptografa uma string
    /// </summary>
    string Decrypt(string cipherText);
}
