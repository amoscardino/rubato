using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Data.Models;
using Rubato.Models;

namespace Rubato.Services;

public class ProjectService(IDbContextFactory<RubatoDataContext> dataContextFactory)
{
    /// <summary>
    /// Every project, each with the number of entries assigned to it — counted in the query rather
    /// than by loading the entries themselves, since the only thing the page does with it is decide
    /// whether the project may be deleted.
    /// </summary>
    public async Task<List<ProjectModel>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await dataContext.Projects
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                Project = p,
                EntryCount = p.Entries.Count
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => ProjectModel.FromData(r.Project, r.EntryCount))];
    }

    public async Task<long> CreateProjectAsync(CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var project = new Project();

        dataContext.Projects.Add(project);
        await dataContext.SaveChangesAsync(cancellationToken);

        return project.Id;
    }

    public async Task UpdateProjectAsync(ProjectModel projectModel, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var projectData = await dataContext.Projects
            .FirstOrDefaultAsync(p => p.Id == projectModel.Id, cancellationToken);

        if (projectData is null)
        {
            return;
        }

        projectData.Name = projectModel.Name;
        projectData.Color = projectModel.Color;
        projectData.WorkItemId = projectModel.WorkItemId;

        await dataContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes a project, but only while nothing is assigned to it — an entry whose project has been
    /// deleted out from under it would lose which work it was for. Returns false when entries still
    /// hold the project, which the row reports; the count the button was disabled from is a page-load
    /// snapshot, so this is the check that actually decides. A project already gone is not a failure:
    /// another tab may have deleted it, and the caller wanted it gone either way.
    /// </summary>
    public async Task<bool> DeleteProjectAsync(long projectId, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var projectData = await dataContext.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (projectData is null)
        {
            return true;
        }

        if (await dataContext.Entries.AnyAsync(e => e.ProjectId == projectId, cancellationToken))
        {
            return false;
        }

        dataContext.Projects.Remove(projectData);
        await dataContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
