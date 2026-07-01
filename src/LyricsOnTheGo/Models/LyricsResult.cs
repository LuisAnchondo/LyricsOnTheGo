namespace LyricsOnTheGo.Models;

/// <summary>Result of a lyrics lookup (matches the original's cached shape).</summary>
public sealed record LyricsResult
{
    public bool Found { get; init; }
    public bool Instrumental { get; init; }
    public string Synced { get; init; } = "";
    public string Plain { get; init; } = "";
    public string Source { get; init; } = "";

    public static LyricsResult NotFound { get; } = new();
}
