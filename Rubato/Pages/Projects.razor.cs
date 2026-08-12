using Microsoft.AspNetCore.Components;
using Rubato.Models;
using Rubato.Services;

namespace Rubato.Pages;

public partial class Projects
{
    [Inject] private ProjectService ProjectService { get; set; } = default!;

    private List<ProjectModel> ProjectList { get; set; } = [];

    protected override Task OnInitializedAsync()
        => RunInitialLoadAsync(
            async token => ProjectList = await ProjectService.GetProjectsAsync(token),
            errorPrefix: "Could not load projects");

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
