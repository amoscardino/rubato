using Microsoft.AspNetCore.Components;

namespace Rubato.Components.Shared;

public abstract class CancellableComponentBase : ComponentBase, IDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;

    protected CancellationToken CancellationToken => (_cancellationTokenSource ??= new()).Token;

    /// <summary>
    /// True while <see cref="RunGuardedAsync"/> has work in flight, for disabling whatever started it.
    /// A <c>disabled</c> attribute is only a client-side hint and clicks queued before it rendered
    /// still arrive, so a handler whose work is not idempotent has to re-check its own preconditions
    /// rather than trust this.
    /// </summary>
    protected bool IsBusy { get; private set; }

    /// <summary>
    /// What to tell the user about the last failed operation, or null. Settable, so a handler that
    /// fails without throwing reports in the same place the guarded ones do.
    /// </summary>
    protected string? Error { get; set; }

    /// <summary>
    /// Runs <paramref name="work"/> with the component's cancellation token, holding
    /// <see cref="IsBusy"/> for the duration and turning a failure into <see cref="Error"/> rather
    /// than an exception. An exception escaping an event handler takes the whole Blazor circuit down,
    /// so every handler that awaits goes through here — including the lifecycle loads, where an
    /// unhandled failure would otherwise replace the page with a dead circuit.
    /// <para>
    /// A cancellation means the component is being disposed: there is nothing left to re-render and
    /// nobody left to notify, so it returns silently. The busy flag is still cleared, since leaving
    /// it set is what once permanently disabled a row's delete button.
    /// </para>
    /// <para>
    /// Re-entrancy is deliberately not blocked — two field edits in quick succession are two real
    /// saves, and dropping the second would lose an edit.
    /// </para>
    /// </summary>
    /// <param name="onSuccess">Invoked only when the work actually succeeded.</param>
    /// <param name="errorPrefix">Prepended to the exception message, e.g. "Not saved".</param>
    protected async Task RunGuardedAsync(Func<CancellationToken, Task> work, EventCallback onSuccess = default, string? errorPrefix = null)
    {
        IsBusy = true;
        Error = null;
        StateHasChanged();

        try
        {
            await work(CancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Error = errorPrefix is null ? ex.Message : $"{errorPrefix}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        StateHasChanged();

        if (Error is null && onSuccess.HasDelegate)
        {
            await onSuccess.InvokeAsync();
        }
    }

    public virtual void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
}
