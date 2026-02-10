using System.Text.RegularExpressions;
using PFD.Shared.Enums;

namespace PFD.Services;

/// <summary>
/// Parses natural language time expressions from task descriptions.
/// Returns the cleaned task title and parsed time.
/// </summary>
public static class TaskTimeParser
{
    public record ParseResult(
        string CleanedTitle,
        TimeSpan? ScheduledTime,
        RecurrenceType RecurrenceType = RecurrenceType.None,
        List<string>? RecurrenceDays = null,
        DateTime? RecurrenceEndDate = null);

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

    // Day abbreviation mappings for recurring patterns
    private static readonly Dictionary<string, string> DayMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "M", "Mon" }, { "Mo", "Mon" }, { "Mon", "Mon" }, { "Monday", "Mon" },
        { "T", "Tue" }, { "Tu", "Tue" }, { "Tue", "Tue" }, { "Tuesday", "Tue" },
        { "W", "Wed" }, { "We", "Wed" }, { "Wed", "Wed" }, { "Wednesday", "Wed" },
        { "Th", "Thu" }, { "Thu", "Thu" }, { "Thursday", "Thu" }, { "R", "Thu" },
        { "F", "Fri" }, { "Fr", "Fri" }, { "Fri", "Fri" }, { "Friday", "Fri" },
        { "S", "Sat" }, { "Sa", "Sat" }, { "Sat", "Sat" }, { "Saturday", "Sat" },
        { "Su", "Sun" }, { "Sun", "Sun" }, { "Sunday", "Sun" },
    };

    // Patterns for day combinations like "MW", "MWF", "TTh", "M W F"
    private static readonly Regex DayPatternRegex = new(
        @"\b((?:M|Mo|Mon|Monday|T|Tu|Tue|Tuesday|W|We|Wed|Wednesday|Th|Thu|Thursday|R|F|Fr|Fri|Friday|S|Sa|Sat|Saturday|Su|Sun|Sunday)(?:\s*(?:M|Mo|Mon|Monday|T|Tu|Tue|Tuesday|W|We|Wed|Wednesday|Th|Thu|Thursday|R|F|Fr|Fri|Friday|S|Sa|Sat|Saturday|Su|Sun|Sunday))*)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Pattern for "till/until/through/thru [date]" or "ends [date]"
    private static readonly Regex EndDateRegex = new(
        @"\b(?:till|until|through|thru|ending|ends?)\s+(\w+\s+\d{1,2}(?:,?\s+\d{4})?|\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Pattern for daily recurrence
    private static readonly Regex DailyPatternRegex = new(
        @"\b(?:every\s*day|daily)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parse a task description and extract time and recurrence information.
    /// Examples:
    /// - "teach 333 MW 3:00 pm till May 1" -> Weekly on Mon, Wed at 3pm until May 1
    /// - "staff meeting TTh 10am" -> Weekly on Tue, Thu at 10am
    /// - "daily standup 9am" -> Daily at 9am
    /// </summary>
    public static ParseResult ParseWithRecurrence(string taskText)
    {
        if (string.IsNullOrWhiteSpace(taskText))
            return new ParseResult(taskText, null);

        var cleanedText = taskText;
        TimeSpan? scheduledTime = null;
        RecurrenceType recurrenceType = RecurrenceType.None;
        List<string>? recurrenceDays = null;
        DateTime? endDate = null;

        // 1. Check for end date first (before removing other parts)
        var endDateMatch = EndDateRegex.Match(cleanedText);
        if (endDateMatch.Success)
        {
            endDate = ParseEndDate(endDateMatch.Groups[1].Value);
            cleanedText = cleanedText.Remove(endDateMatch.Index, endDateMatch.Length);
            cleanedText = CleanUpText(cleanedText);
        }

        // 2. Check for daily pattern
        var dailyMatch = DailyPatternRegex.Match(cleanedText);
        if (dailyMatch.Success)
        {
            recurrenceType = RecurrenceType.Daily;
            cleanedText = cleanedText.Remove(dailyMatch.Index, dailyMatch.Length);
            cleanedText = CleanUpText(cleanedText);
        }

        // 3. Check for weekly day patterns (MW, MWF, TTh, etc.)
        if (recurrenceType == RecurrenceType.None)
        {
            var dayMatch = DayPatternRegex.Match(cleanedText);
            if (dayMatch.Success)
            {
                var parsedDays = ParseDayPattern(dayMatch.Groups[1].Value);
                if (parsedDays.Count > 0)
                {
                    recurrenceType = RecurrenceType.Weekly;
                    recurrenceDays = parsedDays;
                    cleanedText = cleanedText.Remove(dayMatch.Index, dayMatch.Length);
                    cleanedText = CleanUpText(cleanedText);
                }
            }
        }

        // 4. Parse time from the remaining text
        var timeResult = Parse(cleanedText);
        scheduledTime = timeResult.ScheduledTime;
        cleanedText = timeResult.CleanedTitle;

        return new ParseResult(cleanedText, scheduledTime, recurrenceType, recurrenceDays, endDate);
    }

    /// <summary>
    /// Parse day pattern like "MW", "MWF", "TTh", "M W F", "Monday Wednesday Friday"
    /// </summary>
    private static List<string> ParseDayPattern(string pattern)
    {
        var days = new List<string>();

        // Try to parse as combined abbreviations (MW, MWF, TTh)
        if (!pattern.Contains(' '))
        {
            // Handle special cases like "TTh" (Tuesday Thursday)
            var remaining = pattern;
            while (remaining.Length > 0)
            {
                string? matchedDay = null;

                // Try longer matches first (Th before T, etc.)
                foreach (var length in new[] { 8, 7, 6, 5, 4, 3, 2, 1 })
                {
                    if (remaining.Length >= length)
                    {
                        var candidate = remaining.Substring(0, length);
                        if (DayMappings.TryGetValue(candidate, out var day))
                        {
                            if (!days.Contains(day))
                                days.Add(day);
                            matchedDay = candidate;
                            break;
                        }
                    }
                }

                if (matchedDay != null)
                    remaining = remaining.Substring(matchedDay.Length);
                else
                    remaining = remaining.Substring(1); // Skip unrecognized character
            }
        }
        else
        {
            // Space-separated days
            var parts = pattern.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (DayMappings.TryGetValue(part.Trim(), out var day))
                {
                    if (!days.Contains(day))
                        days.Add(day);
                }
            }
        }

        return days;
    }

    /// <summary>
    /// Parse end date from various formats like "May 1", "May 1, 2024", "5/1", "5/1/2024"
    /// </summary>
    private static DateTime? ParseEndDate(string dateStr)
    {
        dateStr = dateStr.Trim();

        // Try standard date parsing
        if (DateTime.TryParse(dateStr, out var date))
        {
            // If no year specified and date is in the past, assume next year
            if (!dateStr.Contains("202") && date < DateTime.Today)
                date = date.AddYears(1);
            return date;
        }

        // Try "Month Day" format (May 1)
        var monthDayRegex = new Regex(@"(\w+)\s+(\d{1,2})(?:,?\s+(\d{4}))?", RegexOptions.IgnoreCase);
        var match = monthDayRegex.Match(dateStr);
        if (match.Success)
        {
            var monthName = match.Groups[1].Value;
            var day = int.Parse(match.Groups[2].Value);
            var year = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : DateTime.Today.Year;

            if (DateTime.TryParse($"{monthName} {day}, {year}", out var parsedDate))
            {
                // If no year specified and date is in the past, assume next year
                if (!match.Groups[3].Success && parsedDate < DateTime.Today)
                    parsedDate = parsedDate.AddYears(1);
                return parsedDate;
            }
        }

        return null;
    }

    private static string CleanUpText(string text)
    {
        text = Regex.Replace(text, @"\s{2,}", " ").Trim();
        text = text.Trim(' ', '-', ',', ';');
        return text;
    }
}
