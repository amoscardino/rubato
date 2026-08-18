using Microsoft.AspNetCore.Components;
using Rubato.Models;
using Rubato.Services;

namespace Rubato.Pages;

public partial class Projects
{
    [Inject] private ProjectService ProjectService { get; set; } = default!;

    private List<ProjectModel> ProjectList { get; set; } = [];

    protected override Task OnInitializedAsync()
        => RunInitialLoadAsync(LoadProjectsAsync, errorPrefix: "Could not load projects");

    /// <summary>
    /// Refetches the list after a row deleted itself. Deliberately not
    /// <see cref="CancellableComponentBase.RunInitialLoadAsync"/>: the page is already on screen, and
    /// a full reload is what drops the deleted row and picks up any entry counts that have moved.
    /// </summary>
    private Task ReloadProjectsAsync()
        => RunGuardedAsync(LoadProjectsAsync, errorPrefix: "Could not load projects");

    private async Task LoadProjectsAsync(CancellationToken cancellationToken)
        => ProjectList = await ProjectService.GetProjectsAsync(cancellationToken);

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
