using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Data.Models;
using Rubato.Models;

namespace Rubato.Services;

public class ProjectService(RubatoDataContext dataContext)
{
    public async Task<List<ProjectModel>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        return await dataContext.Projects
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
        var project = new Project();

        dataContext.Projects.Add(project);
        await dataContext.SaveChangesAsync(cancellationToken);

        return project.Id;
    }

    public async Task UpdateProjectAsync(ProjectModel projectModel, CancellationToken cancellationToken = default)
    {
        var projectData = await dataContext.Projects.FirstAsync(p => p.Id == projectModel.Id, cancellationToken);

        projectData.Name = projectModel.Name;
        projectData.Color = projectModel.Color;

        await dataContext.SaveChangesAsync(cancellationToken);
    }
}