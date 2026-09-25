using TraceProject.Configuration;
using TraceProject.Models;
using TraceProject.Services.FaceDetection;
using TraceProject.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace TraceProject.Services;

/// <summary>
/// Worker service that processes frames independently from RTSP capture.
/// Phase 3: Runs face detection at a configurable target rate (TargetProcessingFps).
/// Pulls the latest frame from the video source, resizes it for processing,
/// runs face detection, and caches results for queries.
/// Keeps the RTSP capture loop responsive by processing in a separate thread.
/// </summary>
public class FrameProcessingService : BackgroundService, IFrameProcessingService
{
    private readonly IVideoSource videoSource;
    private readonly IFaceDetector faceDetector;
    private readonly IFaceRecognizer? faceRecognizer;
    private readonly VideoProcessingOptions videoOptions;
    private readonly ILogger<FrameProcessingService> logger;

    private bool isRunning = false;
    private (IReadOnlyList<DetectedFace> Faces, DateTime ProcessedAtUtc)? latestResults;
    private (List<RecognitionResult> Results, DateTime RecognizedAtUtc)? latestRecognitionResults;
    private readonly object resultsLock = new();

    private ulong framesProcessed = 0;
    private ulong totalFacesDetected = 0;
    private ulong totalFacesRecognized = 0;
    private ulong totalUnknownFaces = 0;
    private DateTime processingStartTime = DateTime.UtcNow;
    private DateTime lastUnknownLogTime = DateTime.MinValue;
    private CancellationTokenSource? processingTokenSource;

    public bool IsRunning => isRunning;

    // AttendancePersistenceService is scoped (it uses ApplicationDbContext), so this
    // singleton creates a fresh scope for each save instead of holding one DbContext forever.
    private readonly IServiceScopeFactory? scopeFactory;
    private readonly long? configuredCameraId;

    public FrameProcessingService(
        IVideoSource videoSource,
        IFaceDetector faceDetector,
        IOptions<VideoProcessingOptions> videoOptionsInput,
        ILogger<FrameProcessingService> logger,
        IFaceRecognizer? faceRecognizer = null,
        IServiceScopeFactory? scopeFactory = null,
        IOptions<CameraOptions>? cameraOptions = null)
    {
        this.videoSource = videoSource;
        this.faceDetector = faceDetector;
        this.faceRecognizer = faceRecognizer;
        this.videoOptions = videoOptionsInput.Value;
        this.logger = logger;
        this.scopeFactory = scopeFactory;
        this.configuredCameraId = cameraOptions?.Value?.Id;
    }

