namespace FunnyCodeAnalyzer.Api.Models;

internal sealed class EmailNotificationRequest
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string ToEmail { get; set; } = string.Empty;

    public string? Cc { get; set; }

    public string? Bcc { get; set; }

    public string Subject { get; set; } = "Funny Code Analyzer Report";

    public string Body { get; set; } = string.Empty;

    public bool IsHtml { get; set; }

    public bool IncludeHumor { get; set; } = true;

    public string? IssueTopic { get; set; }

    public string HumorMode { get; set; } = "light";

    public string? FromEmail { get; set; }

    public string? FromDisplayName { get; set; }
}

internal sealed class TeamsNotificationRequest
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string GraphAccessToken { get; set; } = string.Empty;

    public bool IncludeHumor { get; set; } = true;

    public string HumorMode { get; set; } = "light";

    public string? IssueTopic { get; set; }

    public string? TeamsChatId { get; set; }

    public string? RecipientUserId { get; set; }

    public string? SenderUserId { get; set; }
}

internal sealed class OutgoingEmailMessage
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string ToEmail { get; set; } = string.Empty;

    public string? Cc { get; set; }

    public string? Bcc { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsHtml { get; set; }

    public string? FromEmail { get; set; }

    public string? FromDisplayName { get; set; }
}

internal sealed class OutgoingTeamsMessage
{
    public string GraphAccessToken { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? TeamsChatId { get; set; }

    public string? RecipientUserId { get; set; }

    public string? SenderUserId { get; set; }
}

internal sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DefaultFromEmail { get; set; } = string.Empty;

    public string DefaultFromName { get; set; } = "Funny Code Analyzer";
}

internal sealed class TeamsOptions
{
    public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
}

internal sealed class ReportDispatchRequest
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string Channel { get; set; } = "teams";

    public string RecipientName { get; set; } = string.Empty;

    public string HumorMode { get; set; } = "light";

    public string HumorTopic { get; set; } = "general";

    public bool IncludeHumor { get; set; } = true;

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

    public string? Cc { get; set; }

    public string? Bcc { get; set; }

    public string Subject { get; set; } = "Funny Code Analyzer Report";

    public string? Body { get; set; }

    public bool IsHtml { get; set; }

    public string? FromEmail { get; set; }

    public string? FromDisplayName { get; set; }
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

    public string RecipientName { get; set; } = "there";

    public int IssueCount { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? HumorAddon { get; set; }
}

internal sealed class TeamsMessageDetailsResponse
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string RecipientName { get; set; } = "there";

    public int IssueCount { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? HumorAddon { get; set; }
}