using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Rubato.Components;

/// <summary>
/// Copies <see cref="Text"/> to the clipboard and shows a check for a couple of seconds. Reports a
/// failed write on the button itself rather than through the parent, so the whole interaction —
/// the interop call, the transient feedback, and the failure — is one component's business.
/// </summary>
public partial class CopyButton
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string? Text { get; set; }

    private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(2);

    private bool JustCopied { get; set; }

    private int CopyCount { get; set; }

    private string ButtonClass => Error is not null
        ? "btn-outline-danger"
        : JustCopied ? "btn-outline-success" : "btn-outline-secondary";

    private string IconClass => Error is not null
        ? "bi-clipboard-x"
        : JustCopied ? "bi-clipboard-check" : "bi-clipboard";

    private string ButtonTitle => Error ?? (JustCopied ? "Copied" : "Copy to clipboard");

    /// <summary>
    /// Hand-written rather than guarded: there is no busy state to hold for a clipboard write, and
    /// the failure has a message of its own rather than an exception to relay.
    /// </summary>
    private async Task CopyAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("copyToClipboard", CancellationToken, Text);
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
