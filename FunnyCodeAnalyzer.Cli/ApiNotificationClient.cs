using System.Text;
using System.Text.Json;

namespace FunnyCodeAnalyzer;

internal sealed class ApiNotificationClient
{
    private static readonly HttpClient HttpClient = new();

    public async Task<IReadOnlyList<MessageDraft>> GenerateMessageDetailsAsync(AnalysisReport report, CliOptions options)
    {
        var clientConfig = HumorApiClientConfig.Load(options.HumorApiConfigPath);
        var baseUrl = ResolveBaseUrl(options, clientConfig);

        var drafts = new List<MessageDraft>();
        var detailsRequest = BuildNotificationDetailsRequest(report, options);

        if (options.Channel is OutputChannel.Email or OutputChannel.Both)
        {
            var emailPath = string.IsNullOrWhiteSpace(clientConfig.EmailDetailsPath)
                ? "/api/notifications/email/details"
                : clientConfig.EmailDetailsPath;
            var emailUrl = BuildUrl(baseUrl, emailPath);
            var emailDetails = await PostJsonAsync<NotificationDetailsRequest, EmailMessageDetailsResponse>(emailUrl, detailsRequest);
            drafts.Add(new MessageDraft("Email", emailDetails.Subject, emailDetails.Body, "email-message.txt"));
        }

        if (options.Channel is OutputChannel.Teams or OutputChannel.Both)
        {
            var teamsPath = string.IsNullOrWhiteSpace(clientConfig.TeamsDetailsPath)
                ? "/api/notifications/teams/details"
                : clientConfig.TeamsDetailsPath;
            var teamsUrl = BuildUrl(baseUrl, teamsPath);
            var teamsDetails = await PostJsonAsync<NotificationDetailsRequest, TeamsMessageDetailsResponse>(teamsUrl, detailsRequest);
            drafts.Add(new MessageDraft("Teams", teamsDetails.Title, teamsDetails.Message, "teams-message.md"));
        }

        return drafts;
    }

