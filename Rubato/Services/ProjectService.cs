using Microsoft.EntityFrameworkCore;
using Rubato.Data;
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
}