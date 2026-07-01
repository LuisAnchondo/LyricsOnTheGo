using System;
using System.IO;
using System.Text;
using System.Text.Json;
using LyricsOnTheGo.Models;

namespace LyricsOnTheGo.Services;

/// <summary>
/// On-disk lyrics cache: one JSON file per song under %LOCALAPPDATA%\LyricsOnTheGo\
/// lyrics-cache. A song is downloaded from LRCLIB at most once. The cache is versioned
/// so improvements to the matching logic re-fetch already-cached songs.
/// </summary>
public static class LyricsCache
{
    // Bump when the matching logic changes so stale entries are dropped on next launch.
    private const string CacheVersion = "2";

    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LyricsOnTheGo", "lyrics-cache");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>FNV-1a 64-bit hex of normalized "title|artist|album|durationSec".</summary>
    public static string Key(string title, string artist, string album, double durationMs)
    {
        long durationSec = (long)(durationMs / 1000);
        string raw = $"{title.ToLowerInvariant()}|{artist.ToLowerInvariant()}|{album.ToLowerInvariant()}|{durationSec}";

        ulong hash = 0xcbf29ce484222325UL;
        foreach (byte b in Encoding.UTF8.GetBytes(raw))
        {
            hash ^= b;
            hash *= 0x100000001b3UL;
        }
        return hash.ToString("x16");
    }

    public static LyricsResult? Read(string key)
    {
        try
        {
            string path = Path.Combine(Dir, key + ".json");
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<LyricsResult>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string key, LyricsResult result)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, key + ".json"), JsonSerializer.Serialize(result, JsonOptions));
        }
        catch { /* cache is best-effort */ }
    }

    /// <summary>One-time migration: drop cached .json if the cache version changed.</summary>
    public static void EnsureVersion()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string marker = Path.Combine(Dir, ".version");
            if (File.Exists(marker) && File.ReadAllText(marker).Trim() == CacheVersion)
                return;

            foreach (var f in Directory.EnumerateFiles(Dir, "*.json"))
                TryDelete(f);

            File.WriteAllText(marker, CacheVersion);
        }
        catch { /* best-effort */ }
    }

    /// <summary>Deletes all cached .json (keeps the .version marker). Returns the count removed.</summary>
    public static int Clear()
    {
        int count = 0;
        try
        {
            if (!Directory.Exists(Dir))
                return 0;
            foreach (var f in Directory.EnumerateFiles(Dir, "*.json"))
                if (TryDelete(f))
                    count++;
        }
        catch { /* best-effort */ }
        return count;
    }

    private static bool TryDelete(string path)
    {
        try { File.Delete(path); return true; }
        catch { return false; }
    }
}
