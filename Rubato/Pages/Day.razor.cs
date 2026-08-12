using Microsoft.AspNetCore.Components;
using Rubato.Models;
using Rubato.Services;

namespace Rubato.Pages;

public partial class Day
{
    [Inject] private EntryService EntryService { get; set; } = default!;
    [Inject] private ProjectService ProjectService { get; set; } = default!;

    [Parameter] public DateTime? DateParam { get; set; }

    private DateOnly Date => DateParam is null ? Clock.Today : DateOnly.FromDateTime(DateParam.Value);

    private bool IsToday => Date == Clock.Today;

    private List<EntryModel> Entries { get; set; } = [];

    private List<ProjectModel> Projects { get; set; } = [];

    private double WeekTotalHours { get; set; }

    private double TotalHours => Entries.Sum(e => e.Duration ?? 0);

    private bool HasInvalidTimes => Entries.Any(e => e.HasInvalidTime);

    /// <summary>
    /// The order the rows are shown in: by sort order, unnumbered rows last, then by time. This is the
    /// only place row order is decided — <see cref="EntryService.GetEntriesAsync"/> deliberately does
    /// not impose one.
    /// </summary>
    private IEnumerable<EntryModel> OrderedEntries
        => Entries.OrderBy(e => e.SortOrder.GetValueOrDefault(int.MaxValue)).ThenBy(e => e.Time);

    private bool CanCopy => IsToday && Entries.Count == 0 && !IsBusy;

    /// <summary>
    /// Projects are fetched once for the lifetime of the page rather than on every parameter set —
    /// they do not change as you page from one day to the next.
    /// </summary>
    protected override Task OnInitializedAsync()
        => RunGuardedAsync(
            async token => Projects = await ProjectService.GetProjectsAsync(token),
            errorPrefix: "Could not load projects");

    /// <summary>
    /// Runs on every navigation, including from one day to the next, so the loader goes back up while
    /// the new day is fetched.
    /// </summary>
    protected override Task OnParametersSetAsync()
        => RunInitialLoadAsync(LoadDayAsync, errorPrefix: "Could not load this day");

    /// <summary>
    /// Refetches the day in place, for when a row has just saved. Deliberately not
    /// <see cref="CancellableComponentBase.RunInitialLoadAsync"/>: the page is already on screen and
    /// replacing it with the loader on every field edit would make the table flicker.
    /// </summary>
    private Task ReloadDayAsync()
        => RunGuardedAsync(LoadDayAsync, errorPrefix: "Could not load this day");

    private async Task LoadDayAsync(CancellationToken cancellationToken)
    {
        Entries = await EntryService.GetEntriesAsync(Date, cancellationToken);
        WeekTotalHours = await EntryService.GetWeekTotalAsync(Date, cancellationToken);
    }

    private Task AddEntryAsync()
        => RunGuardedAsync(
            async token =>
            {
                var entryId = await EntryService.CreateEntryAsync(Date, token);

                Entries.Add(new EntryModel
                {
                    Id = entryId,
                    Date = Date
                });
            },
            errorPrefix: "Could not add an entry");

    private Task CopyEntriesAsync()
    {
        // The entry count CanCopy depends on does not change until the copy finishes, and a `disabled`
        // attribute does not stop clicks queued before it rendered, so re-check here — without this a
        // double-click copied the previous day twice.
        if (!CanCopy)
        {
            return Task.CompletedTask;
        }

        return RunGuardedAsync(
            async token =>
            {
                await EntryService.CopyFromPreviousDayAsync(Date, token);
                await LoadDayAsync(token);
            },
            errorPrefix: "Could not copy the previous day");
    }
}
