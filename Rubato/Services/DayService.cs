using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Models;

namespace Rubato.Services;

public class DayService(RubatoDataContext dataContext)
{
    public async Task<DayModel> GetDayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        return new DayModel
        {
            Date = date,
            Entries = await dataContext.Entries
                .Where(e => e.Date == date)
                .OrderBy(e => e.SortOrder)
                .Select(e => EntryModel.FromData(e))
                .ToListAsync(cancellationToken)
        };
    }
}