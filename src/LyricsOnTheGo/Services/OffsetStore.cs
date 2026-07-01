using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LyricsOnTheGo.Services;

/// <summary>
/// Per-song lyrics offset, persisted independently of the lyrics cache (its own JSON map of
/// song-key → offset in ms). Kept separate so "Clear lyrics cache" and cache-version migrations
/// never wipe a user's hand-tuned offsets. Songs default to 0 (most lyrics are correctly synced);
/// only songs the user actually adjusted are stored.
/// </summary>
public static class OffsetStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LyricsOnTheGo");
    private static readonly string FilePath = Path.Combine(Dir, "offsets.json");

    private static Dictionary<string, int>? _map;
    private static Dictionary<string, int> Map => _map ??= Load();

    private static Dictionary<string, int> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(FilePath))
                       ?? new Dictionary<string, int>();
        }
        catch { /* fall back to empty */ }
        return new Dictionary<string, int>();
    }

    /// <summary>The saved offset (ms) for a song, or 0 if it was never adjusted.</summary>
    public static int Get(string key)
        => !string.IsNullOrEmpty(key) && Map.TryGetValue(key, out int v) ? v : 0;

    /// <summary>Save (or clear, when 0) a song's offset.</summary>
    public static void Set(string key, int offsetMs)
    {
        if (string.IsNullOrEmpty(key))
            return;
        if (offsetMs == 0)
        {
            if (!Map.Remove(key))
                return; // nothing changed
        }
        else
        {
            Map[key] = offsetMs;
        }
        Save();
    }

    /// <summary>Clear all saved offsets. Returns how many songs were cleared.</summary>
    public static int Clear()
    {
        int n = Map.Count;
        if (n == 0)
            return 0;
        Map.Clear();
        Save();
        return n;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Map));
        }
        catch { /* best-effort */ }
    }
}
