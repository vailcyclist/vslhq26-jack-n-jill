using System.Text.Json;
using System.Text.Json.Serialization;

namespace FunnyCodeAnalyzer;

internal enum OutputChannel
{
    Email,
    Teams,
    Both
}

internal enum CliRunMode
{
    Command,
    Interactive
}

internal enum HumorMode
{
    Dad,
    Light,
    Dirty,
    KidFriendly
}

internal enum HumorSourceKind
{
    Local,
    Web,
    Mixed
}

internal enum SourceControlKind
{
    Auto,
    Git,
    Svn,
    Directory
}

internal enum IssueKind
{
    TodoComment,
    CommentedOutCode,
    MonsterClass,
    LongMethod,
    EmptyCatch,
    LargeFile
}

internal sealed record CliOptions(
    string RepositoryPath,
    SourceControlKind SourceControl,
    OutputChannel Channel,
    string SelfUser,
    string? UserEmail,
    HumorMode HumorMode,
    string? HumorLevel,
    string? Topic,
    string? AudienceProfileId,
    HumorSourceKind HumorSource,
    string? HumorApiConfigPath,
    string? HumorApiUrlTemplate,
    string? HumorApiTextProperty,
    string? ApiBaseUrl,
    string? ToEmail,
    string? GraphAccessToken,
    string? TeamsChatId,
    string? TeamsRecipientUserId,
    string? TeamsSenderUserId,
    bool IncludeHumorInApiMessage,
    string? OutputDirectory,
    string? StateStorePath,
    int TodoThreshold,
    int MonsterLinesThreshold,
    int LongMethodLinesThreshold,
    CliRunMode RunMode,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        var repositoryPath = Environment.CurrentDirectory;
        var sourceControl = SourceControlKind.Auto;
        var channel = OutputChannel.Both;
        var selfUser = "there";
        string? userEmail = null;
        var humorMode = HumorMode.Light;
        string? humorLevel = null;
        string? topic = null;
        string? audienceProfileId = null;
        var humorSource = HumorSourceKind.Local;
        string? humorApiConfigPath = null;
        string? humorApiUrlTemplate = null;
        string? humorApiTextProperty = null;
        string? apiBaseUrl = null;
        string? toEmail = null;
        string? graphAccessToken = null;
        string? teamsChatId = null;
        string? teamsRecipientUserId = null;
        string? teamsSenderUserId = null;
        var includeHumorInApiMessage = true;
        string? outputDirectory = null;
        string? stateStorePath = null;
        var todoThreshold = 1;
        var monsterLinesThreshold = 250;
        var longMethodLinesThreshold = 80;
        var runMode = CliRunMode.Command;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            string NextValue()
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {argument}.");
                }

                index++;
                return args[index];
            }

            switch (argument.ToLowerInvariant())
            {
                case "--repo":
                case "-r":
                    repositoryPath = NextValue();
                    break;
                case "--source-control":
                case "--scm":
                    sourceControl = ParseSourceControl(NextValue());
                    break;
                case "--channel":
                    channel = ParseChannel(NextValue());
                    break;
                case "--self-user":
                case "--recipient-name":
                    selfUser = NextValue();
                    break;
                case "--user-email":
                    userEmail = NextValue();
                    break;
                case "--humor-mode":
                    humorMode = ParseHumorMode(NextValue());
                    break;
                case "--humor-level":
                    humorLevel = NextValue();
                    break;
                case "--topic":
                    topic = NextValue();
                    break;
                case "--audience-profile":
                    audienceProfileId = NextValue();
                    break;
                case "--humor-source":
                    humorSource = ParseHumorSource(NextValue());
                    break;
                case "--humor-api-config":
                    humorApiConfigPath = NextValue();
                    break;
                case "--humor-api-url-template":
                    humorApiUrlTemplate = NextValue();
                    break;
                case "--humor-api-text-property":
                    humorApiTextProperty = NextValue();
                    break;
                case "--api-base-url":
                    apiBaseUrl = NextValue();
                    break;
                case "--to-email":
                    toEmail = NextValue();
                    break;
                case "--graph-access-token":
                    graphAccessToken = NextValue();
                    break;
                case "--teams-chat-id":
                    teamsChatId = NextValue();
                    break;
                case "--teams-recipient-user-id":
                    teamsRecipientUserId = NextValue();
                    break;
                case "--teams-sender-user-id":
                    teamsSenderUserId = NextValue();
                    break;
                case "--no-humor":
                    includeHumorInApiMessage = false;
                    break;
                case "--output-dir":
                    outputDirectory = NextValue();
                    break;
                case "--state-store":
                    stateStorePath = NextValue();
                    break;
                case "--todo-threshold":
                    todoThreshold = ParsePositiveInt(NextValue(), argument);
                    break;
                case "--monster-lines":
                    monsterLinesThreshold = ParsePositiveInt(NextValue(), argument);
                    break;
                case "--long-method-lines":
                    longMethodLinesThreshold = ParsePositiveInt(NextValue(), argument);
                    break;
                case "--run-mode":
                    runMode = ParseRunMode(NextValue());
                    break;
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {argument}");
            }
        }

        return new CliOptions(
            repositoryPath,
            sourceControl,
            channel,
            selfUser,
            userEmail,
            humorMode,
            humorLevel,
            topic,
            audienceProfileId,
            humorSource,
            humorApiConfigPath,
            humorApiUrlTemplate,
            humorApiTextProperty,
            apiBaseUrl,
            toEmail,
            graphAccessToken,
            teamsChatId,
            teamsRecipientUserId,
            teamsSenderUserId,
            includeHumorInApiMessage,
            outputDirectory,
            stateStorePath,
            todoThreshold,
            monsterLinesThreshold,
            longMethodLinesThreshold,
            runMode,
            showHelp);
    }

    private static CliRunMode ParseRunMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "command" => CliRunMode.Command,
            "interactive" => CliRunMode.Interactive,
            _ => throw new ArgumentException($"Unknown run mode: {value}")
        };
    }

    private static HumorMode ParseHumorMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "dad" => HumorMode.Dad,
            "light" => HumorMode.Light,
            "dirty" => HumorMode.Dirty,
            "kid-friendly" => HumorMode.KidFriendly,
            "kidfriendly" => HumorMode.KidFriendly,
            "kid_friendly" => HumorMode.KidFriendly,
            _ => throw new ArgumentException($"Unknown humor mode: {value}")
        };
    }

    private static HumorSourceKind ParseHumorSource(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "local" => HumorSourceKind.Local,
            "web" => HumorSourceKind.Web,
            "mixed" => HumorSourceKind.Mixed,
            _ => throw new ArgumentException($"Unknown humor source: {value}")
        };
    }

    private static SourceControlKind ParseSourceControl(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "auto" => SourceControlKind.Auto,
            "git" => SourceControlKind.Git,
            "svn" => SourceControlKind.Svn,
            "directory" => SourceControlKind.Directory,
            "filesystem" => SourceControlKind.Directory,
            _ => throw new ArgumentException($"Unknown source control mode: {value}")
        };
    }

    private static OutputChannel ParseChannel(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "email" => OutputChannel.Email,
            "teams" => OutputChannel.Teams,
            "both" => OutputChannel.Both,
            _ => throw new ArgumentException($"Unknown channel: {value}")
        };
    }

    private static int ParsePositiveInt(string value, string argument)
    {
        if (!int.TryParse(value, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException($"{argument} expects a positive integer.");
        }

        return parsed;
    }
}

