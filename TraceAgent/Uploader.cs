using System.Text.Json;

namespace TraceAgent;

/// <summary>
/// Sends the tracker's finished sessions plus the open one to the server. Sessions the server
/// has not accepted stay queued, and are saved to disk so they survive a restart while offline.
/// </summary>
internal sealed class Uploader(ActivityTracker tracker)
{
    private const int BatchSize = 400;
    private static readonly string QueuePath = Path.Combine(AgentSettings.Folder, "pending.json");

    private bool busy;

    public DateTime? LastSuccessAt { get; private set; }
    public string? LastError { get; private set; }
    public bool IsDisabled { get; private set; }

    public async Task UploadAsync(AgentApi api, CancellationToken cancellation = default)
    {
        if (busy)
            return;
        busy = true;
        try
        {
            // Oldest first; the open session goes in the last batch so the live status is current
            do
            {
                var batch = tracker.Pending.Take(BatchSize).ToList();
                var sessions = new List<AgentSessionDto>(batch);
                var isLast = tracker.Pending.Count <= BatchSize;
                if (isLast && tracker.OpenSnapshot() is { } open)
                    sessions.Add(open);

                await api.ReportAsync(new AgentReportDto(sessions, tracker.CurrentState, tracker.CurrentApp, AgentApi.Version), cancellation);

                // Sampling can close sessions while the request was in flight; only drop what was sent
                tracker.Pending.RemoveRange(0, batch.Count);
                LastSuccessAt = DateTime.Now;
                LastError = null;
                IsDisabled = false;
            }
            while (tracker.Pending.Count > 0 && !cancellation.IsCancellationRequested);
        }
        catch (UnpairedException)
        {
            throw;
        }
        catch (DeviceDisabledException ex)
        {
            // Nothing is recorded while disabled; the token is kept, so it resumes once re-enabled
            tracker.Pending.Clear();
            IsDisabled = true;
            LastError = ex.Message;
        }
        catch (Exception ex)
        {
            LastError = ex is TaskCanceledException ? "Server did not answer (timeout)" : ex.Message;
        }
        finally
        {
            busy = false;
            SaveQueue();
        }
    }

    public void SaveQueue()
    {
        try
        {
            Directory.CreateDirectory(AgentSettings.Folder);
            if (tracker.Pending.Count == 0)
                File.Delete(QueuePath);
            else
                File.WriteAllText(QueuePath, JsonSerializer.Serialize(tracker.Pending));
        }
        catch (Exception)
        {
            // Disk trouble: the queue stays in memory
        }
    }

    public void LoadQueue()
    {
        try
        {
            if (File.Exists(QueuePath)
                && JsonSerializer.Deserialize<List<AgentSessionDto>>(File.ReadAllText(QueuePath)) is { } saved)
                tracker.Pending.InsertRange(0, saved);
        }
        catch (Exception)
        {
            // Unreadable queue: start fresh
        }
    }
}
