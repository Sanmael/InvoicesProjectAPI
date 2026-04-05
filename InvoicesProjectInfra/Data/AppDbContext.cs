using InvoicesProjectEntities.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoicesProjectInfra.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<TagEvento> TagEventos => Set<TagEvento>();
    public DbSet<Receivable> Receivables => Set<Receivable>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<CardPurchase> CardPurchases => Set<CardPurchase>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.IsAdmin).HasDefaultValue(false);
            entity.Property(e => e.WhatsAppPhoneNumber).HasMaxLength(20);
            entity.HasIndex(e => e.WhatsAppPhoneNumber).IsUnique().HasFilter("\"WhatsAppPhoneNumber\" IS NOT NULL");
        });

        // Debt
        modelBuilder.Entity<Debt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(50).HasDefaultValue("Outros");
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Debts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Receivable
        modelBuilder.Entity<Receivable>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Receivables)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CreditCard
        modelBuilder.Entity<CreditCard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastFourDigits).HasMaxLength(4).IsRequired();
            entity.Property(e => e.CreditLimit).HasPrecision(18, 2);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.CreditCards)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CardPurchase
        modelBuilder.Entity<CardPurchase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(50).HasDefaultValue("Outros");
            
            entity.HasOne(e => e.CreditCard)
                .WithMany(c => c.Purchases)
                .HasForeignKey(e => e.CreditCardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LowBalanceThreshold).HasPrecision(18, 2);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.TelegramUsername).HasMaxLength(100);
            entity.Property(e => e.TelegramLinkToken).HasMaxLength(64);

            entity.HasOne(e => e.User)
                .WithOne(u => u.NotificationPreference)
                .HasForeignKey<NotificationPreference>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailNotification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Subject).HasMaxLength(300).IsRequired();
            entity.Property(e => e.RecipientEmail).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ReferenceKey).HasMaxLength(200);

            entity.HasIndex(e => new { e.UserId, e.ReferenceKey });

            entity.HasOne(e => e.User)
                .WithMany(u => u.EmailNotifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SmtpHost).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SenderEmail).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SenderName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EncryptedPassword).IsRequired();
        });

        modelBuilder.Entity<SavingsGoal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.TargetAmount).HasPrecision(18, 2);
            entity.Property(e => e.CurrentAmount).HasPrecision(18, 2);
            entity.Property(e => e.Category).HasMaxLength(50).HasDefaultValue("Outros");

            entity.HasOne(e => e.User)
                .WithMany(u => u.SavingsGoals)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TagEvento
        modelBuilder.Entity<TagEvento>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nome).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Descricao).HasMaxLength(1000);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Debts)
                .WithOne(d => d.TagEvento)
                .HasForeignKey(d => d.TagEventoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Receivables)
                .WithOne(r => r.TagEvento)
                .HasForeignKey(r => r.TagEventoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.CardPurchases)
                .WithOne(c => c.TagEvento)
                .HasForeignKey(c => c.TagEventoId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
