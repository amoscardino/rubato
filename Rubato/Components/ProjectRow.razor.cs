using Microsoft.AspNetCore.Components;
using Rubato.Models;
using Rubato.Services;

namespace Rubato.Components;

public partial class ProjectRow
{
    [Inject] private ProjectService ProjectService { get; set; } = default!;

    [Parameter] public ProjectModel Project { get; set; } = new();
    [Parameter] public EventCallback OnProjectDeleted { get; set; }

    /// <summary>
    /// Why the delete button is disabled, when it is. A disabled button says nothing on its own, and
    /// "this project is still in use" is the whole reason a project cannot be deleted.
    /// </summary>
    private string DeleteMessage
        => Project.CanDelete
            ? "Delete project"
            : $"Assigned to {Project.EntryCount} {(Project.EntryCount == 1 ? "entry" : "entries")}, so it cannot be deleted";

    private Task SaveProjectAsync()
        => RunGuardedAsync(token => ProjectService.UpdateProjectAsync(Project, token), errorPrefix: "Not saved");

    /// <summary>
    /// The button is already disabled for a project that has entries, but that only reflects the count
    /// the page loaded with — an entry assigned in another tab since then is caught by the service,
    /// which refuses rather than throwing, so the refusal is reported into <c>Error</c> by hand. The
    /// guard skips the callback whenever <c>Error</c> is set, so the page only reloads on a real delete.
    /// </summary>
    private Task DeleteProjectAsync()
        => RunGuardedAsync(
            async token =>
            {
                if (!await ProjectService.DeleteProjectAsync(Project.Id, token))
                {
                    Error = "Not deleted: entries are still assigned to this project.";
                }
            },
            OnProjectDeleted,
            "Not deleted");
}
