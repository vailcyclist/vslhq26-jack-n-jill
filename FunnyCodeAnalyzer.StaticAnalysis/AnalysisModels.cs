namespace FunnyCodeAnalyzer.StaticAnalysis;

public enum SourceControlKind
{
    Auto,
    Git,
    Svn,
    Directory
}

public enum IssueKind
{
    TodoComment,
    CommentedOutCode,
    MonsterClass,
    LongMethod,
    EmptyCatch,
    LargeFile,
    IssueSuppression
}

public sealed record AnalyzerOptions(
    string RepositoryPath,
    SourceControlKind SourceControl,
    int TodoThreshold,
    int MonsterLinesThreshold,
    int LongMethodLinesThreshold);

public sealed record CodeIssue(
    IssueKind Kind,
    string FilePath,
    int Line,
    string Description,
    string Detail,
    string Severity);

public sealed record AnalysisReport(
    string RepositoryPath,
    SourceControlKind SourceControl,
    IReadOnlyList<CodeIssue> Issues,
    IReadOnlyList<string> WorkingFiles,
    DateTimeOffset ScanStart,
    DateTimeOffset ScanEnd)
{
    public string FormatSummary()
    {
        var issueGroups = Issues
            .GroupBy(issue => issue.Kind)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}: {group.Count()}")
            .ToArray();

        var summaryLines = new List<string>
        {
            $"Repository: {RepositoryPath}",
            $"Source control: {SourceControl}",
            $"Scan window: {ScanStart:yyyy-MM-dd HH:mm:ss} -> {ScanEnd:yyyy-MM-dd HH:mm:ss}",
            $"Working files scanned: {WorkingFiles.Count}",
            $"Issues found: {Issues.Count}"
        };

        if (issueGroups.Length > 0)
        {
            summaryLines.Add($"Breakdown: {string.Join(", ", issueGroups)}");
        }

        if (Issues.Count > 0)
        {
            summaryLines.Add("Top findings:");
            foreach (var issue in Issues.Take(5))
            {
                summaryLines.Add($"  - {issue.Kind} at {issue.FilePath}:{issue.Line} - {issue.Description}");
            }
        }

        return string.Join(Environment.NewLine, summaryLines);
    }
}
