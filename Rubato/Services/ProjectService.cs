using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Data.Models;
using Rubato.Models;

namespace Rubato.Services;

public class ProjectService(IDbContextFactory<RubatoDataContext> dataContextFactory)
{
    public async Task<List<ProjectModel>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        return await dataContext.Projects
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => ProjectModel.FromData(p))
            .ToListAsync(cancellationToken);
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
}