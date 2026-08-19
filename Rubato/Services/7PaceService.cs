using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Rubato.Data;
using Rubato.Models;

namespace Rubato.Services;

public class SevenPaceService(IDbContextFactory<RubatoDataContext> dataContextFactory, IConfiguration configuration, HttpClient httpClient)
{
    public async Task<int> PushDayAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var settings = ReadSettings();
        var entries = await GetEntriesAsync(day, cancellationToken);

        // Unreadable time lines are the one thing that must stop the push. The parser deliberately never
        // counts them as zero hours (see EntryModel.ParseTime), so pushing anyway would quietly under-report
        // the day in 7Pace, where the shortfall is far harder to spot than the red field that flagged it here.
        var invalidLines = entries
            .SelectMany(e => e.Entry.InvalidTimeLines)
            .ToList();

        if (invalidLines.Count > 0)
        {
            throw new InvalidOperationException(
                $"{day:yyyy-MM-dd} has {invalidLines.Count} time value(s) that could not be read: " +
                $"{string.Join(", ", invalidLines)}. Fix them before pushing.");
        }

        var workLogs = BuildWorkLogs(entries, settings);
        var created = 0;
        List<string> failures = [];

        foreach (var workLog in workLogs)
        {
            try
            {
                await PostWorkLogAsync(workLog.Request, settings, cancellationToken);
                created++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add($"{workLog.Label} ({exception.Message})");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException($"Pushed {created} of {workLogs.Count} worklog(s). Failed: {string.Join("; ", failures)}");
        }

        return created;
    }

    /// <summary>
    /// The day's entries paired with their project's work item ID, in the same order the day is shown in
    /// (<c>Day.OrderedEntries</c>): by sort order with unnumbered rows last, then by time. Order matters here
    /// because it decides which entry a sort order 0 row's time rolls into.
    /// </summary>
    private async Task<List<PushableEntry>> GetEntriesAsync(DateOnly day, CancellationToken cancellationToken)
    {
        await using var dataContext = await dataContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await dataContext.Entries
            .AsNoTracking()
            .Where(e => e.Date == day)
            .Select(e => new
            {
                Entry = e,
                WorkItemId = e.Project == null ? null : e.Project.WorkItemId
            })
            .ToListAsync(cancellationToken);

        return [.. rows
            .Select(r => new PushableEntry(EntryModel.FromData(r.Entry), r.WorkItemId))
            .OrderBy(e => e.Entry.SortOrder.GetValueOrDefault(int.MaxValue))
            .ThenBy(e => e.Entry.Time)];
    }

    /// <summary>
    /// Turns the day's entries into the worklogs to post, applying the three rules that decide what 7Pace
    /// sees: sort order 0 rows are not sent but their hours are, an entry without a linked work item ID is
    /// skipped, and sort order picks the activity type.
    /// </summary>
    private static List<PendingWorkLog> BuildWorkLogs(List<PushableEntry> entries, SevenPaceSettings settings)
    {
        // Sort order 0 is the day's unassigned time — it has no work item of its own, so it is folded into
        // another entry rather than sent.
        var rolledHours = entries
            .Where(e => e.Entry.SortOrder == 0)
            .Sum(e => e.Entry.Duration ?? 0);

        var sendable = entries
            .Where(e => e.Entry.SortOrder != 0 && !string.IsNullOrWhiteSpace(e.WorkItemId))
            .ToList();

        if (rolledHours > 0 && sendable.Count == 0)
        {
            throw new InvalidOperationException($"{rolledHours:0.##} hour(s) of unnumbered time has nowhere to go — the day has no entry with a linked work item ID to roll it into.");
        }

        // Unnumbered rows sort last, so GetValueOrDefault(int.MaxValue) reads them as development, the same
        // reading the day's own ordering gives them. Where no development entry exists, the hours go to the
        // first entry that is being sent at all rather than being dropped.
        var rollTarget = sendable.FirstOrDefault(e => e.Entry.SortOrder.GetValueOrDefault(int.MaxValue) >= 10)
            ?? sendable.FirstOrDefault();

        List<PendingWorkLog> workLogs = [];

        foreach (var entry in sendable)
        {
            var hours = entry.Entry.Duration ?? 0;

            if (ReferenceEquals(entry, rollTarget))
            {
                hours += rolledHours;
            }

            var seconds = (int)Math.Round(hours * 3600, MidpointRounding.AwayFromZero);

            if (seconds <= 0)
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(entry.Entry.Label)
                ? $"entry {entry.Entry.Id}"
                : entry.Entry.Label;

            if (!int.TryParse(entry.WorkItemId, out var workItemId))
            {
                throw new InvalidOperationException($"'{entry.WorkItemId}' is not a valid work item ID (on the project behind {label}).");
            }

            workLogs.Add(new PendingWorkLog(
                label,
                new WorkLogRequest(
                    entry.Entry.Date.ToDateTime(TimeOnly.MinValue).ToString("s"),
                    seconds,
                    seconds,
                    workItemId,
                    entry.Entry.Label,
                    settings.UserId,
                    ActivityTypeIdFor(entry.Entry, settings))));
        }

        return workLogs;
    }

