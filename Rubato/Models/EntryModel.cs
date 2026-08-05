using System.Text.RegularExpressions;
using Rubato.Data.Models;

namespace Rubato.Models;

public partial class EntryModel
{
    private string? _time;
    private TimeParseResult? _parseResult;

    public long Id { get; set; }

    public long? ProjectId { get; set; }
    public DateOnly Date { get; set; }

    public string? Time
    {
        get => _time;
        set
        {
            _time = value;
            _parseResult = null;
        }
    }

    public double? Duration => ParseTime().TotalHours;
    public string? TaskId { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }

    public IReadOnlyList<string> InvalidTimeLines => ParseTime().InvalidLines;
    public bool HasInvalidTime => InvalidTimeLines.Count > 0;

    public string ClipboardText
    {
        get
        {
            var taskId = TaskId?.Trim();
            var description = Description?.Trim();

            if (string.IsNullOrWhiteSpace(taskId))
                return description ?? string.Empty;

            return string.IsNullOrWhiteSpace(description) ? taskId : $"{taskId} - {description}";
        }
    }

    public static EntryModel FromData(Entry entry)
        => new()
        {
            Id = entry.Id,
            ProjectId = entry.ProjectId,
            Date = entry.Date,
            Time = entry.Time,
            TaskId = entry.TaskId,
            Description = entry.Description,
            SortOrder = entry.SortOrder
        };

    public Entry ToData()
        => new()
        {
            ProjectId = ProjectId,
            Date = Date,
            Time = Time,
            Duration = Duration,
            TaskId = TaskId,
            Description = Description,
            SortOrder = SortOrder
        };

    /// <summary>
    /// Parses every line of <see cref="Time"/>, summing the hours from the lines that are a
    /// readable range and collecting the ones that are not. The result is cached until
    /// <see cref="Time"/> is next assigned, since a single render reads it several times over.
    /// </summary>
    public TimeParseResult ParseTime()
    {
        _parseResult ??= Parse(Time);

        return _parseResult.Value;
    }

    private static TimeParseResult Parse(string? time)
    {
        var lines = (time ?? string.Empty)
            .Split(LineSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            return new TimeParseResult(null, []);
        }

        var totalHours = 0d;
        List<string> invalidLines = [];

        foreach (var line in lines)
        {
            if (TryConvertToHours(line, out var hours))
            {
                totalHours += hours;
            }
            else
            {
                invalidLines.Add(line);
            }
        }

        return new TimeParseResult(totalHours, invalidLines);
    }

    /// <summary>
    /// Converts a single "start-end" line to hours. Returns false when the line is not a range we
    /// can make sense of, so the caller can flag it instead of counting it as zero hours. A range
    /// with no end time yet ("9-") is in progress: readable, but worth nothing so far.
    /// </summary>
    private static bool TryConvertToHours(string time, out double hours)
    {
        hours = 0;

        var match = TimeRegex().Match(time);

        if (!match.Success)
            return false;

        if (!TryReadClockTime(match.Groups["startHour"], match.Groups["startMinutes"], out var startHour, out var startMinutes))
            return false;

        var minutesOnly = match.Groups["endMinutesOnly"];
        var endNamesItsOwnHour = minutesOnly.Success;
        int endHour;
        int endMinutes;

        if (endNamesItsOwnHour)
        {
            // "7:15-:30" names only the minutes, so the end sits in the start's own hour.
            endHour = startHour;

            if (!int.TryParse(minutesOnly.Value, out endMinutes) || endMinutes > 59)
                return false;
        }
        else if (!match.Groups["endHour"].Success)
        {
            // No end time yet, so the entry is still running and has no duration to report.
            return true;
        }
        else if (!TryReadClockTime(match.Groups["endHour"], match.Groups["endMinutes"], out endHour, out endMinutes))
        {
            return false;
        }

        var start = new TimeSpan(startHour, startMinutes, 0);
        var end = new TimeSpan(endHour, endMinutes, 0);

        // The 12-hour assumption: an end time before the start means the afternoon, so "9-5" is 9am
        // to 5pm. Compare the whole time, not just the hour — otherwise "9:30-9" is rejected while
        // the equivalent "9:30-8" is accepted, purely because the hours happen to tie. An end hour
        // of 12 or more is already unambiguous and is taken literally, and a minutes-only end has
        // named the start's hour outright, so there is no other hour it could have meant.
        if (end < start && endHour < 12 && !endNamesItsOwnHour)
            end += TimeSpan.FromHours(12);

        // Still backwards after the afternoon reading: either an unambiguous range running the
        // wrong way ("14-13") or one crossing midnight ("23-1"), which a single day's entry cannot
        // express. Neither has a duration worth reporting.
        if (end < start)
            return false;

        hours = (end - start).TotalHours;
        return true;
    }

    /// <summary>
    /// Reads an hour and its optional minutes, rejecting anything out of range. Validating here is
    /// what keeps unchecked regex captures out of the <see cref="TimeSpan"/> constructors below —
    /// feeding them straight in is what used to throw out of an event handler and kill the circuit.
    /// </summary>
    private static bool TryReadClockTime(Group hourGroup, Group minutesGroup, out int hour, out int minutes)
    {
        hour = 0;
        minutes = 0;

        if (!int.TryParse(hourGroup.Value, out hour) || hour > 23)
            return false;

        if (minutesGroup.Success && (!int.TryParse(minutesGroup.Value, out minutes) || minutes > 59))
            return false;

        return true;
    }

    /// <summary>
    /// The outcome of parsing a <see cref="Time"/> field: the hours from the lines that were
    /// readable, and the lines that were not. <see cref="TotalHours"/> is null only when there
    /// were no lines at all.
    /// </summary>
    public readonly record struct TimeParseResult(double? TotalHours, IReadOnlyList<string> InvalidLines);

    private static readonly string[] LineSeparators = ["\r\n", "\r", "\n"];

    // The end time is either absent ("9-"), an hour with optional minutes ("5", "5:30", "530"), or
    // minutes alone against the start's hour (":30"). The last form needs its colon, so that "9-30"
    // stays an out-of-range hour rather than quietly becoming 9:30.
    [GeneratedRegex(@"^(?<startHour>\d{1,2})(?::?(?<startMinutes>\d{2}))?\s*-\s*(?:(?<endHour>\d{1,2})(?::?(?<endMinutes>\d{2}))?|:(?<endMinutesOnly>\d{2}))?$")]
    private static partial Regex TimeRegex();
}