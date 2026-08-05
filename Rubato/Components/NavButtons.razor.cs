using Microsoft.AspNetCore.Components;

namespace Rubato.Components;

public partial class NavButtons
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter] public DateOnly Date { get; set; }

    private bool IsToday => Date == Clock.Today;

    private static string DayUrl(DateOnly date) => $"/day/{date:yyyy-MM-dd}";

    /// <summary>
    /// The date picker cannot be an anchor, so this is the one navigation that goes through the
    /// NavigationManager — the arrows and Today are plain links the router handles.
    /// </summary>
    private void GoToDate(ChangeEventArgs e)
    {
        if (DateOnly.TryParse(e.Value?.ToString(), out var newDate))
        {
            NavigationManager.NavigateTo(DayUrl(newDate));
        }
    }
}
