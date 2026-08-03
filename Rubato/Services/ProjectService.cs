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
            .Select(p => new ProjectModel
            {
                Id = p.Id,
                Name = p.Name,
                Color = p.Color
            })
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

    /// <summary>
    /// Writes the model back over the stored project. Returns false when the project is no longer
    /// there, so a save racing a delete from another tab does not surface as an error.
    /// </summary>
    public async Task<bool> UpdateProjectAsync(ProjectModel projectModel, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var projectData = await dataContext.Projects
            .FirstOrDefaultAsync(p => p.Id == projectModel.Id, cancellationToken);

        if (projectData is null)
        {
            return false;
        }

        projectData.Name = projectModel.Name;
        projectData.Color = projectModel.Color;

        await dataContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}