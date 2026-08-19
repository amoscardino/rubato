using Microsoft.AspNetCore.Components;
using Rubato.Services;

namespace Rubato.Components;

public partial class SevenPacePushButton
{
    private static readonly TimeSpan SuccessLifetime = TimeSpan.FromSeconds(5);

    [Inject] private SevenPaceService SevenPaceService { get; set; } = default!;

    [Parameter] public DateOnly Date { get; set; }

    /// <summary>
    /// Passed in rather than counted here, so the component does not repeat a query the page has already run.
    /// </summary>
    [Parameter] public bool HasEntries { get; set; }

    private int? Created { get; set; }
    private int? Deleted { get; set; }

    private bool IsPushing { get; set; }

    private bool CanPush => HasEntries && !IsPushing;

    private CancellationTokenSource? _dismissCancellation;

    private async Task PushAsync()
    {
        if (!CanPush)
        {
            return;
        }

        IsPushing = true;
        CancelPendingDismissal();
        Created = Deleted = null;

        await RunGuardedAsync(
            async token =>
            {
                try
                {
                    (Created, Deleted) = await SevenPaceService.PushDayAsync(Date, token);
                }
                finally
                {
                    IsPushing = false;
                }
            },
            errorPrefix: "Not pushed");

        if (Created is not null)
        {
            await DismissSuccessAfterDelayAsync();
        }
    }

    private async Task DismissSuccessAfterDelayAsync()
    {
        // Linked to the component's own token so disposal ends the wait rather than leaving a timer
        // holding a component that is already gone.
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        _dismissCancellation = cancellation;

        try
        {
            await Task.Delay(SuccessLifetime, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (_dismissCancellation == cancellation)
            {
                _dismissCancellation = null;
            }

            cancellation.Dispose();
        }

        Created = Deleted = null;
        await InvokeAsync(StateHasChanged);
    }

    private void CancelPendingDismissal()
    {
        _dismissCancellation?.Cancel();
        _dismissCancellation = null;
    }
}
