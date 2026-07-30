namespace FunnyCodeAnalyzer.Api.Models;

internal sealed class UserIssueSummary
{
    public string IssueKind { get; set; } = string.Empty;

    public int Count { get; set; }

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; }
}

internal sealed class UserStatisticsResponse
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string? FilterIssueKind { get; set; }

    public int TotalIssueOccurrences { get; set; }

    public int DistinctIssueKinds { get; set; }

    public IReadOnlyList<UserIssueSummary> Issues { get; set; } = Array.Empty<UserIssueSummary>();
}
