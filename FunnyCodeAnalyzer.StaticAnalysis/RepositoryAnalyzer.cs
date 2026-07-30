using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FunnyCodeAnalyzer.StaticAnalysis;

public sealed class RepositoryAnalyzer
{
    private static readonly Regex TodoPattern = new(@"\b(TODO|FIXME|XXX|HACK)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CommentedOutCodePattern = new(@"^(?:\s*//|\s*/\*)", RegexOptions.Compiled);
    private static readonly Regex SuppressMessagePattern = new(@"\bSuppressMessage\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PragmaWarningDisablePattern = new(@"^\s*#\s*pragma\s+warning\s+disable\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NoWarnPattern = new(@"<\s*NoWarn\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EditorConfigSeverityNonePattern = new(@"^\s*dotnet_diagnostic\..*\.severity\s*=\s*none\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SuppressMessageJustificationPattern = new(@"\bJustification\s*=\s*""[^""]+""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TypePattern = new(@"^\s*(?:public|internal|private|protected|static|partial|abstract|sealed|record|new|unsafe|readonly|ref|file|virtual|override|async|extern|\s)*\b(class|record|struct|interface)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex MethodPattern = new(@"^\s*(?:public|internal|private|protected|static|partial|abstract|sealed|virtual|override|async|extern|new|unsafe|readonly|ref|sealed|\s)+[A-Za-z0-9_<>,\[\]\?\.]+\s+[A-Za-z_][A-Za-z0-9_]*\s*\([^;]*\)\s*(?:where\b.*)?\s*(\{|=>)?\s*$", RegexOptions.Compiled);
    private static readonly Regex CatchPattern = new(@"\bcatch\s*\(\s*Exception(?:\s+[A-Za-z_][A-Za-z0-9_]*)?\s*\)", RegexOptions.Compiled);

    public AnalysisReport Analyze(AnalyzerOptions options)
    {
        var workingDirectory = Path.GetFullPath(options.RepositoryPath);
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"Repository path does not exist: {workingDirectory}");
        }

        var sourceControl = DetectSourceControl(workingDirectory, options.SourceControl);
        var repoRoot = ResolveRepositoryRoot(workingDirectory, sourceControl);
        var scanStart = DateTimeOffset.Now;
        var changedFiles = GetWorkingTreeFiles(repoRoot, sourceControl);
        var scanEnd = DateTimeOffset.Now;

        var issues = new List<CodeIssue>();

        foreach (var relativePath in changedFiles)
        {
            var fullPath = Path.GetFullPath(Path.Combine(repoRoot, relativePath));
            if (!File.Exists(fullPath))
            {
                continue;
            }

            if (!LooksLikeSourceFile(fullPath))
            {
                continue;
            }

            issues.AddRange(AnalyzeFile(fullPath, relativePath, options));
        }

        return new AnalysisReport(
            repoRoot,
            sourceControl,
            issues.OrderBy(issue => issue.FilePath).ThenBy(issue => issue.Line).ToArray(),
            changedFiles,
            scanStart,
            scanEnd);
    }

    private static IReadOnlyList<CodeIssue> AnalyzeFile(string fullPath, string relativePath, AnalyzerOptions options)
    {
        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var isCodeFile = extension is ".cs" or ".razor" or ".xaml";
        var lines = File.ReadAllLines(fullPath);
        var issues = new List<CodeIssue>();

        if (lines.Length > 600)
        {
            issues.Add(new CodeIssue(
                IssueKind.LargeFile,
                relativePath,
                1,
                "The file is large enough to attract its own zip code.",
                $"File length: {lines.Length} lines.",
                "Medium"));
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            var suppressionIssue = TryCreateIssueSuppressionIssue(line, extension, relativePath, index + 1);
            if (suppressionIssue is not null)
            {
                issues.Add(suppressionIssue);
            }

            if (!isCodeFile)
            {
                continue;
            }

            if (TodoPattern.IsMatch(line))
            {
                issues.Add(new CodeIssue(
                    IssueKind.TodoComment,
                    relativePath,
                    index + 1,
                    "Found a TODO-style comment in live code.",
                    line.Trim(),
                    "Low"));
            }

            if (LooksLikeCommentedOutCode(line))
            {
                issues.Add(new CodeIssue(
                    IssueKind.CommentedOutCode,
                    relativePath,
                    index + 1,
                    "Found commented-out code that looks alive enough to be a maintenance problem.",
                    line.Trim(),
                    "Medium"));
            }

            if (CatchPattern.IsMatch(line))
            {
                var catchBlock = TryCaptureBlock(lines, index);
                if (catchBlock is not null && IsEffectivelyEmptyCatch(catchBlock.Value.StartLine, catchBlock.Value.EndLine, lines))
                {
                    issues.Add(new CodeIssue(
                        IssueKind.EmptyCatch,
                        relativePath,
                        index + 1,
                        "A catch(Exception) block swallows the problem instead of handling it.",
                        line.Trim(),
                        "Medium"));
                }
            }

            if (TypePattern.IsMatch(line))
            {
                var block = TryCaptureBlock(lines, index);
                if (block is not null)
                {
                    var blockLines = block.Value.EndLine - block.Value.StartLine + 1;
                    var memberCount = CountMethodLikeMembers(lines, block.Value.StartLine, block.Value.EndLine);
                    if (blockLines >= options.MonsterLinesThreshold || memberCount >= 18)
                    {
                        issues.Add(new CodeIssue(
                            IssueKind.MonsterClass,
                            relativePath,
                            index + 1,
                            "A type is acting like a monster class.",
                            $"Size: {blockLines} lines, members: {memberCount}.",
                            "High"));
                    }
                }
            }

            if (MethodPattern.IsMatch(line))
            {
                var block = TryCaptureBlock(lines, index);
                if (block is not null)
                {
                    var blockLines = block.Value.EndLine - block.Value.StartLine + 1;
                    if (blockLines >= options.LongMethodLinesThreshold)
                    {
                        issues.Add(new CodeIssue(
                            IssueKind.LongMethod,
                            relativePath,
                            index + 1,
                            "A method is longer than the configured threshold.",
                            $"Length: {blockLines} lines.",
                            "Medium"));
                    }
                }
            }
        }

        return issues;
    }

    private static CodeIssue? TryCreateIssueSuppressionIssue(string line, string extension, string relativePath, int lineNumber)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (PragmaWarningDisablePattern.IsMatch(trimmed))
        {
            var hasContextComment = trimmed.Contains("//", StringComparison.Ordinal);
            return new CodeIssue(
                IssueKind.IssueSuppression,
                relativePath,
                lineNumber,
                hasContextComment
                    ? "Found a #pragma warning disable that suppresses diagnostics."
                    : "Found a #pragma warning disable without justification comment.",
                trimmed,
                hasContextComment ? "Medium" : "High");
        }

        if (SuppressMessagePattern.IsMatch(trimmed))
        {
            var hasJustification = SuppressMessageJustificationPattern.IsMatch(trimmed);
            return new CodeIssue(
                IssueKind.IssueSuppression,
                relativePath,
                lineNumber,
                hasJustification
                    ? "Found a SuppressMessage attribute that hides an analyzer warning."
                    : "Found a SuppressMessage attribute without a clear justification.",
                trimmed,
                hasJustification ? "Medium" : "High");
        }

        if (extension is ".csproj" or ".props" or ".targets" && NoWarnPattern.IsMatch(trimmed))
        {
            return new CodeIssue(
                IssueKind.IssueSuppression,
                relativePath,
                lineNumber,
                "Found NoWarn configuration that disables diagnostics at project scope.",
                trimmed,
                "High");
        }

        if (extension == ".editorconfig" && EditorConfigSeverityNonePattern.IsMatch(trimmed))
        {
            return new CodeIssue(
                IssueKind.IssueSuppression,
                relativePath,
                lineNumber,
                "Found .editorconfig analyzer severity set to none.",
                trimmed,
                "Medium");
        }

        return null;
    }

