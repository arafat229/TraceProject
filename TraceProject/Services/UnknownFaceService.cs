using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Hubs;
using TraceProject.Models;
using TraceProject.Services.FaceRecognition;

namespace TraceProject.Services;

/// <summary>
/// Saves faces the recognizer could not match as UnknownFace records (with a JPEG of the
/// aligned face) so they can be resolved from the dashboard, and broadcasts UnknownFaceDetected.
/// The same stranger stays in front of the camera for many frames, so faces similar to one
/// saved in the last DedupWindow are skipped.
/// </summary>
public class UnknownFaceService
{
    // Only clear, reasonably large faces are worth reviewing
    private const float MinDetectionScore = 0.85f;
    private const int MinFaceSizePixels = 60;
    private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly IHubContext<NotificationHub> hubContext;
    private readonly SystemSettingsService settings;
    private readonly CameraOptions cameraOptions;
    private readonly ILogger<UnknownFaceService> logger;

    private readonly List<(float[] Embedding, DateTime SeenUtc)> recentUnknowns = new();
    private readonly object recentLock = new();

    public UnknownFaceService(
        IServiceScopeFactory scopeFactory,
        IHubContext<NotificationHub> hubContext,
        SystemSettingsService settings,
        IOptions<CameraOptions> cameraOptions,
        ILogger<UnknownFaceService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.hubContext = hubContext;
        this.settings = settings;
        this.cameraOptions = cameraOptions.Value;
        this.logger = logger;
    }

    public async Task HandleUnknownFaceAsync(DetectedFace face, RecognitionResult result, CancellationToken cancellationToken = default)
    {
        if (result.IsMatch || result.Embedding is null)
            return;
        if (face.Confidence < MinDetectionScore ||
            face.BoundingBox.Width < MinFaceSizePixels || face.BoundingBox.Height < MinFaceSizePixels)
            return;

        if (!TryRemember(result.Embedding))
            return; // already saved this person recently

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var location = await CameraLocationResolver.ResolveAsync(db, cameraOptions, cancellationToken);

            var unknownFace = new UnknownFace
            {
                face = FaceImageHelper.EncodeJpeg(face.FaceImageData, face.FaceImageWidth, face.FaceImageHeight),
                DetectedAtUtc = DateTime.UtcNow,
                CameraId = cameraOptions.Id,
                DetectionScore = face.Confidence,
                UnknownFaceLocations = location is null
                    ? new List<UnknownFaceLocation>()
                    : new List<UnknownFaceLocation> { new() { LocationId = location.Id } },
            };

            db.UnknownFaces.Add(unknownFace);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Unknown face saved (#{id}) at {location}", unknownFace.Id, location?.Place ?? "no location");

            await hubContext.Clients.All.SendAsync("UnknownFaceDetected", new
            {
                unknownFaceId = unknownFace.Id,
                locationName = location?.Place ?? "Unknown location",
                detectedAtUtc = unknownFace.DetectedAtUtc,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save unknown face (not crashing pipeline)");
        }
    }

    /// <summary>
    /// Returns false if a similar face was seen within DedupWindow (and refreshes its time).
    /// </summary>
    private bool TryRemember(float[] embedding)
    {
        var now = DateTime.UtcNow;
        var threshold = settings.Current.RecognitionThreshold;

        lock (recentLock)
        {
            recentUnknowns.RemoveAll(r => now - r.SeenUtc > DedupWindow);

            for (int i = 0; i < recentUnknowns.Count; i++)
            {
                if (CosineSimilarity(embedding, recentUnknowns[i].Embedding) >= threshold)
                {
                    recentUnknowns[i] = (recentUnknowns[i].Embedding, now);
                    return false;
                }
            }

            recentUnknowns.Add((embedding, now));
            return true;
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length && i < b.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0f : (float)(dot / Math.Sqrt(na * nb));
    }
}
