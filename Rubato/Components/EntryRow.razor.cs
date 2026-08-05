using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Rubato.Models;
using Rubato.Services;

namespace Rubato.Components;

public partial class EntryRow
{
    [Inject] private EntryService EntryService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public EntryModel Entry { get; set; } = new();
    [Parameter] public List<ProjectModel> Projects { get; set; } = [];
    [Parameter] public EventCallback OnEntryChanged { get; set; }

    private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(2);

    private bool JustCopied { get; set; }

    private int CopyCount { get; set; }

    private string ProjectSelectStyle
        => Entry.ProjectId.HasValue
            ? $"--bs-border-color: {Projects.FirstOrDefault(p => p.Id == Entry.ProjectId)?.Color}"
            : string.Empty;

    /// <summary>
    /// The height the time textarea needs to show every line the user has typed, at least one. A lone
    /// "\r" is not worth splitting on here — a browser sends "\n" or "\r\n", and the trailing "\r" the
    /// latter leaves behind does not change the count.
    /// </summary>
    private int TimeRows => Math.Max(Entry.Time?.Split('\n').Length ?? 1, 1);

    private string? InvalidTimeMessage
        => Entry.HasInvalidTime
            ? $"Not a time range, so it adds no hours: {string.Join(", ", Entry.InvalidTimeLines)}"
            : null;

    private Task SaveEntryAsync()
        => RunGuardedAsync(token => EntryService.UpdateEntryAsync(Entry, token), OnEntryChanged, "Not saved");

    private Task DeleteEntryAsync()
        => RunGuardedAsync(token => EntryService.DeleteEntryAsync(Entry.Id, token), OnEntryChanged, "Not deleted");

    /// <summary>
    /// Copies the entry and shows a check for a couple of seconds. Hand-written rather than guarded:
    /// there is no busy state to hold for a clipboard write, and the failure has a message of its own
    /// rather than an exception to relay.
    /// </summary>
    private async Task CopyToClipboardAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("copyToClipboard", CancellationToken, Entry.ClipboardText);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (JSException)
        {
            Error = "Could not copy to the clipboard.";
            StateHasChanged();
            return;
        }

        var copy = ++CopyCount;
        Error = null;
        JustCopied = true;
        StateHasChanged();

        try
        {
            await Task.Delay(CopiedFeedbackDuration, CancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // A later copy is now responsible for clearing the check
        if (copy != CopyCount)
        {
            return;
        }

        JustCopied = false;
        StateHasChanged();
    }
}
