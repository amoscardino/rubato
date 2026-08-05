using Microsoft.AspNetCore.Components;
using Rubato.Models;
using Rubato.Services;

namespace Rubato.Pages;

public partial class Projects
{
    [Inject] private ProjectService ProjectService { get; set; } = default!;

    private List<ProjectModel> ProjectList { get; set; } = [];

    private bool IsLoading { get; set; } = true;

    protected override Task OnInitializedAsync()
        => RunGuardedAsync(LoadProjectsAsync, errorPrefix: "Could not load projects");

    private async Task LoadProjectsAsync(CancellationToken cancellationToken)
    {
        try
        {
            ProjectList = await ProjectService.GetProjectsAsync(cancellationToken);
        }
        finally
        {
            // A failed load is still a finished load: drop the loader, or the error has nowhere to show.
            IsLoading = false;
        }
    }

    private Task AddProjectAsync()
        => RunGuardedAsync(
            async token =>
            {
                var projectId = await ProjectService.CreateProjectAsync(token);

                ProjectList.Add(new ProjectModel
                {
                    Id = projectId
                });
            },
            errorPrefix: "Could not add a project");
}
