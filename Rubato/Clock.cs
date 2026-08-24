namespace Rubato;

/// <summary>
/// Today's date, asked for in one place. The app is day-oriented and never needs the time of day, so
/// everything works in <see cref="DateOnly"/> and nothing reaches for <see cref="DateTime.Now"/>.
///
/// The zone is configured rather than inherited from the process. A container has no local zone unless
/// one is handed to it, so <see cref="DateTime.Today"/> there is UTC, and every "is this today?" — the
/// day the page opens on, the Today button, whether the previous day may be copied — rolled over hours
/// early for anyone west of UTC. Entries typed in the evening were written to tomorrow's date.
/// </summary>
public class Clock
{
    private readonly TimeZoneInfo timeZone;

    /// <summary>
    /// Reads the <c>TimeZone</c> setting: an IANA id such as <c>America/New_York</c>. An id this machine
    /// cannot resolve throws, and <c>Program.cs</c> resolves the clock at startup so that throw lands on
    /// launch — quietly standing in UTC for a typo is the bug this class exists to prevent. With nothing
    /// configured the process's own zone stands in, which is what <c>TZ</c> sets in the container.
    /// </summary>
    public Clock(IConfiguration configuration)
    {
        var configured = configuration.GetValue<string>("TimeZone");

        if (string.IsNullOrWhiteSpace(configured))
        {
            timeZone = TimeZoneInfo.Local;
            return;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(configured);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"The TimeZone setting '{configured}' is not a time zone this machine knows. " +
                "Use an IANA id such as America/New_York.",
                exception);
        }
    }

    /// <summary>
    /// Named so a launch can report which zone it settled on — an absent setting is then a line in the
    /// log rather than a date that is silently a day ahead.
    /// </summary>
    public string TimeZoneId => timeZone.Id;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
}
