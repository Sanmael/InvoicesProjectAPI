using InvoicesProjectApplication.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvoicesProjectInfra.Services;

public class NotificationProcessingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationProcessingHostedService> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;

    public NotificationProcessingHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<NotificationProcessingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _enabled = configuration.GetValue("NotificationProcessing:Enabled", true);

        var intervalMinutes = configuration.GetValue("NotificationProcessing:IntervalMinutes", 60);
        _interval = TimeSpan.FromMinutes(Math.Max(5, intervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Processamento automático de notificações está desabilitado.");
            return;
        }

        _logger.LogInformation("Processamento automático de notificações iniciado com intervalo de {IntervalMinutes} minuto(s).",
            _interval.TotalMinutes);

        using var timer = new PeriodicTimer(_interval);

        await ProcessNotificationsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessNotificationsAsync(stoppingToken);
        }
    }

    private async Task ProcessNotificationsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            await notificationService.ProcessAllNotificationsAsync();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar notificações automáticas.");
        }
    }
}