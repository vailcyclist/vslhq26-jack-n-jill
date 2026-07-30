using System.Text.Json;

namespace FunnyCodeAnalyzer;

internal sealed class HumorApiClientConfig
{
    public string? BaseUrl { get; init; }

    public string DispatchReportPath { get; init; } = "/api/notifications/dispatch-report";

    public string EmailDetailsPath { get; init; } = "/api/notifications/email/details";

    public string TeamsDetailsPath { get; init; } = "/api/notifications/teams/details";

    public string? UrlTemplate { get; init; }

    public string? TextProperty { get; init; }

    public static HumorApiClientConfig Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new HumorApiClientConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<HumorApiClientConfig>(json, JsonOptions.Default) ?? new HumorApiClientConfig();
        }
        catch
        {
            return new HumorApiClientConfig();
        }
    }
}