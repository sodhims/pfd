using System.Text.RegularExpressions;

namespace PFD.Services;

/// <summary>
/// Parses natural language time expressions from task descriptions.
/// Returns the cleaned task title and parsed time.
/// </summary>
public static class TaskTimeParser
{
    public record ParseResult(string CleanedTitle, TimeSpan? ScheduledTime);

    // Named times
    private static readonly Dictionary<string, TimeSpan> NamedTimes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "midnight", new TimeSpan(0, 0, 0) },
        { "dawn", new TimeSpan(6, 0, 0) },
        { "sunrise", new TimeSpan(6, 30, 0) },
        { "morning", new TimeSpan(9, 0, 0) },
        { "noon", new TimeSpan(12, 0, 0) },
        { "midday", new TimeSpan(12, 0, 0) },
        { "afternoon", new TimeSpan(14, 0, 0) },
        { "evening", new TimeSpan(18, 0, 0) },
        { "sunset", new TimeSpan(18, 30, 0) },
        { "dusk", new TimeSpan(18, 30, 0) },
        { "night", new TimeSpan(20, 0, 0) },
        { "eod", new TimeSpan(17, 0, 0) },
        { "end of day", new TimeSpan(17, 0, 0) },
        { "cob", new TimeSpan(17, 0, 0) },
        { "lunchtime", new TimeSpan(12, 0, 0) },
        { "lunch", new TimeSpan(12, 0, 0) },
        { "dinnertime", new TimeSpan(18, 0, 0) },
        { "dinner", new TimeSpan(18, 0, 0) },
        { "breakfast", new TimeSpan(8, 0, 0) },
    };

    // Patterns ordered from most specific to least specific
    private static readonly (Regex Pattern, Func<Match, TimeSpan?> Parser)[] Patterns = new[]
    {
        // "at 2:30 pm", "at 14:30", "at 2:30pm"
        (new Regex(@"\b(?:at|@)\s+(\d{1,2}):(\d{2})\s*(am|pm|a\.m\.|p\.m\.)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         new Func<Match, TimeSpan?>(m => ParseHourMinAmPm(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value))),

        // "at 3 pm", "at 3pm", "at 15"
        (new Regex(@"\b(?:at|@)\s+(\d{1,2})\s*(am|pm|a\.m\.|p\.m\.)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         new Func<Match, TimeSpan?>(m => ParseHourAmPm(m.Groups[1].Value, m.Groups[2].Value))),

        // "at noon", "at midnight", "at lunch", etc.
        (new Regex(@"\b(?:at|@)\s+(midnight|dawn|sunrise|morning|noon|midday|afternoon|evening|sunset|dusk|night|eod|cob|lunchtime|lunch|dinnertime|dinner|breakfast)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         new Func<Match, TimeSpan?>(m => NamedTimes.GetValueOrDefault(m.Groups[1].Value.ToLowerInvariant()))),

        // "2:30 pm", "14:30" (without "at") - only match if clearly a time expression
        (new Regex(@"\b(\d{1,2}):(\d{2})\s*(am|pm|a\.m\.|p\.m\.)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         new Func<Match, TimeSpan?>(m => ParseHourMinAmPm(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value))),

        // "3pm", "3 pm" (without "at") - requires am/pm suffix
        (new Regex(@"\b(\d{1,2})\s*(am|pm|a\.m\.|p\.m\.)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         new Func<Match, TimeSpan?>(m => ParseHourAmPm(m.Groups[1].Value, m.Groups[2].Value))),

        // "by noon", "before noon", "around noon" etc.
        (new Regex(@"\b(?:by|before|around|after)\s+(midnight|dawn|sunrise|morning|noon|midday|afternoon|evening|sunset|dusk|night|eod|cob|lunchtime|lunch|dinnertime|dinner|breakfast)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         new Func<Match, TimeSpan?>(m => NamedTimes.GetValueOrDefault(m.Groups[1].Value.ToLowerInvariant()))),
    };

    /// <summary>
    /// Parse a task description and extract any time information.
    /// Returns cleaned title and parsed time.
    /// </summary>
    public static ParseResult Parse(string taskText)
    {
        if (string.IsNullOrWhiteSpace(taskText))
            return new ParseResult(taskText, null);

        foreach (var (pattern, parser) in Patterns)
        {
            var match = pattern.Match(taskText);
            if (match.Success)
            {
                var time = parser(match);
                if (time.HasValue)
                {
                    // Remove the matched time expression from the title
                    var cleaned = taskText.Remove(match.Index, match.Length).Trim();
                    // Clean up extra spaces
                    cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
                    // Remove trailing/leading punctuation artifacts
                    cleaned = cleaned.Trim(' ', '-', ',', ';');

                    if (string.IsNullOrWhiteSpace(cleaned))
                        cleaned = taskText.Trim(); // Don't lose the title entirely

                    return new ParseResult(cleaned, time.Value);
                }
            }
        }

        return new ParseResult(taskText.Trim(), null);
    }

    private static TimeSpan? ParseHourMinAmPm(string hourStr, string minStr, string amPmStr)
    {
        if (!int.TryParse(hourStr, out var hour) || !int.TryParse(minStr, out var min))
            return null;

        if (hour < 0 || hour > 23 || min < 0 || min > 59)
            return null;

        if (!string.IsNullOrWhiteSpace(amPmStr))
        {
            var isPm = amPmStr.TrimEnd('.').Equals("pm", StringComparison.OrdinalIgnoreCase) ||
                       amPmStr.TrimEnd('.').Equals("p.m", StringComparison.OrdinalIgnoreCase);
            var isAm = !isPm;

            if (isPm && hour < 12) hour += 12;
            if (isAm && hour == 12) hour = 0;
        }

        return new TimeSpan(hour, min, 0);
    }

    private static TimeSpan? ParseHourAmPm(string hourStr, string amPmStr)
    {
        if (!int.TryParse(hourStr, out var hour))
            return null;

        if (hour < 1 || hour > 12)
            return null;

        var isPm = amPmStr.TrimEnd('.').Equals("pm", StringComparison.OrdinalIgnoreCase) ||
                   amPmStr.TrimEnd('.').Equals("p.m", StringComparison.OrdinalIgnoreCase);

        if (isPm && hour < 12) hour += 12;
        if (!isPm && hour == 12) hour = 0;

        return new TimeSpan(hour, 0, 0);
    }
}
