using Rubato.Data.Models;

namespace Rubato.Models;

public class EntryModel
{
    public long Id { get; set; }

    public long? ProjectId { get; set; }
    public DateOnly Date { get; set; }
    public string? Time { get; set; }
    public double? Duration { get; set; }
    public string? TaskId { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }

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
            Id = Id,
            ProjectId = ProjectId,
            Date = Date,
            Time = Time,
            Duration = Duration,
            TaskId = TaskId,
            Description = Description,
            SortOrder = SortOrder
        };
}