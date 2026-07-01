namespace LyricsOnTheGo.Models;

/// <summary>One synced lyric line: its start time (ms) and text.</summary>
public sealed record LyricLine(double TimeMs, string Text);
