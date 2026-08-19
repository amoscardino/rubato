using Microsoft.AspNetCore.Components;
using Rubato.Services;

namespace Rubato.Components;

/// <summary>
/// Pushes a day's entries to 7Pace and reports what the push replaced and created.
/// </summary>
public partial class SevenPacePushButton
{
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

    private async Task PushAsync()
    {
        if (!CanPush)
        {
            return;
        }

        IsPushing = true;
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
    }
}
