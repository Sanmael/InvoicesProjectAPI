namespace InvoicesProjectEntities.Entities;

/// <summary>
/// Configurações de email do sistema (SMTP).
/// Apenas um registro deve existir no banco (singleton).
/// </summary>
public class EmailSettings : BaseEntity
{
    /// <summary>
    /// Endereço do servidor SMTP (ex: smtp.gmail.com)
    /// </summary>
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    
    /// <summary>
    /// Porta do servidor SMTP (ex: 587 para TLS, 465 para SSL)
    /// </summary>
    public int SmtpPort { get; set; } = 587;
    
    /// <summary>
    /// Email que será usado para enviar as notificações
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome que aparecerá como remetente
    /// </summary>
    public string SenderName { get; set; } = "Sistema de Faturas";
    
    /// <summary>
    /// Senha de app do Gmail (criptografada)
    /// </summary>
    public string EncryptedPassword { get; set; } = string.Empty;
    
    /// <summary>
    /// Usar SSL/TLS
    /// </summary>
    public bool UseSsl { get; set; } = true;
    
    /// <summary>
    /// Se as configurações estão ativas e prontas para uso
    /// </summary>
    public bool IsConfigured { get; set; } = false;
}
