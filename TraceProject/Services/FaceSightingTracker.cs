using TraceProject.Models;

namespace TraceProject.Services;

/// <summary>
/// What the frame's recognition result means once the face's recent history is considered.
/// </summary>
/// <param name="ConfirmedEmployeeId">The employee this face is confirmed to be, if any.</param>
/// <param name="IsStranger">Unmatched, and nothing suggests this face belongs to an employee.</param>
/// <param name="Held">Why an employee with several matches is not being confirmed, for the log.</param>
public readonly record struct SightingDecision(long? ConfirmedEmployeeId, bool IsStranger, string? Held);

/// <summary>
/// Follows each face across frames and decides who it is from all of its recent frames, not from
/// single matches. A bad frame (head turning, poor light) can land near the wrong employee: on
/// 2026-10-29 one employee was checked in at similarity 0.659, then 28 s later a few stray frames
/// of that same face checked a second employee in at 0.389, with nobody else in front of the camera.
///
/// To confirm an employee, a face needs RequiredVotes matches in the last VoteWindow, making up at
/// least MinVoteShare of its recognized frames and averaging at least MinAverageSimilarity. A face
/// already confirmed as one employee needs SwitchVotes to become another, so a second person
/// stepping into the same spot is still picked up. Used only from the frame processing loop.
/// </summary>
public class FaceSightingTracker
{
    private const int RequiredVotes = 4;
    private const int SwitchVotes = 8;
    private const double MinVoteShare = 0.6;
    // Faces enrolled recently on this camera check in at 0.59-0.70; the false check-in was 0.389.
    // Set high on purpose: a missed check-in is visible and easy to correct, a wrong one is not.
    // Older enrollments score lower, so re-enroll anyone who is not being checked in.
    // Fixed, so lowering the Settings threshold can't weaken it.
    private const float MinAverageSimilarity = 0.50f;
    private static readonly TimeSpan VoteWindow = TimeSpan.FromSeconds(4);
    // Longer than a head turn; a face gone for this long may be someone else
    private static readonly TimeSpan TrackTimeout = TimeSpan.FromSeconds(5);

    private readonly List<Track> tracks = new();

    /// <summary>A face too poor to recognize still shows the person is there: keep following it.</summary>
    public void Touch((int X, int Y, int Width, int Height) box, DateTime nowUtc) => FindTrack(box, nowUtc);

    public SightingDecision Observe((int X, int Y, int Width, int Height) box, RecognitionResult result, DateTime nowUtc)
    {
        var track = FindTrack(box, nowUtc);
        track.Votes.Enqueue(new Vote(nowUtc, result.IsMatch ? result.EmployeeId : null, result.Confidence));
        while (nowUtc - track.Votes.Peek().AtUtc > VoteWindow)
            track.Votes.Dequeue();

        if (!result.IsMatch || result.EmployeeId is not { } employeeId)
        {
            var isStranger = !result.IsAmbiguous
                && track.ConfirmedEmployeeId is null
                && track.Votes.All(v => v.EmployeeId is null);
            return new SightingDecision(track.ConfirmedEmployeeId, isStranger, null);
        }

        if (track.ConfirmedEmployeeId == employeeId)
            return new SightingDecision(employeeId, false, null);

        var support = track.Votes.Where(v => v.EmployeeId == employeeId).ToList();
        var share = support.Count / (double)track.Votes.Count;
        var average = support.Average(v => v.Similarity);
        var required = track.ConfirmedEmployeeId is null ? RequiredVotes : SwitchVotes;

        if (support.Count >= required && share >= MinVoteShare && average >= MinAverageSimilarity)
        {
            track.ConfirmedEmployeeId = employeeId;
            return new SightingDecision(employeeId, false, null);
        }

        var held = support.Count >= RequiredVotes - 1
            ? $"{result.MatchedEmployeeName}: {support.Count} of {track.Votes.Count} recent frames, average similarity {average:F3}" +
              (track.ConfirmedEmployeeId is null ? "" : ", face already confirmed as another employee")
            : null;
        return new SightingDecision(track.ConfirmedEmployeeId, false, held);
    }

    /// <summary>The track this box continues (the nearest one within one face size), or a new one.</summary>
    private Track FindTrack((int X, int Y, int Width, int Height) box, DateTime nowUtc)
    {
        tracks.RemoveAll(t => nowUtc - t.LastSeenUtc > TrackTimeout);

        var x = box.X + box.Width / 2.0;
        var y = box.Y + box.Height / 2.0;
        var size = Math.Max(box.Width, box.Height);

        var track = tracks
            .Select(t => (Track: t, Distance: Math.Sqrt(Math.Pow(t.X - x, 2) + Math.Pow(t.Y - y, 2))))
            .Where(t => t.Distance <= Math.Max(size, t.Track.Size))
            .OrderBy(t => t.Distance)
            .Select(t => t.Track)
            .FirstOrDefault();

        if (track is null)
            tracks.Add(track = new Track());

        (track.X, track.Y, track.Size, track.LastSeenUtc) = (x, y, size, nowUtc);
        return track;
    }

    private sealed record Vote(DateTime AtUtc, long? EmployeeId, float Similarity);

    private sealed class Track
    {
        public double X, Y;
        public int Size;
        public DateTime LastSeenUtc;
        public long? ConfirmedEmployeeId;
        public Queue<Vote> Votes { get; } = new();
    }
}