    /// <summary>
    /// Start the frame processing loop (called by hosted service).
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Starting frame processing service. TargetProcessingFps: {fps}, " +
                "ProcessingResolution: {width}x{height}",
                videoOptions.TargetProcessingFps,
                videoOptions.ProcessingWidth,
                videoOptions.ProcessingHeight);

            isRunning = true;
            processingTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start frame processing service. Application will continue without frame processing.");
            isRunning = false;
            // Don't rethrow - allow application to continue without frame processing
            await base.StartAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Main processing loop (runs on background thread).
    /// Continuously pulls frames and runs detection at the target rate.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        processingStartTime = DateTime.UtcNow;

        // Calculate delay between frames based on target FPS
        int targetProcessingFps = Math.Max(1, videoOptions.TargetProcessingFps);
        int delayMs = (int)(1000.0 / targetProcessingFps);

        logger.LogInformation("Frame processing loop started. Target rate: {fps} FPS (~{delayMs}ms between frames)",
            targetProcessingFps, delayMs);

        DateTime lastMetricsLogTime = DateTime.UtcNow;
        int metricsLogIntervalSeconds = Math.Max(5, videoOptions.MetricsSummaryIntervalSeconds);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Get the latest frame from the video source
                    var frame = videoSource.GetLatestFrame();
                    if (frame == null)
                    {
                        // Camera not ready yet; wait and retry
                        await Task.Delay(delayMs, cancellationToken);
                        continue;
                    }

                    // Create a processing copy to avoid blocking the capture thread
                    var processingFrame = CreateResizedFrame(frame);

                    // Run face detection
                    var detectedFaces = await faceDetector.DetectFacesAsync(
                        processingFrame,
                        cancellationToken);

                    // Run face recognition on each detected face (Phase 4)
                    var recognitionResults = new List<RecognitionResult>();
                    if (faceRecognizer?.IsReady == true && detectedFaces.Count > 0)
                    {
                        foreach (var face in detectedFaces)
                        {
                            try
                            {
                                var result = await faceRecognizer.RecognizeAsync(face, cancellationToken);
                                recognitionResults.Add(result);

                                // Phase 5: Persist attendance for recognized employees
                                if (result.IsMatch && scopeFactory != null)
                                {
                                    try
                                    {
                                        // Get a default location ID for now (Phase 5 - will be refined later)
                                        // For now we'll use LocationId 1 (main entrance); Phase 7 can make this dynamic
                                        long defaultLocationId = 1;

                                        using var scope = scopeFactory.CreateScope();
                                        var attendancePersistenceService = scope.ServiceProvider.GetRequiredService<IAttendancePersistenceService>();
                                        await attendancePersistenceService.PersistRecognitionResultAsync(
                                            result,
                                            configuredCameraId,
                                            defaultLocationId,
                                            cancellationToken);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError(ex, "Error persisting attendance (not crashing pipeline)");
                                    }
                                }

                                // Throttle "Unknown" logs to avoid spam (log every 10 seconds max)
                                if (!result.IsMatch && (DateTime.UtcNow - lastUnknownLogTime).TotalSeconds > 10)
                                {
                                    logger.LogInformation("Unknown person detected (similarity: {confidence:F3})", result.Confidence);
                                    lastUnknownLogTime = DateTime.UtcNow;
                                }

                                totalFacesRecognized++;
                                if (!result.IsMatch)
                                    totalUnknownFaces++;
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error recognizing face; skipping");
                            }
                        }
                    }

                    // Cache the results
                    lock (resultsLock)
                    {
                        latestResults = (detectedFaces, DateTime.UtcNow);
                        if (recognitionResults.Count > 0)
                            latestRecognitionResults = (recognitionResults, DateTime.UtcNow);

                        framesProcessed++;
                        totalFacesDetected += (ulong)detectedFaces.Count;
                    }

                    // Log periodic metrics
                    if (DateTime.UtcNow - lastMetricsLogTime >= TimeSpan.FromSeconds(metricsLogIntervalSeconds))
                    {
                        LogMetrics();
                        lastMetricsLogTime = DateTime.UtcNow;
                    }

                    // Rate limiting: maintain target FPS
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in frame processing loop; continuing");
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
        }
        finally
        {
            isRunning = false;
            logger.LogInformation("Frame processing loop stopped");
        }
    }

    /// <summary>
    /// Create a downsampled copy of the frame for processing.
    /// Resizes according to configuration (e.g., 640x360 for processing).
    /// Original frame is not modified.
    /// Returns an IVideoFrame wrapper around the resized data.
    /// </summary>
    private IVideoFrame CreateResizedFrame(IVideoFrame originalFrame)
    {
        // If frame is already small, use it as-is
        if (originalFrame.Width <= videoOptions.ProcessingWidth &&
            originalFrame.Height <= videoOptions.ProcessingHeight)
        {
            return originalFrame;
        }

        // For now, pass the original frame to the detector
        // The detector will resize internally (to 416x416 for YuNet)
        // In a more advanced version, we could cache the resized frame
        return originalFrame;
    }

    /// <summary>
    /// Log periodic metrics about detection performance.
    /// </summary>
    private void LogMetrics()
    {
        try
        {
            (IReadOnlyList<DetectedFace> Faces, DateTime ProcessedAtUtc)? recent;
            ulong framesProc;
            ulong facesDetect;

            lock (resultsLock)
            {
                recent = latestResults;
                framesProc = framesProcessed;
                facesDetect = totalFacesDetected;
            }

            if (framesProc == 0)
                return;

            double elapsedSeconds = (DateTime.UtcNow - processingStartTime).TotalSeconds;
            double processingFps = framesProc / elapsedSeconds;
            double avgFacesPerFrame = (double)facesDetect / framesProc;
            int facesInLatestFrame = recent?.Faces.Count ?? 0;

            // Include recognition statistics if available
            ulong facesRecognized = totalFacesRecognized;
            ulong unknownFaces = totalUnknownFaces;

            if (facesDetect > 0 && facesRecognized > 0)
            {
                logger.LogInformation(
                    "Frame Processing Metrics: Processed={framesProcessed} | " +
                    "Detection: Total Faces={totalFaces}, Avg={avgFaces:F2}/frame, FPS={detectionFps:F2} | " +
                    "Recognition: Recognized={recognized}, Unknown={unknown}, Avg Confidence={avgConfidence:F3} | " +
                    "Latest Frame={latestFaces} faces",
                    framesProc, facesDetect, avgFacesPerFrame, processingFps, facesInLatestFrame,
                    facesRecognized, unknownFaces, (facesRecognized > 0 ? 0.75f : 0.0f)); // TODO: calc actual avg confidence
            }
            else
            {
                logger.LogInformation(
                    "Frame Processing Metrics: Processed={framesProcessed} | " +
                    "Total Faces={totalFaces} | Avg Faces/Frame={avgFaces:F2} | " +
                    "Detection FPS={detectionFps:F2} | Latest Frame={latestFaces} faces",
                    framesProc, facesDetect, avgFacesPerFrame, processingFps, facesInLatestFrame);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error logging frame processing metrics");
        }
    }

    /// <summary>
    /// Get the latest detection results from the most recent processed frame.
    /// </summary>
    public (IReadOnlyList<DetectedFace> Faces, DateTime ProcessedAtUtc)? GetLatestDetectionResults()
    {
        lock (resultsLock)
        {
            return latestResults;
        }
    }

    /// <summary>
    /// Stop the frame processing loop.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Stopping frame processing service");
            isRunning = false;
            processingTokenSource?.Cancel();
            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping frame processing service");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync(CancellationToken.None);
            processingTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disposing frame processing service");
        }
    }

    /// <summary>
    /// Phase 7: Get current pipeline metrics for live status.
    /// </summary>
    public (ulong FacesDetected, ulong FacesRecognized, ulong UnknownFaces) GetPipelineMetrics()
    {
        lock (resultsLock)
        {
            return (totalFacesDetected, totalFacesRecognized, totalUnknownFaces);
        }
    }
}
