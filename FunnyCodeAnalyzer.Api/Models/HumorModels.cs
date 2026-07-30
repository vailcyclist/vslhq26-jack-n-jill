namespace FunnyCodeAnalyzer.Api.Models;

internal sealed class HumorRecord
{
    public long Id { get; set; }

    public string UserIdentifier { get; set; } = string.Empty;

    public string IssueTopic { get; set; } = string.Empty;

    public string HumorMode { get; set; } = "light";

    public string HumorText { get; set; } = string.Empty;

    public string Channel { get; set; } = "humor";

    public string Recipient { get; set; } = string.Empty;

    public string Source { get; set; } = "api-local";

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class HumorRequest
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string IssueTopic { get; set; } = string.Empty;

    public string HumorMode { get; set; } = "light";

    public string? Topic { get; set; }
}

internal sealed class HumorResponse
{
    public string IssueTopic { get; set; } = string.Empty;

    public string HumorMode { get; set; } = "light";

    public string Topic { get; set; } = "general";

    public int IssueEncounterCount { get; set; }

    public string Text { get; set; } = string.Empty;
}

internal sealed class HumorPolicyResponse
{
    public string RequestedIssueTopic { get; set; } = string.Empty;

    public string RequestedHumorMode { get; set; } = string.Empty;

    public string RequestedTopic { get; set; } = string.Empty;

    public string RequestedChannel { get; set; } = string.Empty;

    public string ResolvedIssueType { get; set; } = string.Empty;

    public string ResolvedHumorLevel { get; set; } = string.Empty;

    public string ResolvedTopic { get; set; } = string.Empty;

    public string ResolvedChannel { get; set; } = string.Empty;

    public int MaxSpice { get; set; }

    public bool AllowSarcasm { get; set; }

    public bool AllowMildInsults { get; set; }

    public bool AllowProfanity { get; set; }

    public bool IssueFound { get; set; }

    public string[] CandidateTemplateIds { get; set; } = Array.Empty<string>();

    public int CandidateTemplateCount { get; set; }
}