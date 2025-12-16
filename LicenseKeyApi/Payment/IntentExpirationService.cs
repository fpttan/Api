using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

public class IntentExpirationService : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly IAppLogger _log;

    public IntentExpirationService(IServiceProvider provider, IAppLogger log)
    {
        _provider = provider;
        _log = log;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Info("IntentExpirationService STARTED");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

                var now = DateTime.UtcNow.AddHours(7);

                // ❗ GIỐNG Y CHANG LOGIC BẠN VIẾT TRONG CreateIntent
                int affected = await db.PaymentIntent
                    .Where(pi => pi.Status == IntentStatus.Pending &&
                                 pi.ExpiresAtUtc <= now)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.Status, IntentStatus.Expired),
                        cancellationToken: stoppingToken
                    );

                if (affected > 0)
                {
                    _log.Info($"Auto-expired {affected} intents", new { now });
                }
            }
            catch (Exception ex)
            {
                _log.Error("IntentExpirationService error", ex);
            }

            // Chạy mỗi 20–30 giây (tùy nhu cầu)
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _log.Warn("IntentExpirationService STOPPED");
    }
}
