using System;

namespace ZedRecentProjects;

/// <summary>
/// Formats an UTC timestamp as a compact relative time string.
/// </summary>
public static class RelativeTime
{
    public static string Format(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }

        var delta = DateTime.UtcNow - utc;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero; // guard against clock skew
        }

        if (delta.TotalSeconds < 60) return "just now";
        if (delta.TotalMinutes < 60) return Ago((int)delta.TotalMinutes, "minute");
        if (delta.TotalHours < 24) return Ago((int)delta.TotalHours, "hour");
        if (delta.TotalDays < 2) return "yesterday";
        if (delta.TotalDays < 30) return Ago((int)delta.TotalDays, "day");
        if (delta.TotalDays < 365) return Ago((int)(delta.TotalDays / 30), "month");
        return Ago((int)(delta.TotalDays / 365), "year");
    }

    private static string Ago(int count, string unit) => $"{count} {unit}{(count == 1 ? string.Empty : "s")} ago";
}