internal sealed record CodeIssue(
    IssueKind Kind,
    string FilePath,
    int Line,
    string Description,
    string Detail,
    string Severity,
    string UserIdentifier);

internal sealed record AnalysisReport(
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

internal sealed record MessageDraft(string Kind, string Subject, string Body, string FileName)
{
    public string FormatConsole()
    {
        var lines = new List<string>
        {
            $"[{Kind}] {Subject}",
            Body
        };

        return string.Join(Environment.NewLine, lines);
    }

    public string ToFileContent()
    {
        if (Kind.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"Subject: {Subject}",
                "",
                Body
            });
        }

        return string.Join(Environment.NewLine, new[]
        {
            $"{Kind} Draft",
            $"Title: {Subject}",
            "",
            Body
        });
    }
}

internal sealed class FunnyMessageConfig
{
    public string Version { get; init; } = "1.0";

    public string DefaultCulture { get; init; } = "en-US";

    public HumorCatalogDefaults Defaults { get; init; } = HumorCatalogDefaults.Defaults();

    public Dictionary<string, HumorLevelRule> HumorLevels { get; init; } = HumorLevelRule.Defaults();

    public Dictionary<string, TopicRule> Topics { get; init; } = TopicRule.Defaults();

    public Dictionary<string, ChannelRule> Channels { get; init; } = ChannelRule.Defaults();

