using Microsoft.Extensions.Options;
using TraceProject.Configuration;
using TraceProject.Services;
using TraceProject.Services.Interfaces;
using TraceProject.Services.VideoSources;

namespace TraceProject.Workers;

/// <summary>
/// Background service (IHostedService) that manages camera video capture.
/// Runs on application startup and handles graceful shutdown.
/// Does NOT block request threads; runs on a separate background worker thread.
/// </summary>
public class CameraCaptureWorker : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<CameraCaptureWorker> logger;
    private readonly CameraOptions cameraOptions;
    private IVideoSource? videoSource;
    private CameraConnectionManager? connectionManager;

    public CameraCaptureWorker(
        IServiceProvider serviceProvider,
        ILogger<CameraCaptureWorker> logger,
        IOptions<CameraOptions> cameraOptionsOptions)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.cameraOptions = cameraOptionsOptions.Value;
    }

    /// <summary>
    /// Called when the application starts.
    /// Validates configuration and starts the video capture pipeline.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("CameraCaptureWorker starting");

        try
        {
            // Validate configuration
            if (!cameraOptions.Enabled)
            {
                logger.LogInformation("Camera capture is disabled in configuration");
                await base.StartAsync(cancellationToken);
                return;
            }

            // Debug: Log all camera configuration values
            logger.LogDebug("Camera Configuration - Id: {Id}, Name: {Name}, IpAddress: {IpAddress}, Username: {Username}, Transport: {Transport}, Enabled: {Enabled}, Password: '{Password}'",
                cameraOptions.Id, cameraOptions.Name, cameraOptions.IpAddress, cameraOptions.Username, cameraOptions.Transport, cameraOptions.Enabled,
                string.IsNullOrWhiteSpace(cameraOptions.Password) ? "<null or empty>" : "<set>");

            if (string.IsNullOrWhiteSpace(cameraOptions.Password))
            {
                logger.LogError("Camera password not configured. Set via User Secrets (Camera:Password) or environment variable. Current resolved values: Id={Id}, Name={Name}, IpAddress={IpAddress}, Username={Username}",
                    cameraOptions.Id, cameraOptions.Name, cameraOptions.IpAddress, cameraOptions.Username);
                // Don't fail startup, but log the error
                await base.StartAsync(cancellationToken);
                return;
            }

            // Get or create video source from DI
            videoSource = serviceProvider.GetRequiredService<IVideoSource>();
            logger.LogDebug("IVideoSource resolved from DI");

            // Start the connection manager
            connectionManager = new CameraConnectionManager(
                videoSource,
                serviceProvider.GetRequiredService<IOptions<ConnectionOptions>>().Value,
                serviceProvider.GetRequiredService<ILogger<CameraConnectionManager>>());

            await connectionManager.StartAsync(cancellationToken);
            logger.LogInformation("CameraCaptureWorker started successfully");

            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting CameraCaptureWorker");
            // Don't crash the application if camera setup fails
            // The API will still be available, just without live frames
            await base.StartAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Called when the ExecuteAsync loop should run.
    /// In this case, we just keep the worker alive without doing anything;
    /// the actual work is done by the IVideoSource/CameraConnectionManager.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Simply keep the worker alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { });
    }

    /// <summary>
    /// Called when the application is shutting down.
    /// Cleanly stops the video source and releases resources.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("CameraCaptureWorker stopping");

        try
        {
            if (connectionManager != null)
            {
                await connectionManager.StopAsync();
                await connectionManager.DisposeAsync();
            }

            if (videoSource != null)
            {
                await videoSource.StopAsync();
                await videoSource.DisposeAsync();
            }

            logger.LogInformation("CameraCaptureWorker stopped");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping CameraCaptureWorker");
        }

        await base.StopAsync(cancellationToken);
    }
}
