using SA = FunnyCodeAnalyzer.StaticAnalysis;
using System.Diagnostics;

namespace FunnyCodeAnalyzer;

internal sealed class RepositoryAnalyzer
{
    private readonly SA.RepositoryAnalyzer _inner = new();

    public AnalysisReport Analyze(CliOptions options)
    {
        var staticOptions = new SA.AnalyzerOptions(
            RepositoryPath: options.RepositoryPath,
            SourceControl: ParseSourceControl(options.SourceControl),
            TodoThreshold: options.TodoThreshold,
            MonsterLinesThreshold: options.MonsterLinesThreshold,
            LongMethodLinesThreshold: options.LongMethodLinesThreshold);

        var report = _inner.Analyze(staticOptions);
        var issueUserIdentifiers = ResolveIssueUserIdentifiers(report, options);

        var issues = report.Issues
            .Select(issue => new CodeIssue(
                Kind: Enum.Parse<IssueKind>(issue.Kind.ToString()),
                FilePath: issue.FilePath,
                Line: issue.Line,
                Description: issue.Description,
                Detail: issue.Detail,
                Severity: issue.Severity,
                UserIdentifier: ResolveIssueUserIdentifier(issueUserIdentifiers, issue.FilePath, options.SelfUser)))
            .ToArray();

        return new AnalysisReport(
            RepositoryPath: report.RepositoryPath,
            SourceControl: Enum.Parse<SourceControlKind>(report.SourceControl.ToString()),
            Issues: issues,
            WorkingFiles: report.WorkingFiles,
            ScanStart: report.ScanStart,
            ScanEnd: report.ScanEnd);
    }

    private static SA.SourceControlKind ParseSourceControl(SourceControlKind kind)
    {
        return Enum.Parse<SA.SourceControlKind>(kind.ToString());
    }

    private static IReadOnlyDictionary<string, string> ResolveIssueUserIdentifiers(SA.AnalysisReport report, CliOptions options)
    {
        var fallback = string.IsNullOrWhiteSpace(options.SelfUser) ? "default-user" : options.SelfUser;
        if (report.SourceControl == SA.SourceControlKind.Directory)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var byFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in report.Issues.Select(issue => issue.FilePath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var user = report.SourceControl switch
            {
                SA.SourceControlKind.Git => ResolveGitFileUser(report.RepositoryPath, file),
                SA.SourceControlKind.Svn => ResolveSvnFileUser(report.RepositoryPath, file),
                _ => null
            };

            byFile[file] = string.IsNullOrWhiteSpace(user) ? fallback : user!;
        }

        return byFile;
    }

    private static string ResolveIssueUserIdentifier(IReadOnlyDictionary<string, string> byFile, string filePath, string fallback)
    {
        if (byFile.TryGetValue(filePath, out var user) && !string.IsNullOrWhiteSpace(user))
        {
            return user;
        }

        return string.IsNullOrWhiteSpace(fallback) ? "default-user" : fallback;
    }

    private static string? ResolveGitFileUser(string repoRoot, string relativePath)
    {
        var result = RunProcess(repoRoot, "git", "log", "-1", "--pretty=format:%an", "--", relativePath);
        return result.ExitCode == 0 ? NormalizeUser(result.StandardOutput) : null;
    }

    private static string? ResolveSvnFileUser(string repoRoot, string relativePath)
    {
        var result = RunProcess(repoRoot, "svn", "info", "--show-item", "last-changed-author", relativePath);
        return result.ExitCode == 0 ? NormalizeUser(result.StandardOutput) : null;
    }

    private static ProcessResult RunProcess(string workingDirectory, string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ProcessResult(1, string.Empty, $"Failed to start process {fileName}.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output, error);
    }

    private static string? NormalizeUser(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