    private static int CountMethodLikeMembers(string[] lines, int startLine, int endLine)
    {
        var count = 0;

        for (var index = startLine; index <= endLine; index++)
        {
            var line = lines[index];
            if (MethodPattern.IsMatch(line) && !line.Contains("class", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static (int StartLine, int EndLine)? TryCaptureBlock(string[] lines, int declarationLine)
    {
        var opened = false;
        var depth = 0;

        for (var index = declarationLine; index < lines.Length; index++)
        {
            var line = StripLineComment(lines[index]);
            for (var charIndex = 0; charIndex < line.Length; charIndex++)
            {
                var current = line[charIndex];
                if (current == '{')
                {
                    opened = true;
                    depth++;
                }
                else if (current == '}' && opened)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return (declarationLine, index);
                    }
                }
            }
        }

        return null;
    }

    private static bool IsEffectivelyEmptyCatch(int startLine, int endLine, string[] lines)
    {
        var bodyLines = lines.Skip(startLine + 1).Take(endLine - startLine - 1).ToArray();
        if (bodyLines.Length == 0)
        {
            return true;
        }

        foreach (var bodyLine in bodyLines)
        {
            var trimmed = bodyLine.Trim();
            if (trimmed.Length == 0 || trimmed == "{" || trimmed == "}")
            {
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Contains("throw", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Log", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string StripLineComment(string line)
    {
        var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
        return commentIndex >= 0 ? line[..commentIndex] : line;
    }

    private static bool LooksLikeCommentedOutCode(string line)
    {
        var trimmed = line.TrimStart();
        if (!CommentedOutCodePattern.IsMatch(trimmed))
        {
            return false;
        }

        if (trimmed.StartsWith("///", StringComparison.Ordinal) || trimmed.StartsWith("//!", StringComparison.Ordinal))
        {
            return false;
        }

        var body = trimmed.StartsWith("//", StringComparison.Ordinal)
            ? trimmed[2..].TrimStart('*').Trim()
            : trimmed[2..].TrimStart('*').Trim();

        if (body.Length == 0 || TodoPattern.IsMatch(body))
        {
            return false;
        }

        if (body.Contains("http://", StringComparison.OrdinalIgnoreCase) || body.Contains("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var codeMarkers = new[]
        {
            ";",
            "=>",
            "{",
            "}",
            "=",
            "(",
            ")"
        };

        if (codeMarkers.Any(marker => body.Contains(marker, StringComparison.Ordinal)))
        {
            return true;
        }

        var codeWords = new[]
        {
            "if ",
            "for ",
            "while ",
            "switch ",
            "return ",
            "throw ",
            "try ",
            "catch ",
            "foreach ",
            "using ",
            "var ",
            "new ",
            "class ",
            "public ",
            "private ",
            "internal ",
            "protected ",
            "static ",
            "Console."
        };

        return codeWords.Any(word => body.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeSourceFile(string fullPath)
    {
        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        return extension is ".cs" or ".razor" or ".xaml" or ".csproj" or ".props" or ".targets" or ".editorconfig";
    }

    private static SourceControlKind DetectSourceControl(string repoRoot, SourceControlKind configured)
    {
        if (configured != SourceControlKind.Auto)
        {
            return configured;
        }

        if (RunGit(repoRoot, ["rev-parse", "--is-inside-work-tree"]).ExitCode == 0)
        {
            return SourceControlKind.Git;
        }

        if (RunSvn(repoRoot, ["info", "--show-item", "wc-root"]).ExitCode == 0)
        {
            return SourceControlKind.Svn;
        }

        return SourceControlKind.Directory;
    }

    private static IReadOnlyList<string> GetWorkingTreeFiles(string repoRoot, SourceControlKind sourceControl)
    {
        return sourceControl switch
        {
            SourceControlKind.Git => GetGitWorkingTreeFiles(repoRoot),
            SourceControlKind.Svn => GetSvnWorkingTreeFiles(repoRoot),
            SourceControlKind.Directory => GetDirectoryFiles(repoRoot),
            _ => throw new InvalidOperationException("Working tree scanning requires an explicit git or SVN repository.")
        };
    }

    private static IReadOnlyList<string> GetGitWorkingTreeFiles(string repoRoot)
    {
        var result = RunGit(repoRoot, ["status", "--porcelain=v1", "--untracked-files=all"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git status failed.{Environment.NewLine}{result.StandardError}");
        }

        var files = new List<string>();
        foreach (var rawLine in result.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawLine.Length < 4)
            {
                continue;
            }

            var status = rawLine[..2];
            var pathPart = rawLine[3..].Trim();
            if (status == "??")
            {
                files.Add(pathPart);
                continue;
            }

            if (status.Contains('D'))
            {
                continue;
            }

            var renameSeparator = pathPart.IndexOf(" -> ", StringComparison.Ordinal);
            if (renameSeparator >= 0)
            {
                files.Add(pathPart[(renameSeparator + 4)..].Trim());
            }
            else
            {
                files.Add(pathPart);
            }
        }

        return files
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetSvnWorkingTreeFiles(string repoRoot)
    {
        var result = RunSvn(repoRoot, ["status"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"SVN status failed.{Environment.NewLine}{result.StandardError}");
        }

        var files = new List<string>();
        foreach (var line in result.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 9)
            {
                continue;
            }

            var status = line[0];
            var path = line[8..].Trim();
            if (status is '?' or 'M' or 'A' or 'R' or 'C')
            {
                files.Add(path);
            }
        }

        return files
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetDirectoryFiles(string repoRoot)
    {
        var files = new List<string>();
        foreach (var filePath in Directory.EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories))
        {
            if (ShouldSkipDirectoryScanFile(repoRoot, filePath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(filePath);
            if (!LooksLikeSourceFile(fullPath))
            {
                continue;
            }

            files.Add(Path.GetRelativePath(repoRoot, fullPath));
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ShouldSkipDirectoryScanFile(string repoRoot, string filePath)
    {
        var relative = Path.GetRelativePath(repoRoot, filePath);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
            segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".svn", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveRepositoryRoot(string workingDirectory, SourceControlKind sourceControl)
    {
        return sourceControl switch
        {
            SourceControlKind.Git => ResolveGitRoot(workingDirectory),
            SourceControlKind.Svn => ResolveSvnRoot(workingDirectory),
            SourceControlKind.Directory => workingDirectory,
            _ => throw new InvalidOperationException("Source control must be git, SVN, or directory mode to resolve a repository root.")
        };
    }

    private static string ResolveGitRoot(string workingDirectory)
    {
        var result = RunGit(workingDirectory, ["rev-parse", "--show-toplevel"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"The directory is not a git repository: {workingDirectory}{Environment.NewLine}{result.StandardError}");
        }

        return result.StandardOutput.Trim();
    }

    private static string ResolveSvnRoot(string workingDirectory)
    {
        var result = RunSvn(workingDirectory, ["info", "--show-item", "wc-root"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"The directory is not an SVN working copy: {workingDirectory}{Environment.NewLine}{result.StandardError}");
        }

        return result.StandardOutput.Trim();
    }

    private static ProcessResult RunGit(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
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

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output, error);
    }

    private static ProcessResult RunSvn(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "svn",
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

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start svn.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output, error);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
