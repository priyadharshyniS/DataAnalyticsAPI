using DataAnalyticsApi.Data;
using Microsoft.Extensions.Hosting;

namespace DataAnalyticsApi.Services;

public class RefreshService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RefreshService> _logger;
    private readonly IConfiguration _config;

    public RefreshService(IServiceProvider services, ILogger<RefreshService> logger, IConfiguration config)
    {
        _services = services;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RefreshService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hour = _config.GetValue<int>("Refresh:Hour", 2);
                var now = DateTime.Now;
                var next = new DateTime(now.Year, now.Month, now.Day, hour, 0, 0);
                if (next <= now) next = next.AddDays(1);

                var delay = next - now;
                _logger.LogInformation("Next scheduled refresh at {next} (in {delay})", next, delay);
                await Task.Delay(delay, stoppingToken);

                using var scope = _services.CreateScope();
                var loader = scope.ServiceProvider.GetRequiredService<CsvLoader>();
                var csvPath = _config.GetValue<string>("Csv:Path") ?? "data/sales_sample.csv";
                var loaded = await loader.LoadAsync(csvPath, overwrite: false, ct: stoppingToken);
                _logger.LogInformation("Scheduled refresh completed, {count} records loaded", loaded);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled refresh failed");
                // Wait a bit before retrying
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
