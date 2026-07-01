using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using LyricsOnTheGo.Models;

namespace LyricsOnTheGo.Services;

/// <summary>
/// Parses LRC text into timed lines. A line may carry several timestamps
/// (e.g. a repeated chorus) — each is expanded into its own entry. Timestamps are
/// stripped from the text and the result is sorted by time.
/// </summary>
public static class LrcParser
{
    // [mm:ss.xx] or [mm:ss.xxx] or [mm:ss]
    private static readonly Regex TagRegex =
        new(@"\[(\d{1,2}):(\d{1,2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);

    public static List<LyricLine> Parse(string lrc)
    {
        var lines = new List<LyricLine>();
        if (string.IsNullOrEmpty(lrc))
            return lines;

        foreach (var raw in lrc.Split('\n'))
        {
            var matches = TagRegex.Matches(raw);
            if (matches.Count == 0)
                continue;

            string text = TagRegex.Replace(raw, "").Trim();

            foreach (Match m in matches)
            {
                int min = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int sec = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                double frac = 0;
                if (m.Groups[3].Success)
                {
                    string f = m.Groups[3].Value;
                    frac = double.Parse(f, CultureInfo.InvariantCulture) / Math.Pow(10, f.Length);
                }

                double timeMs = ((min * 60) + sec + frac) * 1000.0;
                lines.Add(new LyricLine(timeMs, text));
            }
        }

        lines.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return lines;
    }
}
