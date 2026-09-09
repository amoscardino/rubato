using Microsoft.AspNetCore.Components;
using Rubato.Models;
using Rubato.Services;

namespace Rubato.Components;

public partial class EntryRow
{
    [Inject] private EntryService EntryService { get; set; } = default!;

    [Parameter] public EntryModel Entry { get; set; } = new();
    [Parameter] public List<ProjectModel> Projects { get; set; } = [];
    [Parameter] public EventCallback OnEntryChanged { get; set; }

    private string ProjectSelectStyle
        => Entry.ProjectId.HasValue
            ? $"--bs-border-color: {Projects.FirstOrDefault(p => p.Id == Entry.ProjectId)?.Color}"
            : string.Empty;

    private string? InvalidTimeMessage
        => Entry.HasInvalidTime
            ? $"Not a time range, so it adds no hours: {string.Join(", ", Entry.InvalidTimeLines)}"
            : null;

    private Task SaveEntryAsync()
        => RunGuardedAsync(token => EntryService.UpdateEntryAsync(Entry, token), OnEntryChanged, "Not saved");

    private Task CloneEntryAsync()
        => RunGuardedAsync(token => EntryService.CloneEntryAsync(Entry, token), OnEntryChanged, "Not cloned");

    private Task DeleteEntryAsync()
        => RunGuardedAsync(token => EntryService.DeleteEntryAsync(Entry.Id, token), OnEntryChanged, "Not deleted");
}
