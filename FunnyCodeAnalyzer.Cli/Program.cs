using System.Text;

namespace FunnyCodeAnalyzer;

internal static class Program
{
    private static int Main(string[] args)
    {
        var options = CliOptions.Parse(args);

        if (options.ShowHelp)
        {
            Console.WriteLine(CliHelpText.Build());
            return 0;
        }

        try
        {
            var analyzer = new RepositoryAnalyzer();
            var report = analyzer.Analyze(options);
            Console.WriteLine(report.FormatSummary());

            var dispatchClient = new ApiNotificationClient();

            if (options.RunMode == CliRunMode.Interactive)
            {
                var generatedDrafts = dispatchClient.GenerateMessageDetailsAsync(report, options).GetAwaiter().GetResult();
                foreach (var draft in generatedDrafts)
                {
                    Console.WriteLine();
                    Console.WriteLine(draft.FormatConsole());
                }

                if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
                {
                    Console.WriteLine();
                    DraftWriter.WriteDrafts(options.OutputDirectory!, generatedDrafts);
                    Console.WriteLine($"Drafts written to {Path.GetFullPath(options.OutputDirectory!)}");
                }

                Console.WriteLine();
                Console.WriteLine("Generated message details from API. Interactive mode does not send notifications automatically.");
            }
            else
            {
                dispatchClient.SendReportAsync(report, options).GetAwaiter().GetResult();
                Console.WriteLine();
                Console.WriteLine("Report sent to API dispatch endpoint.");
            }

            return report.Issues.Count > 0 ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}

internal static class CliHelpText
{
    public static string Build() => string.Join(Environment.NewLine, new[]
    {
        "Funny Code Analyzer",
        "",
        "Scans uncommitted working files in a local git or SVN repository, finds code smells, and generates a comical email or Teams draft.",
        "",
        "Usage:",
        "  funny-code-analyzer --repo <path> [options]",
        "",
        "Options:",
        "  --repo <path>             Repository root to inspect. Defaults to the current directory.",
        "  --source-control <auto|git|svn|directory>  SCM mode. Defaults to auto-detect.",
        "  --channel <email|teams|both>  Output format. Defaults to both.",
        "  --self-user <name>        Default user identifier. Used for directory mode and fallback identity.",
        "  --user-email <email>      Optional audience identity for profile matching.",
        "  --humor-api-config <path>  JSON file with API base URL and endpoint paths.",
        "  API behavior is mode-driven:",
        "    command mode sends messages automatically through the API.",
        "    interactive mode generates message details through the API without sending.",
        "  --api-base-url <url>       Base URL for notification API (for example http://localhost:5188).",
        "  --to-email <email>         Recipient email when channel is email or both.",
        "  --graph-access-token <token>  Graph token when channel is teams or both.",
        "  --teams-chat-id <id>       Existing Teams chat id (optional alternative to recipient/sender ids).",
        "  --teams-recipient-user-id <id>  Teams recipient user id when no chat id is provided.",
        "  --teams-sender-user-id <id>  Teams sender user id when no chat id is provided.",
        "  --output-dir <path>       Directory where drafts are written.",
        "  --state-store <path>      Optional JSON file for per-user issue history and escalation.",
        "  --todo-threshold <n>      TODO count threshold for extra warnings. Defaults to 1.",
        "  --monster-lines <n>       Line threshold for monster class detection. Defaults to 250.",
        "  --long-method-lines <n>   Line threshold for long method detection. Defaults to 80.",
        "  --run-mode <command|interactive>  Controls API behavior. Defaults to command.",
        "  --help                    Show this help text.",
        ""
    });
}

internal static class DraftWriter
{
    public static void WriteDrafts(string outputDirectory, IReadOnlyList<MessageDraft> drafts)
    {
        Directory.CreateDirectory(outputDirectory);

        foreach (var draft in drafts)
        {
            var path = Path.Combine(outputDirectory, draft.FileName);
            File.WriteAllText(path, draft.ToFileContent(), Encoding.UTF8);
        }
    }
}