    /// <summary>
    /// Sort order decides the activity type — 1-9 are meetings, 10 and up (and unnumbered rows, which sort
    /// there too) are development — except that a description mentioning a deployment is a deployment
    /// whichever band it sits in.
    /// </summary>
    private static string ActivityTypeIdFor(EntryModel entry, SevenPaceSettings settings)
    {
        if (entry.Description?.Contains("deployment", StringComparison.OrdinalIgnoreCase) ?? false)
        {
            return settings.DeploymentActivityTypeId;
        }

        return entry.SortOrder.GetValueOrDefault(int.MaxValue) < 10
            ? settings.MeetingActivityTypeId
            : settings.DevelopmentActivityTypeId;
    }

    private async Task PostWorkLogAsync(WorkLogRequest workLog, SevenPaceSettings settings, CancellationToken cancellationToken)
    {
        var url = $"{settings.ApiUrl.TrimEnd('/')}/api/rest/workLogs?api-version={Uri.EscapeDataString(settings.ApiVersion)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(workLog)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException($"7Pace returned {(int)response.StatusCode}: {DescribeError(body) ?? response.ReasonPhrase}");
        }
    }

    /// <summary>
    /// Pulls 7Pace's own description out of an error response.
    /// </summary>
    private static string? DescribeError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            var root = JsonDocument.Parse(body).RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                root = error;
            }

            return root.TryGetProperty("errorDescription", out var description)
                ? description.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private SevenPaceSettings ReadSettings()
    {
        var section = configuration.GetSection("7Pace");

        return new SevenPaceSettings(
            Required("ApiUrl"),
            Required("ApiKey"),
            Required("ApiVersion"),
            Required("UserId"),
            Required("MeetingActivityTypeId"),
            Required("DevelopmentActivityTypeId"),
            Required("DeploymentActivityTypeId"));

        string Required(string key)
        {
            var value = section[key];

            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException($"7Pace:{key} is not configured.")
                : value;
        }
    }

    private sealed record PushableEntry(EntryModel Entry, string? WorkItemId);

    private sealed record PendingWorkLog(string Label, WorkLogRequest Request);

    private sealed record WorkLogRequest(
        [property: JsonPropertyName("timeStamp")] string TimeStamp,
        [property: JsonPropertyName("length")] int Length,
        [property: JsonPropertyName("billableLength")] int BillableLength,
        [property: JsonPropertyName("workItemId")] int WorkItemId,
        [property: JsonPropertyName("comment")] string Comment,
        [property: JsonPropertyName("userId")] string UserId,
        [property: JsonPropertyName("activityTypeId")] string ActivityTypeId);

    private sealed record SevenPaceSettings(
        string ApiUrl,
        string ApiKey,
        string ApiVersion,
        string UserId,
        string MeetingActivityTypeId,
        string DevelopmentActivityTypeId,
        string DeploymentActivityTypeId);
}
