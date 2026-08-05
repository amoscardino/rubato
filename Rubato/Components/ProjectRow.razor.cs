using Microsoft.AspNetCore.Components;
using Rubato.Models;
using Rubato.Services;

namespace Rubato.Components;

public partial class ProjectRow
{
    [Inject] private ProjectService ProjectService { get; set; } = default!;

    [Parameter] public ProjectModel Project { get; set; } = new();

    private Task SaveProjectAsync()
        => RunGuardedAsync(token => ProjectService.UpdateProjectAsync(Project, token), errorPrefix: "Not saved");
}
