using Microsoft.Extensions.Hosting;

namespace TraceProject.Workers;

/// <summary>
/// Wraps an IHostedService to gracefully handle exceptions during startup and prevent
/// the entire application from crashing if a hosted service fails to initialize.
/// </summary>
public class SafeHostedServiceWrapper : IHostedService
{
    private readonly IHostedService innerService;
    private readonly ILogger<SafeHostedServiceWrapper> logger;
    private readonly string serviceName;

    public SafeHostedServiceWrapper(
        IHostedService innerService,
        ILogger<SafeHostedServiceWrapper> logger,
        string serviceName)
    {
        this.innerService = innerService;
        this.logger = logger;
        this.serviceName = serviceName;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting hosted service: {serviceName}", serviceName);
            await innerService.StartAsync(cancellationToken);
            logger.LogInformation("Successfully started hosted service: {serviceName}", serviceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start hosted service {serviceName}. Application will continue without this service.", serviceName);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await innerService.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while stopping hosted service {serviceName}", serviceName);
        }
    }
}
