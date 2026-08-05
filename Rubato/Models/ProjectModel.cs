using Rubato.Data.Models;

namespace Rubato.Models;

public class ProjectModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;

    public static ProjectModel FromData(Project project)
        => new()
        {
            Id = project.Id,
            Name = project.Name,
            Color = project.Color
        };
}