    public async Task SendReportAsync(AnalysisReport report, CliOptions options)
    {
        var clientConfig = HumorApiClientConfig.Load(options.HumorApiConfigPath);
        var baseUrl = ResolveBaseUrl(options, clientConfig);

        var channel = options.Channel.ToString().ToLowerInvariant();
        var dispatchPath = string.IsNullOrWhiteSpace(clientConfig.DispatchReportPath)
            ? "/api/notifications/dispatch-report"
            : clientConfig.DispatchReportPath;
        var url = BuildUrl(baseUrl, dispatchPath);
        var userIdentifier = ResolvePrimaryUserIdentifier(report, options);

        var request = new ReportDispatchRequest
        {
            UserIdentifier = userIdentifier,
            Channel = channel,
            Report = new ReportSnapshot
            {
                RepositoryPath = report.RepositoryPath,
                SourceControl = report.SourceControl.ToString(),
                WorkingFileCount = report.WorkingFiles.Count,
                IssueCount = report.Issues.Count,
                Issues = report.Issues.Select(issue => new ReportIssueSnapshot
                {
                    Kind = issue.Kind.ToString(),
                    FilePath = issue.FilePath,
                    Line = issue.Line,
                    Description = issue.Description,
                    Detail = issue.Detail,
                    Severity = issue.Severity,
                    UserIdentifier = issue.UserIdentifier
                }).ToArray()
            },
            Email = options.Channel is OutputChannel.Email or OutputChannel.Both
                ? new DispatchEmailTarget
                {
                    ToEmail = options.ToEmail ?? string.Empty,
                    Subject = $"Funny Code Analyzer: {report.Issues.Count} issue(s)",
                    Body = null
                }
                : null,
            Teams = options.Channel is OutputChannel.Teams or OutputChannel.Both
                ? new DispatchTeamsTarget
                {
                    GraphAccessToken = options.GraphAccessToken ?? string.Empty,
                    TeamsChatId = options.TeamsChatId,
                    RecipientUserId = options.TeamsRecipientUserId,
                    SenderUserId = options.TeamsSenderUserId,
                    Message = null
                }
                : null
        };

        ValidateRequiredTargets(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions.Default), Encoding.UTF8, "application/json")
        };

        using var response = await HttpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"API dispatch failed: {(int)response.StatusCode} {response.ReasonPhrase} {payload}");
        }
    }

    private static NotificationDetailsRequest BuildNotificationDetailsRequest(
        AnalysisReport report,
        CliOptions options)
    {
        var userIdentifier = ResolvePrimaryUserIdentifier(report, options);

        return new NotificationDetailsRequest
        {
            UserIdentifier = userIdentifier,
            RepositoryPath = report.RepositoryPath,
            SourceControl = report.SourceControl.ToString(),
            Issues = report.Issues.Select(issue => new ReportIssueSnapshot
            {
                Kind = issue.Kind.ToString(),
                FilePath = issue.FilePath,
                Line = issue.Line,
                Description = issue.Description,
                Detail = issue.Detail,
                Severity = issue.Severity,
                UserIdentifier = issue.UserIdentifier
            }).ToArray()
        };
    }

    private static string ResolveBaseUrl(CliOptions options, HumorApiClientConfig clientConfig)
    {
        var baseUrl = options.ApiBaseUrl ?? clientConfig.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("API base URL is required. Use --api-base-url or HumorApiClientConfig.BaseUrl.");
        }

        return baseUrl;
    }

    private static string ResolvePrimaryUserIdentifier(AnalysisReport report, CliOptions options)
    {
        var fallback = string.IsNullOrWhiteSpace(options.SelfUser) ? "default-user" : options.SelfUser;

        if (report.SourceControl == SourceControlKind.Directory)
        {
            return fallback;
        }

        var dominant = report.Issues
            .Select(issue => issue.UserIdentifier)
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .GroupBy(identifier => identifier, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(dominant) ? fallback : dominant;
    }

    private static async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
        string url,
        TRequest requestBody)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions.Default), Encoding.UTF8, "application/json")
        };

        using var response = await HttpClient.SendAsync(httpRequest);
        var payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"API request failed: {(int)response.StatusCode} {response.ReasonPhrase} {payload}");
        }

        var result = JsonSerializer.Deserialize<TResponse>(payload, JsonOptions.Default);
        if (result is null)
        {
            throw new InvalidOperationException("API returned an empty response payload.");
        }

        return result;
    }

    private static void ValidateRequiredTargets(ReportDispatchRequest request)
    {
        if (request.Channel is "email" or "both")
        {
            if (request.Email is null || string.IsNullOrWhiteSpace(request.Email.ToEmail))
            {
                throw new InvalidOperationException("--to-email is required when sending email or both channels.");
            }
        }

        if (request.Channel is "teams" or "both")
        {
            if (request.Teams is null || string.IsNullOrWhiteSpace(request.Teams.GraphAccessToken))
            {
                throw new InvalidOperationException("--graph-access-token is required when sending teams or both channels.");
            }

            if (string.IsNullOrWhiteSpace(request.Teams.TeamsChatId) &&
                (string.IsNullOrWhiteSpace(request.Teams.RecipientUserId) || string.IsNullOrWhiteSpace(request.Teams.SenderUserId)))
            {
                throw new InvalidOperationException("Provide --teams-chat-id or both --teams-recipient-user-id and --teams-sender-user-id for Teams delivery.");
            }
        }
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        return trimmedBase + trimmedPath;
    }
}

internal sealed class ReportDispatchRequest
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string Channel { get; set; } = "teams";

    public ReportSnapshot Report { get; set; } = new();

    public DispatchEmailTarget? Email { get; set; }

    public DispatchTeamsTarget? Teams { get; set; }
}

internal sealed class ReportSnapshot
{
    public string RepositoryPath { get; set; } = string.Empty;

    public string SourceControl { get; set; } = "directory";

    public int WorkingFileCount { get; set; }

    public int IssueCount { get; set; }

    public IReadOnlyList<ReportIssueSnapshot> Issues { get; set; } = Array.Empty<ReportIssueSnapshot>();
}

internal sealed class ReportIssueSnapshot
{
    public string Kind { get; set; } = "General";

    public string FilePath { get; set; } = string.Empty;

    public int Line { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string Severity { get; set; } = "Low";

    public string UserIdentifier { get; set; } = string.Empty;
}

internal sealed class DispatchEmailTarget
{
    public string ToEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = "Funny Code Analyzer Report";

    public string? Body { get; set; }
}

internal sealed class DispatchTeamsTarget
{
    public string GraphAccessToken { get; set; } = string.Empty;

    public string? Message { get; set; }

    public string? TeamsChatId { get; set; }

    public string? RecipientUserId { get; set; }

    public string? SenderUserId { get; set; }
}

internal sealed class NotificationDetailsRequest
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string RepositoryPath { get; set; } = string.Empty;

    public string SourceControl { get; set; } = "directory";

    public IReadOnlyList<ReportIssueSnapshot> Issues { get; set; } = Array.Empty<ReportIssueSnapshot>();
}

internal sealed class EmailMessageDetailsResponse
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public int IssueCount { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? HumorAddon { get; set; }
}

internal sealed class TeamsMessageDetailsResponse
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public int IssueCount { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? HumorAddon { get; set; }
}