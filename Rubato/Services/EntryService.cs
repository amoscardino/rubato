using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Data.Models;
using Rubato.Models;

namespace Rubato.Services;

public class EntryService(IDbContextFactory<RubatoDataContext> dataContextFactory)
{
    public async Task<List<EntryModel>> GetEntriesAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        return await dataContext.Entries
            .AsNoTracking()
            .Where(e => e.Date == date)
            .OrderBy(e => e.SortOrder)
            .Select(e => EntryModel.FromData(e))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Sums the hours worked over the Monday-start week containing <paramref name="date"/>. The
    /// hours come from the parser by way of <see cref="EntryModel.Duration"/> rather than from the
    /// stored column, so the week total cannot disagree with the day totals it is made of.
    /// </summary>
    public async Task<double> GetWeekTotalAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        // DayOfWeek counts from Sunday, so shift by 6 to land the week on Monday.
        var weekStart = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(7);

        var entries = await dataContext.Entries
            .AsNoTracking()
            .Where(e => e.Date >= weekStart && e.Date < weekEnd)
            .Select(e => EntryModel.FromData(e))
            .ToListAsync(cancellationToken);

        return entries.Sum(e => e.Duration ?? 0);
    }

    public async Task<long> CreateEntryAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var entry = new Entry
        {
            Date = date,
        };

        dataContext.Entries.Add(entry);
        await dataContext.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }

    /// <summary>
    /// Writes the model back over the stored entry. Returns false when the entry is no longer there
    /// — another tab may have deleted it while this one still had the row on screen, and a save
    /// racing a delete should not be an error the user has to see.
    /// </summary>
    public async Task<bool> UpdateEntryAsync(EntryModel entryModel, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var entryData = await dataContext.Entries
            .FirstOrDefaultAsync(e => e.Id == entryModel.Id, cancellationToken);

        if (entryData is null)
        {
            return false;
        }

        entryData.Date = entryModel.Date;
        entryData.Time = entryModel.Time;
        entryData.Duration = entryModel.Duration;
        entryData.ProjectId = entryModel.ProjectId;
        entryData.TaskId = entryModel.TaskId;
        entryData.Description = entryModel.Description;
        entryData.SortOrder = entryModel.SortOrder;

        await dataContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task DeleteEntryAsync(long entryId, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var entryData = await dataContext.Entries
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        if (entryData is not null)
        {
            dataContext.Entries.Remove(entryData);
            await dataContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Rewrites any stored duration that disagrees with what the parser now makes of that entry's
    /// time text, and returns how many rows changed. The column is only ever a copy of a derived
    /// value, so rows last written by an older parser can hold hours that no longer follow from
    /// their time field — including negative ones from overnight ranges. Nothing recomputes them
    /// otherwise until that row's time is edited again. Idempotent: a second run finds nothing.
    /// </summary>
    public async Task<int> ReconcileDurationsAsync(CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var entries = await dataContext.Entries.ToListAsync(cancellationToken);
        var changed = 0;

        foreach (var entry in entries)
        {
            var duration = EntryModel.FromData(entry).Duration;

            if (entry.Duration.Equals(duration))
            {
                continue;
            }

            entry.Duration = duration;
            changed++;
        }

        if (changed > 0)
        {
            await dataContext.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    public async Task CopyFromPreviousDayAsync(CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

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
            .AsNoTracking()
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