namespace LyricsOnTheGo.Models;

/// <summary>
/// A snapshot of the currently playing track from Windows SMTC, with the position
/// already interpolated to "now".
/// </summary>
public sealed record NowPlaying
{
    public bool HasSession { get; init; }
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public double DurationMs { get; init; }
    public double PositionMs { get; init; }
    public bool IsPlaying { get; init; }

    public static NowPlaying None { get; } = new();

    /// <summary>Stable identity of the track (title/artist/album/duration) — changes on song change.</summary>
    public string Key => $"{Title}|{Artist}|{Album}|{(int)(DurationMs / 1000)}".ToLowerInvariant();
}