    public Dictionary<string, IssueRule> Issues { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public HumorFallbacks Fallbacks { get; init; } = HumorFallbacks.Defaults();

    public List<AudienceProfile> AudienceProfiles { get; init; } = new();

    public Dictionary<string, ChannelFormattingRule> ChannelFormatting { get; init; } = ChannelFormattingRule.Defaults();

    public MessageTemplateSet Email { get; init; } = MessageTemplateSet.EmailDefaults();

    public MessageTemplateSet Teams { get; init; } = MessageTemplateSet.TeamsDefaults();

    public HumorModeConfig HumorModes { get; init; } = HumorModeConfig.Defaults();

    public HumorTemplates Humor { get; init; } = HumorTemplates.Defaults();

    public HumorApiConfig HumorApi { get; init; } = new();

    public static FunnyMessageConfig Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new FunnyMessageConfig();
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<FunnyMessageConfig>(json, JsonOptions.Default);
        return config ?? new FunnyMessageConfig();
    }
}

internal sealed class MessageTemplateSet
{
    public string[] SubjectTemplates { get; init; } = Array.Empty<string>();

    public string[] OpeningTemplates { get; init; } = Array.Empty<string>();

    public string[] BodyLeadTemplates { get; init; } = Array.Empty<string>();

    public string[] ClosingTemplates { get; init; } = Array.Empty<string>();

    public static MessageTemplateSet EmailDefaults() => new()
    {
        SubjectTemplates = new[]
        {
            "Code goblin report: {IssueCount} finding(s) across {WorkingFileCount} working file(s)",
            "A polite warning from the analyzer: {IssueCount} issue(s) discovered"
        },
        OpeningTemplates = new[]
        {
            "Hello {RecipientName},",
            "Hi {RecipientName},"
        },
        BodyLeadTemplates = new[]
        {
            "I inspected {WorkingFileCount} working file(s) that have not been committed yet.",
            "The analyzer wandered through the uncommitted changes and came back with a few notes."
        },
        ClosingTemplates = new[]
        {
            "Regards,\nThe Friendly Static Analyzer",
            "Cheers,\nYour local code goblin wrangler"
        }
    };

    public static MessageTemplateSet TeamsDefaults() => new()
    {
        SubjectTemplates = new[]
        {
            "Teams alert: {IssueCount} finding(s) in {WorkingFileCount} working file(s)"
        },
        OpeningTemplates = new[]
        {
            "{RecipientName}, I checked the uncommitted changes and found some code that wants supervision.",
            "I brought receipts from {WorkingFileCount} working file(s) waiting for a decision."
        },
        BodyLeadTemplates = new[]
        {
            "Working files scanned: {WorkingFileCount}.",
            "Nothing catastrophic, but the static checks have opinions about the pre-commit scene."
        },
        ClosingTemplates = new[]
        {
            "Please do not feed the code goblins after midnight.",
            "May your next commit be smaller and less theatrical."
        }
    };
}

internal sealed class HumorTemplates
{
    public Dictionary<IssueKind, string[]> IssuePuns { get; init; } = new();

    public Dictionary<HumorMode, string[]> ModePunchlines { get; init; } = new();

    public string[] GeneralPunchlines { get; init; } = Array.Empty<string>();

    public string[] GeneralClosings { get; init; } = Array.Empty<string>();

    public static HumorTemplates Defaults() => new();
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

internal sealed class HumorModeConfig
{
    public string[] DefaultModes { get; init; } = Array.Empty<string>();

    public static HumorModeConfig Defaults() => new()
    {
        DefaultModes = new[] { "dad", "light", "dirty", "kid-friendly" }
    };
}

internal sealed class HumorApiConfig
{
    public bool EnabledByDefault { get; init; }

    public string? UrlTemplate { get; init; }

    public string? TextProperty { get; init; }
}

internal sealed class HumorCatalogDefaults
{
    public string HumorLevel { get; init; } = "lightHearted";

    public string Topic { get; init; } = "general";

    public string Channel { get; init; } = "teams";

