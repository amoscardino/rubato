namespace Rubato;

/// <summary>
/// Today's date, asked for in one place. The app is day-oriented and never needs the time of day, so
/// everything works in <see cref="DateOnly"/> and nothing reaches for <see cref="DateTime.Now"/>.
/// </summary>
public static class Clock
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
