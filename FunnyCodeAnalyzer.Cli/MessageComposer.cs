namespace FunnyCodeAnalyzer;

internal sealed class MessageComposer
{
    private readonly FunnyMessageConfig _config;

    public MessageComposer(FunnyMessageConfig config)
    {
        _config = config;
    }

    public IReadOnlyList<MessageDraft> Compose(AnalysisReport report, CliOptions options)
    {
        var drafts = new List<MessageDraft>();

        if (options.Channel is OutputChannel.Email or OutputChannel.Both)
        {
            drafts.Add(BuildDraft("Email", _config.Email, report, options, "email-draft.md"));
        }

        if (options.Channel is OutputChannel.Teams or OutputChannel.Both)
        {
            drafts.Add(BuildDraft("Teams", _config.Teams, report, options, "teams-message.md"));
        }

        return drafts;
    }

    private static MessageDraft BuildDraft(string kind, MessageTemplateSet templates, AnalysisReport report, CliOptions options, string fileName)
    {
        var issueCount = report.Issues.Count;
        var subject = ApplyTemplate(PickTemplate(templates.SubjectTemplates, $"{kind} report: {issueCount} issue(s)"), report, options);
        var opening = ApplyTemplate(PickTemplate(templates.OpeningTemplates, $"Hello {options.SelfUser},"), report, options);
        var lead = ApplyTemplate(PickTemplate(templates.BodyLeadTemplates, "The analyzer found a few items worth reviewing."), report, options);
        var findings = BuildFindings(report);
        var closer = ApplyTemplate(PickTemplate(templates.ClosingTemplates, "Regards,\nFunny Code Analyzer"), report, options);

        var bodyLines = new List<string>
        {
            opening,
            string.Empty,
            lead,
            string.Empty
        };

        if (report.Issues.Count > 0)
        {
            bodyLines.AddRange(findings);
        }
        else
        {
            bodyLines.Add("No issues were found in the analyzed working files.");
        }

        bodyLines.Add(string.Empty);
        bodyLines.Add(closer);

        var body = string.Join(Environment.NewLine, bodyLines.Where(line => line is not null));
        return new MessageDraft(kind, subject, body, fileName);
    }

    private static string[] BuildFindings(AnalysisReport report)
    {
        var lines = new List<string>
        {
            "Findings:"
        };

        foreach (var grouping in report.Issues.GroupBy(issue => issue.Kind).OrderBy(group => group.Key))
        {
            var count = grouping.Count();
            var sample = grouping.First();
            lines.Add($"- {grouping.Key}: {count} issue(s). Sample: {sample.FilePath}:{sample.Line} - {sample.Description}");
        }

        lines.Add(string.Empty);
        lines.Add("Selected issues:");

        foreach (var issue in report.Issues.Take(8))
        {
            lines.Add($"- {issue.FilePath}:{issue.Line} [{issue.Severity}] {issue.Description} ({issue.Detail})");
        }

        return lines.ToArray();
    }

    private static string PickTemplate(string[] templates, string fallback)
    {
        if (templates.Length == 0)
        {
            return fallback;
        }

        return templates[Random.Shared.Next(templates.Length)];
    }

    private static string ApplyTemplate(string template, AnalysisReport report, CliOptions options)
    {
        return template
            .Replace("{RecipientName}", options.SelfUser, StringComparison.OrdinalIgnoreCase)
            .Replace("{SelfUser}", options.SelfUser, StringComparison.OrdinalIgnoreCase)
            .Replace("{HumorMode}", options.HumorMode.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{HumorSource}", options.HumorSource.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{CommitCount}", report.WorkingFiles.Count.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{FileCount}", report.WorkingFiles.Count.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{WorkingFileCount}", report.WorkingFiles.Count.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{SourceControl}", report.SourceControl.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{IssueCount}", report.Issues.Count.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
