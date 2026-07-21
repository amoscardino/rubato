using System.ComponentModel.DataAnnotations;

namespace Rubato.Data.Models;

public class Entry
{
    [Key]
    public long Id { get; set; }

    public long ProjectId { get; set; }
    public DateOnly Date { get; set; }
    public string Time { get; set; } = string.Empty;
    public double Duration { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public Project Project { get; set; } = null!;
}