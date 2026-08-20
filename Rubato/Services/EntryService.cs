using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Data.Models;
using Rubato.Models;

namespace Rubato.Services;

public class EntryService(IDbContextFactory<RubatoDataContext> dataContextFactory)
{
    /// <summary>
    /// The day's entries, in no particular order — <c>Day.OrderedEntries</c> is the single place that
    /// decides how rows are arranged, so imposing an order here would only be overwritten.
    /// </summary>
    public async Task<List<EntryModel>> GetEntriesAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        return await dataContext.Entries
            .AsNoTracking()
            .Where(e => e.Date == date)
            .Select(e => EntryModel.FromData(e))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Sums the hours worked over the Monday-start week containing <paramref name="date"/>, from the
    /// parser rather than the stored column so the week total cannot disagree with the day totals it
    /// is made of.
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

    /// <summary>
    /// Adds a copy of <paramref name="entryModel"/> as a new entry on the same day, with no time of its
    /// own and the next free sort order after the source's. The copy comes from the model the row is
    /// holding rather than a fresh read — every field saves as it changes, so that model is the row as
    /// the user sees it — and <see cref="EntryModel.ToData"/> leaves the id behind, so this adds a row
    /// rather than overwriting the one being cloned.
    /// </summary>
    public async Task CloneEntryAsync(EntryModel entryModel, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var entryData = entryModel.ToData();

        // A clone is a template for work still to be logged, not a second copy of hours already
        // worked: repeating the source row's times would double the day total the moment it is
        // cloned. Duration is only the denormalized copy of what the time text parses to, so it
        // clears alongside it and the two stay in agreement.
        entryData.Time = null;
        entryData.Duration = null;

        // The clone belongs directly after its source, so it takes the next sort order up — walking
        // past any that the day has already handed out, since duplicates would leave the two rows'
        // order down to the Time tiebreak and the clone has no time yet. An unnumbered source has no
        // number to count from and stays unnumbered, sorting last alongside its source.
        if (entryData.SortOrder is int sourceSortOrder)
        {
            var takenSortOrders = await dataContext.Entries
                .AsNoTracking()
                .Where(e => e.Date == entryData.Date && e.SortOrder != null)
                .Select(e => e.SortOrder!.Value)
                .ToHashSetAsync(cancellationToken);

            var sortOrder = sourceSortOrder;

            do
            {
                sortOrder++;
            }
            while (takenSortOrders.Contains(sortOrder));

            entryData.SortOrder = sortOrder;
        }

        dataContext.Entries.Add(entryData);
        await dataContext.SaveChangesAsync(cancellationToken);
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

    /// <summary>
    /// Copies every entry from the most recent earlier day that has any onto <paramref name="date"/>.
    /// The target day is a parameter rather than "today" so the method stands on its own — the caller
    /// is the one that decides which days may be copied onto.
    /// </summary>
    public async Task CopyFromPreviousDayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var previousDate = await dataContext.Entries
            .Where(e => e.Date < date)
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
            var copy = EntryModel.FromData(entry);
            copy.Date = date;

            dataContext.Entries.Add(copy.ToData());
        }

        await dataContext.SaveChangesAsync(cancellationToken);
    }
}