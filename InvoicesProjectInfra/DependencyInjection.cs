using System.Text;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectApplication.Services;
using InvoicesProjectEntities.Interfaces;
using InvoicesProjectInfra.Data;
using InvoicesProjectInfra.Repositories;
using InvoicesProjectInfra.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace InvoicesProjectInfra;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDebtRepository, DebtRepository>();
        services.AddScoped<IReceivableRepository, ReceivableRepository>();
        services.AddScoped<ICreditCardRepository, CreditCardRepository>();
        services.AddScoped<ICardPurchaseRepository, CardPurchaseRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<IEmailNotificationRepository, EmailNotificationRepository>();
        services.AddScoped<IEmailSettingsRepository, EmailSettingsRepository>();

        // Services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<IEmailSenderService, EmailSenderService>();

        // Application Services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDebtService, DebtService>();
        services.AddScoped<IReceivableService, ReceivableService>();
        services.AddScoped<ICreditCardService, CreditCardService>();
        services.AddScoped<ICardPurchaseService, CardPurchaseService>();
        services.AddScoped<IFinancialSummaryService, FinancialSummaryService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<IEmailSettingsService, EmailSettingsService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddHttpClient("Groq");
        services.AddHostedService<NotificationProcessingHostedService>();

        // JWT Authentication
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] 
            ?? throw new InvalidOperationException("JWT SecretKey não configurada.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(secretKey))
            };
        });

        return services;
    }
}