    public string FallbackTopic { get; init; } = "general";

    public string FallbackHumorLevel { get; init; } = "lightHearted";

    public bool AllowAdultHumor { get; init; }

    public bool AllowSarcasm { get; init; } = true;

    public bool AllowInsults { get; init; }

    public static HumorCatalogDefaults Defaults() => new();
}

internal sealed class HumorLevelRule
{
    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool AllowSarcasm { get; init; }

    public bool AllowMildInsults { get; init; }

    public bool AllowProfanity { get; init; }

    public int MaxSpice { get; init; }

    public static Dictionary<string, HumorLevelRule> Defaults() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["kidFriendly"] = new() { DisplayName = "Kid-Friendly", Description = "Silly and safe.", AllowSarcasm = false, AllowMildInsults = false, AllowProfanity = false, MaxSpice = 0 },
        ["lightHearted"] = new() { DisplayName = "Light-Hearted", Description = "Friendly workplace humor.", AllowSarcasm = false, AllowMildInsults = false, AllowProfanity = false, MaxSpice = 1 },
        ["dadJoke"] = new() { DisplayName = "Dad Joke", Description = "Pun-heavy and groan-worthy.", AllowSarcasm = false, AllowMildInsults = false, AllowProfanity = false, MaxSpice = 1 },
        ["average"] = new() { DisplayName = "Average", Description = "Casual dev humor.", AllowSarcasm = true, AllowMildInsults = false, AllowProfanity = false, MaxSpice = 2 },
        ["sarcastic"] = new() { DisplayName = "Sarcastic", Description = "Dry humor with mild teasing.", AllowSarcasm = true, AllowMildInsults = true, AllowProfanity = false, MaxSpice = 3 },
        ["spicy"] = new() { DisplayName = "Spicy", Description = "Snarky and intense.", AllowSarcasm = true, AllowMildInsults = true, AllowProfanity = false, MaxSpice = 4 },
        ["chaotic"] = new() { DisplayName = "Chaotic", Description = "Over-the-top and dramatic.", AllowSarcasm = true, AllowMildInsults = true, AllowProfanity = false, MaxSpice = 4 },
        ["dirty"] = new() { DisplayName = "Dirty", Description = "Adult humor.", AllowSarcasm = true, AllowMildInsults = true, AllowProfanity = true, MaxSpice = 5 }
    };
}

internal sealed class TopicRule
{
    public string DisplayName { get; init; } = string.Empty;

    public string[] Aliases { get; init; } = Array.Empty<string>();

    public static Dictionary<string, TopicRule> Defaults() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["general"] = new() { DisplayName = "General", Aliases = new[] { "default", "none", "generic" } },
        ["starTrek"] = new() { DisplayName = "Star Trek", Aliases = new[] { "trek", "enterprise", "federation", "vulcan" } },
        ["starWars"] = new() { DisplayName = "Star Wars", Aliases = new[] { "jedi", "sith", "force", "empire" } },
        ["football"] = new() { DisplayName = "Football", Aliases = new[] { "nfl", "touchdown", "quarterback", "gridiron" } },
        ["baseball"] = new() { DisplayName = "Baseball", Aliases = new[] { "mlb", "home-run", "pitcher" } },
        ["basketball"] = new() { DisplayName = "Basketball", Aliases = new[] { "nba", "slam-dunk", "point-guard" } },
        ["soccer"] = new() { DisplayName = "Soccer", Aliases = new[] { "futbol", "goal", "striker" } },
        ["fantasy"] = new() { DisplayName = "Fantasy", Aliases = new[] { "dragon", "wizard", "quest" } },
        ["pirate"] = new() { DisplayName = "Pirate", Aliases = new[] { "arr", "captain", "treasure" } },
        ["corporate"] = new() { DisplayName = "Corporate", Aliases = new[] { "office", "meeting", "synergy" } },
        ["office"] = new() { DisplayName = "Office", Aliases = new[] { "desk", "printer", "spreadsheet" } },
        ["coffee"] = new() { DisplayName = "Coffee", Aliases = new[] { "espresso", "latte", "caffeine" } },
        ["retroComputing"] = new() { DisplayName = "Retro Computing", Aliases = new[] { "dos", "floppy", "mainframe" } },
        ["superhero"] = new() { DisplayName = "Superhero", Aliases = new[] { "cape", "villain", "hero" } },
        ["detective"] = new() { DisplayName = "Detective", Aliases = new[] { "mystery", "clue", "case" } },
        ["western"] = new() { DisplayName = "Western", Aliases = new[] { "sheriff", "saloon", "frontier" } },
        ["space"] = new() { DisplayName = "Space", Aliases = new[] { "galaxy", "orbit", "rocket" } },
        ["dungeonCrawler"] = new() { DisplayName = "Dungeon Crawler", Aliases = new[] { "dungeon", "loot", "raid" } },
        ["videoGames"] = new() { DisplayName = "Video Games", Aliases = new[] { "gaming", "speedrun", "boss" } },
        ["anime"] = new() { DisplayName = "Anime", Aliases = new[] { "shonen", "mecha", "senpai" } },
        ["sciFi"] = new() { DisplayName = "Sci-Fi", Aliases = new[] { "cyberpunk", "android", "wormhole" } },
        ["horror"] = new() { DisplayName = "Horror", Aliases = new[] { "haunted", "ghost", "nightmare" } },
        ["holiday"] = new() { DisplayName = "Holiday", Aliases = new[] { "festive", "xmas", "new-year" } },
        ["dadJokes"] = new() { DisplayName = "Dad Jokes", Aliases = new[] { "pun", "groan", "corny" } }
    };
}

