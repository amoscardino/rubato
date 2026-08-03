using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Data.Models;
using Rubato.Models;

namespace Rubato.Services;

public class EntryService(RubatoDataContext dataContext)
{
    public async Task<List<EntryModel>> GetEntriesAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        return await dataContext.Entries
            .Where(e => e.Date == date)
            .OrderBy(e => e.SortOrder)
            .Select(e => EntryModel.FromData(e))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CreateEntryAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var entry = new Entry
        {
            Date = date,
        };

        dataContext.Entries.Add(entry);
        await dataContext.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }

    public async Task UpdateEntryAsync(EntryModel entryModel, CancellationToken cancellationToken = default)
    {
        var entryData = await dataContext.Entries.FirstAsync(e => e.Id == entryModel.Id, cancellationToken);

        entryData.Date = entryModel.Date;
        entryData.Time = entryModel.Time;
        entryData.Duration = entryModel.Duration;
        entryData.ProjectId = entryModel.ProjectId;
        entryData.TaskId = entryModel.TaskId;
        entryData.Description = entryModel.Description;
        entryData.SortOrder = entryModel.SortOrder;

        await dataContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteEntryAsync(long entryId, CancellationToken cancellationToken = default)
    {
        var entryData = await dataContext.Entries
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        if (entryData is not null)
        {
            dataContext.Entries.Remove(entryData);
            await dataContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CopyFromPreviousDayAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var previousDate = await dataContext.Entries
            .Where(e => e.Date < today)
            .OrderByDescending(e => e.Date)
            .Select(e => (DateOnly?)e.Date)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousDate is null)
        {
            return;
        }

        var previousDayEntries = await dataContext.Entries
            .Where(e => e.Date == previousDate)
            .ToListAsync(cancellationToken);

        foreach (var entry in previousDayEntries)
        {
            dataContext.Entries.Add(new Entry
            {
                Date = today,
                Time = entry.Time,
                Duration = entry.Duration,
                ProjectId = entry.ProjectId,
                TaskId = entry.TaskId,
                Description = entry.Description,
                SortOrder = entry.SortOrder
            });
        }

        await dataContext.SaveChangesAsync(cancellationToken);
    }
}