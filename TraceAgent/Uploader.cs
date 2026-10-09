using System.Text.Json;

namespace TraceAgent;

/// <summary>
/// Sends the tracker's finished sessions plus the open one, and its lifecycle events, to the server.
/// Whatever the server has not accepted stays queued, and is saved to disk so it survives a
/// restart while offline (an exit that could not be sent is delivered on the next start).
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
            // Oldest first; the open session and the events go in the last batch, so the newest
            // event (e.g. "closed by the user") is what the server sees last
            do
            {
                var batch = tracker.Pending.Take(BatchSize).ToList();
                var sessions = new List<AgentSessionDto>(batch);
                var isLast = tracker.Pending.Count <= BatchSize;
                List<AgentEventDto>? events = null;
                if (isLast)
                {
                    if (tracker.OpenSnapshot() is { } open)
                        sessions.Add(open);
                    events = tracker.PendingEvents.ToList();
                }

                await api.ReportAsync(new AgentReportDto(sessions, tracker.CurrentState, tracker.CurrentApp, AgentApi.Version, events), cancellation);

                // Sampling can close sessions or record events while the request was in flight; only drop what was sent
                tracker.Pending.RemoveRange(0, batch.Count);
                if (events is not null)
                    tracker.PendingEvents.RemoveAll(e => events.Contains(e));
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
            tracker.PendingEvents.Clear();
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
            if (tracker.Pending.Count == 0 && tracker.PendingEvents.Count == 0)
                File.Delete(QueuePath);
            else
                File.WriteAllText(QueuePath, JsonSerializer.Serialize(new Queue(tracker.Pending, tracker.PendingEvents)));
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
            if (!File.Exists(QueuePath))
                return;
            var json = File.ReadAllText(QueuePath);
            // Agent 1.0.0 saved a bare list of sessions
            var saved = json.TrimStart().StartsWith('[')
                ? new Queue(JsonSerializer.Deserialize<List<AgentSessionDto>>(json) ?? [], [])
                : JsonSerializer.Deserialize<Queue>(json);
            if (saved is null)
                return;
            tracker.Pending.InsertRange(0, saved.Sessions ?? []);
            tracker.PendingEvents.InsertRange(0, saved.Events ?? []);
        }
        catch (Exception)
        {
            // Unreadable queue: start fresh
        }
    }

    private sealed record Queue(List<AgentSessionDto>? Sessions, List<AgentEventDto>? Events);
}
