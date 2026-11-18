using SmartClinic.Application;

namespace SmartClinic.API;

public sealed class NoShowBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<NoShowBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IClinicService>();
            var count = service.ProcessNoShows(DateTimeOffset.UtcNow);
            if (count > 0)
            {
                logger.LogInformation("{Count} rendez-vous ont été marqués NoShow.", count);
            }
        }
    }
}
