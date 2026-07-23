using System.Text.RegularExpressions;
using Rubato.Data.Models;
using Rubato.Extensions;

namespace Rubato.Models;

public partial class EntryModel
{
    public long Id { get; set; }

    public long? ProjectId { get; set; }
    public DateOnly Date { get; set; }
    public string? Time { get; set; }
    public double? Duration { get; set; }
    public string? TaskId { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }

    public int TimeRows => Time?.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Length ?? 1;

    public static EntryModel FromData(Entry entry)
        => new()
        {
            Id = entry.Id,
            ProjectId = entry.ProjectId,
            Date = entry.Date,
            Time = entry.Time,
            Duration = entry.Duration,
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

    public double? GetDuration()
    {
        var times = (Time ?? string.Empty)
            .Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (times.Count == 0)
        {
            return null;
        }

        return times.Sum(ConvertToHours);
    }

    private static double ConvertToHours(string time)
    {
        var match = TimeRegex().Match(time);

        if (match == null || match.Groups.Count < 1)
            return 0;

        var startHour = int.Parse((match.Groups.GetValueOrDefault("startHour")?.Value).OrDefault("12"));
        var startMinutes = int.Parse((match.Groups.GetValueOrDefault("startMinutes")?.Value).OrDefault("0"));
        var endHour = int.Parse((match.Groups.GetValueOrDefault("endHour")?.Value).OrDefault(startHour.ToString()));
        var endMinutes = int.Parse((match.Groups.GetValueOrDefault("endMinutes")?.Value).OrDefault("0"));

        if (endHour < startHour)
            endHour += 12;

        if (startHour == endHour && endMinutes == 0)
            endMinutes = startMinutes;

        // The year/month/day parameters are not important so long as they are the same for both
        var startDate = new DateTime(2000, 1, 1, startHour, startMinutes, 0);
        var endDate = new DateTime(2000, 1, 1, endHour, endMinutes, 0);

        return (endDate - startDate).TotalMinutes / 60;
    }

    [GeneratedRegex(@"(?<startHour>\d{1,2}):?(?<startMinutes>\d{2})?-(?<endHour>\d{0,2})?:?(?<endMinutes>\d{2})?")]
    private static partial Regex TimeRegex();
}