internal sealed class ChannelRule
{
    public List<WeightedTemplate> SubjectTemplates { get; init; } = new();

    public List<WeightedTemplate> OpeningTemplates { get; init; } = new();

    public List<WeightedTemplate> BodyLeadTemplates { get; init; } = new();

    public List<WeightedTemplate> ClosingTemplates { get; init; } = new();

    public static Dictionary<string, ChannelRule> Defaults() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = new(),
        ["teams"] = new(),
        ["cli"] = new()
    };
}

internal sealed class IssueRule
{
    public string DisplayName { get; init; } = string.Empty;

    public string Severity { get; init; } = "info";

    public string DefaultTopic { get; init; } = "general";

    public string[] Tags { get; init; } = Array.Empty<string>();

    public List<WeightedTemplate> Messages { get; init; } = new();
}

internal sealed class HumorFallbacks
{
    public List<WeightedTemplate> NoTopicMatch { get; init; } = new();

    public List<WeightedTemplate> NoHumorLevelMatch { get; init; } = new();

    public static HumorFallbacks Defaults() => new();
}

internal sealed class AudienceProfile
{
    public string Id { get; init; } = string.Empty;

    public string[] Usernames { get; init; } = Array.Empty<string>();

    public string[] Emails { get; init; } = Array.Empty<string>();

    public string[] ApiTokens { get; init; } = Array.Empty<string>();

    public string? HumorLevel { get; init; }

    public string? Topic { get; init; }

    public bool? AllowSarcasm { get; init; }

    public bool? AllowInsults { get; init; }

    public bool? AllowProfanity { get; init; }

    public bool? AllowAdultHumor { get; init; }
}

internal sealed class ChannelFormattingRule
{
    public bool SupportsMarkdown { get; init; }

    public bool SupportsAnsiColors { get; init; }

    public int MaxLength { get; init; } = 4000;

    public bool IncludeEmoji { get; init; }

    public string LineBreak { get; init; } = "\n";

    public static Dictionary<string, ChannelFormattingRule> Defaults() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["teams"] = new() { SupportsMarkdown = true, MaxLength = 2800, IncludeEmoji = true, LineBreak = "\n" },
        ["email"] = new() { SupportsMarkdown = false, MaxLength = 10000, IncludeEmoji = false, LineBreak = "\n" },
        ["cli"] = new() { SupportsAnsiColors = true, MaxLength = 4000, IncludeEmoji = false, LineBreak = "\n" }
    };
}

internal sealed class WeightedTemplate
{
    public string Id { get; init; } = string.Empty;

    public string Template { get; init; } = string.Empty;

    public string[] HumorLevels { get; init; } = Array.Empty<string>();

    public string[] Topics { get; init; } = Array.Empty<string>();

    public string[] Channels { get; init; } = Array.Empty<string>();

    public int Spice { get; init; }

    public int Weight { get; init; } = 1;

    public bool Enabled { get; init; } = true;
}