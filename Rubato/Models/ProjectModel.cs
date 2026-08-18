using Rubato.Data.Models;

namespace Rubato.Models;

public class ProjectModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string WorkItemId { get; set; } = string.Empty;

    /// <summary>
    /// How many entries point at this project. Deleting a project that still has entries would leave
    /// them pointing at nothing, so the count is what decides whether the row offers a delete button.
    /// A project the page has only just created has none, hence the default.
    /// </summary>
    public int EntryCount { get; set; }

    public bool CanDelete => EntryCount == 0;

    public static ProjectModel FromData(Project project, int entryCount)
        => new()
        {
            Id = project.Id,
            Name = project.Name,
            Color = project.Color,
            WorkItemId = project.WorkItemId,
            EntryCount = entryCount
        };
}
