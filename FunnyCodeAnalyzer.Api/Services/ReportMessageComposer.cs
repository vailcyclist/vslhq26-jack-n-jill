using FunnyCodeAnalyzer.Api.Models;

namespace FunnyCodeAnalyzer.Api.Services;

internal sealed class ReportMessageComposer
{
    public string BuildBaseMessage(ReportDispatchRequest request, HumorEngine? humorEngine = null)
    {
        var lines = new List<string>
        {
            $"Hello {request.RecipientName},",
            string.Empty,
            $"Repository: {request.Report.RepositoryPath}",
            $"Source control: {request.Report.SourceControl}",
            $"Working files scanned: {request.Report.WorkingFileCount}",
            $"Issues found: {request.Report.IssueCount}",
            string.Empty
        };

        if (request.Report.Issues.Count == 0)
        {
            lines.Add("No issues found in the current analysis.");
            return string.Join(Environment.NewLine, lines);
        }

        lines.Add("Findings:");
        foreach (var group in request.Report.Issues.GroupBy(issue => issue.Kind).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"- {group.Key}: {group.Count()} issue(s)");
        }

        lines.Add(string.Empty);
        lines.Add("Selected issues:");

        foreach (var issue in request.Report.Issues.Take(8))
        {
            var issueLine = $"- {issue.FilePath}:{issue.Line} [{issue.Severity}] {issue.Description} ({issue.Detail})";
            if (request.IncludeHumor && humorEngine is not null)
            {
                var humor = humorEngine.Generate(issue.Kind, request.HumorMode, request.HumorTopic);
                issueLine += $" Pun: {humor.Text}";
            }

            lines.Add(issueLine);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
