using Microsoft.AspNetCore.Components;
using Rubato.Services;

namespace Rubato.Components;

/// <summary>
/// Pushes a day's entries to 7Pace and reports how many worklogs it created. It takes plain values and
/// keeps the rest to itself — the service call, the in-flight state, the count, and the failure — so the
/// day page hands it a date and reads nothing back: a push changes nothing the page derives, so there is
/// no callback to relay.
/// <para>
/// Give it <c>@key="Date"</c> so a new day is a new instance. That is what clears a stale count, and it
/// cancels a push still in flight for the day being navigated away from rather than letting it land its
/// success message on a day it does not describe.
/// </para>
/// </summary>
public partial class SevenPacePushButton
{
    [Inject] private SevenPaceService SevenPaceService { get; set; } = default!;

    [Parameter] public DateOnly Date { get; set; }

    /// <summary>
    /// Whether the day has anything worth pushing. Passed in rather than counted here, so the component
    /// does not repeat a query the page has already run.
    /// </summary>
    [Parameter] public bool HasEntries { get; set; }

    private int? PushedWorkLogs { get; set; }

    /// <summary>
    /// Its own flag rather than <see cref="CancellableComponentBase.IsBusy"/>, which
    /// <see cref="CancellableComponentBase.RunGuardedAsync"/> deliberately does not use to block
    /// re-entrancy. A push is the one action here that a second, overlapping run would duplicate in
    /// 7Pace, where the extra worklogs then have to be deleted by hand.
    /// </summary>
    private bool IsPushing { get; set; }

    private bool CanPush => HasEntries && !IsPushing;

    private Task PushAsync()
    {
        // Nothing a push changes is visible to CanPush, and a `disabled` attribute does not stop clicks
        // queued before it rendered, so the flag has to be tested and set here — a second run would post
        // the whole day to 7Pace again.
        if (!CanPush)
        {
            return Task.CompletedTask;
        }

        IsPushing = true;
        PushedWorkLogs = null;

        // Cleared inside the guarded work rather than around it, the way RunInitialLoadAsync handles
        // IsLoading, so the re-enabled button is part of the render RunGuardedAsync already does at the
        // end — and so a cancelled push, where the component is going away, still does not force one.
        return RunGuardedAsync(
            async token =>
            {
                try
                {
                    PushedWorkLogs = await SevenPaceService.PushDayAsync(Date, token);
                }
                finally
                {
                    IsPushing = false;
                }
            },
            errorPrefix: "Not pushed");
    }
}
