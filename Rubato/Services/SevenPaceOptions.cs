namespace Rubato.Services;

public class SevenPaceOptions(IConfiguration configuration)
{
    public bool IsEnabled { get; } = configuration.GetSection("7Pace").GetValue("Enabled", false);

    public SevenPaceSettings ReadSettings()
    {
        var section = configuration.GetSection("7Pace");

        return new SevenPaceSettings
        {
            ApiUrl = Required("ApiUrl"),
            ApiKey = Required("ApiKey"),
            UserId = Required("UserId"),
            MeetingActivityTypeId = Required("MeetingActivityTypeId"),
            DevelopmentActivityTypeId = Required("DevelopmentActivityTypeId"),
            DeploymentActivityTypeId = Required("DeploymentActivityTypeId")
        };

        string Required(string key)
        {
            var value = section[key];

            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException($"7Pace:{key} is not configured.")
                : value;
        }
    }

    public record SevenPaceSettings
    {
        public required string ApiUrl { get; init; }
        public required string ApiKey { get; init; }
        public required string UserId { get; init; }
        public required string MeetingActivityTypeId { get; init; }
        public required string DevelopmentActivityTypeId { get; init; }
        public required string DeploymentActivityTypeId { get; init; }
    }
}
