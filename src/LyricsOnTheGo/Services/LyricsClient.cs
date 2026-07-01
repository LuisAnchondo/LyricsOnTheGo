using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LyricsOnTheGo.Models;

namespace LyricsOnTheGo.Services;

/// <summary>
/// Fetches lyrics from LRCLIB, tolerant of how different players report metadata.
/// Priority: exact /api/get → /api/search (track+artist) → fuzzy /api/search?q=title
/// (this last one makes YouTube work). The first SYNCED hit wins; otherwise the best
/// plain-only hit. Disk cache is consulted first by the caller.
/// </summary>
public sealed class LyricsClient
{
    private static readonly HttpClient Http = CreateHttp();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "LyricsOnTheGo/2.0.0 (https://github.com/LuisAnchondo)");
        return http;
    }

    public async Task<LyricsResult> FetchAsync(string title, string artist, string album, double durationMs)
    {
        double targetSeconds = durationMs / 1000.0;
        LyricsResult? bestPlain = null;

        // 1. Exact get — ideal when the player reports clean metadata (e.g. Spotify).
        int durSec = Math.Max(0, (int)(durationMs / 1000));
        string getUrl = "https://lrclib.net/api/get"
            + $"?artist_name={Esc(artist)}&track_name={Esc(title)}&album_name={Esc(album)}&duration={durSec}";
        var single = await GetAsync<LrcLibEntry>(getUrl);
        if (single is not null)
        {
            var r = EntryToResult(single, "lrclib:get");
            if (r is not null)
            {
                if (!string.IsNullOrWhiteSpace(r.Synced))
                    return r;
                bestPlain ??= r;
            }
        }

        // 2. Structured search, then 3. fuzzy title-only search.
        var attempts = new (string Url, string Source)[]
        {
            ($"https://lrclib.net/api/search?track_name={Esc(title)}&artist_name={Esc(artist)}", "lrclib:search"),
            ($"https://lrclib.net/api/search?q={Esc(title)}", "lrclib:q"),
        };

        foreach (var (url, source) in attempts)
        {
            var list = await GetAsync<List<LrcLibEntry>>(url) ?? new List<LrcLibEntry>();
            var (synced, plain) = SelectBest(list, targetSeconds, source);
            if (synced is not null)
                return synced;
            bestPlain ??= plain;
        }

        return bestPlain ?? LyricsResult.NotFound;
    }

    /// <summary>Picks the synced hit whose duration is closest to the player's, plus the first plain fallback.</summary>
    private static (LyricsResult? Synced, LyricsResult? Plain) SelectBest(
        List<LrcLibEntry> entries, double targetSeconds, string source)
    {
        LyricsResult? bestSynced = null;
        double bestDiff = double.MaxValue;
        LyricsResult? bestPlain = null;

        foreach (var entry in entries)
        {
            bool hasSynced = !string.IsNullOrWhiteSpace(entry.SyncedLyrics);
            double dur = entry.Duration ?? 0.0;
            double diff = (targetSeconds > 0 && dur > 0) ? Math.Abs(dur - targetSeconds) : double.MaxValue;

            var r = EntryToResult(entry, source);
            if (r is null)
                continue;

            if (hasSynced)
            {
                if (bestSynced is null || diff < bestDiff)
                {
                    bestSynced = r;
                    bestDiff = diff;
                }
            }
            else
            {
                bestPlain ??= r;
            }
        }

        return (bestSynced, bestPlain);
    }

    private static LyricsResult? EntryToResult(LrcLibEntry e, string source)
    {
        if (e.Instrumental)
            return new LyricsResult { Found = true, Instrumental = true, Source = source };

        string synced = e.SyncedLyrics ?? "";
        string plain = e.PlainLyrics ?? "";
        if (string.IsNullOrWhiteSpace(synced) && string.IsNullOrWhiteSpace(plain))
            return null;

        return new LyricsResult { Found = true, Synced = synced, Plain = plain, Source = source };
    }

    private static async Task<T?> GetAsync<T>(string url)
    {
        try
        {
            using var resp = await Http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return default;
            await using var stream = await resp.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private static string Esc(string s) => Uri.EscapeDataString(s ?? "");

    private sealed class LrcLibEntry
    {
        [JsonPropertyName("syncedLyrics")] public string? SyncedLyrics { get; set; }
        [JsonPropertyName("plainLyrics")] public string? PlainLyrics { get; set; }
        [JsonPropertyName("instrumental")] public bool Instrumental { get; set; }
        [JsonPropertyName("duration")] public double? Duration { get; set; }
    }
}
